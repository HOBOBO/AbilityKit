#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Runtime;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal static class AbilityTriggerChangeSetApplier
    {
        [Serializable]
        private sealed class ChangeSet
        {
            public string TargetModuleAssetPath;
            public List<TriggerChange> Triggers;
        }

        [Serializable]
        private sealed class TriggerChange
        {
            public int TriggerId;
            public bool? Enabled;
            public string EventId;
            public string Note;
            public bool ReplaceConditions;
            public bool ReplaceActions;
            public List<ConditionNode> Conditions;
            public List<ActionNode> Actions;
        }

        [Serializable]
        private sealed class ConditionNode
        {
            public string Type;
            public Dictionary<string, object> Args;
            public List<ConditionNode> Items;
            public ConditionNode Item;
        }

        [Serializable]
        private sealed class ActionNode
        {
            public string Type;
            public Dictionary<string, object> Fields;
            public List<ActionNode> Items;
        }

        [MenuItem("AbilityKit/Ability/Apply Trigger ChangeSet Json -> Module")]
        private static void ApplyFromJsonMenu()
        {
            var jsonPath = EditorUtility.OpenFilePanel("Select Trigger ChangeSet Json", Application.dataPath, "json");
            if (string.IsNullOrEmpty(jsonPath)) return;

            var json = File.ReadAllText(jsonPath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[AbilityTriggerChangeSetApplier] Json is empty: {jsonPath}");
                return;
            }

            ChangeSet changeSet;
            try
            {
                changeSet = JsonConvert.DeserializeObject<ChangeSet>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AbilityTriggerChangeSetApplier] Failed to parse json: {jsonPath}\n{e}");
                return;
            }

            if (changeSet == null || changeSet.Triggers == null || changeSet.Triggers.Count == 0)
            {
                Debug.LogError($"[AbilityTriggerChangeSetApplier] ChangeSet is empty: {jsonPath}");
                return;
            }

            var module = ResolveTargetModule(changeSet);
            if (module == null)
            {
                Debug.LogError("[AbilityTriggerChangeSetApplier] Target module not found.");
                return;
            }

            try
            {
                Apply(module, changeSet);
                EditorUtility.SetDirty(module);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = module;
                Debug.Log($"[AbilityTriggerChangeSetApplier] Applied changeset. module={AssetDatabase.GetAssetPath(module)}, triggers={changeSet.Triggers.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AbilityTriggerChangeSetApplier] Apply failed: {e}");
            }
        }

        private static AbilityModuleSO ResolveTargetModule(ChangeSet changeSet)
        {
            if (Selection.activeObject is AbilityModuleSO selected) return selected;

            if (!string.IsNullOrEmpty(changeSet.TargetModuleAssetPath))
            {
                var p = changeSet.TargetModuleAssetPath.Replace('\\', '/');
                var m = AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(p);
                if (m != null) return m;
            }

            var path = EditorUtility.OpenFilePanel("Select AbilityModuleSO asset", Application.dataPath, "asset");
            if (string.IsNullOrEmpty(path)) return null;

            var rel = ToAssetPath(path);
            if (string.IsNullOrEmpty(rel)) return null;
            return AssetDatabase.LoadAssetAtPath<AbilityModuleSO>(rel);
        }

        private static string ToAssetPath(string absolute)
        {
            if (string.IsNullOrEmpty(absolute)) return null;
            absolute = absolute.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!absolute.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return null;
            var rel = "Assets" + absolute.Substring(dataPath.Length);
            return rel;
        }

        private static void Apply(AbilityModuleSO module, ChangeSet changeSet)
        {
            if (module.Triggers == null) module.Triggers = new List<TriggerEditorConfig>();

            var map = new Dictionary<int, TriggerEditorConfig>();
            for (int i = 0; i < module.Triggers.Count; i++)
            {
                var t = module.Triggers[i];
                if (t == null) continue;
                var id = t.TriggerId;
                if (id <= 0) continue;
                map[id] = t;
            }

            var actionTypeMap = BuildStrongTypeMap(typeof(ActionEditorConfigBase));
            var conditionTypeMap = BuildStrongTypeMap(typeof(ConditionEditorConfigBase));

            for (int i = 0; i < changeSet.Triggers.Count; i++)
            {
                var ch = changeSet.Triggers[i];
                if (ch == null || ch.TriggerId <= 0) continue;

                if (!map.TryGetValue(ch.TriggerId, out var tr))
                {
                    tr = new TriggerEditorConfig();
                    if (tr.Core == null) tr.Core = new TriggerHeaderDTO();
                    tr.TriggerId = ch.TriggerId;
                    module.Triggers.Add(tr);
                    map[ch.TriggerId] = tr;
                }

                if (tr.Core == null) tr.Core = new TriggerHeaderDTO();

                if (ch.Enabled.HasValue) tr.Enabled = ch.Enabled.Value;
                if (ch.EventId != null) tr.EventId = ch.EventId;
                if (ch.Note != null) tr.Note = ch.Note;

                if (ch.ReplaceConditions)
                {
                    tr.ConditionsStrong = new List<ConditionEditorConfigBase>();
                }

                if (ch.ReplaceActions)
                {
                    tr.ActionsStrong = new List<ActionEditorConfigBase>();
                }

                if (ch.Conditions != null)
                {
                    if (tr.ConditionsStrong == null) tr.ConditionsStrong = new List<ConditionEditorConfigBase>();
                    for (int c = 0; c < ch.Conditions.Count; c++)
                    {
                        var node = ch.Conditions[c];
                        var cc = BuildCondition(node, conditionTypeMap);
                        if (cc != null) tr.ConditionsStrong.Add(cc);
                    }
                }

                if (ch.Actions != null)
                {
                    if (tr.ActionsStrong == null) tr.ActionsStrong = new List<ActionEditorConfigBase>();
                    for (int a = 0; a < ch.Actions.Count; a++)
                    {
                        var node = ch.Actions[a];
                        var aa = BuildAction(node, actionTypeMap);
                        if (aa != null) tr.ActionsStrong.Add(aa);
                    }
                }
            }

            module.Triggers.Sort((a, b) => (a?.TriggerId ?? 0).CompareTo(b?.TriggerId ?? 0));
        }

        private static Dictionary<string, Type> BuildStrongTypeMap(Type baseType)
        {
            var map = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var t in GetTypesSafe())
            {
                if (t == null || t.IsAbstract) continue;
                if (!baseType.IsAssignableFrom(t)) continue;

                var actionAttr = (TriggerActionTypeAttribute)Attribute.GetCustomAttribute(t, typeof(TriggerActionTypeAttribute));
                if (actionAttr != null && !string.IsNullOrEmpty(actionAttr.Type))
                {
                    map[actionAttr.Type] = t;
                    continue;
                }

                var condAttr = (TriggerConditionTypeAttribute)Attribute.GetCustomAttribute(t, typeof(TriggerConditionTypeAttribute));
                if (condAttr != null && !string.IsNullOrEmpty(condAttr.Type))
                {
                    map[condAttr.Type] = t;
                }
            }

            return map;
        }

        private static IEnumerable<Type> GetTypesSafe()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type[] types;
                try { types = assemblies[i].GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }
                if (types == null) continue;
                for (int j = 0; j < types.Length; j++) yield return types[j];
            }
        }

        private static ConditionEditorConfigBase BuildCondition(ConditionNode node, Dictionary<string, Type> strongTypeMap)
        {
            if (node == null || string.IsNullOrEmpty(node.Type)) return null;

            var dto = new AbilityKit.Ability.Share.CoreDtos.ConditionDTO
            {
                Type = node.Type,
                Args = node.Args,
            };

            if (node.Items != null && node.Items.Count > 0)
            {
                dto.Items = new List<AbilityKit.Ability.Share.CoreDtos.ConditionDTO>(node.Items.Count);
                for (int i = 0; i < node.Items.Count; i++)
                {
                    var child = node.Items[i];
                    dto.Items.Add(ToConditionDto(child));
                }
            }

            if (node.Item != null)
            {
                dto.Item = ToConditionDto(node.Item);
            }

            return JsonConditionEditorConfig.FromDto(dto);
        }

        private static AbilityKit.Ability.Share.CoreDtos.ConditionDTO ToConditionDto(ConditionNode node)
        {
            if (node == null) return null;
            var dto = new AbilityKit.Ability.Share.CoreDtos.ConditionDTO
            {
                Type = node.Type,
                Args = node.Args,
            };

            if (node.Items != null && node.Items.Count > 0)
            {
                dto.Items = new List<AbilityKit.Ability.Share.CoreDtos.ConditionDTO>(node.Items.Count);
                for (int i = 0; i < node.Items.Count; i++) dto.Items.Add(ToConditionDto(node.Items[i]));
            }

            if (node.Item != null) dto.Item = ToConditionDto(node.Item);
            return dto;
        }

        private static ActionEditorConfigBase BuildAction(ActionNode node, Dictionary<string, Type> strongTypeMap)
        {
            if (node == null || string.IsNullOrEmpty(node.Type)) return null;

            if (string.Equals(node.Type, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                var seq = new SequenceActionEditorConfig();
                if (node.Items != null && node.Items.Count > 0)
                {
                    for (int i = 0; i < node.Items.Count; i++)
                    {
                        var child = BuildAction(node.Items[i], strongTypeMap);
                        if (child != null) seq.Items.Add(child);
                    }
                }
                return seq;
            }

            if (strongTypeMap != null && strongTypeMap.TryGetValue(node.Type, out var t) && t != null)
            {
                var inst = (ActionEditorConfigBase)Activator.CreateInstance(t);
                if (node.Fields != null && node.Fields.Count > 0)
                {
                    ApplyFields(inst, node.Fields);
                }
                return inst;
            }

            var dto = new AbilityKit.Ability.Share.CoreDtos.ActionDTO
            {
                Type = node.Type,
                Args = node.Fields != null ? new Dictionary<string, object>(node.Fields, StringComparer.Ordinal) : null,
            };

            if (node.Items != null && node.Items.Count > 0)
            {
                dto.Items = new List<AbilityKit.Ability.Share.CoreDtos.ActionDTO>(node.Items.Count);
                for (int i = 0; i < node.Items.Count; i++) dto.Items.Add(ToActionDto(node.Items[i]));
            }

            return JsonActionEditorConfig.FromDto(dto);
        }

        private static AbilityKit.Ability.Share.CoreDtos.ActionDTO ToActionDto(ActionNode node)
        {
            if (node == null) return null;
            var dto = new AbilityKit.Ability.Share.CoreDtos.ActionDTO
            {
                Type = node.Type,
                Args = node.Fields != null ? new Dictionary<string, object>(node.Fields, StringComparer.Ordinal) : null,
            };

            if (node.Items != null && node.Items.Count > 0)
            {
                dto.Items = new List<AbilityKit.Ability.Share.CoreDtos.ActionDTO>(node.Items.Count);
                for (int i = 0; i < node.Items.Count; i++) dto.Items.Add(ToActionDto(node.Items[i]));
            }

            return dto;
        }

        private static void ApplyFields(object target, Dictionary<string, object> fields)
        {
            if (target == null || fields == null) return;

            var type = target.GetType();
            foreach (var kv in fields)
            {
                var key = kv.Key;
                if (string.IsNullOrEmpty(key)) continue;

                var member = (MemberInfo)type.GetField(key, BindingFlags.Public | BindingFlags.Instance)
                             ?? type.GetProperty(key, BindingFlags.Public | BindingFlags.Instance);

                if (member == null) continue;

                var memberType = member is FieldInfo fi ? fi.FieldType : ((PropertyInfo)member).PropertyType;
                if (memberType == null) continue;

                object converted;
                if (!TryConvert(kv.Value, memberType, out converted)) continue;

                if (member is FieldInfo f)
                {
                    f.SetValue(target, converted);
                }
                else if (member is PropertyInfo p && p.CanWrite)
                {
                    p.SetValue(target, converted);
                }
            }
        }

        private static bool TryConvert(object value, Type dstType, out object converted)
        {
            converted = null;
            if (dstType == null) return false;

            if (value == null)
            {
                if (!dstType.IsValueType || Nullable.GetUnderlyingType(dstType) != null)
                {
                    converted = null;
                    return true;
                }
                return false;
            }

            var underlying = Nullable.GetUnderlyingType(dstType) ?? dstType;

            if (underlying.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            try
            {
                if (underlying.IsEnum)
                {
                    if (value is string s)
                    {
                        converted = Enum.Parse(underlying, s, ignoreCase: true);
                        return true;
                    }

                    var n = Convert.ToInt32(value);
                    converted = Enum.ToObject(underlying, n);
                    return true;
                }

                if (underlying == typeof(int))
                {
                    converted = Convert.ToInt32(value);
                    return true;
                }

                if (underlying == typeof(float))
                {
                    converted = Convert.ToSingle(value);
                    return true;
                }

                if (underlying == typeof(double))
                {
                    converted = Convert.ToDouble(value);
                    return true;
                }

                if (underlying == typeof(bool))
                {
                    converted = Convert.ToBoolean(value);
                    return true;
                }

                if (underlying == typeof(string))
                {
                    converted = value as string ?? value.ToString();
                    return true;
                }

                if (underlying.IsGenericType && underlying.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elemType = underlying.GetGenericArguments()[0];
                    if (value is System.Collections.IEnumerable enumerable && value is not string)
                    {
                        var list = (System.Collections.IList)Activator.CreateInstance(underlying);
                        foreach (var item in enumerable)
                        {
                            if (TryConvert(item, elemType, out var elem)) list.Add(elem);
                        }
                        converted = list;
                        return true;
                    }
                }

                converted = Convert.ChangeType(value, underlying);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
