using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Collections
{
    /// <summary>
    /// 按升序维护唯一整数，以二分查找定位，插入和删除可能搬移元素。适用于按索引读取的小型集合；非线程安全，复合操作需要外部同步。
    /// </summary>
    public sealed class SortedIntSet
    {
        private readonly List<int> _items;

        /// <summary>
        /// 创建空的有序整数集合；初始容量允许为零，不得为负数。
        /// </summary>
        /// <param name="capacity">初始容量，允许为零，不得为负数。</param>
        public SortedIntSet(int capacity = 0)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new List<int>(capacity);
        }

        /// <summary>
        /// 获取当前保留的唯一整数数量。
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// 按升序位置读取整数；索引从零开始，越界时抛出异常。
        /// </summary>
        /// <param name="index">从零开始的排序索引。</param>
        public int this[int index] => _items[index];

        /// <summary>
        /// 仅在整数不存在时按升序插入；重复添加返回 false，内容和顺序保持不变。
        /// </summary>
        /// <param name="value">用于添加、查找、比较或删除的整数值。</param>
        /// <returns>操作成功返回 true，否则返回 false。</returns>
        public bool Add(int value)
        {
            var index = _items.BinarySearch(value);
            if (index >= 0) return false;

            _items.Insert(~index, value);
            return true;
        }

        /// <summary>
        /// 通过二分查找判断指定整数是否存在。
        /// </summary>
        /// <param name="value">用于添加、查找、比较或删除的整数值。</param>
        public bool Contains(int value) => _items.BinarySearch(value) >= 0;

        /// <summary>
        /// 删除存在的整数并返回 true；不存在时返回 false。
        /// </summary>
        /// <param name="value">用于添加、查找、比较或删除的整数值。</param>
        /// <returns>操作成功返回 true，否则返回 false。</returns>
        public bool Remove(int value)
        {
            var index = _items.BinarySearch(value);
            if (index < 0) return false;

            _items.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// 获取首个大于或等于指定整数的位置；没有符合项时返回 Count。
        /// </summary>
        /// <param name="value">用于添加、查找、比较或删除的整数值。</param>
        public int LowerBound(int value)
        {
            var low = 0;
            var high = _items.Count;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                if (_items[middle] < value)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        /// <summary>
        /// 获取首个严格大于指定整数的位置；没有符合项时返回 Count。
        /// </summary>
        /// <param name="value">用于添加、查找、比较或删除的整数值。</param>
        public int UpperBound(int value)
        {
            var low = 0;
            var high = _items.Count;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                if (_items[middle] <= value)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        /// <summary>
        /// 按索引删除连续区间并保持其余整数的升序；沿用底层列表的区间校验与异常。
        /// </summary>
        /// <param name="index">从零开始的排序索引。</param>
        /// <param name="count">需要删除的连续元素数量。</param>
        public void RemoveRange(int index, int count) => _items.RemoveRange(index, count);

        /// <summary>
        /// 移除全部整数，保留底层列表容量供后续复用。
        /// </summary>
        public void Clear() => _items.Clear();
    }
}
