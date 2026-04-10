using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 游戏标签栈，对标 Unreal Engine 的 FGameplayTagStack。
    /// 用于存储带计数的标签，支持叠加效果（如叠毒、叠buff层数）。
    /// </summary>
    [Serializable]
    public struct GameplayTagStack : IEquatable<GameplayTagStack>
    {
        /// <summary>
        /// 标签
        /// </summary>
        public readonly GameplayTag Tag;

        /// <summary>
        /// 计数
        /// </summary>
        public int Count;

        /// <summary>
        /// 创建标签栈
        /// </summary>
        public GameplayTagStack(GameplayTag tag, int count = 0)
        {
            Tag = tag;
            Count = count < 0 ? 0 : count;
        }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => Tag.IsValid;

        /// <summary>
        /// 是否为空（计数为0）
        /// </summary>
        public bool IsEmpty => Count == 0;

        /// <summary>
        /// 增加计数
        /// </summary>
        public void Increment(int amount = 1)
        {
            if (amount > 0)
            {
                Count += amount;
            }
        }

        /// <summary>
        /// 减少计数（不会低于0）
        /// </summary>
        public void Decrement(int amount = 1)
        {
            Count = Math.Max(0, Count - amount);
        }

        /// <summary>
        /// 设置计数
        /// </summary>
        public void SetCount(int count)
        {
            Count = count < 0 ? 0 : count;
        }

        public bool Equals(GameplayTagStack other) => Tag == other.Tag && Count == other.Count;

        public override bool Equals(object obj) => obj is GameplayTagStack other && Equals(other);

        public override int GetHashCode() => Tag.GetHashCode() ^ Count.GetHashCode();

        public static bool operator ==(GameplayTagStack a, GameplayTagStack b) => a.Equals(b);

        public static bool operator !=(GameplayTagStack a, GameplayTagStack b) => !a.Equals(b);

        public override string ToString() => $"{Tag} x{Count}";
    }

    /// <summary>
    /// 游戏标签栈容器，对标 UE 的 TMap<FGameplayTag, int>。
    /// 管理多个标签及其计数。
    /// </summary>
    [Serializable]
    public class GameplayTagStackContainer : IEnumerable<GameplayTagStack>, IEquatable<GameplayTagStackContainer>
    {
        private readonly Dictionary<int, int> _stacks = new Dictionary<int, int>();

        /// <summary>
        /// 栈的数量
        /// </summary>
        public int Count => _stacks.Count;

        /// <summary>
        /// 总计数（所有栈的计数之和）
        /// </summary>
        public int TotalCount
        {
            get
            {
                int total = 0;
                foreach (var count in _stacks.Values)
                {
                    total += count;
                }
                return total;
            }
        }

        /// <summary>
        /// 获取标签的计数
        /// </summary>
        public int GetStackCount(GameplayTag tag)
        {
            if (!tag.IsValid) return 0;
            return _stacks.TryGetValue(tag.Id, out var count) ? count : 0;
        }

        /// <summary>
        /// 检查标签是否存在
        /// </summary>
        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            return _stacks.TryGetValue(tag.Id, out var count) && count > 0;
        }

        /// <summary>
        /// 添加标签计数
        /// </summary>
        public void AddStack(GameplayTag tag, int count = 1)
        {
            if (!tag.IsValid || count <= 0) return;

            if (_stacks.TryGetValue(tag.Id, out var existing))
            {
                _stacks[tag.Id] = existing + count;
            }
            else
            {
                _stacks[tag.Id] = count;
            }
        }

        /// <summary>
        /// 移除标签计数
        /// </summary>
        public void RemoveStack(GameplayTag tag, int count = 1)
        {
            if (!tag.IsValid || count <= 0) return;

            if (_stacks.TryGetValue(tag.Id, out var existing))
            {
                var newCount = existing - count;
                if (newCount <= 0)
                {
                    _stacks.Remove(tag.Id);
                }
                else
                {
                    _stacks[tag.Id] = newCount;
                }
            }
        }

        /// <summary>
        /// 设置标签计数
        /// </summary>
        public void SetStackCount(GameplayTag tag, int count)
        {
            if (!tag.IsValid) return;

            if (count <= 0)
            {
                _stacks.Remove(tag.Id);
            }
            else
            {
                _stacks[tag.Id] = count;
            }
        }

        /// <summary>
        /// 清空所有标签
        /// </summary>
        public void Clear()
        {
            _stacks.Clear();
        }

        /// <summary>
        /// 批量添加标签计数
        /// </summary>
        public void AddStacks(IEnumerable<GameplayTagStack> stacks)
        {
            if (stacks == null) return;
            foreach (var stack in stacks)
            {
                AddStack(stack.Tag, stack.Count);
            }
        }

        /// <summary>
        /// 获取所有有计数的标签容器
        /// </summary>
        public GameplayTagContainer ToContainer()
        {
            var container = new GameplayTagContainer();
            foreach (var kvp in _stacks)
            {
                if (kvp.Value > 0)
                {
                    container.Add(new GameplayTag(kvp.Key));
                }
            }
            return container;
        }

        /// <summary>
        /// 获取所有栈
        /// </summary>
        public List<GameplayTagStack> ToList()
        {
            var result = new List<GameplayTagStack>(_stacks.Count);
            foreach (var kvp in _stacks)
            {
                if (kvp.Value > 0)
                {
                    result.Add(new GameplayTagStack(new GameplayTag(kvp.Key), kvp.Value));
                }
            }
            return result;
        }

        public IEnumerator<GameplayTagStack> GetEnumerator()
        {
            foreach (var kvp in _stacks)
            {
                if (kvp.Value > 0)
                {
                    yield return new GameplayTagStack(new GameplayTag(kvp.Key), kvp.Value);
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Equals(GameplayTagStackContainer other)
        {
            if (other == null) return false;
            if (_stacks.Count != other._stacks.Count) return false;

            foreach (var kvp in _stacks)
            {
                if (!other._stacks.TryGetValue(kvp.Key, out var otherCount))
                {
                    return false;
                }
                if (kvp.Value != otherCount) return false;
            }
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as GameplayTagStackContainer);

        public override int GetHashCode()
        {
            int hash = 0;
            foreach (var kvp in _stacks)
            {
                hash ^= kvp.Key.GetHashCode() ^ kvp.Value.GetHashCode();
            }
            return hash;
        }
    }
}
