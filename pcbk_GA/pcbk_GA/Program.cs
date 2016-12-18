using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using pcbk_GA.Import;
using pcbk_GA.Common;
using pcbk_GA.Objects;
using pcbk_GA.Solutions.GA;

// Продукция зависит от машины
// плотность зависит от машины и продукции

//  TODO: как дробить заказы?
//  06.11.16:
//      После хакатона решил записать мысли по поводу того, как можно дробить заказы, пока голова свежа.
//  Вариант 1 и видимо пока единственный: Берем каждый заказ*, и сравниваем его со всеми остальными, оставляем только те,
//  которые подходят по типу продукции+граммаж(логично, да) и дедлайн у них >= у расстраиваемого в данный момент заказа*, а так же, чтобы они помещались по ширине хотя бы на 1 машину. 
//      Далее, получив такие группировки(можно использовать LinkedList, удалять элементы и прозводить группы сразу же => меньшее кол-во проходов),
//  мы можем начать процедуру дробления и вообще понять надо ли оно. Такую процедеру нужно сделать 1 раз и запомнить результат, а не 100500 в геном конструкторе.
//      При чём, вероятно, можно рассматривать каждый элемент из группы на должность разделенного на подзаказы. Для этого нужна какая-то формула,
//  которая поможет определить какие заказы пригодны для разбиения на подзаказы(видимо будет использоваться только объем заказа). Среднее арифметическое?
//  Далее получив эти подразбивки, мы можем свалить их в List<Orders>, которым мы заполняем геном - готово.
//
//  кейсы объединений заказов и подсчёт отходов, в данный момент не совсем понятен процесс
//  учет тех.обслуживание во времени
//  вклинивание в тек.производство

namespace pcbk_GA
{
    class Program
    {        
        static void Main(string[] args)
        {
            Console.WriteLine("APP_PATH: " + Consts.APP_PATH);
            Console.WriteLine("START_TIME: " + DateTime.Now.ToString(Consts.FULL_TIME_FORMAT));

            Run();

            Console.WriteLine("END_TIME: " + DateTime.Now.ToString(Consts.FULL_TIME_FORMAT));
            Console.ReadKey();
        }

        static void Run()
        {
            GA.today = new DateTime(2016, 10, 7, 8, 0 , 0); // 07.10.2016 08:00:00 как в файла с обработано...
            DataManager dm = new DataManager();
            dm.loadConsts(Consts.APP_PATH + @"\input\consts.txt");
            List<Order> orders = dm.loadOrders(Consts.APP_PATH + @"\input\(7) Входные данные заказы без дат_.csv");

            //OrderDelimiter delimiter = new OrderDelimiter( orders );
            //orders = delimiter.TrySplitOders();

            GA ga = new GA(dm.Machines, orders, 200, 200);
            GAFitnessEstimator f = new GAFitnessEstimator();
            ga.FitnessFunction = new GAFitnessFunction(f.FitnessFunction);
            ga.FindSolutions();

            var best = ga.m_thisGeneration.Last();
            var score = f.FitnessFunction(best.getGenes());

            var humanGenom = DataImport.LoadHumanGenom(
                Consts.APP_PATH + @"\input\(3) Обработано Б-21.txt",
                Consts.APP_PATH + @"\input\(2) Обработано КП-06.txt",
                Consts.APP_PATH + @"\input\(4) Обработано Б-2300.txt",
                dm,
                orders
            );

            var humanOrders = humanGenom.LoadedMachines
                .SelectMany(x => x.ordersQueue).OrderBy(y => y.InternalOrderId).ToList();

            var test = humanOrders
                .GroupBy(y => y.InternalOrderId)
                .Select(z => new { key = z.Key, sum = z.Sum(m => m.Volume) })
                .ToList();

            var wtf = orders.Where(x => !test.Any(y => y.key == x.InternalOrderId)).ToList();

            // В DataManager используем округление т.к. в выходных данных они тоже округляли
            var join = test
                .Join(orders, x => x.key, y => y.InternalOrderId, (x, y) => new { x.key, x.sum, total = y.Volume })
                .Where(x => x.sum != x.total)
                .ToList();

            var humanScore = f.FitnessFunction(humanGenom.getGenes());

            //Export.SequenceTxtReport(best, "bestgenome.txt");
            //var export = f.Export(humanGenom);
            var export = f.Export(best);
            var output = JsonConvert.SerializeObject(export);
            Export.ExportHelp.WriteOutput("var data=" + output, Consts.APP_PATH + @"\webpcbk\webpcbk\js\output.js");

            var input = JsonConvert.SerializeObject(orders);
            Export.ExportHelp.WriteOutput("var orders=" + input, Consts.APP_PATH + @"\webpcbk\webpcbk\js\input.js");
        }
    }
}
