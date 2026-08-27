using System;
using System.Collections.Generic;

namespace AbilityKit.BehaviorTree.Authoring
{
    /// <summary>
    /// 黑板 key 引用索引与重构：在授权文档内查找某个 key 的所有 KeyRef 属性引用，
    /// 并在改名时同步更新——把"改名后手工排查失效引用"变成一次静态调用。
    /// 纯 C#，编辑器与测试/脚本共用。
    /// </summary>
    public static class BtKeyReferenceIndex
    {
        /// <summary>查找引用指定 key 的全部节点属性（KeyRef 字段，值等于 keyName）。</summary>
        public static List<(string NodeId, string PropertyName)> FindReferences(
            BtTreeDefinition definition,
            BtNodeRegistry registry,
            string keyName)
        {
            var result = new List<(string, string)>();
            if (definition == null || registry == null || string.IsNullOrEmpty(keyName)) return result;

            foreach (var node in definition.Nodes)
            {
                if (!registry.TryGetDescriptor(node.Type, out var descriptor)) continue;

                // 字段名 -> 是否 KeyRef
                var keyRefFields = new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in descriptor.PropertySchema)
                {
                    if (field.Kind == BtPropertyFieldKind.BlackboardKeyRef) keyRefFields.Add(field.Name);
                }

                foreach (var pair in node.Properties.Values)
                {
                    if (!keyRefFields.Contains(pair.Key)) continue;
                    if (pair.Value.TryGetString(out var value)
                        && string.Equals(value, keyName, StringComparison.Ordinal))
                    {
                        result.Add((node.Id, pair.Key));
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// 重命名黑板 key：更新 schema 声明 + 所有 KeyRef 属性引用。
        /// 返回受影响的引用列表（可能为空）。新名与现有 key 冲突或旧名不存在时抛异常。
        /// </summary>
        public static List<(string NodeId, string PropertyName)> RenameKey(
            BtTreeDefinition definition,
            BtNodeRegistry registry,
            string oldName,
            string newName)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrEmpty(oldName)) throw new ArgumentException("oldName 不能为空。", nameof(oldName));
            if (string.IsNullOrEmpty(newName)) throw new ArgumentException("newName 不能为空。", nameof(newName));
            if (string.Equals(oldName, newName, StringComparison.Ordinal)) return new List<(string, string)>();

            // 旧 key 必须存在，新名必须无冲突
            BtBlackboardKeyDefinition? target = null;
            foreach (var key in definition.Blackboard.Keys)
            {
                if (string.Equals(key.Name, oldName, StringComparison.Ordinal)) target = key;
                else if (string.Equals(key.Name, newName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"黑板 key '{newName}' 已存在。");
                }
            }
            if (target == null) throw new InvalidOperationException($"黑板 key '{oldName}' 不存在。");

            // 同步引用
            var affected = new List<(string, string)>();
            foreach (var node in definition.Nodes)
            {
                if (!registry.TryGetDescriptor(node.Type, out var descriptor)) continue;
                foreach (var field in descriptor.PropertySchema)
                {
                    if (field.Kind != BtPropertyFieldKind.BlackboardKeyRef) continue;
                    if (!node.Properties.TryGet(field.Name, out var value)) continue;
                    if (!value.TryGetString(out var refName)
                        || !string.Equals(refName, oldName, StringComparison.Ordinal)) continue;

                    node.Properties.Set(field.Name, BtPropertyValue.Of(newName));
                    affected.Add((node.Id, field.Name));
                }
            }

            // 改 schema 声明名
            target.Name = newName;
            return affected;
        }
    }
}
