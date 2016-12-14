using pcbk_GA.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Objects
{
    public class Order
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
            this.InternalOrderId = DataManager.OrderId;
            this.Id = id;
            this.Consumer = consumer;
            this.ProductId = productid;
            this.Density = density;
            this.Width = width;
            this.Volume = volume;
            this.Deadline = deadline;
            this.AllowedMachines = new List<Machine>();
            DataManager.OrderId++;
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
            return new Order( this, volume );
        }
    }
}
