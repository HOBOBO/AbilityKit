#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Utilities
{
    /// <summary>
    /// Golden example 模块（一个技能 / 一个 Buff / 一个被动），设计文档 §12 P0 验收样例。
    /// 覆盖：强类型条件（复合 + 战斗谓词 + 数值比较）、全局/局部黑板引用、Payload 引用、
    /// 复合动作（give_damage / add_buff / heal / debug_log）、可复用条件组引用。
    /// 参数组合与现有导出器测试中已验证的范式一致，保证校验与 Runtime 导出全绿。
    /// </summary>
    internal static class TriggerAuthoringGoldenExamples
    {
        public static List<TriggerAuthoringModuleData> BuildAll()
        {
            return new List<TriggerAuthoringModuleData>
            {
                BuildSkillModule(),
                BuildBuffModule(),
                BuildPassiveModule()
            };
        }

        /// <summary>技能：施放完成 → 伤害目标 + 日志；施法失败 → 记录原因；演示模块局部黑板与全局黑板条件。</summary>
        public static TriggerAuthoringModuleData BuildSkillModule()
        {
            return new TriggerAuthoringModuleData
            {
                ModuleId = "module.golden_skill",
                DisplayName = "Golden Skill",
                Kind = TriggerModuleKind.Ability,
                Blackboard =
                {
                    new TriggerBlackboardVariableData
                    {
                        Key = "damageBoost",
                        Type = TriggerValueType.Number,
                        Description = "本次施放的额外伤害系数",
                        DefaultValue = ConstNumber(0)
                    }
                },
                Triggers =
                {
                    new TriggerDefinitionData
                    {
                        Id = 101,
                        Name = "OnCastCompleteDamage",
                        Event = "skill.cast.complete",
                        Priority = 50,
                        Condition = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Condition,
                            Type = "all",
                            Children =
                            {
                                Node(TriggerNodeKind.Condition, "num_var_gt", Arg("variable",
                                    LocalRef("damageBoost", TriggerValueType.Number)), Arg("value", ConstNumber(0.5))),
                                Node(TriggerNodeKind.Condition, "num_var_gt", Arg("variable",
                                    GlobalRef("skill.decayFactor", TriggerValueType.Number)), Arg("value", ConstNumber(0)))
                            }
                        },
                        Actions = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Action,
                            Type = "seq",
                            Children =
                            {
                                Node(TriggerNodeKind.Action, "give_damage",
                                    Arg("damage_value", ConstNumber(120)),
                                    Arg("damage_type", ConstInt(1)),
                                    Arg("reason_kind", ConstInt(1)),
                                    Arg("target_source", ConstInt(2)),
                                    Arg("target_actor_id", PayloadRef("target.actor_id", TriggerValueType.Integer))),
                                Node(TriggerNodeKind.Action, "debug_log", Arg("message", ConstString("golden skill cast")))
                            }
                        }
                    },
                    new TriggerDefinitionData
                    {
                        Id = 102,
                        Name = "OnPrecastFailLog",
                        Event = "skill.precast.fail",
                        Actions = Node(TriggerNodeKind.Action, "debug_log",
                            Arg("message", ConstString("golden skill precast failed")))
                    }
                }
            };
        }

        /// <summary>Buff：叠层变化 → 携带该 Buff 时给自己补一层关联 Buff；演示 Payload 条件与 IntegerList 常量。</summary>
        public static TriggerAuthoringModuleData BuildBuffModule()
        {
            return new TriggerAuthoringModuleData
            {
                ModuleId = "module.golden_buff",
                DisplayName = "Golden Buff",
                Kind = TriggerModuleKind.Buff,
                Triggers =
                {
                    new TriggerDefinitionData
                    {
                        Id = 201,
                        Name = "OnStackChangedReapply",
                        Event = "buff.stack_changed",
                        Condition = Node(TriggerNodeKind.Condition, "has_buff",
                            Arg("buff_id", PayloadRef("buff_id", TriggerValueType.Integer)),
                            Arg("check_stack", ConstBool(false))),
                        Actions = Node(TriggerNodeKind.Action, "add_buff",
                            Arg("buff_ids", ConstIntegerList(21001)),
                            Arg("target_source", ConstInt(4)))
                    }
                }
            };
        }

        /// <summary>被动：受击后血量低于阈值 → 自愈；演示可复用条件组引用与上下文谓词条件。</summary>
        public static TriggerAuthoringModuleData BuildPassiveModule()
        {
            return new TriggerAuthoringModuleData
            {
                ModuleId = "module.golden_passive",
                DisplayName = "Golden Passive",
                Kind = TriggerModuleKind.Passive,
                ConditionGroups =
                {
                    new TriggerNodeGroupData
                    {
                        Id = "condition_group_low_health",
                        DisplayName = "Low Health",
                        Description = "持有者血量低于阈值",
                        Root = Node(TriggerNodeKind.Condition, "health_percent",
                            Arg("threshold", ConstNumber(0.3)),
                            Arg("compare_type", ConstInt(0)))
                    }
                },
                Triggers =
                {
                    new TriggerDefinitionData
                    {
                        Id = 301,
                        Name = "OnDamagedSelfHeal",
                        Event = "damage.apply.after",
                        Condition = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Condition,
                            GroupReference = "condition_group_low_health"
                        },
                        Actions = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Action,
                            Type = "seq",
                            Children =
                            {
                                Node(TriggerNodeKind.Action, "heal",
                                    Arg("amount", ConstNumber(100)),
                                    Arg("target_source", ConstInt(4))),
                                Node(TriggerNodeKind.Action, "debug_log",
                                    Arg("message", ConstString("golden passive healed")))
                            }
                        }
                    }
                }
            };
        }

        /// <summary>把 golden 模块落成资产并登记到所选 Project（创建在 Project 资产同目录）。</summary>
        [MenuItem("Assets/AbilityKit/Trigger Authoring/Create Golden Example Modules")]
        private static void CreateGoldenAssets()
        {
            var project = Selection.activeObject as TriggerAuthoringProjectAsset;
            if (project == null) return;
            var projectPath = AssetDatabase.GetAssetPath(project);
            var directory = string.IsNullOrEmpty(projectPath)
                ? "Assets"
                : System.IO.Path.GetDirectoryName(projectPath)?.Replace('\\', '/') ?? "Assets";

            var created = new List<UnityEngine.Object>();
            var examples = BuildAll();
            for (var i = 0; i < examples.Count; i++)
            {
                var data = examples[i];
                var asset = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
                asset.name = data.ModuleId;
                asset.Module = data;
                var assetPath = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + data.ModuleId + ".asset");
                AssetDatabase.CreateAsset(asset, assetPath);
                if (project != null) TriggerAuthoringProjectMembership.Assign(asset, project);
                created.Add(asset);
            }

            AssetDatabase.SaveAssets();
            if (created.Count > 0)
            {
                Selection.activeObject = created[0];
                EditorGUIUtility.PingObject(created[0]);
            }
            Debug.Log($"[TriggerAuthoring] Created {created.Count} golden example module(s) in '{directory}'.");
        }

        [MenuItem("Assets/AbilityKit/Trigger Authoring/Create Golden Example Modules", true)]
        private static bool CanCreateGoldenAssets()
        {
            return Selection.activeObject is TriggerAuthoringProjectAsset;
        }

        private static TriggerNodeData Node(TriggerNodeKind kind, string type, params TriggerArgumentData[] arguments)
        {
            return new TriggerNodeData
            {
                Kind = kind,
                Type = type,
                Arguments = new List<TriggerArgumentData>(arguments)
            };
        }

        private static TriggerArgumentData Arg(string name, TriggerValueRefData value)
        {
            return new TriggerArgumentData { Name = name, Value = value };
        }

        private static TriggerValueRefData ConstNumber(double value)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = TriggerValueType.Number, NumberValue = value };
        }

        private static TriggerValueRefData ConstInt(long value)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = TriggerValueType.Integer, IntegerValue = value };
        }

        private static TriggerValueRefData ConstBool(bool value)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = TriggerValueType.Boolean, BooleanValue = value };
        }

        private static TriggerValueRefData ConstString(string value)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = TriggerValueType.String, StringValue = value };
        }

        private static TriggerValueRefData ConstIntegerList(params long[] values)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = TriggerValueType.IntegerList,
                IntegerListValue = new List<long>(values)
            };
        }

        private static TriggerValueRefData PayloadRef(string path, TriggerValueType type)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.Payload, Type = type, Path = path };
        }

        private static TriggerValueRefData LocalRef(string key, TriggerValueType type)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.LocalBlackboard, Type = type, Path = key };
        }

        private static TriggerValueRefData GlobalRef(string key, TriggerValueType type)
        {
            return new TriggerValueRefData { Source = TriggerValueSource.GlobalBlackboard, Type = type, Path = key };
        }
    }
}
#endif
