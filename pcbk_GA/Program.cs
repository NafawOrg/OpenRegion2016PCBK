using pcbk_GA.Helpers;
using pcbk_GA.Objects;
using pcbk_GA.Solutions.GA;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Продукция зависит от машины
// плотность зависит от машины и продукции

namespace pcbk_GA
{
    public class Compression
    {
        public readonly int orderId_first;
        public readonly int orderId_second;
        public Tuple<float, double, Tuple<int, double, int, double>> Comp_result;
        public bool IsGrouped = false;
        public float penaltyComp = 0f; // Выгодность применения этой компрессии по объёму отходов
        public double penaltyTime = 0f; // Выгодность применения этой компрессии по кол-ву времени
        public Compression(int first_orderId, int second_orderId)
        {
            this.orderId_first = first_orderId;
            this.orderId_second = second_orderId;
        }
    }
    class Program
    {
        //  TODO: как дробить заказы?
        //  кейсы объединений заказов и подсчёт отходов, в данный момент не совсем понятен процесс
        //  учет тех.обслуживание во времени
        public static double FitnessFunction( List<LoadedMachine> loadedMachines, List<Output> history )
        {            
            DateTime curr_date = GA.today;
            float wasteVolume = 0; // кол-во отходов
            float deadlineHours = 0; // кол-во часов прошедших после даты отгрузки заказа до выполнения заказа
            float readjustmentHours = 0; // кол-во времени на переналадки

            for (int i = 0; i < loadedMachines.Count; i++)
            {
                LoadedMachine curr_lm = loadedMachines[i];
                Machine curr_m = curr_lm.M;
                if ( history != null ) history.Add( new Output( curr_m ) );
                DateTime queue_time = curr_date;
                float queue_wasteVolume = 0f; // отходы в рамках текущей очереди у данной машины
                float queue_readjustmentHours = 0f;
                float queue_deadlineHours = 0f;
                List<Tuple<int, int>> compressions = null;

                // Возможно нет компрессий в текущей генерации?
                var queue = curr_lm.ordersQueue.ToList();
                var stats = QueueStats( 0, queue.Count, queue_time, queue, curr_m, true, history );
                queue_wasteVolume = stats.Item1;
                queue_deadlineHours = stats.Item2;
                queue_readjustmentHours = stats.Item3;                
                compressions = stats.Item4;

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
                if (compressions.Count != 0)
                {
                    if ( history != null ) history.Last().clearHistory();

                    // при оценке компрессий нужно учитывать распады:
                    // в случае если <1,2>,<2,3>, выбирая одну из компрессий будет 1 распад на <1><2,3> или <1,2><3>
                    // в случае если <1,2><2,3><3,4> и мы выбираем <2,3>, она должна превосходить по выгодности
                    // сумму из <1,2> и <3,4>, т.к. приводит к распаду <1><2,3><4>
                    // TODO: так же нужно что-то думать с deadlineHours...
                    // т.к. компрессии можно выбирать и с точки зрения временных потерь
                    List<Compression> comp_results = new List<Compression>();
                    List<List<Compression>> groups = new List<List<Compression>>();
                    for (int k = 0; k < compressions.Count; k++)
                    {
                        Tuple<int, int> next_comp = null;
                        Tuple<int, int> prev_comp = null;
                        Tuple<int, int> curr_comp = compressions[k];

                        // p - previous, c - current, n - next compression
                        if (k + 1 < compressions.Count && compressions[k + 1].Item1 == curr_comp.Item2) // смежные компрессии P C [N]
                            next_comp = compressions[k + 1];

                        if (k - 1 >= 0 && compressions[k - 1].Item2 == curr_comp.Item1) // смежные компрессии [P] C N
                            prev_comp = compressions[k - 1];

                        Compression comp = new Compression(curr_comp.Item1, curr_comp.Item2) { Comp_result = CompressionInfo(curr_comp, queue, curr_m) };

                        if ( prev_comp == null && next_comp == null )
                        {
                            // Первоначально результаты заполняем одиночками, потом дополним их
                            // элементами из групп, проведя распад некоторых.
                            comp_results.Add( comp ); 
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

                        comp.IsGrouped = true;
                    }

                    // Вычисляем выгодность компрессий
                    for (int j = 0; j < groups.Count; j++)
                    {
                        List<Compression> group = groups[j];

                        for (int k = 0; k < group.Count; k++)
                        {
                            if (k + 1 < group.Count && k - 1 > 0) // элемент по центру [ k - 1 ] [ k ] [ k + 1 ]
                            {
                                // если отрицательное, то отходов меньше, чем сумма 2ух соседних => хорошо
                                group[k].penaltyComp = group[k].Comp_result.Item1 - (group[k - 1].Comp_result.Item1 + group[k + 1].Comp_result.Item1);

                                // если отрицательное, то выполняется быстрее, чем сумма соседних заказов => хорошо
                                group[k].penaltyTime = group[k].Comp_result.Item2 - (group[k - 1].Comp_result.Item2 + group[k + 1].Comp_result.Item2);
                            }
                            else if (k + 1 < group.Count) // элемент слева [ k ] [ k + 1 ]
                            {
                                group[k].penaltyComp = group[k].Comp_result.Item1 - group[k + 1].Comp_result.Item1;
                                group[k].penaltyTime = group[k].Comp_result.Item2 - group[k + 1].Comp_result.Item2;
                            }
                            else // элемент справа [ k - 1 ] [ k ]
                            {
                                group[k].penaltyComp = group[k].Comp_result.Item1 - group[k - 1].Comp_result.Item1;
                                group[k].penaltyTime = group[k].Comp_result.Item2 - group[k - 1].Comp_result.Item2;
                            }
                        }

                        // Производим разгруппировку смежных компрессий в пользу наиболее выгодных
                        comp_results.AddRange( unMergeGroup( group ) );                        
                    }

                    // Подсчитываем итого с использованием компрессий
                    queue_wasteVolume = 0f;
                    queue_deadlineHours = 0f;
                    queue_readjustmentHours = 0f;
                    comp_results = comp_results.OrderBy( x => x.orderId_first ).ToList();
                    int start = 0;
                    int end = 0;
                    foreach ( var r in comp_results ) // считаем блок, компрессию, блок, компрессию и т.д.
                    {
                        end = r.orderId_first;
                        var temp_stats = QueueStats( start, end, queue_time, queue, curr_m, false, history );
                        
                        queue_wasteVolume += temp_stats.Item1 + r.Comp_result.Item1;
                        queue_deadlineHours += temp_stats.Item2;
                        queue_readjustmentHours += temp_stats.Item3;
                        queue_time = temp_stats.Item5.AddHours( r.Comp_result.Item2 );

                        if ( history != null )
                        {
                            history.Last().addOrder( queue[ r.Comp_result.Item3.Item1 ], temp_stats.Item5, temp_stats.Item5.AddHours( r.Comp_result.Item3.Item2 ) );
                            history.Last().addOrder( queue[ r.Comp_result.Item3.Item3 ], temp_stats.Item5, temp_stats.Item5.AddHours( r.Comp_result.Item3.Item4 ) );
                        }
                        start = r.orderId_second + 1;
                    }
                    if ( start < queue.Count ) // подсчитываем последний блок если таковой имеется
                    {
                        end = queue.Count;
                        var temp_stats = QueueStats( start, end, queue_time, queue, curr_m, false, history );

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
            double finalScore = 0.4f * wasteVolume + 0.5f * deadlineHours + 0.1f * readjustmentHours;
            return finalScore;
        }

        // wasteVolume, deadlineHours, readjustmentHours, compressions, queue_finish_time
        public static Tuple<float, float, float, List<Tuple<int, int>>, DateTime> QueueStats(
            int start, int end, DateTime queue_time, List<Order> queue, Machine curr_m, bool collect_compressions = false, List<Output> history = null)
        {
            float wasteVolume = 0; // кол-во отходов
            float deadlineHours = 0; // кол-во часов прошедших после даты отгрузки заказа до выполнения заказа
            float readjustmentHours = 0; // кол-во времени на переналадки            
            List<Tuple<int, int>> compressions = new List<Tuple<int, int>>(); // <orderId,orderId>                      
                        
            for ( int j = start; j < end; j++ )
            {
                Order curr_ord = queue[ j ];                
                int width_ord = curr_ord.Width;
                float volume_ord = curr_ord.Volume;

                // TODO: если ширина позволяет пускать заказ в 2 полосы? ( пересчёт отходов и времени производства )
                // x = k/m * вес заказа  
                // где k - ширина заказа, m - ширина полотна машины, x = сколько реально надо выпустить
                float real_volume_ord = volume_ord / ( ( float )curr_ord.Width / curr_m.StripWidth );
                float ord_wastedVolume = real_volume_ord * ( 1f - ( float )curr_ord.Width / curr_m.StripWidth );
                wasteVolume += ord_wastedVolume;

                // получаем кол-во часов для производства заказа
                double ord_production_time = Math.Round( ( double )( real_volume_ord / curr_m.HourPerfomance ), 2 );
                DateTime finish = queue_time.AddHours( ord_production_time );
                if ( finish > curr_ord.Deadline )
                    deadlineHours += ( finish - curr_ord.Deadline ).Hours;

                if ( history != null ) history.Last().addOrder( curr_ord, queue_time, finish );

                // изучаем следующий заказ на предмет переналадок и компрессий
                if ( j + 1 < end )
                {
                    Order next_ord = queue[ j + 1 ];
                    if ( curr_ord.Density != next_ord.Density || curr_ord.ProductId != next_ord.ProductId )
                    {
                        readjustmentHours += 1;
                        queue_time = finish.AddHours( 1 ); // добавляем час к времени, т.к. будет переналадка перед следующим заказом
                        continue;
                    }

                    if ( collect_compressions && curr_ord.Width + next_ord.Width <= curr_m.StripWidth )
                        compressions.Add( new Tuple<int, int>( j, j + 1 ) );
                }

                queue_time = finish; // сдвигаем текущее время очереди
            }

            return new Tuple<float, float, float, List<Tuple<int, int>>, DateTime>(
                wasteVolume, deadlineHours, readjustmentHours, compressions, queue_time);
        }

        public static List<Compression> unMergeGroup( List<Compression> group )
        {
            // Вероятно очень оптимистичный образ разложения групп,
            // каждый раз выбирая для распада наименьший по штрафам элемент.
            // Не до конца понял, может ли реально влиять элемент больше чем на 2 соседа при распаде
            // или это может повлечь более глубокие последствия.
            // хотя вроде норм!)
            List<Compression> fin_result = new List<Compression>();

            while ( group.Count > 0 )
            {
                group = group.OrderBy( x => x.penaltyComp ).ThenBy( y => y.penaltyTime ).ToList();
                Compression curr_comp = group.First();
                group.RemoveAt( 0 );
                group.RemoveAll( x => x.orderId_second == curr_comp.orderId_first || x.orderId_first == curr_comp.orderId_second );
                fin_result.Add( curr_comp );
            }
                        
            return fin_result;            
        }
        
        /// <summary>
        /// Возвращается <comp_waste, total_production_time, <orderId,prod_time,orderId,prod_time>>
        /// </summary>
        public static Tuple<float, double, Tuple<int,double,int,double>> CompressionInfo(Tuple<int, int> curr_comp, List<Order> queue, Machine curr_m)
        {
            float comp_waste = 0f;
            double production_time = 0f;
            Order ord_first = queue[curr_comp.Item1];
            Order ord_second = queue[curr_comp.Item2];
            float ord_f_volume = ord_first.Volume;
            float ord_s_volume = ord_second.Volume;

            // Если у заказов одинаковая ширина, то можно дополнительно оптимизировать
            // просуммировав объемы заказов, и пустив в одно полотно
            // например 2000х10т 2000х20т => делаем 4000х30т(понятно, что по факту получится объём больше из-за формулы)
            if (ord_first.Width == ord_second.Width) 
            {
                float total_volume = ord_first.Volume + ord_second.Volume;
                float real_total_volume = total_volume / ((float)(ord_first.Width + ord_second.Width) / curr_m.StripWidth);
                float wasted_volume = real_total_volume * (1f - (float)(ord_first.Width + ord_second.Width) / curr_m.StripWidth);
                comp_waste += wasted_volume;

                double total_production_time = Math.Round((double)(real_total_volume / curr_m.HourPerfomance), 2);
                production_time += total_production_time;

                return new Tuple<float, double, Tuple<int, double, int, double>>( comp_waste, production_time,
                    new Tuple<int, double, int, double>( curr_comp.Item1, production_time, curr_comp.Item2, production_time ) );
            }
            else
            {
                // _________________________________________
                // Считаем совместный выпуск                
                float together_volume = Math.Min(ord_f_volume, ord_s_volume) * 2f;
                float real_together_volume = together_volume / ((float)(ord_first.Width + ord_second.Width) / curr_m.StripWidth);
                float together_wastedVolume = real_together_volume * (1f - (float)(ord_first.Width + ord_second.Width) / curr_m.StripWidth);
                comp_waste += together_wastedVolume;

                double together_production_time = Math.Round((double)(real_together_volume / curr_m.HourPerfomance), 2);
                production_time += together_production_time;

                // _________________________________________
                // Считаем остаточный выпуск большего заказа
                // TODO: узнать можно ли его пустить в 2 полотна
                Order bigger_ord = ord_first.Volume > ord_second.Volume ? ord_first : ord_second;
                float left_volume = bigger_ord.Volume - together_volume / 2f;
                float real_left_volume = left_volume / ((float)bigger_ord.Width / curr_m.StripWidth);
                float left_wastedVolume = real_left_volume * (1f - (float)bigger_ord.Width / curr_m.StripWidth);
                comp_waste += left_wastedVolume;

                double left_production_time = Math.Round((double)(real_left_volume / curr_m.HourPerfomance), 2);
                production_time += left_production_time;

                // треш для статистики
                int small_ord = queue[ curr_comp.Item1 ].Volume > queue[ curr_comp.Item2 ].Volume ? curr_comp.Item2 : curr_comp.Item1;
                int big_ord = queue[ curr_comp.Item1 ].Volume > queue[ curr_comp.Item2 ].Volume ? curr_comp.Item1 : curr_comp.Item2;
                return new Tuple<float, double, Tuple<int, double, int, double>>( comp_waste, production_time,
                    new Tuple<int, double, int, double>( small_ord, together_production_time, big_ord, production_time ) );
            }
        }
        
        static void Main(string[] args)
        {
            GA.today = new DateTime( 2016, 11, 3 );
            DataManager dm = new DataManager();
            dm.loadConsts(@"C:\Users\Gleb\YandexDisk\Загрузки\2\open_hack\consts.txt");
            List<Order> orders = dm.loadOrders(@"C:\Users\Gleb\YandexDisk\Загрузки\2\open_hack\(7) Входные данные заказы без дат_.csv");

            GA ga = new GA(dm.Machines, orders);
            ga.FitnessFunction = new GAFitnessFunction(FitnessFunction);
            ga.FindSolutions();

            var best = ga.m_thisGeneration.Last();
            List<Output> history = new List<Output>();
            FitnessFunction( best.getGenes(), history );
            ga.CreateReport(history);
            ga.CreateTxtReport(history);
        }

    }
}
