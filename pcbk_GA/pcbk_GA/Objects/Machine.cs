using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Objects
{
    public class Machine
    {
        public readonly int Id;
        public readonly string Name;
        public readonly int StripWidth;
        public readonly int MaxMonthOutput;
        public List<MachineConstraint> Constraints;
        public readonly float HourPerfomance;

        public Machine(int id, string name, int stripwidth, int maxmonthoutput, List<MachineConstraint> constraints)
        {
            this.Id = id;
            this.Name = name;
            this.StripWidth = stripwidth;
            this.MaxMonthOutput = maxmonthoutput;
            this.Constraints = constraints;
            this.HourPerfomance = maxmonthoutput / 31f / 24f;
        }
    }
}
