using System;
using Svelto.DataStructures;
using Svelto.ECS.Internal;

namespace AbilityKit.Demo.Shooter.Runtime
{
    /// <summary>
    /// 快照导出的排序索引缓冲。
    ///
    /// 性能说明（2026-07-25 优化）：排序从插入排序 O(n²) 改为
    /// <c>Array.Sort(keys, items)</c> 双数组 introsort O(n log n)。
    /// 千实体场景下单次排序从 ~50 万次比较降到 ~1 万次。
    ///
    /// 注意：返回的数组是内部共享缓冲（不拷贝），
    /// 调用方必须在下一次 Create 调用前消费完；本类仅限单线程快照导出使用。
    /// </summary>
    internal sealed class ShooterSnapshotOrderBuffer
    {
        private int[] _order = Array.Empty<int>();
        private int[] _keys = Array.Empty<int>();
        private uint[] _enemyKeys = Array.Empty<uint>();

        public int[] CreateIndexOrder(int count)
        {
            EnsureCapacity(count);
            for (var i = 0; i < count; i++)
            {
                _order[i] = i;
            }

            return _order;
        }

        public int[] CreateSortedPlayerOrder(NB<ShooterSveltoPlayerComponent> players, int count)
        {
            var order = CreateIndexOrder(count);
            for (var i = 0; i < count; i++)
            {
                _keys[i] = players[i].PlayerId;
            }

            Array.Sort(_keys, order, 0, count);
            return order;
        }

        public int[] CreateSortedProjectileOrder(NB<ShooterSveltoProjectileComponent> bullets, int count)
        {
            var order = CreateIndexOrder(count);
            for (var i = 0; i < count; i++)
            {
                _keys[i] = bullets[i].BulletId;
            }

            Array.Sort(_keys, order, 0, count);
            return order;
        }

        public int[] CreateSortedEnemyOrder(NativeEntityIDs ids, int count)
        {
            var order = CreateIndexOrder(count);
            for (var i = 0; i < count; i++)
            {
                _enemyKeys[i] = ids[i];
            }

            Array.Sort(_enemyKeys, order, 0, count);
            return order;
        }

        private void EnsureCapacity(int count)
        {
            if (_order.Length >= count)
            {
                return;
            }

            var newCapacity = _order.Length == 0 ? 16 : _order.Length;
            while (newCapacity < count)
            {
                newCapacity = checked(newCapacity * 2);
            }

            _order = new int[newCapacity];
            _keys = new int[newCapacity];
            _enemyKeys = new uint[newCapacity];
        }
    }
}
