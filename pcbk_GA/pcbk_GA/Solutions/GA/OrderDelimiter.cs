using pcbk_GA.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Solutions.GA
{
    public class OrderDelimiter
    {
        private List<Order> _orders;
        private List<List<Order>> groups;
        public OrderDelimiter(List<Order> orders)
        {
            this._orders = orders;
            this.groups = new List<List<Order>>();
        }

        // Щедрый заказ:) Готов делиться с другими своим объёмом
        private class GenerousOrder
        {
            private float _usedVolume;
            public readonly Order Value;
            public float UsedVolume
            {
                get{ return _usedVolume;}
                set
                {
                    if ( value >= 0 && value <= this.Value.Volume ) _usedVolume = value;
                    else throw new Exception( "Нельзя отделить от заказа часть большую его собственного размера!" );
                }
            }
            public GenerousOrder(Order o)
            {
                this.Value = o;
                this._usedVolume = 0f;
            }
        }

        // Интересно, надо глянуть
        //http://codereview.stackexchange.com/questions/17670/a-simple-unrolled-linked-list-implementation
        public List<Order> TrySplitOders()
        {
            var optimizedOrders = new List<Order>();
            var linkOrders = new LinkedList<Order>(_orders);

            do
            {
                // текущий скиталец, искатель схожих с собой заказов
                Order wanderer = linkOrders.First();
                if (!linkOrders.Remove(wanderer)) throw new Exception("wtf");
                // ищем схожие заказы, которые подходят по deadline
                // нет смысла смотреть на заказы, которые должны быть выпущены раньше
                List<Order> group = linkOrders.ExcludeAll(
                    x =>
                    x.Density == wanderer.Density
                    && x.ProductId == wanderer.ProductId
                    && x.Deadline >= wanderer.Deadline
                    && wanderer.AllowedMachines.Any(y => y.StripWidth >= (x.Width + wanderer.Width))
                );
                group.Insert(0, wanderer);
                groups.Add(group);
            } while (linkOrders.Count != 0);

            /* //debug-test на то, что заказ существует только в 1 группе
            for( int i = 0; i < _orders.Count; i++)
            {
                var count = groups.Count(x => x.Any(y => y.InternalOrderId == _orders[i].InternalOrderId));
                if (count > 1)
                    throw new Exception("wtf");
            }
            */

            var notSingeltonGroups = groups.Where(x => x.Count > 1);

            // Внутри каждой группы нужно понять какие заказы с какими могут быть пущены в параллель
            // т.к. есть ограничение в виде максимальной ширины полотна машины            
            foreach (var group in notSingeltonGroups)
            {
                var linkedGroup = new LinkedList<Order>(group);
                var tuples = new List<Tuple<int, int>>();
                do
                {
                    Order current_order = linkedGroup.First();
                    if (!linkedGroup.Remove(current_order)) throw new Exception("wtf");

                    // пример: внутри группы могут находится заказы шириной 1900, 2100 и 2200.
                    // max ширина полотна у 1 из машин 4200 => туда подойдут 1900 + 2200, 1900 + 2100,
                    // но не 2100+2200, эта проверка как раз для этого.
                    // По сути это можно заменить на декартово произведение с последующей фильтрацией
                    // на наличие общей машины с удлетворяющей шириной полотна
                    var suitablePairs =
                        linkedGroup.Where(
                            x =>
                            x.AllowedMachines.Any(
                                y =>
                                // эта проверка поидее не нужна т.к. если у заказов одни и те же productId и Density,
                                // то у них и машины будут одинаковые, только если не будет специфичной ширины в 
                                // будущем, большей ширины какой-то из машин...
                                current_order.AllowedMachines.Any(z => z.Id == y.Id) // есть одинаковая машина
                                && y.StripWidth >= x.Width + current_order.Width // и её ширина подходит
                            )
                        //x.AllowedMachines.Any(y => y.StripWidth >= current_order.Width + x.Width)
                        //&& current_order.AllowedMachines.Any(z => z.StripWidth >= current_order.Width + x.Width)
                        )
                        .Select(k => new Tuple<int, int>(current_order.InternalOrderId, k.InternalOrderId));
                        //.ToList();

                    tuples.AddRange(suitablePairs);
                } while (linkedGroup.Count != 0);

                // то, что будет твориться дальше не поддается никакой логике, особенно расчёт threshold

                // По сути в tuples мы получили некий граф, который показывает какие с
                // какими заказами можно делать не пренебрегая максимальной шириной полотна доступой машины.
                // дальше нужно как-то оценить вершины этого графа на наибольшую пригодность для дробления.
                // как вариант можно посчитать ср.арифм. или медиану от (каждой вершины + её смежности) 
                // - локальные значения или же у всего графа, так сказать глобальные значения
                //
                // Пытаемся выявить наиболее пригодные для деления вершины-заказы.
                int min_idx = tuples[ 0 ].Item1;
                var splittableOrders = new List<Order>();
                for ( int i = 0; i < group.Count; i++ )
                {
                    Order o = group[i];
                    float mean = o.Volume;
                    float max = o.Volume;
                    int neighbor_items = 1;

                    foreach(
                        var ord_id in tuples
                            .Where(x => x.Item1 == o.InternalOrderId || x.Item2 == o.InternalOrderId)
                            .Select(y => y.Item1 == o.InternalOrderId ? y.Item2 : y.Item1)
                    )
                    {
                        float volume = group[ ord_id - min_idx ].Volume;
                        mean += volume;
                        neighbor_items++;
                        max = max > volume ? max : volume;
                    }

                    mean /= neighbor_items;
                    var variance = group.Select( x => Math.Pow( mean - x.Volume, 2 ) ).Sum() / group.Count;
                    var standard_deviation = ( float )Math.Sqrt( variance );
                    float threshold = mean + standard_deviation;
                    threshold = threshold < max ? threshold : max; // дисперсия может оказаться слишком большой

                    if ( neighbor_items > 1 )
                        splittableOrders.AddRange( 
                            group.Where( x => x.Volume >= threshold && !splittableOrders.Contains(x) ) 
                        );
                }


                // Будем пытаться делить как-то заказы.
                // Получив возможные варианты деления заказов, мы будем пытаться раскинуть каждый из них в рамках своей локальной группы 
                // в графе tuples.
                // Возможная проблема: если splittableOrders несколько и их локальные группы пересекаются между собой, мы можем совершить 
                // лишнее разбиение, например есть заказ А на 5 т, и есть 2 объекты подходящих под разделение B & C.
                // Они так же удовлетворяют ширине машины при параллельном выпуске с А.
                // Мы от каждого из них оторвем кусок в 5т, но использовать сможем только 1 вместе с А заказом.
                // Один кусок в 5т останется бесполезным.
                // 1. Такой ситуации надо избегать
                // 2. Не стоит отрывать кусок от заказа для оптимизации другого, если в итоге от него остается 
                //    неуважительно малый кусок(уточнить). В таких ситуациях, наверное, нужно заглянуть дальше и посмотреть есть ли
                //    другой заказ в splittableOrders от которого можно оторвать кусок для оптимизации
                // 3. Оторвав куски от заказов из splittableOrders, возможно стоит попробовать провести такую же оптимизацию в рамках них.
                //    Особенно это касается заказов, которые нельзя пустить в 2 полотна.
                //
                // TODO Quastion на подумать: 
                // Думая об этих оптимизациях я начинаю приходить к выводу о необходимости наличия возможности фиксации
                // заказов. Первая мысль об этом возникла на награждение, когда разговор зашел о "вклинивание" в текущий процесс производства.
                // Для этого необходима возможность создавать геном, который не будет трогать порядок каких-то объектов, хотя это можно
                // сделать гораздо проще - просто не вставлять эти заказы в геном? А хранить как "приставку" к геному...
                // НО это подходит только в том случае, когда мы планируем на далекое будущее, хотя хз. "приставка" - геном - "суффикс" тоже
                // сработает... Походу это функционал не нужен... Те заказы которые нужно оптимизировать нужно подавать на геном,
                // а все остальные не надо. Это очень странное требование требовать выполнение заказа после какого-то заказа.
                //
                // Однако фукционал оптимизации путем разбиения заказов на подзаказы прямым текстом просит
                // о возможности "группировки" заказов в рамках генома, если их и тусовать, то рядом? Т.к. они уже оптимизированны
                // с точки зрения отходов, но видимо не времени, между собой по deadline они ок, 
                // но неизвестно на каком месте они окажутся в рамках генома.
                // Соответственно имеет смысл подумать над этим - операции над элементами таких "группировок" 
                // должны выполняться как единое целое.

                // работаем над заказами, которым требуется оптимизация
                var generousOrders = splittableOrders.Select( x => new GenerousOrder( x ) ).ToList();
                var forOptimisation = group.Except( splittableOrders );
                var generatedOrders = new List<Order>();
                foreach (var opt in forOptimisation)
                {
                    // Определим граница локального графа для текущего заказа
                    var optTuples = tuples
                        .Where(x => x.Item1 == opt.InternalOrderId || x.Item2 == opt.InternalOrderId);

                    // Для текущего заказа "под оптимизацию" найдем возможные "щедрые" заказы в рамках его локального графа
                    // Если найдем, то оторвем кусок от самого большого
                    var availableGenerousOrders =
                        generousOrders
                        .Where(x => optTuples.Any(y => y.Contains(x.Value.InternalOrderId)))
                        .OrderByDescending(x => x.Value.Volume);

                    GenerousOrder selected = null;

                    foreach (var genOrd in availableGenerousOrders)
                    {
                        // тестовое искусственное ограничение на то, чтобы остатки не были слишком маленькими
                        // TODO:надо уточнить какого минимального размера рулон может быть
                        if ((genOrd.Value.Volume - genOrd.UsedVolume) >= opt.Volume
                            && (genOrd.UsedVolume + opt.Volume) / genOrd.Value.Volume < 0.95f)
                        {
                            genOrd.UsedVolume += opt.Volume;
                            selected = genOrd;
                            break;
                        }
                    }

                    if (selected != null)
                        generatedOrders.Add(selected.Value.Clone(opt.Volume));
                }

                // работаем над остатками щедрых заказов, возможно их тоже можно сплитнуть...
                // сначала пытаемся покрыть самые маленькие заказы
                foreach ( var genOrd in generousOrders.OrderBy( x => x.Value.Volume ) )
                {
                    // На подумать... Стоит ли так делать?
                    // Стоит ли оптимизировать заказы которые уже оторвали от себя кусок кому-то? Наверное стоит, но надо тестить, пока так.
                    if ( genOrd.UsedVolume > 0 ) continue; 

                    var optTuples = tuples
                        .Where( x => x.Item1 == genOrd.Value.InternalOrderId || x.Item2 == genOrd.Value.InternalOrderId );

                    var option =
                        generousOrders
                        .Where(
                            x =>
                            !x.Value.Equals(genOrd.Value)
                            && optTuples.Any(
                                y => y.Contains(x.Value.InternalOrderId)
                                )
                            // подумать, стоит ли так ограничивать? Мы же можем и частично покрыть заказ,
                            // не обязательно полностью и это уже приведет к оптимизации, хотя нужно учитывать
                            // возможность проброса на 2 полотна, может это будет оптимальней?
                            && (x.Value.Volume - x.UsedVolume) >= genOrd.Value.Volume
                        ).OrderBy(z => z.Value.Volume - z.UsedVolume).FirstOrDefault();

                    if (option != null)
                    {
                        generatedOrders.Add(option.Value.Clone(genOrd.Value.Volume));
                        option.UsedVolume += genOrd.Value.Volume;
                    }
                }

                foreach (var genOrd in generousOrders)
                {
                    if (genOrd.UsedVolume > 0 && (genOrd.Value.Volume - genOrd.UsedVolume) > 0)
                        generatedOrders.Add(genOrd.Value.Clone(genOrd.Value.Volume - genOrd.UsedVolume));
                }
                
                // TODO: уже фиксировал, но продублирую : подумать над фиксацией оптимизированных заказов парами..
                // чтобы в геноме они не "отделялись друг от друга"
                optimizedOrders.AddRange(generatedOrders);
            }

            /* Проверка правильности вычислений
            var test = optimizedOrders.GroupBy(x => x.InternalOrderId).Select(y => new { key = y.Key, sum = y.Sum(z => z.Volume) }).ToList();
            var test2 = _orders.Where(y => test.Any(z => z.key == y.InternalOrderId)).Select(q => new { key = q.InternalOrderId, sum = q.Volume }).ToList();
            var join = test.Join( test2, x => x.key, y => y.key, (x, y) => new { x.key, x.sum, total = y.sum } ).ToList();
            */

            // получив конечный результат удаляем из orders щедрые заказы и заносим туда разделенные generatedOrders
            _orders.RemoveAll( x => optimizedOrders.Any( y => y.InternalOrderId == x.InternalOrderId ) );
            _orders.AddRange( optimizedOrders );
            return _orders;
        }
    }
}
