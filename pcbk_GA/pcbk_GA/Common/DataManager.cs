using pcbk_GA.Objects;
using pcbk_GA.Solutions.GA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace pcbk_GA.Common
{
    public class DataManager
    {
        public static int UniqueOrderId = 1; // внутренний primary key для заказов
        public List<Product> Products = null;
        public List<Machine> Machines = null;
        public List<ProductFormat> productFormats = null;
        public List<ProductType> productTypes = null;
        public List<MachineConstraint> machineContsraints = null;

        public DataManager()
        {
            Products = new List<Product>();
            Machines = new List<Machine>();
            productFormats = new List<ProductFormat>();
            productTypes = new List<ProductType>();
            machineContsraints = new List<MachineConstraint>();
        }

        public void loadConsts(string filepath)
        {
            string[] lines = File.ReadAllLines(filepath, Encoding.UTF8);
            string table = String.Empty;
            bool skipColumns = false;

            foreach(string s in lines)
            {
                if (skipColumns)
                {
                    skipColumns = false;
                    continue;
                }

                if (s.IndexOf("=========") > -1)
                {
                    table = s.Substring(10, s.Length - 20);
                    skipColumns = true;
                    continue;
                }

                string[] vars = s.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                switch (table)
                {
                    case "Machines": Machines.Add(new Machine(
                            Convert.ToInt32(vars[0]),
                            vars[1],
                            Convert.ToInt32(vars[2]),
                            Convert.ToInt32(vars[3]),
                            new List<MachineConstraint>()
                            ));
                        break;
                    case "ProductTypes":
                        productTypes.Add(new ProductType(
                            Convert.ToInt32(vars[0]),
                            vars[1]
                            ));
                        break;
                    case "Products":
                        Products.Add(new Product(
                            Convert.ToInt32(vars[0]),
                            vars[2],
                            productTypes.First(x => x.Id == Convert.ToInt32(vars[1]))
                            ));
                        break;
                    case "MachineConstraints":
                        machineContsraints.Add(new MachineConstraint(
                            Convert.ToInt32(vars[0]),
                            Products.First(x => x.Id == Convert.ToInt32(vars[1])),
                            Convert.ToInt32(vars[2])
                            ));
                        break;
                    case "ProductFormats":
                        productFormats.Add(new ProductFormat(
                            Convert.ToInt32(vars[0]),
                            Convert.ToInt32(vars[1])
                            ));
                        break;
                }
            }

            for(int i = 0; i < Machines.Count; i++)
            {
                Machines[i].Constraints.AddRange(
                    machineContsraints.Where(x => x.MachineId == Machines[i].Id)
                    );
            }
        }

        public List<Order> loadOrders(string filepath)
        {
            List<Order> orders = new List<Order>();
            string[] lines = File.ReadAllLines(filepath, Encoding.UTF8);

            foreach (string s in lines.Skip(1))
            {
                string[] cells = s.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                Order order = new Order(
                    cells[ 0 ].Trim(),
                    cells[ 1 ].Trim(),
                    Products.First(x => x.Name.ToLower() == cells[ 3 ].Trim().ToLower()).Id,
                    Convert.ToInt32(cells[ 4 ]),
                    Convert.ToInt32(cells[ 5 ]),
                    (float)Math.Round(float.Parse(cells[ 6 ]), 0),
                    GA.today.AddDays(7) //DateTime.Today.AddDays(7)
                    //Convert.ToDateTime(cells[5])
                );

                //if ( productFormats.Any( x => x.ProductFormatId == order.ProductId && x.Width == order.Width ) )
                    //throw new Exception( "mystic order width" );

                order.AllowedMachines.AddRange(
                    Machines
                        .Where(x => x.StripWidth >= order.Width)
                        .Where(
                            y => y.Constraints.Any(
                                z => z.Product.Id == order.ProductId
                                && z.Density == order.Density
                            )
                        )
                );

                if (order.AllowedMachines.Count == 0)
                    throw new Exception("Не могу найти подходящую машину для производства заказа " + order.Id);

                order.productObj = Products.First(x => x.Id == order.ProductId);
                orders.Add(order);
            }
                        
            return orders;
        }
    }
}
