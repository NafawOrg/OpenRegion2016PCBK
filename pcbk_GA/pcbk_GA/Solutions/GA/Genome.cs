using pcbk_GA.Common;
using pcbk_GA.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Solutions.GA
{
    public class Genome
    {        
        public List<Machine> Machines;
        public List<LoadedMachine> LoadedMachines;
        public double Fitness;
        //public static double MutationRate;
    
        public Genome() { }
        public Genome(List<Machine> machines, List<Order> orders)
        {
            this.Machines = machines;
            this.LoadedMachines = machines.Select(x => new LoadedMachine(x)).ToList();

            orders.Shuffle(); // случайным образом сортируем заказы для разнообразности геномов

            foreach (Order o in orders)
            {
                int machineIndex = GA.Rand.Next(0, o.AllowedMachines.Count); // выбираем случайную машину для производства
                int row = o.AllowedMachines[machineIndex].Id; // узнаем строку этой машины в матрице                
                LoadedMachines[row].AddOrder(o);
            }
        }

        public List<LoadedMachine> getGenes()
        {
            return LoadedMachines;
        }
        
        //private void Crossover(ref Genome parent2, out Genome child1, out Genome child2, List<Order> orders)
        //{
        //    // Может нахер смешивание? Только мутацию? Почему?
        //    // Нам нужно смешивать таким образом, чтобы не было дублей в последовательности
        //    /*
        //     *  Смешивать можно несколькими способами:
        //     *  1) совершать обмен заказами 1x1, при этом надо учитывать, что заказы должны подходить к машинам
        //     *  
        //     *  этот не делаем т.к. проще по 1 и рандом внесет свой вклад
        //     *  2) совершать обмен заказами NxM, при этом надо учитывать, что заказы должны подходить к машинам
        //     *  + поджимать                         
        //     */
        //    child1 = this.Clone();
        //    child2 = parent2.Clone();

        //    int n_changes = GA.Rand.Next(1, Math.Max(2, (int)(0.1f * orders.Count)));

        //    for( int i = 0; i< n_changes; i++)
        //    {
        //        int giveOrders = GA.Rand.Next(0, 3); // кол-во заказов, которые перенесем с child1 на child2
        //        int takeOrders = GA.Rand.Next(0, 3); // кол-во заказов, которые перенесем c child2 на child1

        //        while (giveOrders > 0)
        //        {
        //            int machineRow = GA.Rand.Next(0, child1.Machines.Count); // выбираем машину у child1
        //            int orderColumn = GA.Rand.Next(0, child1.LoadedMachines[machineRow].QueueSize); // выбираем заказ у child1
        //            Order giveOrder = child1.LoadedMachines[machineRow][orderColumn];
        //            child1.LoadedMachines[machineRow].RemoveOrder(giveOrder);

        //            // на какую машину child2 можем перенести выбранный заказ с child1
        //            int toMachineRow = giveOrder.AllowedMachines[GA.Rand.Next(0, giveOrder.AllowedMachines.Count)].Id;

        //            // какой заказ выберем в качестве опорного у child2
        //            // выбрав опорный заказ, мы можем сделать 3 разных действия:
        //            //  1) добавить до этого заказа     => [0]
        //            //  2) добавить после этого заказа  => [1]
        //            //  3) обменяться ими               => [2]
        //            int toOrderColumn = GA.Rand.Next(0, child2.LoadedMachines[toMachineRow].QueueSize);
        //            Order markOrder = child2.LoadedMachines[toMachineRow][toOrderColumn];

        //            if (giveOrder.InternalOrderId == markOrder.InternalOrderId)
        //                continue;

        //            int options_limit = 3; // по default доступны все опции x = random(0, 3) { 0, 1, 2 }
        //            // проверяем может ли совершен обмен
        //            if (markOrder.AllowedMachines.Any(x => x.Id == machineRow) == false)
        //                options_limit--; // x = random(0, 2) { 0, 1 } 

        //            Console.WriteLine("\n");
        //            foreach (var x in child1.LoadedMachines[machineRow].ordersQueue) Console.Write("[" + x.InternalOrderId + "]");
        //            Console.WriteLine("\nCHILD 1 giving:" + giveOrder.InternalOrderId);
        //            foreach(var x in child2.LoadedMachines[toMachineRow].ordersQueue) Console.Write("[" + x.InternalOrderId + "]");
        //            Console.WriteLine("\nCHILD 2 giving:" + markOrder.InternalOrderId);

        //            int choosen_option = GA.Rand.Next(0, options_limit);
        //            switch (choosen_option)
        //            {
        //                case 0: child2.LoadedMachines[toMachineRow].AddBefore(giveOrder, markOrder); break;
        //                case 1: child2.LoadedMachines[toMachineRow].AddAfter(giveOrder, markOrder); break;
        //                case 2:
        //                    child2.LoadedMachines[toMachineRow].Replace(giveOrder, markOrder); 
        //                    child1.LoadedMachines[toMachineRow].Replace(markOrder, giveOrder);
        //                    break;
        //            }

        //            giveOrders--;
        //        }
                
        //        while (takeOrders > 0)
        //        {
        //            takeOrders--;
        //        }
        //    }        
        //}
    
        public void Mutate(List<Order> orders)
        {
            //TODO
            /*
             * При мутации будем изменять текущее состояние последовательностей:
             * 1) можно делать перестановку заказа в рамках машины
             * 2) можно делать обмен заказов между разными машинами(учитывать ограничения заказа)
             * 3) можно делать перемещение заказа с 1 машины на другую, увеличивая нагрузку последней
             */

            int n_changes = GA.Rand.Next(1, Math.Max(2, (int)(0.1f * orders.Count)));

            for (int i = 0; i < n_changes; i++)
            {
                /*
                int machineRow = -1;
                double diceRoll = GA.Rand.NextDouble();
                var probabilities = this.LoadedMachines.Select(
                    x => new { id = x.M.Id, prob = 1f - (x.QueueSize * 1f / orders.Count) }).OrderBy(x => x.prob).ToList();
                float cumulative = 0f;
                for (int j = 0; j < probabilities.Count; i++)
                {
                    cumulative += probabilities[j].prob;
                    if (diceRoll < cumulative)
                    {
                        machineRow = probabilities[j].id;
                        break;
                    }
                }                
                */

                int machineRow = GA.Rand.Next(0, this.Machines.Count);
                int orderColumn = GA.Rand.Next(0, this.LoadedMachines[machineRow].QueueSize);

                GA.Stats[machineRow]++;

                // искуственное ограничение на опустошение машины, больше рандома?
                if (this.LoadedMachines[machineRow].QueueSize/((float)orders.Count) < GA.Rand.NextDouble()*0.5f)//0.25f) 
                //if (this.LoadedMachines[machineRow].QueueSize == 0)
                    continue;

                Order giveOrder = this.LoadedMachines[machineRow][orderColumn];                

                // на какую машину можем перенести выбранный заказ
                int toMachineRow = giveOrder.AllowedMachines[GA.Rand.Next(0, giveOrder.AllowedMachines.Count)].Id;

                // какой заказ выберем в качестве опорного
                // выбрав опорный заказ, мы можем сделать 3 разных действия:
                //  1) добавить до этого заказа     => [0]
                //  2) добавить после этого заказа  => [1]
                //  3) обменяться ими               => [2]
                int toOrderColumn = GA.Rand.Next(0, this.LoadedMachines[toMachineRow].QueueSize);
                Order markOrder = this.LoadedMachines[toMachineRow][toOrderColumn];

                if (giveOrder.InternalOrderId == markOrder.InternalOrderId)
                    continue;

                int options_limit = 3; // по default доступны все опции x = random(0, 3) { 0, 1, 2 }                
                if (markOrder.AllowedMachines.Any(x => x.Id == machineRow) == false) // проверяем может ли совершен обмен
                    options_limit--; // x = random(0, 2) { 0, 1 } 

                int choosen_option = GA.Rand.Next(0, options_limit);
                switch (choosen_option)
                {
                    case 0:
                        this.LoadedMachines[machineRow].RemoveOrder(giveOrder);
                        this.LoadedMachines[toMachineRow].AddBefore(giveOrder, markOrder);
                        break;
                    case 1:
                        this.LoadedMachines[machineRow].RemoveOrder(giveOrder);
                        this.LoadedMachines[toMachineRow].AddAfter(giveOrder, markOrder);
                        break;
                    case 2:
                        if (machineRow == toMachineRow) this.LoadedMachines[machineRow].ExchangeOrders(giveOrder, markOrder);
                        else
                        {
                            this.LoadedMachines[machineRow].ChangeOrder(markOrder, giveOrder);
                            this.LoadedMachines[toMachineRow].ChangeOrder(giveOrder, markOrder);
                        }                        
                        break;
                }

                /*
                int check = 0;
                this.LoadedMachines.ForEach(x => check += x.QueueSize);
                if (check != orders.Count)
                    throw new Exception("WTF");
                */
            }
        }

        public Genome Clone()
        {
            return new Genome()
            {
                Machines = this.Machines,
                LoadedMachines = this.LoadedMachines.Select( x => x.Clone() ).ToList(),
                Fitness = 0f
            };
        }
        
    }
}
