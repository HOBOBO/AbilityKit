using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 数据分区器
    /// 用于控制并行循环的数据划分方式
    /// </summary>
    public static class Partitioner
    {
        /// <summary>
        /// 创建范围分区器
        /// </summary>
        public static List<Range> CreateRange(long from, long toExclusive, long rangeSize = 1)
        {
            var ranges = new List<Range>();
            for (long i = from; i < toExclusive; i += rangeSize)
            {
                var end = Math.Min(i + rangeSize, toExclusive);
                ranges.Add(new Range(i, end));
            }
            return ranges;
        }

        /// <summary>
        /// 创建静态范围分区
        /// </summary>
        public static List<Range> CreateStaticRange(int partitionCount, long from, long toExclusive)
        {
            var ranges = new List<Range>();
            var count = toExclusive - from;
            var rangesPerPartition = Math.Max(1, count / partitionCount);

            for (long i = from; i < toExclusive; i += rangesPerPartition)
            {
                var end = Math.Min(i + rangesPerPartition, toExclusive);
                ranges.Add(new Range(i, end));
            }
            return ranges;
        }
    }

    /// <summary>
    /// 范围
    /// </summary>
    public readonly struct Range
    {
        public long From { get; }
        public long To { get; }

        public Range(long from, long to)
        {
            From = from;
            To = to;
        }

        public long Length => To - From;

        public override string ToString() => $"[{From}, {To})";
    }

    /// <summary>
    /// 并行扩展方法
    /// </summary>
    public static class ParallelExtensions
    {
        /// <summary>
        /// 分区并行 for
        /// </summary>
        public static void ForRange(
            long from,
            long toExclusive,
            Action<long> body,
            int grainSize = 1)
        {
            if (from >= toExclusive)
                return;

            var ranges = Partitioner.CreateRange(from, toExclusive, grainSize);
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, ranges.Count)
            };

            System.Threading.Tasks.Parallel.ForEach(ranges, options, range =>
            {
                for (long i = range.From; i < range.To; i++)
                {
                    body(i);
                }
            });
        }

        /// <summary>
        /// 分区并行 for（带分区信息）
        /// </summary>
        public static void ForRange(
            long from,
            long toExclusive,
            Action<long, Range> body,
            int grainSize = 1)
        {
            if (from >= toExclusive)
                return;

            var ranges = Partitioner.CreateRange(from, toExclusive, grainSize);
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, ranges.Count)
            };

            System.Threading.Tasks.Parallel.ForEach(ranges, options, range =>
            {
                for (long i = range.From; i < range.To; i++)
                {
                    body(i, range);
                }
            });
        }
    }

    /// <summary>
    /// 并行排序算法
    /// </summary>
    public static class ParallelSort
    {
        /// <summary>
        /// 并行快速排序
        /// </summary>
        public static void QuickSort<T>(T[] array, IComparer<T> comparer = null)
        {
            QuickSort(array, 0, array.Length - 1, comparer ?? Comparer<T>.Default);
        }

        /// <summary>
        /// 并行快速排序
        /// </summary>
        public static void QuickSort<T>(T[] array, int left, int right, IComparer<T> comparer)
        {
            if (left >= right)
                return;

            var pivotIndex = Partition(array, left, right, comparer);
            var leftSize = pivotIndex - left;
            var rightSize = right - pivotIndex;

            const int threshold = 1000;

            if (leftSize > threshold && rightSize > threshold)
            {
                System.Threading.Tasks.Parallel.Invoke(
                    () => QuickSort(array, left, pivotIndex - 1, comparer),
                    () => QuickSort(array, pivotIndex + 1, right, comparer)
                );
            }
            else
            {
                if (leftSize > 0)
                    QuickSort(array, left, pivotIndex - 1, comparer);
                if (rightSize > 0)
                    QuickSort(array, pivotIndex + 1, right, comparer);
            }
        }

        private static int Partition<T>(T[] array, int left, int right, IComparer<T> comparer)
        {
            var pivot = array[right];
            var i = left - 1;

            for (var j = left; j < right; j++)
            {
                if (comparer.Compare(array[j], pivot) <= 0)
                {
                    i++;
                    Swap(array, i, j);
                }
            }

            Swap(array, i + 1, right);
            return i + 1;
        }

        private static void Swap<T>(T[] array, int i, int j)
        {
            var temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }

        /// <summary>
        /// 并行归并排序
        /// </summary>
        public static void MergeSort<T>(T[] array, IComparer<T> comparer = null)
        {
            var temp = new T[array.Length];
            MergeSort(array, temp, 0, array.Length - 1, comparer ?? Comparer<T>.Default);
        }

        private static void MergeSort<T>(T[] array, T[] temp, int left, int right, IComparer<T> comparer)
        {
            if (left >= right)
                return;

            var mid = left + (right - left) / 2;
            var size = right - left + 1;

            const int threshold = 1000;

            if (size > threshold)
            {
                System.Threading.Tasks.Parallel.Invoke(
                    () => MergeSort(array, temp, left, mid, comparer),
                    () => MergeSort(array, temp, mid + 1, right, comparer)
                );
            }
            else
            {
                MergeSort(array, temp, left, mid, comparer);
                MergeSort(array, temp, mid + 1, right, comparer);
            }

            Merge(array, temp, left, mid, right, comparer);
        }

        private static void Merge<T>(T[] array, T[] temp, int left, int mid, int right, IComparer<T> comparer)
        {
            var i = left;
            var j = mid + 1;
            var k = left;

            while (i <= mid && j <= right)
            {
                if (comparer.Compare(array[i], array[j]) <= 0)
                {
                    temp[k++] = array[i++];
                }
                else
                {
                    temp[k++] = array[j++];
                }
            }

            while (i <= mid)
                temp[k++] = array[i++];

            while (j <= right)
                temp[k++] = array[j++];

            for (var x = left; x <= right; x++)
                array[x] = temp[x];
        }
    }
}
