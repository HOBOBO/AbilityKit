namespace Emilia.Data
{
    public partial class IntervalTree<T>
    {
        public struct Entry
        {
            public long intervalStart;
            public long intervalEnd;
            public T item;
        }
    }
}