using pcbk_GA.Export;
using pcbk_GA.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Solutions.GA
{
    public class GAFitnessEstimator
    {
        private class CompressionPair
        {
            public readonly int leftId;
            public readonly int rightId;

            public CompressionPair(int leftOrdId, int rightOrdId)
            {
                this.leftId = leftOrdId;
                this.rightId = rightOrdId;
            }
        }     
        private class CompressedOrder: IOrder
        {
            public float Waste { get; private set; } // кол-во отходов двух заказов из компрессии(сумма)
            public double Time { get; private set; } // время производства двух заказов из компрессии(максимальное из 2ух заказов)
            public readonly CompressionPair pair;
            public readonly Order order_1;
            public readonly Order order_2;
            public float PenaltyWaste { get; set; } // Выгодность применения этой компрессии по объёму отходов относительно других компрессий
            public double PenaltyTime { get; set; } // Выгодность применения этой компрессии по кол-ву времени относительно других компрессий

            public Order BigOrder { get; private set; }
            public Order SmallOrder { get; private set; }
            private bool EqualTimeOrders;

            public CompressedOrder(CompressionPair p, Order firstOrder, Order secondOrder, Machine machine)
            {
                this.pair = p;
                this.order_1 = firstOrder;
                this.order_2 = secondOrder;

                // Раньше смотрел по объёму - не верно, потом по объём + ширина - тоже не верно, нужно по времени
                // производства т.к. именно оно определяет длину заказа(длину полотна)
                if (machine.CalcTime(order_1.Volume, order_1.Width) > machine.CalcTime(order_2.Volume, order_2.Width))
                { BigOrder = order_1; SmallOrder = order_2; }
                else
                { BigOrder = order_2; SmallOrder = order_1; }

                EqualTimeOrders = machine.CalcTime(order_1.Volume, order_1.Width) 
                    == machine.CalcTime(order_2.Volume, order_2.Width);

                CalculateCompressionInfo(machine);
            }
            private void CalculateCompressionInfo(Machine m)
            {

                // Если у заказов одинаковая ширина, то можно дополнительно оптимизировать
                // просуммировав объемы заказов, и пустив в одно полотно
                // например 2000х10т 2000х20т => делаем 4000х30т(понятно, что по факту получится объём больше из-за формулы)
                if ( order_1.Width == order_2.Width )
                {
                    Waste += m.CalcWaste( order_1.Volume + order_2.Volume, order_1.Width + order_2.Width);
                    Time += m.CalcTime( order_1.Volume + order_2.Volume, order_1.Width + order_2.Width);
                }
                else
                {
                    // Если остаток большого заказа можно разбить на 2 полотна...
                    // то считаем их совместный выпуск + оставшийся кусок большого заказа разбитый на 2 полотна
                    if (!EqualTimeOrders && BigOrder.Width * 2 < m.StripWidth)
                    {
                        //   _____ ___ ______
                        //  |  1  | 2 |   W1 |
                        //  |_____|___|______|
                        //  |_1.1_|_1.2_|_W2_|
  
                        // #W1
                        Waste = m.CalcWaste( SmallOrder.Volume, SmallOrder.Width, m.StripWidth - BigOrder.Width );
                        Time = m.CalcTime( SmallOrder.Volume, SmallOrder.Width );
                        
                        // #1 - объём Большого заказа, который будет произведен пока будет сделан #2-меньший заказ
                        float bigPartSize = m.CalcWaste( SmallOrder.Volume, SmallOrder.Width, SmallOrder.Width + BigOrder.Width );
                        
                        // #1.1 + 1.2 - оставшийся для производства объём от большего заказа, который пустим в 2 полотна
                        float leftVolume = BigOrder.Volume - bigPartSize;
                        Time += m.CalcTime( leftVolume, BigOrder.Width * 2f );
                        Waste += m.CalcWaste( leftVolume, BigOrder.Width * 2f );
                    }
                    else // считаем стандартный вариант: отходы от большего заказа - итоговый размер меньшего заказа = отходы итог
                    {
                        Time = m.CalcTime( BigOrder.Volume, BigOrder.Width );                        
                        // Узнаем сколько будет произведено отходов от большего заказа
                        Waste = m.CalcWaste( BigOrder.Volume, BigOrder.Width );
                        // Вычитаем из отходов размер меньшего заказа => остаются отходы
                        // в виде разницы длин и тонкой полоски если ширина 2ух заказов не покрывает ширину машины
                        Waste -= SmallOrder.Volume / ( (float)SmallOrder.Width / ( m.StripWidth - BigOrder.Width ) );
                    }
                }
            }

            int IOrder.Density
            {
                get { return order_1.Density; }
            }

            int IOrder.ProductId
            {
                get { return order_1.ProductId; }
            }

            void IOrder.calculateStats(Machine m, ref DateTime queue_time, out float waste, out int deadline)
            {
                var export = new List<OrderJson>();
                var bo = BigOrder;
                var so = SmallOrder;
                waste = Waste;

                if (order_1.Width == order_2.Width)
                {
                    queue_time = queue_time.AddHours(m.CalcTime(bo.Volume + so.Volume, bo.Width + so.Width));
                }
                else
                {                  
                    int parallelLines = (int)Math.Floor(m.StripWidth / (float)bo.Width);

                    if (parallelLines == 1)
                        queue_time = queue_time.AddHours(m.CalcTime(bo.Volume, bo.Width));
                    else
                    {
                        // #1 - объём Большого заказа, который будет произведен пока будет сделан #2-меньший заказ
                        float bigPartSize = m.CalcWaste(so.Volume, so.Width, so.Width + bo.Width);
                        float leftVolume = bo.Volume - bigPartSize;

                        queue_time = queue_time.AddHours(m.CalcTime(so.Volume, so.Width))
                                               .AddHours(m.CalcTime(leftVolume / parallelLines, bo.Width));
                    }
                }

                deadline = 0;
                foreach (var x in export)
                    if (x.Type != OrderTypeJson.Waste && x.Deadline < x.End)
                        deadline += (x.End - x.Deadline.Value).Hours;
            }

            List<OrderJson> IOrder.export(Machine m, ref DateTime queue_time, out float waste, out int deadline)
            {
                var export = new List<OrderJson>();
                var bo = BigOrder;
                var so = SmallOrder;
                waste = Waste;

                if (order_1.Width == order_2.Width)
                {
                    //       ___ ___ ___
                    //      | 1 | 2 | W |
                    //      |   |___|   |
                    //      |---|-3-|---|   1ый заказ перенес свою часть #3 на 2ую линию, чтобы оптимизироваться...
                    //                      итоговый размер идет по символам "---"

                    export.Add( // #1
                        new OrderJson(bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, 0)
                        {
                            Volume = bo.Volume - ((bo.Volume + so.Volume) / 2 - so.Volume),
                            Start = queue_time,
                            End = queue_time.AddHours(m.CalcTime(bo.Volume + so.Volume, bo.Width + so.Width))
                        });

                    if ((bo.Volume - (bo.Volume + so.Volume) / 2) > 0)
                        export.Add( // #3
                            new OrderJson(bo.Id, bo.Consumer, bo.productObj.Name, 0, so.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, bo.Width)
                            {
                                Volume = bo.Volume - (bo.Volume + so.Volume) / 2,
                                Start = queue_time.AddHours(m.CalcTime(so.Volume * 2f, bo.Width + so.Width)),
                                End = queue_time.AddHours(m.CalcTime(bo.Volume + so.Volume, bo.Width + so.Width))
                            });

                    export.Add( // #2
                        new OrderJson(so.Id, so.Consumer, so.productObj.Name, 0, so.Width, so.Density, so.Deadline, OrderTypeJson.Order, bo.Width)
                        {
                            Volume = so.Volume,
                            Start = queue_time,
                            End = queue_time.AddHours(m.CalcTime(so.Volume * 2f, bo.Width + so.Width))
                        });

                    if (bo.Width + so.Width < m.StripWidth)
                        export.Add( // #W
                            new OrderJson(bo.Id + "_" + so.Id + "W", String.Empty, String.Empty, 0, 0, so.Density, null, OrderTypeJson.Waste, bo.Width + so.Width)
                            {
                                Volume = m.CalcWaste(bo.Volume + so.Volume, bo.Width + so.Width),
                                Width = m.StripWidth - (bo.Width + so.Width),
                                Start = queue_time,
                                End = queue_time.AddHours(m.CalcTime(bo.Volume + so.Volume, bo.Width + so.Width))
                            });

                    queue_time = queue_time.AddHours(m.CalcTime(bo.Volume + so.Volume, bo.Width + so.Width));
                }
                else
                {
                    //       _____ ___ ___ 
                    //      |  1  | 2 | W1|   
                    //      |     |___|   |
                    //      |     | W2|   |
                    //      |_____|___|___|                     
                    int parallelLines = (int)Math.Floor(m.StripWidth / (float)bo.Width);

                    if (parallelLines == 1)
                    {
                        export.Add( // #1
                            new OrderJson(bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, 0)
                            {
                                Volume = bo.Volume,
                                Start = queue_time,
                                End = queue_time.AddHours(m.CalcTime(bo.Volume, bo.Width))
                            });
                        export.Add( // #2
                            new OrderJson(so.Id, so.Consumer, so.productObj.Name, 0, so.Width, so.Density, so.Deadline, OrderTypeJson.Order, bo.Width)
                            {
                                Volume = so.Volume,
                                Start = queue_time,
                                End = queue_time.AddHours(m.CalcTime(so.Volume, so.Width))
                            });
                        export.Add( // #W2
                            new OrderJson(so.Id + "W", String.Empty, String.Empty, 0, so.Width, so.Density, null, OrderTypeJson.Waste, bo.Width)
                            {
                                // W2 = Waste от 1 - W1 - 2
                                Volume = m.CalcWaste(bo.Volume, bo.Width)
                                    - m.CalcWaste(so.Volume, so.Width, m.StripWidth - bo.Width)
                                    - so.Volume,
                                Start = queue_time.AddHours(m.CalcTime(so.Volume, so.Width)),
                                End = queue_time.AddHours(m.CalcTime(bo.Volume, bo.Width))
                            });
                        if (bo.Width + so.Width < m.StripWidth)
                            export.Add( // #W1
                                new OrderJson(bo.Id + "_" + so.Id + "W", String.Empty, String.Empty, 0, 0, so.Density, null, OrderTypeJson.Waste, bo.Width + so.Width)
                                {
                                    Volume = m.CalcWaste(bo.Volume, bo.Width, m.StripWidth - so.Width),
                                    Width = m.StripWidth - (bo.Width + so.Width),
                                    Start = queue_time,
                                    End = queue_time.AddHours(m.CalcTime(bo.Volume, bo.Width))
                                });

                        queue_time = queue_time.AddHours(m.CalcTime(bo.Volume, bo.Width));
                    }
                    else
                    {
                        //   _____ ___ ______
                        //  |  1  | 2 |   W1 |
                        //  |_____|___|______|
                        //  |_1.1_|_1.2_|_W2_|

                        export.Add( // #1
                            new OrderJson(bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, 0)
                            {
                                Volume = m.CalcWaste(so.Volume, so.Width, so.Width + bo.Width), // объём 1 = отходы от производства 2ого - ширина W1
                                Start = queue_time,
                                End = queue_time.AddHours(m.CalcTime(so.Volume, so.Width))
                            });

                        export.Add( // #2
                            new OrderJson(so.Id, so.Consumer, so.productObj.Name, 0, so.Width, so.Density, so.Deadline, OrderTypeJson.Order, bo.Width)
                            {
                                Volume = so.Volume,
                                Start = queue_time,
                                End = queue_time.AddHours(m.CalcTime(so.Volume, so.Width))
                            });

                        if (bo.Width + so.Width < m.StripWidth)
                            export.Add( // #W1
                                new OrderJson(bo.Id + "_" + so.Id + "W", String.Empty, String.Empty, 0, 0, so.Density, null, OrderTypeJson.Waste, bo.Width + so.Width)
                                {
                                    Volume = m.CalcWaste(so.Volume, so.Width, m.StripWidth - bo.Width),
                                    Width = m.StripWidth - (bo.Width + so.Width),
                                    Start = queue_time,
                                    End = queue_time.AddHours(m.CalcTime(so.Volume, so.Width))
                                });

                        // #1 - объём Большого заказа, который будет произведен пока будет сделан #2-меньший заказ
                        float bigPartSize = m.CalcWaste(so.Volume, so.Width, so.Width + bo.Width);
                        float leftVolume = bo.Volume - bigPartSize;

                        // #1.1 .. 1.N
                        for (int i = 0; i < parallelLines; i++)
                        {
                            export.Add( // #1
                                new OrderJson(bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, bo.Width * i)
                                {
                                    Volume = leftVolume / parallelLines,
                                    Start = queue_time.AddHours(m.CalcTime(so.Volume, so.Width)),
                                    End = queue_time.AddHours(m.CalcTime(so.Volume, so.Width))
                                                    .AddHours(m.CalcTime(leftVolume / parallelLines, bo.Width))
                                });
                        }

                        if (bo.Width * parallelLines < m.StripWidth)
                            export.Add( // #W2
                                new OrderJson(bo.Id + "W", String.Empty, String.Empty, 0, 0, bo.Density, null, OrderTypeJson.Waste, bo.Width * parallelLines)
                                {
                                    Volume = m.CalcWaste(leftVolume, bo.Width * parallelLines, m.StripWidth),
                                    Width = m.StripWidth - (bo.Width * parallelLines),
                                    Start = queue_time.AddHours(m.CalcTime(so.Volume, so.Width)),
                                    End = queue_time.AddHours(m.CalcTime(so.Volume, so.Width))
                                                    .AddHours(m.CalcTime(leftVolume, bo.Width * parallelLines))
                                });

                        queue_time = queue_time.AddHours(m.CalcTime(so.Volume, so.Width))
                                               .AddHours(m.CalcTime(leftVolume / parallelLines, bo.Width));
                    }
                }

                deadline = 0;
                foreach (var x in export)
                    if (x.Type != OrderTypeJson.Waste && x.Deadline < x.End)
                        deadline += (x.End - x.Deadline.Value).Hours;

                return export;
            }
        }         

        private List<CompressionPair> CollectCompressions(List<Order> queue, Machine curr_m)
        {         
            List<CompressionPair> compressions = new List<CompressionPair>();

            for ( int j = 0; j < queue.Count; j++ )
            {
                Order curr_ord = queue[ j ];

                // изучаем следующий заказ на предмет компрессий
                if ( j + 1 < queue.Count )
                {
                    Order next_ord = queue[ j + 1 ];
                    if ( curr_ord.Density != next_ord.Density || curr_ord.ProductId != next_ord.ProductId )
                        continue;

                    if ( curr_ord.Width + next_ord.Width <= curr_m.StripWidth )
                        compressions.Add( new CompressionPair( j, j + 1 ) );
                }
            }

            return compressions;
        }
        private List<CompressedOrder> ChooseBestCompressions(List<CompressionPair> compressions, List<Order> queue, Machine curr_m)
        {
            // COMPRESSION TASKS:
            // - оценить отходы
            // - выбирать лучшие варианты комбинаций из смежных компрессий
            //
            //  0.1v - самая простая, разницу в объёме считаем за отход
            //
            // отходы последовательности предварительно посчитаны,
            // однако если есть возможность комрессии придется это сделать заного
            // так же видимо придется заного учесть информацию по deadlineHours и finish время заказа
            // т.к. заказы могут пойти параллельно

            // при оценке компрессий нужно учитывать распады:
            // в случае если <1,2>,<2,3>, выбирая одну из компрессий будет 1 распад на <1><2,3> или <1,2><3>
            // в случае если <1,2><2,3><3,4> и мы выбираем <2,3>, она должна превосходить по выгодности
            // сумму из <1,2> и <3,4>, т.к. приводит к распаду <1><2,3><4>
            // TODO: так же нужно что-то думать с deadlineHours...
            // т.к. компрессии можно выбирать и с точки зрения временных потерь
            List<CompressedOrder> finalCompressions = new List<CompressedOrder>();
            List<List<CompressedOrder>> groups = new List<List<CompressedOrder>>();
            for ( int k = 0; k < compressions.Count; k++ )
            {
                CompressionPair next_comp = null, prev_comp = null;
                CompressionPair curr_comp = compressions[ k ];

                // p - prev_comp, c - curr_comp, n - next_comp
                if ( k + 1 < compressions.Count && compressions[ k + 1 ].leftId == curr_comp.rightId ) // смежные компрессии P C [N]
                    next_comp = compressions[ k + 1 ];

                if ( k - 1 >= 0 && compressions[ k - 1 ].rightId == curr_comp.leftId ) // смежные компрессии [P] C N
                    prev_comp = compressions[ k - 1 ];

                CompressedOrder comp = new CompressedOrder(curr_comp, queue[ curr_comp.leftId ], queue[ curr_comp.rightId ], curr_m);

                if ( prev_comp == null && next_comp == null )
                {
                    // Первоначально finalCompressions заполняем одиночками. Далее дополним их элементами из групп, проведя распад некоторых.
                    finalCompressions.Add( comp );
                    continue;
                }
                else if ( prev_comp == null ) // next_compression присутствует, создаем новую "активную(текущую)" группу смежных компрессий
                {
                    var group = new List<CompressedOrder>();
                    group.Add( comp );
                    groups.Add( group );
                }
                else if ( next_comp == null ) // prev_compression присутствует, добавляем в последнюю активную группу и он будет замыкающим этой группы
                {
                    groups[ groups.Count - 1 ].Add( comp );
                }
                else // next_compression + prev_compression, добавляем в последнюю активную группу
                {
                    groups[ groups.Count - 1 ].Add( comp );
                }
            }

            // Вычисляем выгодность компрессий
            for ( int j = 0; j < groups.Count; j++ )
            {
                List<CompressedOrder> group = groups[ j ];

                for ( int k = 0; k < group.Count; k++ )
                {
                    if ( k + 1 < group.Count && k - 1 > 0 ) // элемент по центру [ k - 1 ] [ k ] [ k + 1 ]
                    {
                        // если отрицательное, то отходов меньше, чем сумма 2ух соседних => хорошо
                        group[ k ].PenaltyWaste = group[ k ].Waste - ( group[ k - 1 ].Waste + group[ k + 1 ].Waste );

                        // если отрицательное, то выполняется быстрее, чем сумма соседних заказов => хорошо
                        group[ k ].PenaltyTime = group[ k ].Time - ( group[ k - 1 ].Time + group[ k + 1 ].Time );
                    }
                    else if ( k + 1 < group.Count ) // элемент слева [ k ] [ k + 1 ]
                    {
                        group[ k ].PenaltyWaste = group[ k ].Waste - group[ k + 1 ].Waste;
                        group[ k ].PenaltyTime = group[ k ].Time - group[ k + 1 ].Time;
                    }
                    else // элемент справа [ k - 1 ] [ k ]
                    {
                        group[ k ].PenaltyWaste = group[ k ].Waste - group[ k - 1 ].Waste;
                        group[ k ].PenaltyTime = group[ k ].Time - group[ k - 1 ].Time;
                    }
                }

                // Производим разгруппировку смежных компрессий в пользу наиболее выгодных
                finalCompressions.AddRange( unMergeGroup( group ) );
            }

            return finalCompressions.OrderBy( x => x.pair.leftId ).ToList();
        }
        private List<CompressedOrder> unMergeGroup(List<CompressedOrder> group)
        {
            // Вероятно очень оптимистичный образ разложения групп,
            // каждый раз выбирая для распада наименьший по штрафам элемент.
            // Не до конца понял, может ли реально влиять элемент больше чем на 2 соседа при распаде
            // или это может повлечь более глубокие последствия.
            // хотя вроде норм!)
            List<CompressedOrder> result = new List<CompressedOrder>();

            while ( group.Count > 0 )
            {
                group = group.OrderBy( x => x.PenaltyWaste ).ThenBy( y => y.PenaltyTime ).ToList();
                CompressedOrder c = group.First();
                group.RemoveAt( 0 );
                // Раскрывая группу из смежных элементов, удаляем смежные с выбранным[c] элементом
                group.RemoveAll( x => x.pair.rightId == c.pair.leftId || x.pair.leftId == c.pair.rightId );
                result.Add( c );
            }

            return result;
        }
        private List<IOrder> UnionAllOrders(List<Order> orders, List<CompressedOrder> compressions)
        {
            List<IOrder> result = new List<IOrder>();

            if (compressions.Count == 0)
            {
                orders.ForEach(x => result.Add(x));
                return result;
            }

            for (int i = 0, j = 0; i < orders.Count; )
            {
                if (j == compressions.Count)
                {
                    // Сюда попадем только в том случае если обошли все компрессии и последняя
                    // компрессия не являлась склейкой двух последних элементов в orders
                    result.Add(orders[ i ]);
                    i++;
                }
                else if (i < compressions[ j ].pair.leftId) // Идем по обычным заказам
                {
                    result.Add(orders[ i ]);
                    i++;
                }
                else // Встретили компрессию
                {
                    result.Add(compressions[ j ]);
                    i = compressions[ j ].pair.rightId + 1;
                    j++;
                }
            }

            return result;
        }

        public double FitnessFunction( List<LoadedMachine> loadedMachines )
        {
            DateTime curr_date = GA.today;
            float wasteVolume = 0; // кол-во отходов
            float deadlineHours = 0; // кол-во часов прошедших после даты отгрузки заказа до выполнения заказа
            float readjustmentHours = 0; // кол-во времени на переналадки

            for ( int i = 0; i < loadedMachines.Count; i++ )
            {
                LoadedMachine curr_lm = loadedMachines[ i ];
                DateTime queue_time = curr_date;

                // оценочные переменные в рамках текущей очереди у данной машины
                float queue_wasteVolume = 0f;
                float queue_readjustmentHours = 0f;
                float queue_deadlineHours = 0f;
                float waste = 0f;
                int deadHours = 0;
                int readjusttHours = 0;
                DateTime finish;
                
                // Возможно нет компрессий в текущей генерации?
                var queue = curr_lm.ordersQueue.ToList();
                List<CompressionPair> compressions = CollectCompressions(queue, curr_lm);
                List<CompressedOrder> finalCompressions = ChooseBestCompressions(compressions, queue, curr_lm);

                var finalQueue = UnionAllOrders(queue, finalCompressions);
                for ( int k = 0; k < finalQueue.Count; k++ )
                {
                    IOrder curr = finalQueue[ k ];
                    if ( k > 0 )
                    {
                        IOrder prev = finalQueue[ k - 1 ];
                        //if ( curr is CompressedOrder )
                            //finish = queue_time.AddHours( 1 );

                        if (curr.Density != prev.Density || curr.ProductId != prev.ProductId)
                        {
                            finish = queue_time.AddHours(1);
                            queue_time = finish; readjusttHours++;
                        }
                    }

                    curr.calculateStats(curr_lm, ref queue_time, out waste, out deadHours);

                    queue_wasteVolume += waste;
                    queue_deadlineHours += deadHours;
                    queue_readjustmentHours += readjusttHours;
                    readjusttHours = 0;
                }                

                wasteVolume += queue_wasteVolume;
                readjustmentHours += queue_readjustmentHours;
                deadlineHours += queue_deadlineHours;
            }

            // коэффициенты важности каждого значения - нужно выбирать эампирически
            // чем больше тем хуже
            // добавить ln?          

            double finalScore = 0.5f * Math.Log( wasteVolume + 1 ) + 0.4f * Math.Log( deadlineHours + 1 ) + 0.1f * Math.Log( readjustmentHours + 1 );
                //0.4f * wasteVolume + 0.5f * deadlineHours + 0.1f * readjustmentHours;
            return finalScore;
        }

        /***************
         * EXPORT BLOCK *
        ****************/   
        public ResultJson Export(Genome g)
        {            
            var result = new ResultJson();

            for (int i = 0; i < g.LoadedMachines.Count; i++)
            {
                var lm = g.LoadedMachines[i];
                LoadedMachineJson lmj = new LoadedMachineJson(lm);
                lmj.TimelineStart = GA.today.Date;

                DateTime queue_time = GA.today;
                var queue = lm.ordersQueue.ToList();                
                var compressions = CollectCompressions(queue, lm);
                
                var finalCompressions = ChooseBestCompressions(compressions, queue, lm);

                float waste = 0f;
                int deadlineHours = 0;
                int readjustmentHours = 0;
                DateTime finish;
                var finalQueue = UnionAllOrders(queue, finalCompressions);
                for( int k = 0; k < finalQueue.Count; k++)
                {
                    IOrder curr = finalQueue[k];
                    if (k > 0)
                    {
                        Object prevObj = finalQueue[ k - 1 ];
                        #region добавляем переналадку
                        if (curr is Order)
                        {
                            var curOrder = ( Order )curr;                            
                            if (prevObj is Order)
                            {                                    
                                var prevOrder = ( Order )prevObj;
                                if ( curOrder.Density != prevOrder.Density
                                    || curOrder.ProductId != prevOrder.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );                                        
                                    lmj.Orders.AddReadjustment( queue_time, finish, lm, prevOrder );
                                    queue_time = finish; readjustmentHours++;
                                }
                            }
                            else
                            {
                                var prevComp = (CompressedOrder)prevObj;
                                if ( curOrder.Density != prevComp.order_1.Density
                                    || curOrder.ProductId != prevComp.order_1.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    lmj.Orders.AddReadjustment( queue_time, finish, lm, prevComp.order_1, prevComp.order_2 );
                                    queue_time = finish; readjustmentHours++;
                                }
                            }                            
                        }
                        else
                        {
                            var curComp = (CompressedOrder)curr;
                            finish = queue_time.AddHours( 1 );
                            if ( prevObj is Order )
                            {
                                var prevOrder = ( Order )prevObj;
                                if ( curComp.order_1.Density != prevOrder.Density
                                    || curComp.order_1.ProductId != prevOrder.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    lmj.Orders.AddReadjustment( queue_time, finish, lm, prevOrder );
                                    queue_time = finish; readjustmentHours++;
                                }
                            }
                            else
                            {
                                var prevComp = (CompressedOrder)prevObj;
                                if ( curComp.order_1.Density != prevComp.order_1.Density
                                    || curComp.order_1.ProductId != prevComp.order_1.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    lmj.Orders.AddReadjustment( queue_time, finish, lm, prevComp.order_1, prevComp.order_2 );
                                    queue_time = finish; readjustmentHours++;                                    
                                }
                            }
                            
                        }
                        #endregion                        
                    }

                    lmj.Orders.AddRange(curr.export(lm, ref queue_time, out waste, out deadlineHours));

                    lmj.WasteVolume += waste;
                    lmj.DeadlineHours += deadlineHours;
                    lmj.ReadjustmentHours += readjustmentHours;
                    readjustmentHours = 0;
                }
                
                result.Machines.Add(lmj);
            }

            return result;
        }
    }
}
