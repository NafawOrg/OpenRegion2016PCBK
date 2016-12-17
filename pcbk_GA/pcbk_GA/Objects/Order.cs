using pcbk_GA.Common;
using pcbk_GA.Export;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Objects
{
    public class Order : IOrder
    {
        public readonly int InternalOrderId;    // Программный уникальный ид        
        public readonly string Id;              // Заказ 1С
        public readonly string Consumer;        // Потребитель
        public readonly int ProductId;          // 
        public readonly int Density;            // Плотность(граммаж)
        public readonly int Width;              // Формат(ширина)
        public readonly float Volume;           // Объем заказа
        public readonly DateTime Deadline;      // Дата отгрузки
        public List<Machine> AllowedMachines;   // Доступные для производства машины
        private Product _productObj;
        public Product productObj {
            set { if (_productObj == null) _productObj = value; }
            get { return _productObj; }
        }

        public Order(string id, string consumer, int productid, int density, int width, float volume, DateTime deadline)
        {
            this.InternalOrderId = DataManager.UniqueOrderId;
            this.Id = id;
            this.Consumer = consumer;
            this.ProductId = productid;
            this.Density = density;
            this.Width = width;
            this.Volume = volume;
            this.Deadline = deadline;
            this.AllowedMachines = new List<Machine>();
            DataManager.UniqueOrderId++;
        }

        private Order(Order parent, float volume)
        {
            this.InternalOrderId = parent.InternalOrderId;
            this.Id = parent.Id;
            this.Consumer = parent.Consumer;
            this.ProductId = parent.ProductId;
            this.Density = parent.Density;
            this.Width = parent.Width;
            this.Volume = volume;
            this.Deadline = parent.Deadline;
            this.AllowedMachines = parent.AllowedMachines;
            this.productObj = parent.productObj;
        }

        public Order Clone(float volume)
        {
            return new Order(this, volume);
        }

        int IOrder.Density
        {
            get { return this.Density; }
        }

        int IOrder.ProductId
        {
            get { return this.ProductId; }
        }

        void IOrder.calculateStats(Machine m, ref DateTime queue_time, out float waste, out int deadline)
        {
            // во сколько полос мы можем уложить данный заказ?
            int parallelLines = (int)Math.Floor(m.StripWidth / (float)Width);

            //if ( parallelLines < 1 ) throw new Exception( "Меньше одной полосы не может быть!" );

            deadline = 0;
            waste = m.CalcWaste(Volume, Width * parallelLines);
            double ord_production_time = m.CalcTime(Volume, Width * parallelLines);
            DateTime finish = queue_time.AddHours(ord_production_time);

            if (finish > Deadline)
                deadline = (finish - Deadline).Hours * parallelLines;

            queue_time = finish;
        }

        List<OrderJson> IOrder.export(Machine m, ref DateTime queue_time, out float waste, out int deadline)
        {
            var export = new List<OrderJson>();

            // во сколько полос мы можем уложить данный заказ?
            int parallelLines = (int)Math.Floor(m.StripWidth / (float)Width);

            //if ( parallelLines < 1 ) throw new Exception( "Меньше одной полосы не может быть!" );

            deadline = 0;
            waste = m.CalcWaste(Volume, Width * parallelLines);
            double ord_production_time = m.CalcTime(Volume, Width * parallelLines);
            DateTime finish = queue_time.AddHours(ord_production_time);

            for (int i = 0; i < parallelLines; i++)
            {
                export.Add(
                    new OrderJson(Id, Consumer, productObj.Name, Volume / parallelLines,
                        Width, Density, Deadline, OrderTypeJson.Order, Width * i)
                    {
                        Start = queue_time,
                        End = finish
                    });
            }

            if (finish > Deadline)
                deadline = (finish - Deadline).Hours * parallelLines;

            if (m.StripWidth - Width * parallelLines > 0)
                export.Add(
                    new OrderJson(Id + "W", String.Empty, String.Empty, waste,
                        m.StripWidth - Width * parallelLines, Density, null,
                        OrderTypeJson.Waste, Width * parallelLines)
                    {
                        Start = queue_time,
                        End = finish
                    });

            queue_time = finish;

            return export;
        }
    }
}
