#if UNITY_EDITOR
using System;
using AbilityKit.Ability.Config.Authoring;
using Newtonsoft.Json;
using UnityEditor;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// 节点级复制/粘贴：以 Source 同一套序列化形态（camelCase + 字符串枚举）把单个节点子树
    /// 写入系统剪贴板，带 marker 前缀和 Kind 标注。跨触发器、跨模块可用，
    /// 也是 AI/外部编辑工作流可识别的文本格式。
    /// </summary>
    internal static class TriggerAuthoringNodeClipboard
    {
        internal const string Marker = "abilitykit-trigger-node:v1:";

        public static bool HasNode()
        {
            return HasNode(EditorGUIUtility.systemCopyBuffer);
        }

        public static void Copy(TriggerNodeData node, TriggerNodeKind kind)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            EditorGUIUtility.systemCopyBuffer = Marker + Serialize(new TriggerNodeClipboardEntry
            {
                Kind = kind,
                Node = node
            });
        }

        public static bool TryPaste(TriggerNodeKind expectedKind, out TriggerNodeData node)
        {
            node = null;
            if (!TryDeserialize(EditorGUIUtility.systemCopyBuffer, out var entry)) return false;
            if (entry.Kind != expectedKind) return false;
            node = entry.Node;
            return node != null;
        }

        public static string Serialize(TriggerNodeClipboardEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            return JsonConvert.SerializeObject(entry, TriggerSourceJson.CreateSettings(Newtonsoft.Json.Formatting.Indented));
        }

        public static bool TryDeserialize(string clipboardText, out TriggerNodeClipboardEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(clipboardText) ||
                !clipboardText.StartsWith(Marker, StringComparison.Ordinal)) return false;
            try
            {
                entry = JsonConvert.DeserializeObject<TriggerNodeClipboardEntry>(
                    clipboardText.Substring(Marker.Length),
                    TriggerSourceJson.CreateSettings(Newtonsoft.Json.Formatting.None));
            }
            catch (JsonException)
            {
                return false;
            }

            return entry != null && entry.Node != null;
        }

        public static bool HasNode(string clipboardText)
        {
            return !string.IsNullOrEmpty(clipboardText) &&
                   clipboardText.StartsWith(Marker, StringComparison.Ordinal);
        }
    }

    [Serializable]
    internal sealed class TriggerNodeClipboardEntry
    {
        public TriggerNodeKind Kind;
        public TriggerNodeData Node = new TriggerNodeData();
    }
}
#endif
