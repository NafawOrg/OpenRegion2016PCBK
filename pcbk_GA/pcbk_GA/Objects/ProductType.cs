using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Objects
{
    public class ProductType
    {
        public readonly int Id;
        public readonly string Name;

        public ProductType(int id, string name)
        {
            this.Id = id;
            this.Name = name;
        }
    }
}
