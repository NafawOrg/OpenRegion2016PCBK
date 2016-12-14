using pcbk_GA.Objects;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace pcbk_GA.Solutions.GA
{
    public delegate double GAFitnessFunction( List<LoadedMachine> loadedMachines, List<Output> history );
    public class GA
    {
        public static Random Rand { get; private set; }
        public static int[] Stats = new int[3];
        public static DateTime today;

        public int PopulationSize;
        public int Generations;
        public double CrossoverRate;
        public double MutationRate;
        public string FitnessFile;
        public string ReportTemplate;
        public string ReportTxt;

        private List<Machine> Machines;
        private List<Order> Orders;
        private double m_totalFitness;

        public List<Genome> m_thisGeneration;
        private List<Genome> m_nextGeneration;
        private List<double> m_fitnessTable;

        //static private GAFitnessFunction getFitness;
        public GAFitnessFunction FitnessFunction;

        /// <summary>
        /// Помещает лучшего представителя предыдущего поколения на позицию худшего в текущем.
        /// </summary>
        public bool Elitism;

        public GA(List<Machine> machines, List<Order> orders)
        {
            this.Machines = machines;
            this.Orders = orders;
            Elitism = false;
            //MutationRate = 0.05;
            //CrossoverRate = 0.80;
            PopulationSize = 200;
            Generations = 200;
            FitnessFile = "TEST.txt";
            Rand = new Random(15);
            ReportTemplate = "template.html";
            ReportTxt = "result.txt";
        }
        public void CreateTxtReport(List<Output> history)
        {
            
            using (StreamWriter writer = new StreamWriter(ReportTxt))
            {
                for (int i = 0; i < history.Count; i++)
                {
                    writer.Write("Машина " + history[i].M.Name + " :");
                    writer.WriteLine("");
                    for (int j = 0; j < history[i].Orders.Count; j++)
                    {
                        writer.WriteLine("");
                        writer.WriteLine("              Id: " + history[i].Orders[j].Id);
                        writer.WriteLine("              Product: " + history[i].Orders[j].productObj.Name);
                        writer.WriteLine("              Density: " + history[i].Orders[j].Density);
                        writer.WriteLine("              Start: " + history[i].Times[j].Item1);

                        writer.WriteLine("              Finish: " + history[i].Times[j].Item2);
                        
                    }
                    writer.WriteLine("");
                    writer.WriteLine("==============================");
                }
            }
        }
        public void CreateReport(List<Output> history)
        {
            for (int i = 0; i < history.Count; i++)
            {
                var report = string.Empty;
                using (StreamReader reader = new StreamReader(ReportTemplate))
                {
                    report = reader.ReadToEnd();
                    reader.Close();
                }
                string[] orders = new string[history[i].Orders.Count];
                for (int j = 0; j < history[i].Orders.Count; j++)
                {
                    string[] orderParams = new string[8];
                    orderParams[0] = "'" + history[i].Orders[j].Id + "'";//Task ID
                    //Task Name
                    orderParams[1] = String.Format(@"'{0}\r\n{1}'", history[i].Orders[j].Id, history[i].Orders[j].productObj.Name);
                    orderParams[2] = "'" + history[i].Orders[j].Density + "'";//Resource
                    DateTime start = history[i].Times[j].Item1;
                    //Start
                    orderParams[3] = String.Format("new Date({0},{1},{2},{3},{4})", start.Year, start.Month, start.Day, start.Hour, start.Minute);
                    //Finish
                    DateTime finish = history[i].Times[j].Item2;
                    orderParams[4] = String.Format("new Date({0},{1},{2},{3},{4})", finish.Year, finish.Month, finish.Day, finish.Hour, finish.Minute);                   
                    orderParams[5] = "null";
                    orderParams[6] = "0";
                    orderParams[7] = "null";
                    string order = "[" + String.Join(",", orderParams) + "]";
                    orders[j] = order;
                }
                report = Regex.Replace(report, "@ROWS@", String.Join(",\r\n", orders));
                int height = history[i].Orders.Count * 42 + 40;
                report = Regex.Replace(report, "@HEIGHT@", height.ToString());

                using (StreamWriter writer = new StreamWriter(history[i].M.Name + ".html"))
                {
                    writer.Write(report);
                    writer.Close();
                }
            }
        }
        public void FindSolutions()
        {
            if (FitnessFunction == null)
                throw new ArgumentNullException("Need to supply fitness function");

            //  Create the fitness table.
            m_fitnessTable = new List<double>();
            m_thisGeneration = new List<Genome>(PopulationSize);
            m_nextGeneration = new List<Genome>(PopulationSize);            

            CreateGenomes();
            RankPopulation(true);

            StreamWriter outputFitness = null;
            bool write = false;
            if (FitnessFile != String.Empty)
            {
                write = true;
                outputFitness = new StreamWriter(FitnessFile);
            }

            for (int i = 0; i < Generations; i++)
            {                
                CreateNextGeneration();
                RankPopulation();   // Пофиксить: убрать лишний GetFitness вызов
                if (write)
                {
                    if (outputFitness != null)
                    {
                        double d = m_thisGeneration[PopulationSize - 1].Fitness;
                        outputFitness.WriteLine("{0},{1}", i, d);
                    }
                }
            }

            if (outputFitness != null)
                outputFitness.Close();
        }

        private void CreateGenomes()
        {
            for (int i = 0; i < PopulationSize; i++)
            {
                Genome g = new Genome(this.Machines, this.Orders);
                m_thisGeneration.Add(g);
            }
            // Восстанавливаем первоначальную сортировку, чтобы обращаться по [i] к элементам в смешивание
            this.Orders = this.Orders.OrderBy(x => x.InternalOrderId).ToList();
        }

        private void CreateNextGeneration()
        {
            //m_nextGeneration.Clear();
            /*
            Genome g = null;

            if (Elitism)
                g = m_thisGeneration[PopulationSize - 1];
            */
            for (int i = 0; i < PopulationSize; i++)
            {                                
                Genome parent1, child1;
                parent1 = m_thisGeneration[i];                

                child1 = parent1.Clone();                
                child1.Mutate(this.Orders, parent1);
                child1.Fitness = FitnessFunction(child1.getGenes(), null);                
                
                if (parent1.Fitness > child1.Fitness) m_nextGeneration.Add(child1);
                else m_nextGeneration.Add(parent1);               
            }

            /*
            if (Elitism && g != null)
                m_nextGeneration[0] = g;
            */
            m_thisGeneration.Clear();

            //var test = m_nextGeneration.OrderBy( x => x.Fitness ).ToList();

            for (int i = 0; i < PopulationSize; i++)
                m_thisGeneration.Add(m_nextGeneration[i]);

            m_nextGeneration.Clear();
        }

        private void RankPopulation(bool calculate_fit = false)
        {
            m_totalFitness = 0;
            for (int i = 0; i < PopulationSize; i++)
            {
                Genome g = m_thisGeneration[i];
                if (calculate_fit)
                    g.Fitness = FitnessFunction(g.getGenes(), null);
                m_totalFitness += g.Fitness;
            }
            m_thisGeneration.Sort(new GenomeComparer());            

            //  now sorted in order of fitness.
            double fitness = 0.0;
            m_fitnessTable.Clear();
            for (int i = 0; i < PopulationSize; i++)
            {
                fitness += m_thisGeneration[i].Fitness;
                m_fitnessTable.Add(fitness);
            }
        }

        private int RouletteSelection()
        {
            double randomFitness = Rand.NextDouble() * m_totalFitness;
            int idx = -1;
            int mid;
            int first = 0;
            int last = PopulationSize - 1;
            mid = (last - first) / 2;

            //  ArrayList's BinarySearch is for exact values only
            //  so do this by hand.
            while (idx == -1 && first <= last)
            {
                if (randomFitness < m_fitnessTable[mid])
                {
                    last = mid;
                }
                else if (randomFitness >= m_fitnessTable[mid])
                {
                    first = mid;
                }
                mid = (first + last) / 2;
                //  lies between i and i+1
                if ((last - first) == 1)
                    idx = last;
            }
            return idx;
        }

    }
}
