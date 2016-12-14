using pcbk_GA.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace pcbk_GA.Solutions.GA
{
    public static class GAUtils
    {
        public static void Shuffle<T>(this List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                int k = (GA.Rand.Next(0, n) % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static List<T> ExcludeAll<T>(this LinkedList<T> list, Func<T, bool> predicate)
        {
            List<T> excluded = new List<T>();
            var currentNode = list.First;
            while (currentNode != null)
            {
                if (predicate(currentNode.Value))
                {
                    var toRemove = currentNode;
                    currentNode = currentNode.Next;
                    list.Remove(toRemove);
                    excluded.Add(toRemove.Value);
                }
                else
                {
                    currentNode = currentNode.Next;
                }
            }

            return excluded;
        }

        public static bool Contains<T>(this Tuple<T,T> tuple, T value)
        {
            return tuple.Item1.Equals( value ) || tuple.Item2.Equals( value );
        }
    }
}
