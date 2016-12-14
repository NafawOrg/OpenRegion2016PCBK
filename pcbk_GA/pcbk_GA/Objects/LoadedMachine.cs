using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Objects
{
    public class LoadedMachine
    {
        public readonly Machine M;
        public int QueueSize {  get { return ordersQueue.Count; } }
        public LinkedList<Order> ordersQueue;
              
        public Order this[int i] {
            get {
                return this.ordersQueue.Skip(i).First();
            }
        }
        public LoadedMachine(Machine m)
        {
            this.M = m;
            this.ordersQueue = new LinkedList<Order>();            
        }

        public void AddOrder(Order o)
        {
            this.ordersQueue.AddLast(o);        
        }

        public void RemoveOrder(Order o)
        {
            bool result = false;
            result = this.ordersQueue.Remove(o);
            if (!result) throw new Exception("WTF");
        }

        public void AddBefore(Order newOrder, Order mark)
        {
            this.ordersQueue.AddBefore(ordersQueue.Find(mark), newOrder);
        }

        public void AddAfter(Order newOrder, Order mark)
        {
            this.ordersQueue.AddAfter(ordersQueue.Find(mark), newOrder);
        }

        public void ChangeOrder(Order newOrder, Order oldOrder)
        {
            LinkedListNode<Order> _old = this.ordersQueue.Find(oldOrder);
            _old.Value = newOrder;
        }

        public void ExchangeOrders(Order first, Order second)
        {
            LinkedListNode<Order> _first = this.ordersQueue.Find(first);
            LinkedListNode<Order> _second = this.ordersQueue.Find(second);
            _first.Value = first;
            _second.Value = second;
        }

        public LoadedMachine Clone()
        {
            return new LoadedMachine( this.M )
            {
                ordersQueue = new LinkedList<Order>( this.ordersQueue )
            };
        }
    }
}
