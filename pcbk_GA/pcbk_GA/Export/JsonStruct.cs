using pcbk_GA.Objects;
using pcbk_GA.Solutions.GA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace pcbk_GA.Export
{
    public enum OrderTypeJson
    {
        Order = 1,
        Waste = 2,
        Readjustment = 3,
        Maintenance = 4
    }
    public class OrderJson
    {
        public OrderTypeJson Type;
        public int OffsetX;
        public string Id;
        public string Consumer;
        public string ProductName;
        public float Volume;
        public int Width;
        public int Density;
        public DateTime? Deadline;
        public DateTime Start;
        public DateTime End;

        public OrderJson(string id, string consumer, string productName, float volume, 
            int width, int density, DateTime? deadline, OrderTypeJson type, int offsetX)
        {
            this.Id = id;
            this.Consumer = consumer;
            this.ProductName = productName;
            this.Volume = volume;
            this.Width = width;
            this.Density = density;
            this.Deadline = deadline;
            this.Type = type;
            this.OffsetX = offsetX;
        }
    }
    public class LoadedMachineJson
    {
        public int Id;
        public string Name;
        public int StripWidth;
        public List<OrderJson> Orders = new List<OrderJson>();
        public float WasteVolume;
        public int DeadlineHours;
        public int ReadjustmentHours;
        public DateTime TimelineStart;
        public DateTime TimelineEnd { 
            get { 
                if ( Orders.Count > 0 ) return Orders.Max( x => x.End ).Date.AddDays( 1 );
                else return TimelineStart;
            }
        }

        public LoadedMachineJson() { }
        public LoadedMachineJson(Machine m)
        {
            this.Id = m.Id;
            this.Name = m.Name;
            this.StripWidth = m.StripWidth;
            this.WasteVolume = 0f;
            this.ReadjustmentHours = 0;
            this.DeadlineHours = 0;
        }
    }
    public class ResultJson
    {
        public List<LoadedMachineJson> Machines = new List<LoadedMachineJson>();
    }

    public static class ExportHelp
    {
        public static void AddReadjustment(this List<OrderJson> export, DateTime start, DateTime end, Machine m, params Order[] ords)
        {
            string id = String.Join( "_", ords.Select(x=> x.Id) ) + "R";

            export.Add(
                new OrderJson(id, String.Empty, String.Empty, 0, m.StripWidth, 0, null, OrderTypeJson.Readjustment, 0)
                {
                    Start = start,
                    End = end
                });
        }        

        public static void SequenceTxtReport(Genome g, string outputfile)
        {
            using ( StreamWriter writer = new StreamWriter( outputfile ) )
            {
                for ( int i = 0; i < g.LoadedMachines.Count; i++ )
                {
                    writer.Write( "Машина " + g.LoadedMachines[ i ].M.Name + " :" );
                    writer.WriteLine( "" );
                    int j = 0;
                    foreach ( var order in g.LoadedMachines[ i ].ordersQueue )
                    {
                        j++;
                        writer.WriteLine( "" );
                        writer.WriteLine( "{0}   {1}", order.Id, order.Volume );

                    }
                    writer.WriteLine( "" );
                    writer.WriteLine( "==============================" );
                }
            }
        }

        public static void WriteOutput(string output, string outputfile)
        {
            using ( StreamWriter writer = new StreamWriter( outputfile ) )
            {
                writer.Write( output );
            }
        }
    }    
}
