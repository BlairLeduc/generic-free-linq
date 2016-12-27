// Copyright © 2017 Blair Leduc
// This source is subject to the MIT License.
// See https://opensource.org/licenses/MIT
// All other rights reserved.

using System;
using Microsoft.SPOT;
using System.Collections;

/// Inspired from the blog of Oberon Microsystems.
/// That original code is contributed to Marc Frei and Cuno Pfister.
/// For more information, see the original blog entry:
/// http://blogs.oberon.ch/tamberg/2009-02-06/implementing-linq-on-the-dotnet-mf.html

namespace System.Linq
{
    public delegate bool Predicate(object x);
    public delegate object Func2(object x);
    public delegate object Func3(object x, object y);
    public delegate IEnumerable EnumerableFunc2(object x);

    public sealed class WhereIterator : IEnumerable, IEnumerator
    {
        private IEnumerable source;
        private IEnumerator enumerator;
        private Predicate predicate;

        internal WhereIterator(IEnumerable source, Predicate predicate)
        {
            this.source = source;
            this.enumerator = source.GetEnumerator();
            this.predicate = predicate;
        }

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return new WhereIterator(source, predicate);
        }

        #endregion

        #region IEnumerator Members

        public object Current
        {
            get { return enumerator.Current; }
        }

        public bool MoveNext()
        {
            bool result = enumerator.MoveNext();
            while (result && !predicate(enumerator.Current))
            {
                result = enumerator.MoveNext();
            }
            return result;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        #endregion
    }

    public sealed class SelectIterator : IEnumerable, IEnumerator
    {
        private IEnumerable source;
        private IEnumerator enumerator;
        private Func2 func2;

        internal SelectIterator(IEnumerable source, Func2 func)
        {
            this.source = source;
            this.enumerator = source.GetEnumerator();
            this.func2 = func;
        }

        #region IEnumerator

        object IEnumerator.Current
        {
            get { return func2(enumerator.Current); }
        }

        bool IEnumerator.MoveNext()
        {
            return enumerator.MoveNext();
        }

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return new SelectIterator(source, func2);
        }

        #endregion
    }

    public sealed class DefaultIterator : IEnumerable, IEnumerator
    {
        private IEnumerable source;
        private IEnumerator enumerator;
        private object defaultValue;
        private bool first = true;
        private bool useDefault = false;

        internal DefaultIterator(IEnumerable source, object defaultValue)
        {
            this.source = source;
            this.enumerator = source.GetEnumerator();
            this.defaultValue = defaultValue;
        }

        #region IEnumerator

        object IEnumerator.Current
        {
            get
            {
                return useDefault ? defaultValue : enumerator.Current;
            }
        }

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }

        bool IEnumerator.MoveNext()
        {
            if (first)
            {
                if (!enumerator.MoveNext())
                {
                    useDefault = true;
                }
                first = false;
                return true;
            }
            else
            {
                useDefault = false;
                return enumerator.MoveNext();
            }
        }

        #endregion


        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return this;
        }

        #endregion
    }

    public sealed class ReverseIterator : IEnumerable, IEnumerator
    {
        private IEnumerable source;
        private IEnumerator enumerator;
        private ArrayList arrayList = null;
        private int index = 0;

        internal ReverseIterator(IEnumerable source)
        {
            this.source = source;
            this.enumerator = source.GetEnumerator();
        }

        #region IEnumerator

        object IEnumerator.Current
        {
            get { return arrayList[index]; }
        }

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }

        bool IEnumerator.MoveNext()
        {
            if (arrayList == null)
            {
                arrayList = new ArrayList();

                if (enumerator.MoveNext())
                {
                    do
                    {
                        arrayList.Add(enumerator.Current);
                    }
                    while (enumerator.MoveNext());
                }
                index = arrayList.Count;
            }
            return (--index) >= 0;
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return new ReverseIterator(source);
        }

        #endregion
    }

    public sealed class SortIterator : IOrderedEnumerable, IEnumerable, IEnumerator
    {
        private IEnumerable source;
        private IEnumerator enumerator;
        private Func2 keySelector;
        private IComparer comparer;
        private bool descending;
        private ArrayList arrayList = null;
        private ArrayList sortSpecifiers = new ArrayList();
        private int index = -1;

        private class KeyValue
        {
            public object Key { get; set; }
            public object Value { get; set; }
        }

        private class SortSpecifier
        {
            public Func2 KeySelector { get; set; }
            public IComparer Comparer { get; set; }
            public bool Descending { get; set; }
        }

        internal SortIterator(IEnumerable source, Func2 keySelector, IComparer comparer, bool descending)
        {
            this.source = source;
            this.enumerator = source.GetEnumerator();
            this.comparer = comparer;
            this.keySelector = keySelector;
            this.descending = descending;
        }

        #region Quicksort

        private int objectCompare(object x, object y)
        {
            int result;

            if (x is IComparable)
            {
                result = ((IComparable)x).CompareTo(y);
            }
            else if (x is int && y is int)
            {
                result = (int)x - (int)y;
            }
            else if (x is string && y is string)
            {
                result = string.Compare((string)x, (string)y);
            }
            else if (x is double && y is double)
            {
                double xd = (double)x;
                double yd = (double)y;
                result = (xd == yd) ? 0 : (xd < yd) ? -1 : 1;
            }
            else if (x is float && y is float)
            {
                float xd = (float)x;
                float yd = (float)y;
                result = (xd == yd) ? 0 : (xd < yd) ? -1 : 1;
            }
            else if (x is DateTime && y is DateTime)
            {
                result = DateTime.Compare((DateTime)x, (DateTime)y);
            }
            else if (x is TimeSpan && y is TimeSpan)
            {
                result = TimeSpan.Compare((TimeSpan)x, (TimeSpan)y);
            }
            else
            {
                throw new NotImplementedException();
            }

            return result;
        }

        private int compare(KeyValue x, KeyValue y)
        {
            int result;

            result = (comparer == null) ? objectCompare(x.Key, y.Key) : comparer.Compare(x.Key, y.Key);

            if (descending)
            {
                result = -result;
            }
            if (result == 0)
            {
                // Subsort
                for (int i = 0; i < sortSpecifiers.Count; i++)
                {
                    SortSpecifier spec = (SortSpecifier)sortSpecifiers[i];

                    IComparer comp = spec.Comparer;

                    result = (comp == null) ? objectCompare(spec.KeySelector(x.Value), spec.KeySelector(y.Value))
                        : comp.Compare(spec.KeySelector(x.Value), spec.KeySelector(y.Value));

                    if (spec.Descending)
                    {
                        result = -result;
                    }
                    if (result != 0)
                    {
                        break;
                    }
                }
            }

            return result;
        }

        private void swap(int x, int y)
        {
            object obj = arrayList[x];
            arrayList[x] = arrayList[y];
            arrayList[y] = obj;
        }

        private int partition(KeyValue pivot, int start, int end)
        {
            int split = start - 1;

            for (int i = start; i < end; i++)
            {
                if (compare((KeyValue)arrayList[i], pivot) <= 0)
                {
                    split++;
                    swap(split, i);
                }
            }
            swap(split + 1, end);
            return split + 1;
        }

        private void quicksort(int start, int end)
        {
            if (end > start)
            {
                KeyValue pivot = (KeyValue)arrayList[end];

                int split = partition(pivot, start, end);

                quicksort(start, split - 1);
                quicksort(split + 1, end);
            }
        }

        #endregion

        #region IEnumerator

        object IEnumerator.Current
        {
            get { return ((KeyValue)arrayList[index]).Value; }
        }

        void IEnumerator.Reset()
        {
            throw new NotSupportedException();
        }

        bool IEnumerator.MoveNext()
        {
            if (arrayList == null)
            {
                arrayList = new ArrayList();

                if (enumerator.MoveNext())
                {
                    do
                    {
                        arrayList.Add(new KeyValue { Key = keySelector(enumerator.Current), Value = enumerator.Current });
                    }
                    while (enumerator.MoveNext());
                }

                quicksort(0, arrayList.Count - 1);
            }
            return (++index) < arrayList.Count;
        }

        #endregion

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return new SortIterator(source, keySelector, comparer, descending);
        }

        #endregion

        #region IOrderedEnumerable Members

        public IOrderedEnumerable CreateOrderedEnumerable(Func2 keySelector, IComparer comparer, bool descending)
        {
            sortSpecifiers.Add(new SortSpecifier() { KeySelector = keySelector, Comparer = comparer, Descending = descending });
            return this;
        }

        #endregion
    }

    public sealed class ConcatIterator : IEnumerable, IEnumerator
    {
        private IEnumerable first;
        private IEnumerable second;
        private IEnumerator source;
        private IEnumerator secondEnumerator;

        internal ConcatIterator(IEnumerable first, IEnumerable second)
        {
            this.first = first;
            this.second = second;
            this.source = null;
            this.secondEnumerator = null;
        }

        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return new ConcatIterator(first, second);
        }

        #endregion

        #region IEnumerator Members

        public object Current
        {
            get { return source.Current; }
        }

        public bool MoveNext()
        {
            if (source == null)
            {
                source = first.GetEnumerator();
            }
            if (!source.MoveNext())
            {
                if (secondEnumerator != null)
                {
                    return false;
                }
                
                secondEnumerator = second.GetEnumerator();

                source = secondEnumerator;
                return source.MoveNext();
            }
            return true;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        #endregion
    }

    public sealed class RangeIterator : IEnumerable, IEnumerator
    {
        private int start;
        private int end;
        private int current;
        private bool reset;

        internal RangeIterator(int start, int end)
        {
            this.start = start;
            this.end = end;
            reset = true;
        }

        #region IEnumerator

        object IEnumerator.Current
        {
            get { return current; }
        }

        void IEnumerator.Reset()
        {
            current = -1;
        }

        bool IEnumerator.MoveNext()
        {
            if (reset)
            {
                current = start;
                reset = false;
                return true;
            }
            if (current >= end)
            {
                return false;
            }
            current++;
            return true;
        }

        #endregion


        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return new RangeIterator(start, end);
        }

        #endregion
    }

    public sealed class RepeatIterator : IEnumerable, IEnumerator
    {
        private object obj;
        private int count;
        private int index;

        internal RepeatIterator(object obj, int count)
        {
            this.obj = obj;
            this.count = count;
            index = count;
        }

        #region IEnumerator

        object IEnumerator.Current
        {
            get { return obj; }
        }

        void IEnumerator.Reset()
        {
            index = count;
        }

        bool IEnumerator.MoveNext()
        {
            if (count > 0)
            {
                count--;
                return true;
            }
            return false;
        }

        #endregion


        #region IEnumerable Members

        public IEnumerator GetEnumerator()
        {
            return new RepeatIterator(obj, count);
        }

        #endregion
    }


    public static class Enumerable
    {
        public static object Aggregate(this IEnumerable source, Func3 func)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (func == null)
            {
                throw new ArgumentNullException("func");
            }

            IEnumerator enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException();
            }

            object obj = enumerator.Current;
            while (enumerator.MoveNext())
            {
                obj = func(obj, enumerator.Current);
            }
            return obj;
        }

        public static object Aggregate(this IEnumerable source, object seed, Func3 func)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (func == null)
            {
                throw new ArgumentNullException("func");
            }

            object obj = seed;
            foreach (var obj2 in source)
            {
                obj = func(obj, obj2);
            }
            return obj;
        }

        public static object Aggregate(this IEnumerable source, object seed, Func3 func, Func2 resultSelector)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (func == null)
            {
                throw new ArgumentNullException("func");
            }

            object obj = seed;
            foreach (var obj2 in source)
            {
                obj = func(obj, obj2);
            }
            return resultSelector(obj);
        }

        public static bool All(this IEnumerable source, Predicate predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            foreach (var obj in source)
            {
                if (!predicate(obj))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Any(this IEnumerable source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            IEnumerator enumerator = source.GetEnumerator();
            {
                if (enumerator.MoveNext())
                {
                    return true;
                }
            }
            return false;
        }

        public static bool Any(this IEnumerable source, Predicate predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            foreach (var obj in source)
            {
                if (predicate(obj))
                {
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable Concat(this IEnumerable first, IEnumerable second)
        {
            if (first == null)
            {
                throw new ArgumentNullException("first");
            }
            if (second == null)
            {
                throw new ArgumentNullException("second");
            }

            return new ConcatIterator(first, second);
        }

        private class ObjectComparer : IEqualityComparer
        {
            #region IEqualityComparer Members

            public new bool Equals(object x, object y)
            {
                return x.Equals(y);
            }

            public int GetHashCode(object obj)
            {
                return obj.GetHashCode();
            }

            #endregion
        }


        public static bool Contains(this IEnumerable source, object value)
        {
            return Contains(source, value, new ObjectComparer());
        }

        public static bool Contains(this IEnumerable source, object value, IEqualityComparer comparer)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (comparer == null)
            {
                return Contains(source, value);
            }

            foreach (var obj in source)
            {
                if (comparer.Equals(obj, value))
                {
                    return true;
                }
            }
            return false;
        }

        public static int Count(this IEnumerable source)
        {
            ICollection collection = source as ICollection;
            if (collection != null)
            {
                return collection.Count;
            }

            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            int count = 0;
            foreach (object obj in source)
            {
                count++;
            }
            return count;
        }

        public static int Count(this IEnumerable source, Predicate predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            int count = 0;
            foreach (object obj in source)
            {
                if (predicate(obj))
                {
                    count++;
                }
            }
            return count;
        }

        public static IEnumerable DefaultIfEmpty(this IEnumerable source)
        {
            return DefaultIfEmpty(source, default(object));
        }

        public static IEnumerable DefaultIfEmpty(this IEnumerable source, object defaultValue)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            return new DefaultIterator(source, defaultValue);
        }

        public static IEnumerable Distinct(this IEnumerable source)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable Distinct(this IEnumerable source, IEqualityComparer comparer)
        {
            throw new NotImplementedException();
        }

        public static object ElementAt(this IEnumerable source, int index)
        {
            IList list = source as IList;
            if (list != null)
            {
                return list[index];
            }
            foreach (var obj in source)
            {
                if (index == 0)
                {
                    return obj;
                }
                index--;
            }
            return null;
        }

        public static object ElementAtOrDefault(this IEnumerable source, int index)
        {
            IList list = source as IList;
            if (list != null)
            {
                if (index >= 0 && index < list.Count)
                {
                    return list[index];
                }
                else
                {
                    return default(object);
                }
            }

            foreach (var obj in source)
            {
                if (index == 0)
                {
                    return obj;
                }
                index--;
            }
            return default(object);
        }

        public static IEnumerable Empty()
        {
            return new ArrayList();
        }

        public static IEnumerable Except(this IEnumerable first, IEnumerable second)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable Except(this IEnumerable first, IEnumerable second, IEqualityComparer comparer)
        {
            throw new NotImplementedException();
        }

        public static object First(this IEnumerable source)
        {
            IList list = source as IList;
            if (list != null)
            {
                return list[0];
            }

            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            IEnumerator enumerator = source.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException();
            }
            return enumerator.Current;
        }

        public static object First(this IEnumerable source, Predicate predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            IEnumerator enumerator = new WhereIterator(source, predicate);
            if (!enumerator.MoveNext())
            {
                throw new InvalidOperationException();
            }
            return enumerator.Current;
        }

        public static IEnumerable Intersect(this IEnumerable first, IEnumerable second)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable Intersect(this IEnumerable first, IEnumerable second, IEqualityComparer comparer)
        {
            throw new NotImplementedException();
        }

        public static object Last(this IEnumerable source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            IList list = source as IList;
            if (list != null)
            {
                int count = list.Count;
                if (count > 0)
                {
                    return list[count - 1];
                }
            }
            else
            {
                IEnumerator enumerator = source.GetEnumerator();
                if (enumerator.MoveNext())
                {
                    object obj;
                    do
                    {
                        obj = enumerator.Current;
                    }
                    while (enumerator.MoveNext());
                    return obj;
                }
            }
            throw new InvalidOperationException();
        }

        public static object Last(this IEnumerable source, Predicate predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            bool flag = false;
            IEnumerator enumerator = source.GetEnumerator();
            if (enumerator.MoveNext())
            {
                object obj = default(object);
                do
                {
                    if (predicate(enumerator.Current))
                    {
                        obj = enumerator.Current;
                        flag = true;
                    }
                }
                while (enumerator.MoveNext());
                if (flag)
                {
                    return obj;
                }
            }
            throw new InvalidOperationException();
        }

        public static IEnumerable OfType(this IEnumerable source, Type type)
        {
            return new WhereIterator(source, obj => type.IsInstanceOfType(obj));
        }

        public static IOrderedEnumerable OrderBy(this IEnumerable source, Func2 keySelector)
        {
            return OrderBy(source, keySelector, null);
        }

        public static IOrderedEnumerable OrderBy(this IEnumerable source, Func2 keySelector, IComparer comparer)
        {
            return new SortIterator(source, keySelector, comparer, false);
        }

        public static IOrderedEnumerable OrderByDescending(this IEnumerable source, Func2 keySelector)
        {
            return OrderByDescending(source, keySelector, null);
        }

        public static IOrderedEnumerable OrderByDescending(this IEnumerable source, Func2 keySelector, IComparer comparer)
        {
            return new SortIterator(source, keySelector, comparer, true);
        }

        public static IEnumerable Range(int start, int count)
        {
            if (count < 0 || (int.MaxValue - start) < count)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            return new RangeIterator(start, start + count - 1);
        }

        public static IEnumerable Repeat(object element, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }
            return new RepeatIterator(element, count);
        }

        public static IEnumerable Reverse(this IEnumerable source)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            return new ReverseIterator(source);
        }

        public static IEnumerable Select(this IEnumerable source, Func2 selector)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (selector == null)
            {
                throw new ArgumentNullException("selector");
            }

            return new SelectIterator(source, selector);
        }

        public static IEnumerable SelectMany(this IEnumerable source, EnumerableFunc2 selector)
        {
            throw new NotImplementedException();
        }

        public static bool SequenceEqual(this IEnumerable first, IEnumerable second)
        {
            throw new NotImplementedException();
        }

        public static bool SequenceEqual(this IEnumerable first, IEnumerable second, IEqualityComparer comparer)
        {
            throw new NotImplementedException();
        }

        public static object Single(this IEnumerable source)
        {
            throw new NotImplementedException();
        }

        public static object Single(this IEnumerable source, Predicate predicate)
        {
            throw new NotImplementedException();
        }

        public static object SingleOrDefault(this IEnumerable source)
        {
            throw new NotImplementedException();
        }

        public static object SingleOrDefault(this IEnumerable source, Predicate predicate)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable Skip(this IEnumerable source, int count)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable SkipWhile(this IEnumerable source, Predicate predicate)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable Take(this IEnumerable source, int count)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable TakeWhile(this IEnumerable source, Predicate predicate)
        {
            throw new NotImplementedException();
        }

        public static IOrderedEnumerable ThenBy(this IOrderedEnumerable source, Func2 keySelector)
        {
            return ThenBy(source, keySelector, null);
        }

        public static IOrderedEnumerable ThenBy(this IOrderedEnumerable source, Func2 keySelector, IComparer comparer)
        {
            return source.CreateOrderedEnumerable(keySelector, comparer, false);
        }

        public static IOrderedEnumerable ThenByDescending(this IOrderedEnumerable source, Func2 keySelector)
        {
            return ThenByDescending(source, keySelector, null);
        }

        public static IOrderedEnumerable ThenByDescending(this IOrderedEnumerable source, Func2 keySelector, IComparer comparer)
        {
            return source.CreateOrderedEnumerable(keySelector, comparer, true);
        }

        public static object[] ToArray(this IEnumerable source)
        {
            ArrayList list = new ArrayList();
            foreach (var obj in source)
            {
                list.Add(obj);
            }
            return list.ToArray();
        }

        public static IList ToList(this IEnumerable source)
        {
            IList list = source as IList;
            if (list != null)
            {
                return list;
            }

            list = new ArrayList();
            foreach (var obj in source)
            {
                list.Add(obj);
            }
            return list;
        }

        public static IEnumerable Union(this IEnumerable first, IEnumerable second)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable Union(this IEnumerable first, IEnumerable second, IEqualityComparer comparer)
        {
            throw new NotImplementedException();
        }

        public static IEnumerable Where(this IEnumerable source, Predicate predicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }
            return new WhereIterator(source, predicate);
        }
    }
}
