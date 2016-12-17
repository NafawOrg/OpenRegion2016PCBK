using System;
using System.Collections.Generic;

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

        public Machine(Machine data)
        {
            this.Id = data.Id;
            this.Name = data.Name;
            this.StripWidth = data.StripWidth;
            this.MaxMonthOutput = data.MaxMonthOutput;
            this.Constraints = data.Constraints;
            this.HourPerfomance = data.HourPerfomance;
        }

        public float CalcWaste(float volume, float width)
        {
            return CalcWaste(volume, width, this.StripWidth);
        }

        public float CalcWaste(float volume, float width, float stripWidth)
        {
            float real_total_volume = volume / (width / stripWidth);
            float wasted_volume = real_total_volume * (1f - width / stripWidth);
            return wasted_volume;
        }

        public double CalcTime(float volume, float width)
        {
            float real_total_volume = volume / (width / this.StripWidth);
            double production_time = Math.Round((double)(real_total_volume / this.HourPerfomance), 2);
            return production_time;
        }        
    }
}
