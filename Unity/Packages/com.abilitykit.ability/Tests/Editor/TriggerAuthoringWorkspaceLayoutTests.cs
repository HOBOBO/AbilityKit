#if UNITY_EDITOR
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Ability.Editor.Windows;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringWorkspaceLayoutTests
    {
        [TestCase(719f, 0)]
        [TestCase(820f, 1)]
        [TestCase(1499f, 1)]
        [TestCase(1500f, 2)]
        public void Resolve_UsesStableResponsiveBreakpoints(float width, int expected)
        {
            Assert.That((int)TriggerAuthoringWorkspaceLayout.Resolve(width), Is.EqualTo(expected));
        }

        [Test]
        public void PaneWidths_AreClampedAgainstWindowSize()
        {
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampNavigationWidth(50f, 1400f), Is.EqualTo(210f));
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampNavigationWidth(900f, 900f), Is.EqualTo(306f));
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampInspectorWidth(50f, 1400f), Is.EqualTo(280f));
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampInspectorWidth(900f, 900f), Is.EqualTo(342f));
        }

        [Test]
        public void TemplateSearch_MatchesDeclaredCallInput()
        {
            var template = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                template.Template = new TriggerAuthoringTemplateData
                {
                    TemplateId = "template_damage",
                    DisplayName = "通用伤害模板",
                    Parameters =
                    {
                        new TriggerAuthoringTemplateParameterData
                        {
                            Name = "damage_amount",
                            LocalVariableKey = "damage_amount",
                            Type = TriggerValueType.Number,
                            Description = "最终伤害数值"
                        }
                    }
                };

                Assert.That(TriggerAuthoringTemplateTreePanel.MatchesTemplate(template, "damage_amount"), Is.True);
                Assert.That(TriggerAuthoringTemplateTreePanel.MatchesTemplate(template, "最终伤害"), Is.True);
                Assert.That(TriggerAuthoringTemplateTreePanel.MatchesTemplate(template, "missing"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void TriggerListWidth_PreservesEnoughSpaceForDetails()
        {
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampTriggerListWidth(100f, 1100f), Is.EqualTo(260f));
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampTriggerListWidth(300f, 1100f), Is.EqualTo(300f));
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampTriggerListWidth(500f, 1100f), Is.EqualTo(420f));
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampTriggerListWidth(300f, 680f), Is.EqualTo(280f));
        }

        [Test]
        public void NodeWorkspace_SplitsOnlyWhenBothPanesRemainUseful()
        {
            Assert.That(TriggerAuthoringWorkspaceLayout.ShouldSplitNodeWorkspace(599f), Is.False);
            Assert.That(TriggerAuthoringWorkspaceLayout.ShouldSplitNodeWorkspace(600f), Is.True);
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampNodeOutlineWidth(100f, 900f), Is.EqualTo(220f));
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampNodeOutlineWidth(300f, 900f), Is.EqualTo(300f));
            Assert.That(TriggerAuthoringWorkspaceLayout.ClampNodeOutlineWidth(500f, 650f), Is.EqualTo(350f));
        }

        [Test]
        public void RuleOverview_StacksOnNarrowEditors()
        {
            Assert.That(TriggerAuthoringWorkspaceLayout.ShouldSplitRuleOverview(619f), Is.False);
            Assert.That(TriggerAuthoringWorkspaceLayout.ShouldSplitRuleOverview(620f), Is.True);
        }

        [Test]
        public void RuleOverview_GroupsSameEventAndSummarizesConditionToActions()
        {
            var module = new TriggerAuthoringModuleData();
            module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 1,
                Event = "damage.received",
                Condition = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Condition,
                    Type = "arg_gte",
                    Arguments =
                    {
                        new TriggerArgumentData
                        {
                            Name = "left",
                            Value = new TriggerValueRefData
                            {
                                Source = TriggerValueSource.Payload,
                                Type = TriggerValueType.Number,
                                Path = "amount"
                            }
                        }
                    }
                },
                Actions = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = "seq",
                    Children =
                    {
                        new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "add_shield" },
                        new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "play_presentation" },
                        new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "heal" },
                        new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "debug_log" }
                    }
                }
            });
            module.Triggers.Add(new TriggerDefinitionData { Id = 2, Event = "damage.received" });
            module.Triggers.Add(new TriggerDefinitionData { Id = 3, Event = "skill.cast" });

            var rules = TriggerAuthoringRuleOverviewBuilder.Build(
                module,
                "damage.received",
                TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                null);

            Assert.That(rules.Count, Is.EqualTo(2));
            Assert.That(rules[0].ConditionSummary, Does.Contain("参数大于或等于"));
            Assert.That(rules[0].ConditionSummary, Does.Contain("事件参数：amount"));
            Assert.That(
                rules[0].ActionSummary,
                Is.EqualTo("添加护盾 → 播放表现 → 治疗 → 输出调试日志"));
            Assert.That(rules[1].ConditionSummary, Is.EqualTo("无条件"));
            Assert.That(rules[1].ActionSummary, Is.EqualTo("未配置行为"));
        }

        [Test]
        public void RuleOverview_FollowsSelectedBusinessGroupInsteadOfAlwaysGroupingByEvent()
        {
            var module = new TriggerAuthoringModuleData();
            module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 1,
                Event = "buff.apply",
                GroupPath = "AAA配置"
            });
            module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 2,
                Event = "buff.apply",
                GroupPath = "AAA配置"
            });
            module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 3,
                Event = "buff.apply",
                GroupPath = "分组/MOBA/Skill/Cast"
            });
            module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 4,
                EntryMode = TriggerEntryMode.Callable,
                GroupPath = "AAA配置"
            });

            var types = TriggerTypeDescriptorCatalog.CreateProjectDefaults();
            var businessGroup = TriggerAuthoringRuleOverviewBuilder.BuildForGroup(
                module,
                0,
                TriggerAuthoringTriggerGroupMode.GroupPath,
                "groupPath:AAA配置",
                null,
                null,
                types,
                null);
            var eventGroup = TriggerAuthoringRuleOverviewBuilder.BuildForGroup(
                module,
                0,
                TriggerAuthoringTriggerGroupMode.Event,
                null,
                null,
                null,
                types,
                null);

            Assert.That(businessGroup.GroupLabel, Is.EqualTo("分组 / AAA配置"));
            Assert.That(businessGroup.Rules, Has.Count.EqualTo(3));
            Assert.That(businessGroup.Rules.Exists(item => item.Trigger.Id == 3), Is.False);
            Assert.That(businessGroup.Rules.Exists(item => item.Trigger.Id == 4), Is.True);
            Assert.That(eventGroup.Rules, Has.Count.EqualTo(3));
            Assert.That(eventGroup.Rules.Exists(item => item.Trigger.Id == 3), Is.True);
            Assert.That(eventGroup.Rules.Exists(item => item.Trigger.Id == 4), Is.False);
        }

        [Test]
        public void RuleOverview_UsesResolvedTemplateLogicAndInstanceBindings()
        {
            var templateAsset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                templateAsset.Template = new TriggerAuthoringTemplateData
                {
                    TemplateId = "template.damage_branch",
                    Parameters =
                    {
                        new TriggerAuthoringTemplateParameterData
                        {
                            Name = "damage_type",
                            LocalVariableKey = "damage_type",
                            Type = TriggerValueType.Integer
                        }
                    },
                    Definition = new TriggerDefinitionData
                    {
                        Event = "damage.received",
                        Condition = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Condition,
                            Type = "arg_eq",
                            Arguments =
                            {
                                new TriggerArgumentData
                                {
                                    Name = "right",
                                    Value = new TriggerValueRefData
                                    {
                                        Source = TriggerValueSource.LocalBlackboard,
                                        Type = TriggerValueType.Integer,
                                        Path = "trigger:damage_type"
                                    }
                                }
                            }
                        },
                        Actions = new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "give_damage" }
                    }
                };
                var trigger = new TriggerDefinitionData
                {
                    Id = 10,
                    Template = new TriggerTemplateReferenceData
                    {
                        TemplateId = "template.damage_branch",
                        Bindings =
                        {
                            new TriggerArgumentData
                            {
                                Name = "damage_type",
                                Value = new TriggerValueRefData
                                {
                                    Source = TriggerValueSource.Constant,
                                    Type = TriggerValueType.Integer,
                                    IntegerValue = 7
                                }
                            }
                        }
                    }
                };
                var module = new TriggerAuthoringModuleData();
                module.Triggers.Add(trigger);

                var rules = TriggerAuthoringRuleOverviewBuilder.Build(
                    module,
                    "damage.received",
                    TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                    new TriggerTemplateDescriptorCatalog(new[] { templateAsset }));

                Assert.That(rules.Count, Is.EqualTo(1));
                Assert.That(rules[0].SourceLabel, Is.EqualTo("模板"));
                Assert.That(rules[0].ConditionSummary, Does.Contain("右值=7"));
                Assert.That(rules[0].ActionSummary, Is.EqualTo("造成伤害"));
            }
            finally
            {
                Object.DestroyImmediate(templateAsset);
            }
        }

        [Test]
        public void RuleOverview_ShowsEmbeddedConditionThenAndElseBranches()
        {
            var branch = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "conditional",
                Condition = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Condition,
                    Type = "arg_gte",
                    Arguments =
                    {
                        new TriggerArgumentData
                        {
                            Name = "left",
                            Value = new TriggerValueRefData
                            {
                                Source = TriggerValueSource.Payload,
                                Type = TriggerValueType.Integer,
                                Path = "stack_count"
                            }
                        },
                        new TriggerArgumentData
                        {
                            Name = "right",
                            Value = new TriggerValueRefData
                            {
                                Source = TriggerValueSource.Constant,
                                Type = TriggerValueType.Integer,
                                IntegerValue = 3
                            }
                        }
                    }
                },
                Children =
                {
                    new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "give_damage" },
                    new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "play_presentation" }
                },
                ElseChildren =
                {
                    new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "debug_log" }
                }
            };
            var module = new TriggerAuthoringModuleData();
            module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 20,
                Event = "buff.apply",
                Actions = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = "seq",
                    Children = { branch }
                }
            });

            var rules = TriggerAuthoringRuleOverviewBuilder.Build(
                module,
                "buff.apply",
                TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                null);

            Assert.That(rules, Has.Count.EqualTo(1));
            Assert.That(rules[0].ActionSummary, Does.Contain("如果[参数大于或等于"));
            Assert.That(rules[0].ActionSummary, Does.Contain("事件参数：stack_count"));
            Assert.That(rules[0].ActionSummary, Does.Contain("造成伤害 → 播放表现"));
            Assert.That(rules[0].ActionSummary, Does.Contain("否则：输出调试日志"));
        }

        [Test]
        public void GroupResolver_ClonesEmbeddedActionFlowWithoutAliasing()
        {
            var source = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "conditional",
                Condition = new TriggerNodeData { Kind = TriggerNodeKind.Condition, Type = "always_true" },
                Children = { new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "heal" } },
                ElseChildren = { new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "debug_log" } }
            };

            var clone = TriggerAuthoringGroupResolver.CloneNode(source);

            Assert.That(clone.Condition.Type, Is.EqualTo("always_true"));
            Assert.That(clone.Children[0].Type, Is.EqualTo("heal"));
            Assert.That(clone.ElseChildren[0].Type, Is.EqualTo("debug_log"));
            Assert.That(clone.Condition, Is.Not.SameAs(source.Condition));
            Assert.That(clone.ElseChildren, Is.Not.SameAs(source.ElseChildren));
        }

        [Test]
        public void GroupOperations_ExtractAndLocalizePreservePrivateTreeSemantics()
        {
            var module = new TriggerAuthoringModuleData();
            var node = new TriggerNodeData
            {
                Enabled = false,
                Kind = TriggerNodeKind.Action,
                Type = "seq",
                Children =
                {
                    new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "heal" },
                    new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Action,
                        Type = "conditional",
                        Condition = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Condition,
                            Type = "always_true"
                        },
                        Children =
                        {
                            new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "debug_log" }
                        }
                    }
                }
            };

            var extracted = TriggerAuthoringGroupResolver.TryExtract(
                module,
                node,
                TriggerNodeKind.Action,
                "shared.sequence",
                "共享执行序列",
                out var group,
                out var extractError);

            Assert.That(extracted, Is.True, extractError);
            Assert.That(module.ActionGroups, Has.Count.EqualTo(1));
            Assert.That(group.Root.Enabled, Is.True, "公共分组不应继承调用位置的停用状态");
            Assert.That(group.Root.Children, Has.Count.EqualTo(2));
            Assert.That(node.GroupReference, Is.EqualTo("shared.sequence"));
            Assert.That(node.Enabled, Is.False);
            Assert.That(node.Children, Is.Empty);

            var localized = TriggerAuthoringGroupResolver.TryLocalize(
                module,
                node,
                TriggerNodeKind.Action,
                out var localizeFailure);

            Assert.That(localized, Is.True, localizeFailure?.Message);
            Assert.That(node.GroupReference, Is.Null.Or.Empty);
            Assert.That(node.Type, Is.EqualTo("seq"));
            Assert.That(node.Enabled, Is.False);
            Assert.That(node.Children, Has.Count.EqualTo(2));
            Assert.That(node.Children[1].Condition.Type, Is.EqualTo("always_true"));
            Assert.That(node.Children, Is.Not.SameAs(group.Root.Children));
            Assert.That(node.Children[1].Condition, Is.Not.SameAs(group.Root.Children[1].Condition));
        }

        [Test]
        public void GroupOperations_RejectDuplicateIdWithoutChangingNode()
        {
            var module = new TriggerAuthoringModuleData();
            module.ConditionGroups.Add(new TriggerNodeGroupData { Id = "shared.condition" });
            var node = new TriggerNodeData { Kind = TriggerNodeKind.Condition, Type = "always_true" };

            var extracted = TriggerAuthoringGroupResolver.TryExtract(
                module,
                node,
                TriggerNodeKind.Condition,
                "shared.condition",
                "重复条件",
                out var group,
                out var error);

            Assert.That(extracted, Is.False);
            Assert.That(group, Is.Null);
            Assert.That(error, Does.Contain("已存在"));
            Assert.That(module.ConditionGroups, Has.Count.EqualTo(1));
            Assert.That(node.Type, Is.EqualTo("always_true"));
            Assert.That(node.GroupReference, Is.Null.Or.Empty);
        }

        [Test]
        public void GroupResolver_ExpandsConditionGroupInsideActionFlow()
        {
            var module = new TriggerAuthoringModuleData();
            module.ConditionGroups.Add(new TriggerNodeGroupData
            {
                Id = "shared.condition",
                Root = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Condition,
                    Type = "arg_eq"
                }
            });
            module.ActionGroups.Add(new TriggerNodeGroupData
            {
                Id = "shared.flow",
                Root = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = "conditional",
                    Condition = new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Condition,
                        GroupReference = "shared.condition"
                    },
                    Children =
                    {
                        new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "heal" }
                    }
                }
            });
            var reference = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                GroupReference = "shared.flow"
            };

            var success = TriggerAuthoringGroupResolver.TryExpand(
                module,
                reference,
                TriggerNodeKind.Action,
                out var expanded,
                out var failure);

            Assert.That(success, Is.True, failure?.Message);
            Assert.That(expanded.Type, Is.EqualTo("conditional"));
            Assert.That(expanded.Condition.GroupReference, Is.Null.Or.Empty);
            Assert.That(expanded.Condition.Type, Is.EqualTo("arg_eq"));
        }

        [Test]
        public void TriggerReuse_ExtractCreatesVisibleCallableResourceAndReplacesSourceNode()
        {
            var sourceNode = new TriggerNodeData
            {
                Enabled = false,
                Kind = TriggerNodeKind.Action,
                Type = "seq",
                Children =
                {
                    new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "heal" },
                    new TriggerNodeData { Kind = TriggerNodeKind.Action, Type = "debug_log" }
                }
            };
            var sourceTrigger = new TriggerDefinitionData
            {
                Id = 10,
                Name = "受击处理",
                GroupPath = "战斗/受击",
                Scope = "owner",
                Actions = sourceNode
            };
            var module = new TriggerAuthoringModuleData();
            module.Triggers.Add(sourceTrigger);

            var success = TriggerAuthoringTriggerReuse.TryExtract(
                module,
                sourceTrigger,
                sourceNode,
                11,
                "通用受击效果",
                out var extracted,
                out var error);

            Assert.That(success, Is.True, error);
            Assert.That(module.Triggers, Has.Count.EqualTo(2));
            Assert.That(module.Triggers[1], Is.SameAs(extracted));
            Assert.That(extracted.Id, Is.EqualTo(11));
            Assert.That(extracted.EntryMode, Is.EqualTo(TriggerEntryMode.Callable));
            Assert.That(extracted.Actions.Type, Is.EqualTo("seq"));
            Assert.That(extracted.Actions.Enabled, Is.True);
            Assert.That(extracted.Actions, Is.Not.SameAs(sourceNode));
            Assert.That(sourceNode.Type, Is.EqualTo(TriggerAuthoringTriggerReuse.ExecuteTriggerType));
            Assert.That(sourceNode.Enabled, Is.False);
            Assert.That(TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(sourceNode, out var id), Is.True);
            Assert.That(id, Is.EqualTo(11));

            var index = TriggerAuthoringTriggerIndex.Build(
                module.Triggers,
                null,
                null,
                TriggerAuthoringTriggerGroupMode.Event,
                string.Empty);
            Assert.That(index.Exists(group => group.Key == "event:<callable>"), Is.True);
        }

        [Test]
        public void TriggerReuse_LocalizeRestoresConditionAndEditableTreeWithoutAliasing()
        {
            var targetActions = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "seq",
                Children =
                {
                    new TriggerNodeData { Enabled = false, Kind = TriggerNodeKind.Action, Type = "heal" }
                }
            };
            var target = new TriggerDefinitionData
            {
                Id = 20,
                EntryMode = TriggerEntryMode.Callable,
                Condition = new TriggerNodeData { Kind = TriggerNodeKind.Condition, Type = "always_true" },
                Actions = targetActions
            };
            var reference = new TriggerNodeData
            {
                Enabled = false,
                Kind = TriggerNodeKind.Action,
                Type = TriggerAuthoringTriggerReuse.ExecuteTriggerType,
                Arguments =
                {
                    new TriggerArgumentData
                    {
                        Name = TriggerAuthoringTriggerReuse.TriggerIdArgument,
                        Value = new TriggerValueRefData
                        {
                            Source = TriggerValueSource.Constant,
                            Type = TriggerValueType.Integer,
                            IntegerValue = 20
                        }
                    }
                }
            };
            var module = new TriggerAuthoringModuleData();
            module.Triggers.Add(target);

            var success = TriggerAuthoringTriggerReuse.TryLocalize(
                module,
                reference,
                out var resolved,
                out var error);

            Assert.That(success, Is.True, error);
            Assert.That(resolved, Is.SameAs(target));
            Assert.That(reference.Type, Is.EqualTo("conditional"));
            Assert.That(reference.Enabled, Is.False);
            Assert.That(reference.Condition.Type, Is.EqualTo("always_true"));
            Assert.That(reference.Children, Has.Count.EqualTo(1));
            Assert.That(reference.Children[0].Type, Is.EqualTo("seq"));
            Assert.That(reference.Children[0].Children[0].Enabled, Is.False);
            Assert.That(reference.Condition, Is.Not.SameAs(target.Condition));
            Assert.That(reference.Children[0], Is.Not.SameAs(targetActions));
        }

        [Test]
        public void TriggerReuse_LocalizeTemplateMaterializesInstanceBindings()
        {
            var templateAsset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                templateAsset.Template = new TriggerAuthoringTemplateData
                {
                    TemplateId = "template.shared.damage",
                    Parameters =
                    {
                        new TriggerAuthoringTemplateParameterData
                        {
                            Name = "amount",
                            LocalVariableKey = "amount",
                            Type = TriggerValueType.Integer,
                            Required = true
                        }
                    },
                    Definition = new TriggerDefinitionData
                    {
                        EntryMode = TriggerEntryMode.Callable,
                        Actions = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Action,
                            Type = "give_damage",
                            Arguments =
                            {
                                new TriggerArgumentData
                                {
                                    Name = "amount",
                                    Value = new TriggerValueRefData
                                    {
                                        Source = TriggerValueSource.LocalBlackboard,
                                        Type = TriggerValueType.Integer,
                                        Path = "trigger:amount"
                                    }
                                }
                            }
                        }
                    }
                };
                var target = new TriggerDefinitionData
                {
                    Id = 30,
                    EntryMode = TriggerEntryMode.Callable,
                    Template = new TriggerTemplateReferenceData
                    {
                        TemplateId = "template.shared.damage",
                        Bindings =
                        {
                            new TriggerArgumentData
                            {
                                Name = "amount",
                                Value = new TriggerValueRefData
                                {
                                    Source = TriggerValueSource.Constant,
                                    Type = TriggerValueType.Integer,
                                    IntegerValue = 125
                                }
                            }
                        }
                    }
                };
                var reference = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = TriggerAuthoringTriggerReuse.ExecuteTriggerType,
                    Arguments =
                    {
                        new TriggerArgumentData
                        {
                            Name = TriggerAuthoringTriggerReuse.TriggerIdArgument,
                            Value = new TriggerValueRefData
                            {
                                Source = TriggerValueSource.Constant,
                                Type = TriggerValueType.Integer,
                                IntegerValue = 30
                            }
                        }
                    }
                };
                var module = new TriggerAuthoringModuleData();
                module.Triggers.Add(target);
                var templates = new TriggerTemplateDescriptorCatalog(new[] { templateAsset });

                var success = TriggerAuthoringTriggerReuse.TryLocalize(
                    module,
                    reference,
                    templates,
                    out _,
                    out var error);

                Assert.That(success, Is.True, error);
                Assert.That(reference.Type, Is.EqualTo("give_damage"));
                Assert.That(reference.Arguments[0].Value.Source, Is.EqualTo(TriggerValueSource.Constant));
                Assert.That(reference.Arguments[0].Value.IntegerValue, Is.EqualTo(125));
                Assert.That(reference.Arguments[0].Value, Is.Not.SameAs(target.Template.Bindings[0].Value));
            }
            finally
            {
                Object.DestroyImmediate(templateAsset);
            }
        }

        [Test]
        public void TriggerReuse_LocalizeTemplateImportsInternalLocalVarsWithoutRewritingInputBindings()
        {
            var templateAsset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                templateAsset.Template = new TriggerAuthoringTemplateData
                {
                    TemplateId = "template.shared.locals",
                    Parameters =
                    {
                        new TriggerAuthoringTemplateParameterData
                        {
                            Name = "amount",
                            LocalVariableKey = "amount",
                            Type = TriggerValueType.Integer,
                            Required = true
                        }
                    },
                    Definition = new TriggerDefinitionData
                    {
                        EntryMode = TriggerEntryMode.Callable,
                        Blackboard =
                        {
                            new TriggerBlackboardVariableData
                            {
                                Key = "scratch",
                                Type = TriggerValueType.Integer,
                                DefaultValue = new TriggerValueRefData
                                {
                                    Source = TriggerValueSource.Constant,
                                    Type = TriggerValueType.Integer
                                }
                            }
                        },
                        Actions = new TriggerNodeData
                        {
                            Kind = TriggerNodeKind.Action,
                            Type = "seq",
                            Children =
                            {
                                new TriggerNodeData
                                {
                                    Kind = TriggerNodeKind.Action,
                                    Type = "use_input",
                                    Arguments =
                                    {
                                        new TriggerArgumentData
                                        {
                                            Name = "value",
                                            Value = new TriggerValueRefData
                                            {
                                                Source = TriggerValueSource.LocalBlackboard,
                                                Type = TriggerValueType.Integer,
                                                Path = "trigger:amount"
                                            }
                                        }
                                    }
                                },
                                new TriggerNodeData
                                {
                                    Kind = TriggerNodeKind.Action,
                                    Type = "use_scratch",
                                    Arguments =
                                    {
                                        new TriggerArgumentData
                                        {
                                            Name = "value",
                                            Value = new TriggerValueRefData
                                            {
                                                Source = TriggerValueSource.LocalBlackboard,
                                                Type = TriggerValueType.Integer,
                                                Path = "trigger:scratch"
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                var target = new TriggerDefinitionData
                {
                    Id = 30,
                    EntryMode = TriggerEntryMode.Callable,
                    Template = new TriggerTemplateReferenceData
                    {
                        TemplateId = "template.shared.locals",
                        Bindings =
                        {
                            new TriggerArgumentData
                            {
                                Name = "amount",
                                Value = new TriggerValueRefData
                                {
                                    Source = TriggerValueSource.LocalBlackboard,
                                    Type = TriggerValueType.Integer,
                                    Path = "module:scratch"
                                }
                            }
                        }
                    }
                };
                var reference = CreateTriggerIdReference(30);
                var owner = new TriggerDefinitionData
                {
                    Id = 31,
                    EntryMode = TriggerEntryMode.Callable,
                    Blackboard =
                    {
                        new TriggerBlackboardVariableData
                        {
                            Key = "scratch",
                            Type = TriggerValueType.Integer,
                            DefaultValue = new TriggerValueRefData
                            {
                                Source = TriggerValueSource.Constant,
                                Type = TriggerValueType.Integer
                            }
                        }
                    },
                    Actions = reference
                };
                var module = new TriggerAuthoringModuleData();
                module.Blackboard.Add(new TriggerBlackboardVariableData
                {
                    Key = "scratch",
                    Type = TriggerValueType.Integer,
                    DefaultValue = new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Integer
                    }
                });
                module.Triggers.Add(target);
                module.Triggers.Add(owner);
                var templates = new TriggerTemplateDescriptorCatalog(new[] { templateAsset });

                var success = TriggerAuthoringTriggerReuse.TryLocalize(
                    module,
                    reference,
                    templates,
                    out _,
                    out var error);

                Assert.That(success, Is.True, error);
                Assert.That(owner.Blackboard, Has.Count.EqualTo(2));
                Assert.That(owner.Blackboard[1].Key, Is.EqualTo("trigger_30_scratch"));
                Assert.That(reference.Children[0].Arguments[0].Value.Path, Is.EqualTo("module:scratch"));
                Assert.That(reference.Children[1].Arguments[0].Value.Path,
                    Is.EqualTo("trigger:trigger_30_scratch"));
            }
            finally
            {
                Object.DestroyImmediate(templateAsset);
            }
        }

        [Test]
        public void TriggerIndex_BusinessGroupsAreIndependentFromEventsAndNormalizePaths()
        {
            var trigger = new TriggerDefinitionData
            {
                Id = 40,
                EntryMode = TriggerEntryMode.Event,
                Event = "buff.apply",
                GroupPath = " 战斗\\受击//护盾 "
            };
            trigger.GroupPath = TriggerAuthoringTriggerBatchOperations.NormalizeGroupPath(trigger.GroupPath);

            var groups = TriggerAuthoringTriggerIndex.Build(
                new[] { trigger },
                null,
                null,
                TriggerAuthoringTriggerGroupMode.GroupPath,
                string.Empty);

            Assert.That(trigger.GroupPath, Is.EqualTo("战斗/受击/护盾"));
            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].Key, Is.EqualTo("groupPath:战斗/受击/护盾"));
        }

        [Test]
        public void TriggerIndex_NoEventFilterDoesNotTreatCallableEffectsAsMissingEvents()
        {
            var callable = new TriggerDefinitionData
            {
                Id = 50,
                EntryMode = TriggerEntryMode.Callable,
                Event = string.Empty
            };
            var eventRule = new TriggerDefinitionData
            {
                Id = 51,
                EntryMode = TriggerEntryMode.Event,
                Event = string.Empty
            };

            var groups = TriggerAuthoringTriggerIndex.Build(
                new[] { callable, eventRule },
                null,
                null,
                TriggerAuthoringTriggerGroupMode.Flat,
                string.Empty,
                TriggerAuthoringTriggerQuickFilter.NoEvent);

            Assert.That(groups, Has.Count.EqualTo(1));
            Assert.That(groups[0].Entries, Has.Count.EqualTo(1));
            Assert.That(groups[0].Entries[0].Trigger, Is.SameAs(eventRule));
        }

        [Test]
        public void TriggerIdRefactor_UsesProjectWideAllocationAndUpdatesManagedReferences()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            var sourceAsset = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            var consumerAsset = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            try
            {
                var target = new TriggerDefinitionData { Id = 100, Name = "公共伤害" };
                sourceAsset.Module = new TriggerAuthoringModuleData { ModuleId = "source" };
                sourceAsset.Module.Triggers.Add(target);
                sourceAsset.Module.ActionGroups.Add(new TriggerNodeGroupData
                {
                    Id = "shared.flow",
                    Root = CreateTriggerIdReference(100)
                });

                consumerAsset.Module = new TriggerAuthoringModuleData { ModuleId = "consumer" };
                consumerAsset.Module.Triggers.Add(new TriggerDefinitionData
                {
                    Id = 250,
                    Name = "调用方",
                    Actions = new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Action,
                        Type = "seq",
                        Children = { CreateTriggerIdReference(100) }
                    }
                });
                project.SetModules(new[] { sourceAsset, consumerAsset });
                sourceAsset.SetProject(project);
                consumerAsset.SetProject(project);

                Assert.That(TriggerAuthoringTriggerIdRefactor.NextAvailableId(sourceAsset), Is.EqualTo(251));

                var collision = TriggerAuthoringTriggerIdRefactor.BuildPlan(sourceAsset, target, 250);
                Assert.That(collision.IsValid, Is.False);
                Assert.That(collision.Error, Does.Contain("已被模块"));

                var plan = TriggerAuthoringTriggerIdRefactor.BuildPlan(sourceAsset, target, 300);
                Assert.That(plan.IsValid, Is.True, plan.Error);
                Assert.That(plan.References, Has.Count.EqualTo(2));
                Assert.That(plan.AffectedModules, Has.Count.EqualTo(2));

                var changed = plan.Apply();

                Assert.That(changed, Is.EqualTo(2));
                Assert.That(target.Id, Is.EqualTo(300));
                Assert.That(
                    TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(
                        sourceAsset.Module.ActionGroups[0].Root,
                        out var groupReferenceId),
                    Is.True);
                Assert.That(groupReferenceId, Is.EqualTo(300));
                Assert.That(
                    TriggerAuthoringTriggerReuse.TryGetReferencedTriggerId(
                        consumerAsset.Module.Triggers[0].Actions.Children[0],
                        out var consumerReferenceId),
                    Is.True);
                Assert.That(consumerReferenceId, Is.EqualTo(300));
            }
            finally
            {
                Object.DestroyImmediate(sourceAsset);
                Object.DestroyImmediate(consumerAsset);
                Object.DestroyImmediate(project);
            }
        }

        [Test]
        public void EditorLabels_UseChineseWithoutChangingAuthoringValues()
        {
            Assert.That(TriggerAuthoringEditorLabels.Source(TriggerValueSource.Payload), Is.EqualTo("事件参数"));
            Assert.That(TriggerAuthoringEditorLabels.Source(TriggerValueSource.GlobalBlackboard), Is.EqualTo("全局黑板"));
            Assert.That(TriggerAuthoringEditorLabels.ValueType(TriggerValueType.Number), Is.EqualTo("数值"));
            Assert.That(TriggerAuthoringEditorLabels.Node("any", "Any"), Is.EqualTo("任一满足"));
            Assert.That(TriggerAuthoringEditorLabels.Node("arg_lt", "Less Than"), Is.EqualTo("参数小于"));
            Assert.That(TriggerAuthoringEditorLabels.Parameter("left"), Is.EqualTo("左值"));
        }

        [Test]
        public void ModuleSearch_MatchesIdentityAndKind()
        {
            var asset = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            try
            {
                asset.name = "SkillTriggers";
                asset.Module = new TriggerAuthoringModuleData
                {
                    ModuleId = "moba.skill.hero",
                    DisplayName = "Hero Skills",
                    Kind = TriggerModuleKind.Ability
                };

                Assert.That(TriggerAuthoringProjectTreePanel.MatchesModule(asset, "hero skills"), Is.True);
                Assert.That(TriggerAuthoringProjectTreePanel.MatchesModule(asset, "moba.skill"), Is.True);
                Assert.That(TriggerAuthoringProjectTreePanel.MatchesModule(asset, "ability"), Is.True);
                Assert.That(TriggerAuthoringProjectTreePanel.MatchesModule(asset, "buff"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        private static TriggerNodeData CreateTriggerIdReference(int triggerId)
        {
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = TriggerAuthoringTriggerReuse.ExecuteTriggerType,
                Arguments =
                {
                    new TriggerArgumentData
                    {
                        Name = TriggerAuthoringTriggerReuse.TriggerIdArgument,
                        Value = new TriggerValueRefData
                        {
                            Source = TriggerValueSource.Constant,
                            Type = TriggerValueType.Integer,
                            IntegerValue = triggerId
                        }
                    }
                }
            };
        }
    }
}
#endif
