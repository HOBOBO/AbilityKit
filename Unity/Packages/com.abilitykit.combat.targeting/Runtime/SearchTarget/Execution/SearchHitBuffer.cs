namespace AbilityKit.Battle.SearchTarget
{
    internal sealed class SearchHitBuffer
    {
        private SearchHit[] _items;

        public SearchHit[] Items => _items;
        internal int Capacity => _items?.Length ?? 0;

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                _items = null;
                return;
            }

            if (_items == null || _items.Length < capacity)
            {
                _items = new SearchHit[capacity];
            }
        }

        public void Reset(int maxRetainedCapacity)
        {
            if (_items != null && _items.Length > maxRetainedCapacity)
            {
                _items = null;
            }
        }
    }
}
