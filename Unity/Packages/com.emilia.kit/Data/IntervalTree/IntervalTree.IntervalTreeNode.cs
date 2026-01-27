namespace Emilia.Data
{
    public partial class IntervalTree<T>
    {
        struct IntervalTreeNode
        {
            public long center;
            public int first;
            public int last;
            public int left;
            public int right;
        }
    }
}