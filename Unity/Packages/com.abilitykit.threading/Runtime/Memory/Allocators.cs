using System;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 简单内存分配器
    /// </summary>
    public sealed class SimpleAllocator
    {
        private byte[] _buffer;
        private int _offset;
        private readonly int _capacity;

        /// <summary>
        /// 缓冲区容量
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// 已分配的大小
        /// </summary>
        public int AllocatedSize => _offset;

        /// <summary>
        /// 剩余空间
        /// </summary>
        public int RemainingSpace => _capacity - _offset;

        /// <summary>
        /// 创建一个分配器
        /// </summary>
        /// <param name="capacity">缓冲区容量</param>
        public SimpleAllocator(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _capacity = capacity;
            _buffer = new byte[capacity];
            _offset = 0;
        }

        /// <summary>
        /// 分配内存
        /// </summary>
        public ArraySegment<byte> Allocate(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            if (_offset + size > _capacity)
                throw new InvalidOperationException("Out of memory");

            var result = new ArraySegment<byte>(_buffer, _offset, size);
            _offset += size;
            return result;
        }

        /// <summary>
        /// 重置分配器
        /// </summary>
        public void Reset()
        {
            _offset = 0;
        }
    }
}
