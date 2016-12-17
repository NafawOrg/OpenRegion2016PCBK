using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using pcbk_GA.Common;
using pcbk_GA.Objects;
using pcbk_GA.Solutions.GA;

namespace pcbk_GA.Import
{
    public class DataImport
    {
        public static Genome LoadHumanGenom(string b21, string kp06, string b2300, DataManager dm, List<Order> orders)
        {
            var humanGenome = new Genome();
            humanGenome.LoadedMachines = dm.Machines.Select(x => new LoadedMachine(x)).ToList();

            var queue = new List<Order>();
            string[] lines = File.ReadAllLines(b21, Encoding.Default);

            foreach (var s in lines.Skip(3))
            {
                string[] vars = s.Split(new char[] { '\t' });
                string id = vars[ 1 ].Trim();
                if (string.IsNullOrEmpty(id)) continue;

                float volume = 0f;
                float.TryParse(vars[ 6 ], out volume);

                queue.Add(orders.First(x => x.Id == id).Clone(volume));
            }
            humanGenome.LoadedMachines.First(x => x.Name == "Б-21").ordersQueue = new LinkedList<Order>(queue);

            //////////////////////////

            queue = new List<Order>();
            lines = File.ReadAllLines(kp06, Encoding.Default);

            foreach (var s in lines.Skip(3))
            {
                string[] vars = s.Split(new char[] { '\t' });
                string id = vars[ 1 ].Trim();
                if (string.IsNullOrEmpty(id)) continue;
                float volume = 0f;
                float.TryParse(vars[ 7 ], out volume);

                queue.Add(orders.First(x => x.Id == id).Clone(volume));
            }
            humanGenome.LoadedMachines.First(x => x.Name == "КП-06").ordersQueue = new LinkedList<Order>(queue);

            //////////////////////////

            queue = new List<Order>();
            lines = File.ReadAllLines(b2300, Encoding.Default);

            foreach (var s in lines.Skip(3))
            {
                string[] vars = s.Split(new char[] { '\t' });
                string id = vars[ 1 ].Trim();
                if (string.IsNullOrEmpty(id)) continue;
                float volume = 0f;
                float.TryParse(vars[ 6 ], out volume);

                queue.Add(orders.First(x => x.Id == id).Clone(volume));
            }
            humanGenome.LoadedMachines.First(x => x.Name == "Б-2300").ordersQueue = new LinkedList<Order>(queue);

            return humanGenome;
        }
    }
}
