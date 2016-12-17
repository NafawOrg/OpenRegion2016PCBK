using pcbk_GA.Export;
using System;
using System.Collections.Generic;

namespace pcbk_GA.Objects
{
    interface IOrder
    {
        int Density { get; }
        int ProductId { get; }
        void calculateStats(Machine m, ref DateTime queue_time, out float waste, out int deadline);
        List<OrderJson> export(Machine m, ref DateTime queue_time, out float waste, out int deadline);
    }
}
