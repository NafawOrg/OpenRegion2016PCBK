using pcbk_GA.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace pcbk_GA.Solutions.GA
{
    public delegate double GAFitnessFunction( List<LoadedMachine> loadedMachines );
    public class GA
    {
        public static Random Rand { get; private set; }
        public static int[] Stats = new int[3];
        public static DateTime today;

        public readonly int PopulationSize;
        public readonly int Generations;
        public string FitnessFile = "TEST.txt";

        private List<Machine> Machines;
        private List<Order> Orders;

        public List<Genome> m_thisGeneration;
        private List<Genome> m_nextGeneration;
        
        public GAFitnessFunction FitnessFunction;

        public GA(List<Machine> machines, List<Order> orders, int populationSize, int generations, int seed)
        {
            this.Machines = machines;
            this.Orders = orders;
            PopulationSize = populationSize;
            Generations = generations;      
            Rand = new Random(seed);
        }        
        
        public void FindSolutions()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();

            if (FitnessFunction == null)
                throw new ArgumentNullException("Need to supply fitness function");

            m_thisGeneration = new List<Genome>(PopulationSize);
            m_nextGeneration = new List<Genome>(PopulationSize);            

            CreateGenomes();

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
                if (write && outputFitness != null)
                {
                    double d = m_thisGeneration.Min(x => x.Fitness);                        
                    outputFitness.WriteLine("{0},{1}", i, d);
                }
            }

            watch.Stop();

            if ( outputFitness != null )
            {
                outputFitness.WriteLine( "Elapsed time - {0}", watch.Elapsed.ToString("mm\\:ss\\.ff") );
                outputFitness.Close();                
            }                

            // т.к. у нас нет никаких скрещиваний, а мутация применяется ко всем,
            // то можно сортировать лишь в конце
            m_thisGeneration.Sort(new GenomeComparer());
        }

        private void CreateGenomes()
        {
            for (int i = 0; i < PopulationSize; i++)
            {
                Genome g = new Genome(this.Machines, this.Orders);
                g.Fitness = FitnessFunction(g.getGenes());
                m_thisGeneration.Add(g);
            }
            // Восстанавливаем первоначальную сортировку, чтобы обращаться по [i] к элементам в смешивание
            //this.Orders = this.Orders.OrderBy(x => x.InternalOrderId).ToList();
        }

        private void CreateNextGeneration()
        {
            for (int i = 0; i < PopulationSize; i++)
            {                                
                Genome parent, child;
                parent = m_thisGeneration[i];                

                child = parent.Clone();                
                child.Mutate(this.Orders);
                child.Fitness = FitnessFunction(child.getGenes());                
                
                if (parent.Fitness > child.Fitness) m_nextGeneration.Add(child);
                else m_nextGeneration.Add(parent);               
            }

            m_thisGeneration.Clear();

            for (int i = 0; i < PopulationSize; i++)
                m_thisGeneration.Add(m_nextGeneration[i]);

            m_nextGeneration.Clear();
        }
    }
}
