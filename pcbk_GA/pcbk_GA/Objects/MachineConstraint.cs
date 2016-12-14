using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Objects
{
    public class MachineConstraint
    {
        public readonly int MachineId;
        public readonly Product Product;
        public readonly int Density;

        public MachineConstraint(int machineId, Product product, int density)
        {
            this.MachineId = machineId;
            this.Product = product;
            this.Density = density;
        }
    }
}
