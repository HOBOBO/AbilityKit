using System;
using System.Collections;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 游戏标签容器，对标 Unreal Engine 的 FGameplayTagContainer。
    /// 使用 HashSet 存储标签 ID，支持高效查询。
    /// </summary>
    [Serializable]
    public sealed class GameplayTagContainer : IEnumerable<GameplayTag>, IEquatable<GameplayTagContainer>
    {
        /// <summary>
        /// 空容器实例
        /// </summary>
        public static readonly GameplayTagContainer Empty = new GameplayTagContainer();

        private readonly HashSet<int> _ids;

        /// <summary>
        /// 标签数量
        /// </summary>
        public int Count => _ids.Count;

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => _ids.Count == 0;

        /// <summary>
        /// 创建空容器
        /// </summary>
        public GameplayTagContainer()
        {
            _ids = new HashSet<int>();
        }

        /// <summary>
        /// 从单个标签创建容器（优化单标签场景）
        /// </summary>
        public GameplayTagContainer(GameplayTag singleTag)
        {
            _ids = new HashSet<int>();
            if (singleTag.IsValid)
            {
                _ids.Add(singleTag.Id);
            }
        }

        /// <summary>
        /// 从已有标签创建容器
        /// </summary>
        public GameplayTagContainer(IEnumerable<GameplayTag> tags)
        {
            _ids = new HashSet<int>();
            if (tags == null) return;
            foreach (var tag in tags)
            {
                if (tag.IsValid)
                {
                    _ids.Add(tag.Id);
                }
            }
        }

        /// <summary>
        /// 内部构造函数（用于创建包含指定 ID 的容器）
        /// </summary>
        internal GameplayTagContainer(HashSet<int> ids)
        {
            _ids = ids ?? new HashSet<int>();
        }

        /// <summary>
        /// 清空所有标签
        /// </summary>
        public void Clear() => _ids.Clear();

        /// <summary>
        /// 添加标签
        /// </summary>
        public bool Add(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            return _ids.Add(tag.Id);
        }

        /// <summary>
        /// 移除标签
        /// </summary>
        public bool Remove(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            return _ids.Remove(tag.Id);
        }

        /// <summary>
        /// 精确检查是否包含指定标签
        /// </summary>
        public bool HasTagExact(GameplayTag tag)
        {
            if (!tag.IsValid) return false;
            return _ids.Contains(tag.Id);
        }

        /// <summary>
        /// 检查是否包含指定标签（支持父子层级匹配）
        /// </summary>
        public bool HasTag(GameplayTag tag)
        {
            if (!tag.IsValid) return false;

            foreach (var id in _ids)
            {
                if (GameplayTagManager.Instance.Matches(new GameplayTag(id), tag))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查是否包含任意指定标签
        /// </summary>
        public bool HasAny(in GameplayTagContainer other, bool exact = false)
        {
            if (other == null || other._ids.Count == 0) return false;

            if (exact)
            {
                foreach (var id in other._ids)
                {
                    if (_ids.Contains(id)) return true;
                }
                return false;
            }

            foreach (var id in other._ids)
            {
                if (HasTag(new GameplayTag(id))) return true;
            }

            return false;
        }

        /// <summary>
        /// 检查是否包含所有指定标签
        /// </summary>
        public bool HasAll(in GameplayTagContainer other, bool exact = false)
        {
            if (other == null) return true;
            if (other._ids.Count == 0) return true;

            if (exact)
            {
                foreach (var id in other._ids)
                {
                    if (!_ids.Contains(id)) return false;
                }
                return true;
            }

            foreach (var id in other._ids)
            {
                if (!HasTag(new GameplayTag(id))) return false;
            }

            return true;
        }

        /// <summary>
        /// 添加另一个容器的所有标签
        /// </summary>
        public void AppendTags(in GameplayTagContainer other)
        {
            if (other == null) return;
            foreach (var id in other._ids)
            {
                _ids.Add(id);
            }
        }

        /// <summary>
        /// 批量添加标签
        /// </summary>
        public void AddRange(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
            {
                if (tag.IsValid)
                {
                    _ids.Add(tag.Id);
                }
            }
        }

        /// <summary>
        /// 批量移除标签
        /// </summary>
        public void RemoveRange(IEnumerable<GameplayTag> tags)
        {
            if (tags == null) return;
            foreach (var tag in tags)
            {
                if (tag.IsValid)
                {
                    _ids.Remove(tag.Id);
                }
            }
        }

        /// <summary>
        /// 移除另一个容器的所有标签
        /// </summary>
        public void RemoveTags(in GameplayTagContainer other)
        {
            if (other == null) return;
            foreach (var id in other._ids)
            {
                _ids.Remove(id);
            }
        }

        /// <summary>
        /// 获取第一个标签（无序）
        /// </summary>
        public GameplayTag First()
        {
            using (var enumerator = _ids.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    return new GameplayTag(enumerator.Current);
                }
            }
            return GameplayTag.None;
        }

        /// <summary>
        /// 创建并返回标签数组快照
        /// </summary>
        public GameplayTag[] ToArray()
        {
            var result = new GameplayTag[_ids.Count];
            int index = 0;
            foreach (var id in _ids)
            {
                result[index++] = new GameplayTag(id);
            }
            return result;
        }

        /// <summary>
        /// 转换为列表
        /// </summary>
        public List<GameplayTag> ToList()
        {
            var result = new List<GameplayTag>(_ids.Count);
            foreach (var id in _ids)
            {
                result.Add(new GameplayTag(id));
            }
            return result;
        }

        /// <summary>
        /// 并集：返回包含两个容器所有标签的新容器
        /// </summary>
        public GameplayTagContainer Union(in GameplayTagContainer other)
        {
            var result = new GameplayTagContainer();
            foreach (var id in _ids)
            {
                result._ids.Add(id);
            }
            if (other != null)
            {
                foreach (var id in other._ids)
                {
                    result._ids.Add(id);
                }
            }
            return result;
        }

        /// <summary>
        /// 交集：返回包含两个容器共有标签的新容器
        /// </summary>
        public GameplayTagContainer Intersect(in GameplayTagContainer other)
        {
            var result = new GameplayTagContainer();
            if (other == null) return result;

            if (_ids.Count < other._ids.Count)
            {
                foreach (var id in _ids)
                {
                    if (other._ids.Contains(id))
                    {
                        result._ids.Add(id);
                    }
                }
            }
            else
            {
                foreach (var id in other._ids)
                {
                    if (_ids.Contains(id))
                    {
                        result._ids.Add(id);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 差集：返回包含本容器有而 other 没有的标签的新容器
        /// </summary>
        public GameplayTagContainer Except(in GameplayTagContainer other)
        {
            var result = new GameplayTagContainer();
            if (other == null)
            {
                foreach (var id in _ids)
                {
                    result._ids.Add(id);
                }
                return result;
            }

            foreach (var id in _ids)
            {
                if (!other._ids.Contains(id))
                {
                    result._ids.Add(id);
                }
            }
            return result;
        }

        /// <summary>
        /// 网络序列化（可选实现）
        /// </summary>
        public void NetSerialize(FastBufferWriter writer)
        {
            if (_ids.Count == 0)
            {
                writer.WriteValue((byte)0);
                return;
            }

            writer.WriteValue((byte)_ids.Count);
            foreach (var id in _ids)
            {
                writer.WriteValue(id);
            }
        }

        /// <summary>
        /// 网络反序列化（可选实现）
        /// </summary>
        public void NetDeserialize(FastBufferReader reader)
        {
            _ids.Clear();
            byte count = reader.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var id = reader.ReadInt32();
                _ids.Add(id);
            }
        }

        /// <summary>
        /// 精确相等比较
        /// </summary>
        public bool Equals(GameplayTagContainer other)
        {
            if (other == null) return false;
            if (_ids.Count != other._ids.Count) return false;

            foreach (var id in _ids)
            {
                if (!other._ids.Contains(id)) return false;
            }
            return true;
        }

        /// <summary>
        /// 重写相等比较
        /// </summary>
        public override bool Equals(object obj) => Equals(obj as GameplayTagContainer);

        /// <summary>
        /// 获取哈希码
        /// </summary>
        public override int GetHashCode()
        {
            int hash = 0;
            foreach (var id in _ids)
            {
                hash ^= id.GetHashCode();
            }
            return hash;
        }

        public IEnumerator<GameplayTag> GetEnumerator()
        {
            foreach (var id in _ids)
            {
                yield return new GameplayTag(id);
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// 静态运算符：检查是否包含任意标签
        /// </summary>
        public static bool operator &(GameplayTagContainer a, GameplayTagContainer b)
        {
            return a.HasAny(b);
        }

        /// <summary>
        /// 静态运算符：检查是否包含所有标签
        /// </summary>
        public static bool operator |(GameplayTagContainer a, GameplayTagContainer b)
        {
            return a.HasAll(b);
        }

        /// <summary>
        /// 静态运算符：并集
        /// </summary>
        public static GameplayTagContainer operator +(GameplayTagContainer a, GameplayTagContainer b)
        {
            return a.Union(b);
        }

        /// <summary>
        /// 静态运算符：添加标签
        /// </summary>
        public static GameplayTagContainer operator +(GameplayTagContainer a, GameplayTag b)
        {
            var result = new GameplayTagContainer();
            foreach (var id in a._ids)
            {
                result._ids.Add(id);
            }
            if (b.IsValid)
            {
                result._ids.Add(b.Id);
            }
            return result;
        }

        /// <summary>
        /// 静态运算符：差集
        /// </summary>
        public static GameplayTagContainer operator -(GameplayTagContainer a, GameplayTagContainer b)
        {
            return a.Except(b);
        }

        /// <summary>
        /// 静态运算符：移除标签
        /// </summary>
        public static GameplayTagContainer operator -(GameplayTagContainer a, GameplayTag b)
        {
            if (!b.IsValid) return a;
            var result = new GameplayTagContainer();
            foreach (var id in a._ids)
            {
                if (id != b.Id)
                {
                    result._ids.Add(id);
                }
            }
            return result;
        }

        /// <summary>
        /// 隐式从单个标签转换
        /// </summary>
        public static implicit operator GameplayTagContainer(GameplayTag tag)
        {
            return new GameplayTagContainer(tag);
        }
    }

    /// <summary>
    /// 快速缓冲区写入器（简化版，用于网络序列化）
    /// </summary>
    public class FastBufferWriter
    {
        private readonly System.IO.MemoryStream _stream;
        private readonly System.IO.BinaryWriter _writer;

        public FastBufferWriter()
        {
            _stream = new System.IO.MemoryStream();
            _writer = new System.IO.BinaryWriter(_stream);
        }

        public void WriteValue(int value) => _writer.Write(value);
        public void WriteValue(byte value) => _writer.Write(value);
        public void WriteValue(ushort value) => _writer.Write(value);

        public byte[] ToArray() => _stream.ToArray();
    }

    /// <summary>
    /// 快速缓冲区读取器（简化版，用于网络反序列化）
    /// </summary>
    public class FastBufferReader
    {
        private readonly System.IO.MemoryStream _stream;
        private readonly System.IO.BinaryReader _reader;

        public FastBufferReader(byte[] data)
        {
            _stream = new System.IO.MemoryStream(data);
            _reader = new System.IO.BinaryReader(_stream);
        }

        public int ReadInt32() => _reader.ReadInt32();
        public byte ReadByte() => _reader.ReadByte();
        public ushort ReadUInt16() => _reader.ReadUInt16();
    }
}
