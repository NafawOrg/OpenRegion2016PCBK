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
            public readonly int leftOrdId;
            public readonly int rightOrdId;

            public CompressionPair (int _leftOrdId, int _rightOrdId)
            {
                this.leftOrdId = _leftOrdId;
                this.rightOrdId = _rightOrdId;
            }
        }              
        private class CompressionInfo
        {
            public float Waste = 0f;    // кол-во отходов двух заказов из компрессии(сумма)
            public double Time = 0f;    // время производства двух заказов из компрессии(максимальное)
        }
        private class Compression
        {
            public readonly CompressionPair pair;
            public readonly Order order_1;
            public readonly Order order_2;
            public readonly Machine m;
            public readonly CompressionInfo compInfo;
            public float PenaltyWaste { get; set; } // Выгодность применения этой компрессии по объёму отходов относительно других компрессий
            public double PenaltyTime { get; set; } // Выгодность применения этой компрессии по кол-ву времени относительно других компрессий

            // Раньше смотрел по объёму - не верно, потом по объём + ширина - тоже не верно, нужно по времени
            // производства т.к. именно оно определяет длину заказа(длину полотна)
            public Order BigOrder {
                get {
                    if (CalcTime(order_1.Volume, order_1.Width, m) > CalcTime(order_2.Volume, order_2.Width, m))
                        return order_1;
                    else
                        return order_2;
                }
            }
            public Order SmallOrder {
                get {
                    if (CalcTime(order_1.Volume, order_1.Width, m) > CalcTime(order_2.Volume, order_2.Width, m))
                        return order_2;
                    else
                        return order_1;
                }
            }
            private bool EqualTimeOrders
            {
                get { return CalcTime( order_1.Volume, order_1.Width, m ) == CalcTime( order_2.Volume, order_2.Width, m ); }
            }

            public Compression( CompressionPair p, Order firstOrder, Order secondOrder, Machine machine )
            {
                this.pair = p;
                this.order_1 = firstOrder;
                this.order_2 = secondOrder;
                this.m = machine;
                this.compInfo = this.CompressionInfo();
            }
            private CompressionInfo CompressionInfo()
            {
                CompressionInfo info = new CompressionInfo();

                // Если у заказов одинаковая ширина, то можно дополнительно оптимизировать
                // просуммировав объемы заказов, и пустив в одно полотно
                // например 2000х10т 2000х20т => делаем 4000х30т(понятно, что по факту получится объём больше из-за формулы)
                if ( order_1.Width == order_2.Width )
                {
                    info.Waste += CalcWaste( order_1.Volume + order_2.Volume, order_1.Width + order_2.Width, this.m );
                    info.Time += CalcTime( order_1.Volume + order_2.Volume, order_1.Width + order_2.Width, this.m );
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
                        info.Waste = CalcWaste( SmallOrder.Volume, SmallOrder.Width, this.m.StripWidth - BigOrder.Width );
                        info.Time = CalcTime( SmallOrder.Volume, SmallOrder.Width, this.m );
                        
                        // #1 - объём Большого заказа, который будет произведен пока будет сделан #2-меньший заказ
                        float bigPartSize = CalcWaste( SmallOrder.Volume, SmallOrder.Width, SmallOrder.Width + BigOrder.Width );
                        
                        // #1.1 + 1.2 - оставшийся для производства объём от большего заказа, который пустим в 2 полотна
                        float leftVolume = BigOrder.Volume - bigPartSize;
                        info.Time += CalcTime( leftVolume, BigOrder.Width * 2f, this.m );
                        info.Waste += CalcWaste( leftVolume, BigOrder.Width * 2f, this.m );
                    }
                    else // считаем стандартный вариант: отходы от большего заказа - итоговый размер меньшего заказа = отходы итог
                    {
                        info.Time = CalcTime( BigOrder.Volume, BigOrder.Width, this.m );                        
                        // Узнаем сколько будет произведено отходов от большего заказа
                        info.Waste = CalcWaste( BigOrder.Volume, BigOrder.Width, this.m );
                        // Вычитаем из отходов размер меньшего заказа => остаются отходы
                        // в виде разницы длин и тонкой полоски если ширина 2ух заказов не покрывает ширину машины
                        info.Waste -= SmallOrder.Volume / ( (float)SmallOrder.Width / ( this.m.StripWidth - BigOrder.Width ) );
                    }
                }

                return info;
            }
        }
        
        private static float CalcWaste( float volume, float width, Machine machine )
        {
            return CalcWaste(volume, width, machine.StripWidth);
        }
        private static float CalcWaste(float volume, float width, float stripWidth)
        {
            float real_total_volume = volume / (width / stripWidth);
            float wasted_volume = real_total_volume * (1f - width / stripWidth);
            return wasted_volume;
        }
        private static double CalcTime( float volume, float width, Machine machine )
        {
            float real_total_volume = volume / ( width / machine.StripWidth );
            double production_time = Math.Round( ( double )( real_total_volume / machine.HourPerfomance ), 2 );
            return production_time;
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
        private List<Compression> ChooseBestCompressions( List<CompressionPair> compressions, List<Order> queue, Machine curr_m )
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
            List<Compression> finalCompressions = new List<Compression>();
            List<List<Compression>> groups = new List<List<Compression>>();
            for ( int k = 0; k < compressions.Count; k++ )
            {
                CompressionPair next_comp = null, prev_comp = null;
                CompressionPair curr_comp = compressions[ k ];

                // p - prev_comp, c - curr_comp, n - next_comp
                if ( k + 1 < compressions.Count && compressions[ k + 1 ].leftOrdId == curr_comp.rightOrdId ) // смежные компрессии P C [N]
                    next_comp = compressions[ k + 1 ];

                if ( k - 1 >= 0 && compressions[ k - 1 ].rightOrdId == curr_comp.leftOrdId ) // смежные компрессии [P] C N
                    prev_comp = compressions[ k - 1 ];

                Compression comp = new Compression( curr_comp, queue[ curr_comp.leftOrdId ], queue[ curr_comp.rightOrdId ], curr_m );

                if ( prev_comp == null && next_comp == null )
                {
                    // Первоначально finalCompressions заполняем одиночками. Далее дополним их элементами из групп, проведя распад некоторых.
                    finalCompressions.Add( comp );
                    continue;
                }
                else if ( prev_comp == null ) // next_compression присутствует, создаем новую "активную(текущую)" группу смежных компрессий
                {
                    var group = new List<Compression>();
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
                List<Compression> group = groups[ j ];

                for ( int k = 0; k < group.Count; k++ )
                {
                    if ( k + 1 < group.Count && k - 1 > 0 ) // элемент по центру [ k - 1 ] [ k ] [ k + 1 ]
                    {
                        // если отрицательное, то отходов меньше, чем сумма 2ух соседних => хорошо
                        group[ k ].PenaltyWaste = group[ k ].compInfo.Waste - ( group[ k - 1 ].compInfo.Waste + group[ k + 1 ].compInfo.Waste );

                        // если отрицательное, то выполняется быстрее, чем сумма соседних заказов => хорошо
                        group[ k ].PenaltyTime = group[ k ].compInfo.Time - ( group[ k - 1 ].compInfo.Time + group[ k + 1 ].compInfo.Time );
                    }
                    else if ( k + 1 < group.Count ) // элемент слева [ k ] [ k + 1 ]
                    {
                        group[ k ].PenaltyWaste = group[ k ].compInfo.Waste - group[ k + 1 ].compInfo.Waste;
                        group[ k ].PenaltyTime = group[ k ].compInfo.Time - group[ k + 1 ].compInfo.Time;
                    }
                    else // элемент справа [ k - 1 ] [ k ]
                    {
                        group[ k ].PenaltyWaste = group[ k ].compInfo.Waste - group[ k - 1 ].compInfo.Waste;
                        group[ k ].PenaltyTime = group[ k ].compInfo.Time - group[ k - 1 ].compInfo.Time;
                    }
                }

                // Производим разгруппировку смежных компрессий в пользу наиболее выгодных
                finalCompressions.AddRange( unMergeGroup( group ) );
            }

            return finalCompressions.OrderBy( x => x.pair.leftOrdId ).ToList();
        }
        private List<Compression> unMergeGroup( List<Compression> group )
        {
            // Вероятно очень оптимистичный образ разложения групп,
            // каждый раз выбирая для распада наименьший по штрафам элемент.
            // Не до конца понял, может ли реально влиять элемент больше чем на 2 соседа при распаде
            // или это может повлечь более глубокие последствия.
            // хотя вроде норм!)
            List<Compression> result = new List<Compression>();

            while ( group.Count > 0 )
            {
                group = group.OrderBy( x => x.PenaltyWaste ).ThenBy( y => y.PenaltyTime ).ToList();
                Compression c = group.First();
                group.RemoveAt( 0 );
                // Раскрывая группу из смежных элементов, удаляем смежные с выбранным[c] элементом
                group.RemoveAll( x => x.pair.rightOrdId == c.pair.leftOrdId || x.pair.leftOrdId == c.pair.rightOrdId );
                result.Add( c );
            }

            return result;
        }

        // wasteVolume, deadlineHours, readjustmentHours, queue_finish_time
        private Tuple<float, int, int, DateTime> QueueStats(
            int start, int end, DateTime queue_time, List<Order> queue, Machine curr_m)
        {
            float wasteVolume = 0; // кол-во отходов
            int deadlineHours = 0; // кол-во часов прошедших после даты отгрузки заказа до выполнения заказа
            int readjustmentHours = 0; // кол-во времени на переналадки                               

            for ( int j = start; j < end; j++ )
            {
                Order curr_ord = queue[ j ];

                // TODO: если ширина позволяет пускать заказ в 2 полосы? ( пересчёт отходов и времени производства )
                // x = k/m * вес заказа  
                // где k - ширина заказа, m - ширина полотна машины, x = сколько реально надо выпустить
                wasteVolume += CalcWaste( curr_ord.Volume, curr_ord.Width, curr_m );

                // получаем кол-во часов для производства заказа
                double ord_production_time = CalcTime( curr_ord.Volume, curr_ord.Width, curr_m );
                DateTime finish = queue_time.AddHours( ord_production_time );
                if ( finish > curr_ord.Deadline )
                    deadlineHours += ( finish - curr_ord.Deadline ).Hours;

                // изучаем следующий заказ на предмет переналадок
                if ( j + 1 < end )
                {
                    Order next_ord = queue[ j + 1 ];
                    if ( curr_ord.Density != next_ord.Density || curr_ord.ProductId != next_ord.ProductId )
                    {
                        readjustmentHours += 1;
                        queue_time = finish.AddHours( 1 ); // добавляем час к времени, т.к. будет переналадка перед следующим заказом
                        continue;
                    }
                }

                queue_time = finish; // сдвигаем текущее время очереди
            }

            return new Tuple<float, int, int, DateTime>(
                wasteVolume, deadlineHours, readjustmentHours, queue_time );
        }

        public double FitnessFunctionOld(List<LoadedMachine> loadedMachines)
        {
            DateTime curr_date = GA.today;
            float wasteVolume = 0; // кол-во отходов
            float deadlineHours = 0; // кол-во часов прошедших после даты отгрузки заказа до выполнения заказа
            float readjustmentHours = 0; // кол-во времени на переналадки

            for (int i = 0; i < loadedMachines.Count; i++)
            {
                LoadedMachine curr_lm = loadedMachines[i];
                Machine curr_m = curr_lm.M;
                DateTime queue_time = curr_date;

                // оценочные переменные в рамках текущей очереди у данной машины
                float queue_wasteVolume = 0f;
                float queue_readjustmentHours = 0f;
                float queue_deadlineHours = 0f;

                // Возможно нет компрессий в текущей генерации?
                var queue = curr_lm.ordersQueue.ToList();
                List<CompressionPair> compressions = CollectCompressions(queue, curr_m);

                if (compressions.Count == 0)
                {
                    var stats = QueueStats(0, queue.Count, queue_time, queue, curr_m);
                    queue_wasteVolume = stats.Item1;
                    queue_deadlineHours = stats.Item2;
                    queue_readjustmentHours = stats.Item3;
                }
                else
                {
                    // среди найденных компрессий выбираем обычные(не смежные ни с кем) и лучшие из смежных групп
                    // пример смежной группы: <1,2><2,3><3,4>, выбрав элемент по середине, получим <1><2,3><4>
                    List<Compression> finalCompressions = ChooseBestCompressions(compressions, queue, curr_m);

                    // Подсчитываем итого с использованием компрессий
                    queue_wasteVolume = 0f;
                    queue_deadlineHours = 0f;
                    queue_readjustmentHours = 0f;
                    int start = 0;
                    int end = 0;
                    Compression last_comp = null;
                    // кумулятивный результат:
                    // 1. считаем последовательность из обычных заказов( друг за другом ) - QueueStats
                    // 2. прибавляем результат компрессии
                    // 3. возвращаемся к 1. пока не закончатся компрессии
                    foreach (var r in finalCompressions)
                    {
                        end = r.pair.leftOrdId;
                        var temp_stats = QueueStats(start, end, queue_time, queue, curr_m);

                        queue_wasteVolume += temp_stats.Item1 + r.compInfo.Waste;
                        queue_deadlineHours += temp_stats.Item2;
                        queue_readjustmentHours += temp_stats.Item3;
                        queue_time = temp_stats.Item4.AddHours(r.compInfo.Time); //TODO:мне кажется или тут не хватает доп проверки на deadline hours у компрессии??

                        start = r.pair.rightOrdId + 1;
                        last_comp = r;
                    }
                    // 4.подсчитываем последний блок если таковой имеется
                    if (start < queue.Count)
                    {
                        end = queue.Count;
                        var temp_stats = QueueStats(start, end, queue_time, queue, curr_m);

                        queue_wasteVolume += temp_stats.Item1;
                        queue_deadlineHours += temp_stats.Item2;
                        queue_readjustmentHours += temp_stats.Item3;
                    }
                }

                wasteVolume += queue_wasteVolume;
                readjustmentHours += queue_readjustmentHours;
                deadlineHours += queue_deadlineHours;
            }

            // коэффициенты важности каждого значения - нужно выбирать эампирически
            // чем больше тем хуже
            // добавить ln?          

            double finalScore = //0.4f * Math.Log( wasteVolume + 1 ) + 0.5f * Math.Log( deadlineHours + 1 ) + 0.1f * Math.Log( readjustmentHours + 1 );
                0.4f * wasteVolume + 0.5f * deadlineHours + 0.1f * readjustmentHours;
            return finalScore;
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
                Machine curr_m = curr_lm.M;
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
                List<CompressionPair> compressions = CollectCompressions( queue, curr_m );
                List<Compression> finalCompressions = ChooseBestCompressions( compressions, queue, curr_m );

                var finalQueue = ConcatQueueAndCompressions( queue, finalCompressions );
                for ( int k = 0; k < finalQueue.Count; k++ )
                {
                    Object curr = finalQueue[ k ];
                    if ( k > 0 )
                    {
                        Object prevObj = finalQueue[ k - 1 ];
                        #region добавляем переналадку
                        if ( curr is Order )
                        {
                            var curOrder = ( Order )curr;
                            if ( prevObj is Order )
                            {
                                var prevOrder = ( Order )prevObj;
                                if ( curOrder.Density != prevOrder.Density
                                    || curOrder.ProductId != prevOrder.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    queue_time = finish; readjusttHours++;
                                }
                            }
                            else
                            {
                                var prevComp = ( Compression )prevObj;
                                if ( curOrder.Density != prevComp.order_1.Density
                                    || curOrder.ProductId != prevComp.order_1.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    queue_time = finish; readjusttHours++;
                                }
                            }
                        }
                        else
                        {
                            var curComp = ( Compression )curr;
                            finish = queue_time.AddHours( 1 );
                            if ( prevObj is Order )
                            {
                                var prevOrder = ( Order )prevObj;
                                if ( curComp.order_1.Density != prevOrder.Density
                                    || curComp.order_1.ProductId != prevOrder.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    queue_time = finish; readjusttHours++;
                                }
                            }
                            else
                            {
                                var prevComp = ( Compression )prevObj;
                                if ( curComp.order_1.Density != prevComp.order_1.Density
                                    || curComp.order_1.ProductId != prevComp.order_1.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    queue_time = finish; readjusttHours++;
                                }
                            }

                        }
                        #endregion
                    }

                    if ( curr is Order )
                        OrderExport( ( Order )curr, ref queue_time, curr_m, out waste, out deadHours );
                    else
                        CompressionExport( ( Compression )curr, ref queue_time, curr_m, out waste, out deadHours );

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

        private List<OrderJson> CompressionExportOld(
            Compression c, ref DateTime queue_time, Machine curr_m, out float waste, out int deadline)
        {
            var export = new List<OrderJson>();
            var bo = c.BigOrder;
            var so = c.SmallOrder;
            waste = c.compInfo.Waste;            

            if ( c.order_1.Width == c.order_2.Width )
            {
                //       ___ ___ ___
                //      | 1 | 2 | W |
                //      |   |___|   |
                //      |---|-3-|---|   1ый заказ перенес свою часть #3 на 2ую линию, чтобы оптимизироваться...
                //                      итоговый размер идет по символам "---"
                            
                export.Add( // #1
                    new OrderJson( bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, 0 )
                    {
                        Volume = bo.Volume - ( ( bo.Volume + so.Volume ) / 2 - so.Volume ),
                        Start = queue_time,
                        End = queue_time.AddHours(CalcTime( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ))
                    } );

                if ( ( bo.Volume - ( bo.Volume + so.Volume ) / 2 ) > 0 )
                    export.Add( // #3
                        new OrderJson( bo.Id, bo.Consumer, bo.productObj.Name, 0, so.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, bo.Width )
                        {
                            Volume = bo.Volume - ( bo.Volume + so.Volume ) / 2,
                            Start = queue_time.AddHours( CalcTime( so.Volume * 2f, bo.Width + so.Width, curr_m ) ),
                            End = queue_time.AddHours( CalcTime( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ) )
                        } );

                export.Add( // #2
                    new OrderJson( so.Id, so.Consumer, so.productObj.Name, 0, so.Width, so.Density, so.Deadline, OrderTypeJson.Order, bo.Width )
                    {
                        Volume = so.Volume,
                        Start = queue_time,
                        End = queue_time.AddHours( CalcTime( so.Volume * 2f, bo.Width + so.Width, curr_m ) )
                    } );

                if ( bo.Width + so.Width < curr_m.StripWidth )
                    export.Add( // #W
                        new OrderJson( bo.Id + "_" + so.Id + "W", String.Empty, String.Empty, 0, 0, so.Density, null, OrderTypeJson.Waste, bo.Width + so.Width )
                        {
                            Volume = CalcWaste( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ),
                            Width = curr_m.StripWidth - ( bo.Width + so.Width ),
                            Start = queue_time,
                            End = queue_time.AddHours( CalcTime( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ) )
                        } );

                queue_time = queue_time.AddHours( CalcTime( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ) );
            }
            else
            {
                //TODO
                // пока вариант тот, что слева
                //       _____ ___ ___                              _____ ___ ___
                //      |  1  | 2 | W1|                            |  1  | 2 | W |
                //      |     |___|   |       => TODO: сделать так |_____|___|_  |
                //      |     | W2|   |                            |__1__|__1__|_|
                //      |_____|___|___|                          

                export.Add( // #1
                    new OrderJson( bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, 0 )
                    {
                        Volume = bo.Volume,
                        Start = queue_time,
                        End = queue_time.AddHours(CalcTime(bo.Volume, bo.Width, curr_m))
                    });

                export.Add( // #2
                    new OrderJson( so.Id, so.Consumer, so.productObj.Name, 0, so.Width, so.Density, so.Deadline, OrderTypeJson.Order, bo.Width )
                    {
                        Volume = so.Volume,
                        Start = queue_time,
                        End = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) )
                    } );

                export.Add( // #W2
                    new OrderJson( so.Id + "W", String.Empty, String.Empty, 0, so.Width, so.Density, null, OrderTypeJson.Waste, bo.Width )
                    {
                        // W2 = Waste от 1 - W1 - 2
                        Volume = CalcWaste(bo.Volume, bo.Width, curr_m)
                            - CalcWaste(so.Volume, so.Width, curr_m.StripWidth - bo.Width)
                            - so.Volume,
                        Start = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) ),
                        End = queue_time.AddHours( CalcTime( bo.Volume, bo.Width, curr_m ) )
                    } );

                if ( bo.Width + so.Width < curr_m.StripWidth )
                    export.Add( // #W1
                        new OrderJson( bo.Id + "_" + so.Id + "W", String.Empty, String.Empty, 0, 0, so.Density, null, OrderTypeJson.Waste, bo.Width + so.Width )
                        {
                            Volume = CalcWaste( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ),
                            Width = curr_m.StripWidth - (bo.Width + so.Width),
                            Start = queue_time,
                            End = queue_time.AddHours( CalcTime( bo.Volume, bo.Width, curr_m ) )
                        } );

                queue_time = queue_time.AddHours( CalcTime( bo.Volume, bo.Width, curr_m ) );
            }

            deadline = 0;
            foreach(var x in export)
                if ( x.Type != OrderTypeJson.Waste && x.Deadline < x.End )
                    deadline += ( x.End - x.Deadline.Value ).Hours;           

            return export;
        }

        private List<OrderJson> CompressionExport(
            Compression c, ref DateTime queue_time, Machine curr_m, out float waste, out int deadline)
        {
            var export = new List<OrderJson>();
            var bo = c.BigOrder;
            var so = c.SmallOrder;
            waste = c.compInfo.Waste;

            if ( c.order_1.Width == c.order_2.Width )
            {
                //       ___ ___ ___
                //      | 1 | 2 | W |
                //      |   |___|   |
                //      |---|-3-|---|   1ый заказ перенес свою часть #3 на 2ую линию, чтобы оптимизироваться...
                //                      итоговый размер идет по символам "---"

                export.Add( // #1
                    new OrderJson( bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, 0 )
                    {
                        Volume = bo.Volume - ( ( bo.Volume + so.Volume ) / 2 - so.Volume ),
                        Start = queue_time,
                        End = queue_time.AddHours( CalcTime( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ) )
                    } );

                if ( ( bo.Volume - ( bo.Volume + so.Volume ) / 2 ) > 0 )
                    export.Add( // #3
                        new OrderJson( bo.Id, bo.Consumer, bo.productObj.Name, 0, so.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, bo.Width )
                        {
                            Volume = bo.Volume - ( bo.Volume + so.Volume ) / 2,
                            Start = queue_time.AddHours( CalcTime( so.Volume * 2f, bo.Width + so.Width, curr_m ) ),
                            End = queue_time.AddHours( CalcTime( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ) )
                        } );

                export.Add( // #2
                    new OrderJson( so.Id, so.Consumer, so.productObj.Name, 0, so.Width, so.Density, so.Deadline, OrderTypeJson.Order, bo.Width )
                    {
                        Volume = so.Volume,
                        Start = queue_time,
                        End = queue_time.AddHours( CalcTime( so.Volume * 2f, bo.Width + so.Width, curr_m ) )
                    } );

                if ( bo.Width + so.Width < curr_m.StripWidth )
                    export.Add( // #W
                        new OrderJson( bo.Id + "_" + so.Id + "W", String.Empty, String.Empty, 0, 0, so.Density, null, OrderTypeJson.Waste, bo.Width + so.Width )
                        {
                            Volume = CalcWaste( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ),
                            Width = curr_m.StripWidth - ( bo.Width + so.Width ),
                            Start = queue_time,
                            End = queue_time.AddHours( CalcTime( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ) )
                        } );

                queue_time = queue_time.AddHours( CalcTime( bo.Volume + so.Volume, bo.Width + so.Width, curr_m ) );
            }
            else
            {
                //       _____ ___ ___ 
                //      |  1  | 2 | W1|   
                //      |     |___|   |
                //      |     | W2|   |
                //      |_____|___|___|                     
                int parallelLines = ( int )Math.Floor( curr_m.StripWidth / ( float )bo.Width );

                if ( parallelLines == 1 )
                {
                    export.Add( // #1
                        new OrderJson( bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, 0 )
                        {
                            Volume = bo.Volume,
                            Start = queue_time,
                            End = queue_time.AddHours( CalcTime( bo.Volume, bo.Width, curr_m ) )
                        } );
                    export.Add( // #2
                        new OrderJson( so.Id, so.Consumer, so.productObj.Name, 0, so.Width, so.Density, so.Deadline, OrderTypeJson.Order, bo.Width )
                        {
                            Volume = so.Volume,
                            Start = queue_time,
                            End = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) )
                        } );
                    export.Add( // #W2
                        new OrderJson( so.Id + "W", String.Empty, String.Empty, 0, so.Width, so.Density, null, OrderTypeJson.Waste, bo.Width )
                        {
                            // W2 = Waste от 1 - W1 - 2
                            Volume = CalcWaste( bo.Volume, bo.Width, curr_m )
                                - CalcWaste( so.Volume, so.Width, curr_m.StripWidth - bo.Width )
                                - so.Volume,
                            Start = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) ),
                            End = queue_time.AddHours( CalcTime( bo.Volume, bo.Width, curr_m ) )
                        } );
                    if ( bo.Width + so.Width < curr_m.StripWidth )
                        export.Add( // #W1
                            new OrderJson( bo.Id + "_" + so.Id + "W", String.Empty, String.Empty, 0, 0, so.Density, null, OrderTypeJson.Waste, bo.Width + so.Width )
                            {
                                Volume = CalcWaste( bo.Volume, bo.Width, curr_m.StripWidth - so.Width ),
                                Width = curr_m.StripWidth - ( bo.Width + so.Width ),
                                Start = queue_time,
                                End = queue_time.AddHours( CalcTime( bo.Volume, bo.Width, curr_m ) )
                            } );

                    queue_time = queue_time.AddHours( CalcTime( bo.Volume, bo.Width, curr_m ) );
                } 
                else
                {
                    //   _____ ___ ______
                    //  |  1  | 2 |   W1 |
                    //  |_____|___|______|
                    //  |_1.1_|_1.2_|_W2_|

                    export.Add( // #1
                        new OrderJson( bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, 0 )
                        {
                            Volume = CalcWaste( so.Volume, so.Width, so.Width + bo.Width ), // объём 1 = отходы от производства 2ого - ширина W1
                            Start = queue_time,
                            End = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) )
                        } );

                    export.Add( // #2
                        new OrderJson( so.Id, so.Consumer, so.productObj.Name, 0, so.Width, so.Density, so.Deadline, OrderTypeJson.Order, bo.Width )
                        {
                            Volume = so.Volume,
                            Start = queue_time,
                            End = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) )
                        } );

                    if ( bo.Width + so.Width < curr_m.StripWidth)
                        export.Add( // #W1
                            new OrderJson( bo.Id + "_" + so.Id + "W", String.Empty, String.Empty, 0, 0, so.Density, null, OrderTypeJson.Waste, bo.Width + so.Width )
                                {
                                    Volume = CalcWaste( so.Volume, so.Width, curr_m.StripWidth - bo.Width ),
                                    Width = curr_m.StripWidth - ( bo.Width + so.Width ),
                                    Start = queue_time,
                                    End = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) )
                                } );

                    // #1 - объём Большого заказа, который будет произведен пока будет сделан #2-меньший заказ
                    float bigPartSize = CalcWaste( so.Volume, so.Width, so.Width + bo.Width );
                    float leftVolume = bo.Volume - bigPartSize;

                    // #1.1 .. 1.N
                    for ( int i = 0; i < parallelLines; i++ )
                    {
                        export.Add( // #1
                            new OrderJson( bo.Id, bo.Consumer, bo.productObj.Name, 0, bo.Width, bo.Density, bo.Deadline, OrderTypeJson.Order, bo.Width * i )
                            {
                                Volume = leftVolume / parallelLines,
                                Start = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) ),
                                End = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) )
                                                .AddHours( CalcTime( leftVolume / parallelLines, bo.Width, curr_m ) )
                            } );
                    }

                    if ( bo.Width * parallelLines < curr_m.StripWidth)
                        export.Add( // #W2
                            new OrderJson( bo.Id + "W", String.Empty, String.Empty, 0, 0, bo.Density, null, OrderTypeJson.Waste, bo.Width * parallelLines )
                            {
                                Volume = CalcWaste( leftVolume, bo.Width * parallelLines, curr_m.StripWidth ),
                                Width = curr_m.StripWidth - ( bo.Width * parallelLines ),
                                Start = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) ),
                                End = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) )
                                                .AddHours( CalcTime( leftVolume, bo.Width * parallelLines, curr_m ) )
                            } );

                    queue_time = queue_time.AddHours( CalcTime( so.Volume, so.Width, curr_m ) )
                                           .AddHours( CalcTime( leftVolume / parallelLines, bo.Width, curr_m ) );
                }
            }

            deadline = 0;
            foreach ( var x in export )
                if ( x.Type != OrderTypeJson.Waste && x.Deadline < x.End )
                    deadline += ( x.End - x.Deadline.Value ).Hours;

            return export;
        }       
        private List<OrderJson> OrderExport(Order ord, ref DateTime queue_time, Machine curr_m, out float waste, out int deadline)
        {            
            var export = new List<OrderJson>();

            // во сколько полос мы можем уложить данный заказ?
            int parallelLines = (int)Math.Floor( curr_m.StripWidth / ( float )ord.Width );

            //if ( parallelLines < 1 ) throw new Exception( "Меньше одной полосы не может быть!" );
            
            deadline = 0;
            waste = CalcWaste( ord.Volume, ord.Width * parallelLines, curr_m );
            double ord_production_time = CalcTime( ord.Volume, ord.Width * parallelLines, curr_m );
            DateTime finish = queue_time.AddHours( ord_production_time );

            for ( int i = 0; i < parallelLines; i++ )
            {
                export.Add(
                    new OrderJson( ord.Id, ord.Consumer, ord.productObj.Name, ord.Volume / parallelLines, 
                        ord.Width, ord.Density, ord.Deadline, OrderTypeJson.Order, ord.Width * i )
                    {
                        Start = queue_time,
                        End = finish
                    } );
            }

            if ( finish > ord.Deadline )
                deadline = ( finish - ord.Deadline ).Hours * parallelLines;

            if ( curr_m.StripWidth - ord.Width * parallelLines > 0 )
                export.Add(
                    new OrderJson( ord.Id + "W", String.Empty, String.Empty, waste,
                        curr_m.StripWidth - ord.Width * parallelLines, ord.Density, null,
                        OrderTypeJson.Waste, ord.Width * parallelLines )
                    {
                        Start = queue_time,
                        End = finish
                    } );

            queue_time = finish;

            return export;
        }
        private List<Object> ConcatQueueAndCompressions(List<Order> orders, List<Compression> compressions)
        {
            List<Object> result = new List<object>();

            if ( compressions.Count == 0 )
            {
                orders.ForEach( x => result.Add( x ) );
                return result;
            }

            for (int i = 0, j = 0; i < orders.Count;)
            {
                if (j == compressions.Count)
                {
                    // Сюда попадем только в том случае если обошли все компрессии и последняя
                    // компрессия не являелась склейкой двух последних элементов в orders
                    result.Add(orders[i]);
                    i++;
                }
                else if (i < compressions[j].pair.leftOrdId) // Идем по обычным заказам
                {
                    result.Add(orders[i]);
                    i++;
                }
                else // Встретили компрессию
                {
                    result.Add(compressions[j]);
                    i = compressions[j].pair.rightOrdId + 1;
                    j++;
                }
            }

            return result;
        }

        public ResultJson Export(Genome g)
        {            
            var result = new ResultJson();

            for (int i = 0; i < g.LoadedMachines.Count; i++)
            {
                var lm = g.LoadedMachines[i];
                LoadedMachineJson lmj = new LoadedMachineJson(lm.M);
                lmj.TimelineStart = GA.today;

                DateTime queue_time = GA.today;
                var queue = lm.ordersQueue.ToList();                
                var compressions = CollectCompressions(queue, lm.M);
                
                var finalCompressions = ChooseBestCompressions(compressions, queue, lm.M);

                float waste = 0f;
                int deadlineHours = 0;
                int readjustmentHours = 0;
                DateTime finish;
                var finalQueue = ConcatQueueAndCompressions(queue, finalCompressions);
                for( int k = 0; k < finalQueue.Count; k++)
                {
                    Object curr = finalQueue[k];
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
                                    lmj.Orders.AddReadjustment( queue_time, finish, lm.M, prevOrder );
                                    queue_time = finish; readjustmentHours++;
                                }
                            }
                            else
                            {
                                var prevComp = ( Compression )prevObj;
                                if ( curOrder.Density != prevComp.order_1.Density
                                    || curOrder.ProductId != prevComp.order_1.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    lmj.Orders.AddReadjustment( queue_time, finish, lm.M, prevComp.order_1, prevComp.order_2 );
                                    queue_time = finish; readjustmentHours++;
                                }
                            }                            
                        }
                        else
                        {
                            var curComp = ( Compression )curr;
                            finish = queue_time.AddHours( 1 );
                            if ( prevObj is Order )
                            {
                                var prevOrder = ( Order )prevObj;
                                if ( curComp.order_1.Density != prevOrder.Density
                                    || curComp.order_1.ProductId != prevOrder.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    lmj.Orders.AddReadjustment( queue_time, finish, lm.M, prevOrder );
                                    queue_time = finish; readjustmentHours++;
                                }
                            }
                            else
                            {
                                var prevComp = ( Compression )prevObj;
                                if ( curComp.order_1.Density != prevComp.order_1.Density
                                    || curComp.order_1.ProductId != prevComp.order_1.ProductId )
                                {
                                    finish = queue_time.AddHours( 1 );
                                    lmj.Orders.AddReadjustment( queue_time, finish, lm.M, prevComp.order_1, prevComp.order_2 );
                                    queue_time = finish; readjustmentHours++;                                    
                                }
                            }
                            
                        }
                        #endregion                        
                    }

                    if (curr is Order)
                        lmj.Orders.AddRange( OrderExport( ( Order )curr, ref queue_time, lm.M, out waste, out deadlineHours) );
                    else
                        lmj.Orders.AddRange( CompressionExport( ( Compression )curr, ref queue_time, lm.M, out waste, out deadlineHours) );

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
