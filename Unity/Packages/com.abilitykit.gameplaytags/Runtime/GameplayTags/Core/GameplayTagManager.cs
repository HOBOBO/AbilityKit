using System;
using System.Collections.Generic;
using System.Text;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签变更事件参数
    /// </summary>
    public class GameplayTagChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 发生变化的标签
        /// </summary>
        public GameplayTag Tag { get; }

        /// <summary>
        /// 变化类型
        /// </summary>
        public GameplayTagChangeType ChangeType { get; }

        public GameplayTagChangedEventArgs(GameplayTag tag, GameplayTagChangeType changeType)
        {
            Tag = tag;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// 标签变化类型
    /// </summary>
    public enum GameplayTagChangeType
    {
        /// <summary>
        /// 标签被添加
        /// </summary>
        Added,

        /// <summary>
        /// 标签被移除
        /// </summary>
        Removed,

        /// <summary>
        /// 标签被更新（层级变化等）
        /// </summary>
        Updated
    }

    /// <summary>
    /// 标签变更监听器接口
    /// </summary>
    public interface IGameplayTagChangedListener
    {
        /// <summary>
        /// 当标签发生变化时回调
        /// </summary>
        void OnGameplayTagChanged(GameplayTagChangedEventArgs args);
    }

    /// <summary>
    /// 游戏标签管理器，对标 Unreal Engine 的 UGameplayTagsManager。
    /// 单例模式，管理所有标签的注册和层级关系。
    /// </summary>
    public sealed class GameplayTagManager
    {
        /// <summary>
        /// 标签节点
        /// </summary>
        private sealed class Node
        {
            public string Name;
            public int ParentId;
            public List<int> Children;
            public ushort NetIndex;
        }

        private readonly Dictionary<string, int> _byName = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<Node> _nodes = new List<Node>(256);
        private readonly Dictionary<int, HashSet<int>> _ancestors = new Dictionary<int, HashSet<int>>();
        private readonly List<IGameplayTagChangedListener> _listeners = new List<IGameplayTagChangedListener>();
        private ushort _nextNetIndex = 1;

        /// <summary>
        /// 单例实例
        /// </summary>
        public static GameplayTagManager Instance { get; } = new GameplayTagManager();

        /// <summary>
        /// 已注册的标签数量
        /// </summary>
        public int TagCount => _byName.Count;

        /// <summary>
        /// 下一个可用的 NetIndex
        /// </summary>
        public ushort NextNetIndex => _nextNetIndex;

        private GameplayTagManager()
        {
            _nodes.Add(new Node { Name = string.Empty, ParentId = 0, Children = new List<int>(), NetIndex = 0 });
            _byName[string.Empty] = 0;
        }

        /// <summary>
        /// 添加标签变更监听器
        /// </summary>
        public void AddListener(IGameplayTagChangedListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
            {
                _listeners.Add(listener);
            }
        }

        /// <summary>
        /// 移除标签变更监听器
        /// </summary>
        public void RemoveListener(IGameplayTagChangedListener listener)
        {
            _listeners.Remove(listener);
        }

        /// <summary>
        /// 请求注册一个标签（不存在则创建）
        /// </summary>
        public GameplayTag RequestTag(string name)
        {
            if (!TryNormalize(name, out var normalized))
            {
                throw new ArgumentException("Invalid tag name", nameof(name));
            }

            var id = GetOrCreate(normalized);
            return new GameplayTag(id, _nodes[id].NetIndex);
        }

        /// <summary>
        /// 批量注册标签
        /// </summary>
        public void RegisterTags(IEnumerable<string> names)
        {
            if (names == null) return;
            foreach (var name in names)
            {
                try
                {
                    RequestTag(name);
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 批量注册标签（带事件通知）
        /// </summary>
        public void RegisterTagsWithNotification(IEnumerable<string> names)
        {
            if (names == null) return;
            foreach (var name in names)
            {
                try
                {
                    var wasAdded = !_byName.ContainsKey(name);
                    RequestTag(name);
                    if (wasAdded)
                    {
                        NotifyTagChanged(new GameplayTag(_byName[name]), GameplayTagChangeType.Added);
                    }
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// 尝试获取已注册的标签
        /// </summary>
        public bool TryGetTag(string name, out GameplayTag tag)
        {
            tag = default;
            if (!TryNormalize(name, out var normalized)) return false;
            if (_byName.TryGetValue(normalized, out var id))
            {
                tag = new GameplayTag(id, _nodes[id].NetIndex);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 根据 ID 获取标签
        /// </summary>
        public GameplayTag GetTagFromId(int id)
        {
            if (id <= 0 || id >= _nodes.Count) return GameplayTag.None;
            return new GameplayTag(id, _nodes[id].NetIndex);
        }

        /// <summary>
        /// 根据 NetIndex 获取标签
        /// </summary>
        public GameplayTag GetTagFromNetIndex(ushort netIndex)
        {
            if (netIndex == 0) return GameplayTag.None;

            for (int i = 1; i < _nodes.Count; i++)
            {
                if (_nodes[i].NetIndex == netIndex)
                {
                    return new GameplayTag(i, netIndex);
                }
            }
            return GameplayTag.None;
        }

        /// <summary>
        /// 根据 ID 获取标签名称
        /// </summary>
        public string GetName(GameplayTag tag)
        {
            if (!tag.IsValid) return string.Empty;
            var id = tag.Id;
            if (id <= 0 || id >= _nodes.Count) return string.Empty;
            return _nodes[id].Name ?? string.Empty;
        }

        /// <summary>
        /// 根据 ID 获取简略名称
        /// </summary>
        public string GetSimpleName(GameplayTag tag)
        {
            var name = GetName(tag);
            var lastDot = name.LastIndexOf('.');
            return lastDot >= 0 ? name.Substring(lastDot + 1) : name;
        }

        /// <summary>
        /// 获取标签的父标签
        /// </summary>
        public GameplayTag GetParent(GameplayTag tag)
        {
            if (!tag.IsValid) return GameplayTag.None;
            var parentId = _nodes[tag.Id].ParentId;
            return parentId == 0 ? GameplayTag.None : new GameplayTag(parentId, _nodes[parentId].NetIndex);
        }

        /// <summary>
        /// 检查两个标签是否匹配（支持父子层级）
        /// </summary>
        public bool Matches(GameplayTag tag, GameplayTag matchAgainst)
        {
            if (!tag.IsValid || !matchAgainst.IsValid) return false;
            if (tag.Id == matchAgainst.Id) return true;
            return IsChildOf(tag, matchAgainst);
        }

        /// <summary>
        /// 检查 tag 是否是 parent 的子标签
        /// </summary>
        public bool IsChildOf(GameplayTag tag, GameplayTag parent)
        {
            if (!tag.IsValid || !parent.IsValid) return false;
            if (tag.Id == parent.Id) return false;

            if (_ancestors.TryGetValue(tag.Id, out var ancestors))
            {
                return ancestors.Contains(parent.Id);
            }

            return false;
        }

        /// <summary>
        /// 获取标签的所有祖先标签
        /// </summary>
        public IEnumerable<GameplayTag> GetAncestors(GameplayTag tag)
        {
            if (!tag.IsValid) yield break;
            if (!_ancestors.TryGetValue(tag.Id, out var ancestors)) yield break;

            foreach (var ancestorId in ancestors)
            {
                yield return new GameplayTag(ancestorId, _nodes[ancestorId].NetIndex);
            }
        }

        /// <summary>
        /// 获取标签的所有后代标签
        /// </summary>
        public IEnumerable<GameplayTag> GetDescendants(GameplayTag tag)
        {
            if (!tag.IsValid) yield break;

            var children = _nodes[tag.Id].Children;
            foreach (var childId in children)
            {
                yield return new GameplayTag(childId, _nodes[childId].NetIndex);
                foreach (var descendant in GetDescendants(new GameplayTag(childId, _nodes[childId].NetIndex)))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// 获取标签的所有兄弟标签
        /// </summary>
        public IEnumerable<GameplayTag> GetSiblings(GameplayTag tag)
        {
            if (!tag.IsValid) yield break;

            var parentId = _nodes[tag.Id].ParentId;
            if (parentId == 0) yield break;

            var siblings = _nodes[parentId].Children;
            foreach (var siblingId in siblings)
            {
                if (siblingId != tag.Id)
                {
                    yield return new GameplayTag(siblingId, _nodes[siblingId].NetIndex);
                }
            }
        }

        /// <summary>
        /// 获取标签的所有子标签
        /// </summary>
        public IEnumerable<GameplayTag> GetChildren(GameplayTag tag)
        {
            if (!tag.IsValid) yield break;

            foreach (var childId in _nodes[tag.Id].Children)
            {
                yield return new GameplayTag(childId, _nodes[childId].NetIndex);
            }
        }

        /// <summary>
        /// 获取标签的根标签
        /// </summary>
        public GameplayTag GetRootTag(GameplayTag tag)
        {
            if (!tag.IsValid) return GameplayTag.None;

            int currentId = tag.Id;
            while (true)
            {
                var parentId = _nodes[currentId].ParentId;
                if (parentId == 0) return new GameplayTag(currentId, _nodes[currentId].NetIndex);
                currentId = parentId;
            }
        }

        /// <summary>
        /// 获取所有已注册的标签名称
        /// </summary>
        public IReadOnlyList<string> GetAllTagNames()
        {
            var names = new List<string>(_byName.Count - 1);
            foreach (var kvp in _byName)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                {
                    names.Add(kvp.Key);
                }
            }
            return names;
        }

        /// <summary>
        /// 获取所有已注册的标签
        /// </summary>
        public IReadOnlyList<GameplayTag> GetAllTags()
        {
            var tags = new List<GameplayTag>(_byName.Count - 1);
            foreach (var kvp in _byName)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                {
                    tags.Add(new GameplayTag(kvp.Value, _nodes[kvp.Value].NetIndex));
                }
            }
            return tags;
        }

        /// <summary>
        /// 获取所有根标签
        /// </summary>
        public IEnumerable<GameplayTag> GetRootTags()
        {
            foreach (var childId in _nodes[0].Children)
            {
                yield return new GameplayTag(childId, _nodes[childId].NetIndex);
            }
        }

        /// <summary>
        /// 序列化所有标签为 JSON 格式
        /// </summary>
        public string SerializeToJson()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"tags\":[");
            bool first = true;
            for (int i = 1; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (!first) sb.Append(',');
                sb.Append('{');
                sb.Append($"\"name\":\"{EscapeJson(node.Name)}\",");
                sb.Append($"\"netIndex\":{node.NetIndex},");
                sb.Append($"\"parentId\":{node.ParentId}");
                sb.Append('}');
                first = false;
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// 从 JSON 反序列化标签
        /// </summary>
        public void DeserializeFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            try
            {
                var tags = ParseJsonTags(json);
                foreach (var tagInfo in tags)
                {
                    var normalized = tagInfo.Name;
                    if (_byName.ContainsKey(normalized)) continue;

                    var parentId = 0;
                    if (!string.IsNullOrEmpty(tagInfo.ParentName))
                    {
                        if (_byName.TryGetValue(tagInfo.ParentName, out var existingParentId))
                        {
                            parentId = existingParentId;
                        }
                        else
                        {
                            parentId = GetOrCreate(tagInfo.ParentName);
                        }
                    }

                    var id = _nodes.Count;
                    var node = new Node
                    {
                        Name = normalized,
                        ParentId = parentId,
                        Children = new List<int>(),
                        NetIndex = tagInfo.NetIndex > 0 ? tagInfo.NetIndex : _nextNetIndex++
                    };
                    _nodes.Add(node);

                    if (parentId >= 0 && parentId < _nodes.Count)
                    {
                        _nodes[parentId].Children.Add(id);
                    }

                    _byName[normalized] = id;
                    BuildAncestorCache(id, parentId);
                }
            }
            catch
            {
            }
        }

        private struct JsonTagInfo
        {
            public string Name;
            public string ParentName;
            public ushort NetIndex;
        }

        private List<JsonTagInfo> ParseJsonTags(string json)
        {
            var result = new List<JsonTagInfo>();
            var trimmed = json.Trim();

            var tagsStart = trimmed.IndexOf("\"tags\"");
            if (tagsStart < 0) return result;

            var arrayStart = trimmed.IndexOf('[', tagsStart);
            var arrayEnd = trimmed.IndexOf(']', arrayStart);
            if (arrayStart < 0 || arrayEnd < 0) return result;

            var content = trimmed.Substring(arrayStart + 1, arrayEnd - arrayStart - 1);
            var objects = content.Split('{');
            foreach (var obj in objects)
            {
                if (string.IsNullOrWhiteSpace(obj)) continue;

                var end = obj.IndexOf('}');
                if (end < 0) continue;

                var inner = obj.Substring(0, end);
                var tagInfo = ParseJsonObject(inner);
                if (tagInfo.Name != null)
                {
                    result.Add(tagInfo);
                }
            }
            return result;
        }

        private JsonTagInfo ParseJsonObject(string json)
        {
            var info = new JsonTagInfo();

            var nameStart = json.IndexOf("\"name\"");
            if (nameStart >= 0)
            {
                var colon = json.IndexOf(':', nameStart);
                var quote1 = json.IndexOf('"', colon);
                var quote2 = json.IndexOf('"', quote1 + 1);
                if (quote1 >= 0 && quote2 >= 0)
                {
                    info.Name = json.Substring(quote1 + 1, quote2 - quote1 - 1);
                }
            }

            var netIndexStart = json.IndexOf("\"netIndex\"");
            if (netIndexStart >= 0)
            {
                var colon = json.IndexOf(':', netIndexStart);
                var comma = json.IndexOf(',', colon);
                var numStr = (comma > 0 ? json.Substring(colon + 1, comma - colon - 1) : json.Substring(colon + 1)).Trim();
                if (ushort.TryParse(numStr, out var netIndex))
                {
                    info.NetIndex = netIndex;
                }
            }

            var parentStart = json.IndexOf("\"parentName\"");
            if (parentStart >= 0)
            {
                var colon = json.IndexOf(':', parentStart);
                var quote1 = json.IndexOf('"', colon);
                var quote2 = json.IndexOf('"', quote1 + 1);
                if (quote1 >= 0 && quote2 >= 0)
                {
                    info.ParentName = json.Substring(quote1 + 1, quote2 - quote1 - 1);
                }
            }

            return info;
        }

        /// <summary>
        /// 网络序列化所有标签
        /// </summary>
        public void NetworkSerialize(FastBufferWriter writer)
        {
            writer.WriteValue(_nextNetIndex);
            writer.WriteValue((ushort)(_nodes.Count - 1));

            for (int i = 1; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                writer.WriteValue(node.NetIndex);
                writer.WriteValue((ushort)node.Name.Length);
                foreach (var c in node.Name)
                {
                    writer.WriteValue((ushort)c);
                }
                writer.WriteValue((ushort)node.ParentId);
            }
        }

        /// <summary>
        /// 网络反序列化标签
        /// </summary>
        public void NetworkDeserialize(FastBufferReader reader)
        {
            var nextNetIndex = reader.ReadUInt16();
            var count = reader.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                var netIndex = reader.ReadUInt16();
                var nameLen = reader.ReadUInt16();
                var chars = new char[nameLen];
                for (int j = 0; j < nameLen; j++)
                {
                    chars[j] = (char)reader.ReadUInt16();
                }
                var name = new string(chars);
                var parentId = reader.ReadUInt16();

                if (_byName.ContainsKey(name)) continue;

                var id = _nodes.Count;
                var node = new Node
                {
                    Name = name,
                    ParentId = parentId,
                    Children = new List<int>(),
                    NetIndex = netIndex
                };
                _nodes.Add(node);

                if (parentId < _nodes.Count)
                {
                    _nodes[parentId].Children.Add(id);
                }

                _byName[name] = id;
                BuildAncestorCache(id, parentId);
            }

            if (nextNetIndex > _nextNetIndex)
            {
                _nextNetIndex = nextNetIndex;
            }
        }

        /// <summary>
        /// 重置管理器（清空所有自定义标签）
        /// </summary>
        public void Reset()
        {
            _byName.Clear();
            _nodes.Clear();
            _ancestors.Clear();
            _nextNetIndex = 1;

            _nodes.Add(new Node { Name = string.Empty, ParentId = 0, Children = new List<int>(), NetIndex = 0 });
            _byName[string.Empty] = 0;
        }

        private int GetOrCreate(string normalized)
        {
            if (_byName.TryGetValue(normalized, out var existing)) return existing;

            var lastDot = normalized.LastIndexOf('.');
            var parentId = 0;
            if (lastDot > 0)
            {
                var parentName = normalized.Substring(0, lastDot);
                parentId = GetOrCreate(parentName);
            }

            var id = _nodes.Count;
            var node = new Node
            {
                Name = normalized,
                ParentId = parentId,
                Children = new List<int>(),
                NetIndex = _nextNetIndex++
            };
            _nodes.Add(node);

            if (parentId >= 0 && parentId < _nodes.Count)
            {
                _nodes[parentId].Children.Add(id);
            }

            _byName[normalized] = id;
            BuildAncestorCache(id, parentId);
            return id;
        }

        private void BuildAncestorCache(int id, int parentId)
        {
            var ancestors = new HashSet<int>();

            if (parentId != 0)
            {
                ancestors.Add(parentId);

                if (_ancestors.TryGetValue(parentId, out var parentAncestors))
                {
                    foreach (var ancestor in parentAncestors)
                    {
                        ancestors.Add(ancestor);
                    }
                }
            }

            _ancestors[id] = ancestors;
        }

        private void NotifyTagChanged(GameplayTag tag, GameplayTagChangeType changeType)
        {
            var args = new GameplayTagChangedEventArgs(tag, changeType);
            foreach (var listener in _listeners)
            {
                try
                {
                    listener.OnGameplayTagChanged(args);
                }
                catch
                {
                }
            }
        }

        private static bool TryNormalize(string name, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var s = name.Trim();
            if (s.Length == 0) return false;
            if (s[0] == '.' || s[s.Length - 1] == '.') return false;

            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '.')
                {
                    if (i > 0 && s[i - 1] == '.') return false;
                    continue;
                }

                if (char.IsWhiteSpace(c)) return false;
            }

            normalized = s;
            return true;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
