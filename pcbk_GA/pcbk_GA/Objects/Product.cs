using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Objects
{
    public class Product
    {
        public readonly int Id;
        public readonly string Name;
        public readonly ProductType Type;
        public Product(int id, string name, ProductType type)
        {
            this.Id = id;
            this.Name = name;
            this.Type = type;
        }
    }
}
