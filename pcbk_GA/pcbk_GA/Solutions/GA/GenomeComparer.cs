using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Solutions.GA
{
    public sealed class GenomeComparer: IComparer<Genome>
    {
        public GenomeComparer() { }

        public int Compare(Genome x, Genome y)
        {
            if (x.Fitness > y.Fitness)
                return -1;
            else if (x.Fitness == y.Fitness)
                return 0;
            else
                return 1;
        }
    }
}
