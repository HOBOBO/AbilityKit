#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using NUnit.Framework;
using UnityEngine;
using RuntimeStableStringId = AbilityKit.Triggering.Eventing.StableStringId;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringRuntimeExporterTests
    {
        [Test]
        public void Build_DebugLog_ProducesLoadableGoldenRuntimeJson()
        {
            var module = CreateModule(DebugLog("cast"));

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.True, result.BuildMessage());
            Assert.That(result.ExportedTriggerCount, Is.EqualTo(1));
            var json = TriggerAuthoringRuntimeExporter.Serialize(result.Database);
            var stringId = RuntimeStableStringId.Get("str:cast");
            var expected = "{\n" +
                           "  \"FormatVersion\": 1,\n" +
                           "  \"Triggers\": [\n" +
                           "    {\n" +
                           "      \"TriggerId\": 1001,\n" +
                           "      \"EventName\": \"skill.cast\",\n" +
                           $"      \"EventId\": {RuntimeStableStringId.Get("event:skill.cast")},\n" +
                           "      \"AllowExternal\": false,\n" +
                           "      \"Phase\": 0,\n" +
                           "      \"Priority\": 0,\n" +
                           "      \"Scope\": 1,\n" +
                           "      \"Predicate\": {\n" +
                           "        \"Kind\": \"none\"\n" +
                           "      },\n" +
                           "      \"Actions\": [\n" +
                           "        {\n" +
                           $"          \"ActionId\": {RuntimeStableStringId.Get("action:debug_log")},\n" +
                           "          \"Arity\": 1,\n" +
                           "          \"Args\": {\n" +
                           "            \"message\": {\n" +
                           "              \"Kind\": \"Const\",\n" +
                           $"              \"ConstValue\": {stringId}.0,\n" +
                           "              \"BoardId\": 0,\n" +
                           "              \"KeyId\": 0,\n" +
                           "              \"FieldId\": 0,\n" +
                           "              \"HasScale\": false,\n" +
                           "              \"Scale\": 1.0\n" +
                           "            }\n" +
                           "          }\n" +
                           "        }\n" +
                           "      ]\n" +
                           "    }\n" +
                           "  ],\n" +
                           "  \"Strings\": {\n" +
                           $"    \"{stringId}\": \"cast\"\n" +
                           "  }\n" +
                           "}\n";
            Assert.That(NormalizeLineEndings(json), Is.EqualTo(expected));

            var database = new TriggerPlanJsonDatabase();
            Assert.DoesNotThrow(() => database.LoadFromJson(json, "authoring-test"));
            Assert.That(database.Records.Count, Is.EqualTo(1));
        }

        [Test]
        public void Build_CompilesPostfixConditionsAndNestedActionGroups()
        {
            var module = CreateModule(new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                GroupReference = "outer"
            });
            module.ActionGroups.Add(new TriggerNodeGroupData { Id = "log", Root = DebugLog("group") });
            module.ActionGroups.Add(new TriggerNodeGroupData
            {
                Id = "outer",
                Root = new TriggerNodeData
                {
                    Kind = TriggerNodeKind.Action,
                    Type = "seq",
                    Children =
                    {
                        new TriggerNodeData { Kind = TriggerNodeKind.Action, GroupReference = "log" },
                        DebugLog("tail")
                    }
                }
            });
            module.Triggers[0].Condition = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = "all",
                Children =
                {
                    Compare("arg_gt", 5, 2),
                    new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Condition,
                        Type = "not",
                        Children = { new TriggerNodeData { Kind = TriggerNodeKind.Condition, Type = "always_false" } }
                    }
                }
            };

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.True, result.BuildMessage());
            var trigger = result.Database.Triggers[0];
            Assert.That(trigger.Actions.Count, Is.EqualTo(2));
            CollectionAssert.AreEqual(
                new[] { "CompareNumeric", "Const", "Not", "And" },
                trigger.Predicate.Nodes.ConvertAll(node => node.Kind));
        }

        [Test]
        public void Build_CompilesRuntimeValueReferencesAndIndexedIntegerLists()
        {
            var module = CreateModule(new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "add_buff",
                Arguments =
                {
                    Arg("buff_ids", new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.IntegerList,
                        IntegerListValue = new List<long> { 11, 22 }
                    }),
                    Arg("target_actor_id", Ref(TriggerValueSource.Payload, TriggerValueType.Integer, "target_actor_id")),
                    Arg("target_query_id", Ref(TriggerValueSource.Context, TriggerValueType.Integer, "query.id")),
                    Arg("target_filter_param", Ref(TriggerValueSource.Context, TriggerValueType.Integer, "filter.param")),
                    Arg("target_radius", new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Expression,
                        Type = TriggerValueType.Number,
                        Expression = "payload.radius * 2"
                    })
                }
            });
            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.True, result.BuildMessage());
            var action = result.Database.Triggers[0].Actions[0];
            var args = action.Args;
            Assert.That(action.Arity, Is.EqualTo(2));
            Assert.That(args["buff_ids0"].ConstValue, Is.EqualTo(11));
            Assert.That(args["buff_ids1"].ConstValue, Is.EqualTo(22));
            Assert.That(args["target_actor_id"].Kind, Is.EqualTo("PayloadField"));
            Assert.That(args["target_query_id"].Kind, Is.EqualTo("Var"));
            Assert.That(args["target_filter_param"].Kind, Is.EqualTo("Var"));
            Assert.That(args["target_radius"].Kind, Is.EqualTo("Expr"));
        }

        [Test]
        public void Build_CompilesModuleAndTriggerLocalBlackboardsForOwnerScope()
        {
            var module = CreateModule(DebugLog("local"));
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "module.count",
                Type = TriggerValueType.Integer,
                DefaultValue = ConstInt(2)
            });
            module.Triggers[0].Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "trigger.count",
                Type = TriggerValueType.Integer,
                DefaultValue = ConstInt(5)
            });
            module.Triggers[0].Condition = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = "all",
                Children =
                {
                    new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Condition,
                        Type = "arg_eq",
                        Arguments =
                        {
                            Arg("left", Ref(TriggerValueSource.LocalBlackboard, TriggerValueType.Integer, "module.count")),
                            Arg("right", ConstInt(2))
                        }
                    },
                    new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Condition,
                        Type = "arg_eq",
                        Arguments =
                        {
                            Arg("left", Ref(TriggerValueSource.LocalBlackboard, TriggerValueType.Integer, "trigger.count")),
                            Arg("right", ConstInt(5))
                        }
                    }
                }
            };

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.True, result.BuildMessage());
            var nodes = result.Database.Triggers[0].Predicate.Nodes;
            Assert.That(nodes[0].Left.BoardId, Is.EqualTo(BlackboardIdMapper.BoardId("local.module:skill.fireball")));
            Assert.That(nodes[1].Left.BoardId, Is.EqualTo(BlackboardIdMapper.BoardId("local.trigger:skill.fireball:1001")));
            Assert.That(result.Database.Blackboards, Has.Count.EqualTo(2));
            Assert.That(result.Database.Blackboards.TrueForAll(board => board.Scope == BlackboardInitializationScopes.Owner), Is.True);
        }

        [Test]
        public void Build_RejectsLocalBlackboardForGlobalTrigger()
        {
            var module = CreateModule(DebugLog("blocked"));
            module.Triggers[0].Scope = "global";
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "query.id",
                Type = TriggerValueType.Integer,
                DefaultValue = ConstInt(0)
            });
            module.Triggers[0].Condition = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = "arg_eq",
                Arguments =
                {
                    Arg("left", Ref(TriggerValueSource.LocalBlackboard, TriggerValueType.Integer, "query.id")),
                    Arg("right", ConstInt(1))
                }
            };

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Database, Is.Null);
            Assert.That(result.Diagnostics.Exists(d => d.Code == "TRG2057"), Is.True, result.BuildMessage());
        }

        [Test]
        public void Build_CompilesAndInitializesGlobalBlackboard()
        {
            var module = CreateModule(DebugLog("blocked"));
            module.Triggers[0].Condition = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = "arg_eq",
                Arguments =
                {
                    Arg("left", Ref(TriggerValueSource.GlobalBlackboard, TriggerValueType.Integer, "skill.hitCount")),
                    Arg("right", ConstInt(1))
                }
            };

            var globalKey = new TriggerGlobalBlackboardKeyData
            {
                Key = "skill.hitCount",
                Domain = "skill",
                Type = TriggerValueType.Integer,
                DefaultValue = new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.Integer,
                    IntegerValue = 3
                }
            };
            var context = new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                GlobalBlackboard = new TriggerGlobalBlackboardDescriptorCatalog(new[] { globalKey })
            };

            var result = TriggerAuthoringRuntimeExporter.Build(module, context);

            Assert.That(result.Success, Is.True, result.BuildMessage());
            var value = result.Database.Triggers[0].Predicate.Nodes[0].Left;
            Assert.That(value.Kind, Is.EqualTo("Blackboard"));
            Assert.That(value.BoardId, Is.EqualTo(BlackboardIdMapper.BoardId("skill")));
            Assert.That(value.KeyId, Is.EqualTo(BlackboardIdMapper.KeyId("skill.hitCount")));
            Assert.That(result.Database.Blackboards, Has.Count.EqualTo(1));

            var database = new TriggerPlanJsonDatabase();
            database.LoadFromJson(TriggerAuthoringRuntimeExporter.Serialize(result.Database), "global-blackboard-test");
            var resolver = new DictionaryBlackboardResolver();
            database.InitializeBlackboards(resolver);
            Assert.That(resolver.TryResolve(value.BoardId, out var board), Is.True);
            Assert.That(board.TryGetInt(value.KeyId, out var initialValue), Is.True);
            Assert.That(initialValue, Is.EqualTo(3));
        }

        [Test]
        public void Build_ExpandsTemplateTreeAndAppliesInstanceBinding()
        {
            var templateAsset = ScriptableObject.CreateInstance<TriggerAuthoringTemplateAsset>();
            try
            {
                templateAsset.Template = new TriggerAuthoringTemplateData
                {
                    TemplateId = "template.log",
                    TemplateVersion = "1.0.0",
                    Event = "skill.cast",
                    Parameters =
                    {
                        new TriggerAuthoringTemplateParameterData
                        {
                            Name = "message",
                            Type = TriggerValueType.String,
                            Required = true,
                            HasDefault = true,
                            AllowedSources = TriggerTemplateValueSourceMask.Constant,
                            DefaultValue = new TriggerValueRefData
                            {
                                Source = TriggerValueSource.Constant,
                                Type = TriggerValueType.String,
                                StringValue = "default"
                            }
                        }
                    },
                    Actions = new TriggerNodeData
                    {
                        Kind = TriggerNodeKind.Action,
                        Type = "debug_log",
                        Arguments =
                        {
                            Arg("message", new TriggerValueRefData
                            {
                                Source = TriggerValueSource.TemplateParameter,
                                Type = TriggerValueType.String,
                                Path = "message"
                            })
                        }
                    }
                };
                var module = CreateModule(null);
                module.Triggers[0].Template = new TriggerTemplateReferenceData
                {
                    TemplateId = "template.log",
                    Version = "1.0.0",
                    Bindings =
                    {
                        Arg("message", new TriggerValueRefData
                        {
                            Source = TriggerValueSource.Constant,
                            Type = TriggerValueType.String,
                            StringValue = "override"
                        })
                    }
                };
                var context = new TriggerAuthoringValidationContext
                {
                    Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                    Templates = new TriggerTemplateDescriptorCatalog(new[] { templateAsset })
                };

                var result = TriggerAuthoringRuntimeExporter.Build(module, context);

                Assert.That(result.Success, Is.True, result.BuildMessage());
                var trigger = result.Database.Triggers[0];
                Assert.That(trigger.Actions.Count, Is.EqualTo(1));
                Assert.That(trigger.Actions[0].Args["message"].Kind, Is.EqualTo("TemplateParam"));
                Assert.That(trigger.Template.TemplateId, Is.EqualTo("template.log"));
                var overrideId = RuntimeStableStringId.Get("str:override");
                Assert.That(trigger.Template.Bindings["message"].ConstValue, Is.EqualTo(overrideId));
                Assert.That(result.Database.Strings[overrideId], Is.EqualTo("override"));
                Assert.That(result.Database.Strings.ContainsValue("default"), Is.False);

                var json = TriggerAuthoringRuntimeExporter.Serialize(result.Database);
                var database = new TriggerPlanJsonDatabase();
                Assert.DoesNotThrow(() => database.LoadFromJson(json, "template-authoring-test"));
                Assert.That(database.Records.Count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(templateAsset);
            }
        }

        [Test]
        public void Build_SkipsDisabledTriggersAndIsDeterministic()
        {
            var module = CreateModule(DebugLog("enabled"));
            module.Triggers.Add(new TriggerDefinitionData
            {
                Id = 1002,
                Enabled = false,
                Event = "skill.cast",
                Actions = DebugLog("disabled")
            });

            var first = TriggerAuthoringRuntimeExporter.Build(module);
            var second = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(first.Success, Is.True, first.BuildMessage());
            Assert.That(first.ExportedTriggerCount, Is.EqualTo(1));
            Assert.That(first.SkippedDisabledCount, Is.EqualTo(1));
            Assert.That(TriggerAuthoringRuntimeExporter.Serialize(first.Database),
                Is.EqualTo(TriggerAuthoringRuntimeExporter.Serialize(second.Database)));
        }

        [Test]
        public void Build_RejectsUnsupportedRuntimeFieldsWithoutPartialOutput()
        {
            var module = CreateModule(DebugLog("blocked"));
            module.Triggers[0].Schedule.Mode = "periodic";
            module.Triggers[0].Schedule.IntervalMilliseconds = 100;

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Database, Is.Null);
            Assert.That(result.Diagnostics.Exists(d => d.Code == "TRG2011"), Is.True, result.BuildMessage());
        }

        [Test]
        public void Build_RejectsUnsupportedValueKindWithoutPartialOutput()
        {
            var module = CreateModule(DebugLog("blocked"));
            module.Triggers[0].Condition = new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = "arg_eq",
                Arguments =
                {
                    Arg("left", new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Vector3,
                        Vector3Value = new TriggerVector3Data { X = 1, Y = 2, Z = 3 }
                    }),
                    Arg("right", new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Vector3,
                        Vector3Value = new TriggerVector3Data { X = 1, Y = 2, Z = 3 }
                    })
                }
            };

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Database, Is.Null);
            Assert.That(result.Diagnostics.Exists(d => d.Code == "TRG2051"), Is.True, result.BuildMessage());
        }

        [Test]
        public void Build_CompilesNumericBlackboardWritesAsTargets()
        {
            var module = CreateModule(new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "seq",
                Children =
                {
                    NumericWrite("set_num_var", 4),
                    NumericWrite("add_num_var", 2)
                }
            });
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "damageBoost",
                Type = TriggerValueType.Number,
                DefaultValue = new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.Number,
                    NumberValue = 1
                }
            });

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.True, result.BuildMessage());
            Assert.That(result.Database.Triggers[0].Actions, Has.Count.EqualTo(2));
            foreach (var action in result.Database.Triggers[0].Actions)
            {
                var target = action.Args["target"];
                Assert.That(target.Kind, Is.EqualTo("BlackboardTarget"));
                Assert.That(target.BoardId, Is.EqualTo(BlackboardIdMapper.BoardId("local.module:skill.fireball")));
                Assert.That(target.KeyId, Is.EqualTo(BlackboardIdMapper.KeyId("damageBoost")));
                Assert.That(target.KeyType, Is.EqualTo(BlackboardKeyType.Double));
                Assert.That(target.Scope, Is.EqualTo(BlackboardInitializationScopes.Owner));
            }

            var json = TriggerAuthoringRuntimeExporter.Serialize(result.Database);
            var database = new TriggerPlanJsonDatabase();
            Assert.DoesNotThrow(() => database.LoadFromJson(json, "numeric-blackboard-write"));
            var targetArg = database.Records[0].Plan.Actions[0].Args["target"];
            Assert.That(targetArg.Kind, Is.EqualTo(ActionArgKind.BlackboardTarget));
            Assert.That(targetArg.BlackboardTarget.KeyType, Is.EqualTo(BlackboardKeyType.Double));
        }

        [Test]
        public void Build_RejectsReadOnlyNumericBlackboardWrite()
        {
            var module = CreateModule(NumericWrite("set_num_var", 4));
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "damageBoost",
                Type = TriggerValueType.Number,
                ReadOnly = true,
                DefaultValue = new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.Number,
                    NumberValue = 1
                }
            });

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Exists(item => item.Code == "TRG1314"), Is.True, result.BuildMessage());
        }

        [Test]
        public void Build_CompilesTypedBlackboardWritesAndRuntimeJsonLoadsThem()
        {
            var module = CreateModule(new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "seq",
                Children =
                {
                    TypedWrite("enabled", TriggerValueType.Boolean, new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Boolean,
                        BooleanValue = true
                    }),
                    TypedWrite("state", TriggerValueType.String, new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.String,
                        StringValue = "armed"
                    })
                }
            });
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "enabled",
                Type = TriggerValueType.Boolean,
                DefaultValue = new TriggerValueRefData { Source = TriggerValueSource.Constant, Type = TriggerValueType.Boolean }
            });
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "state",
                Type = TriggerValueType.String,
                DefaultValue = new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.String,
                    StringValue = "idle"
                }
            });

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.True, result.BuildMessage());
            Assert.That(result.Database.Triggers[0].Actions[0].Args["value"].Kind, Is.EqualTo("Bool"));
            Assert.That(result.Database.Triggers[0].Actions[0].Args["value"].BoolValue, Is.True);
            Assert.That(result.Database.Triggers[0].Actions[1].Args["value"].Kind, Is.EqualTo("String"));
            Assert.That(result.Database.Triggers[0].Actions[1].Args["value"].StringValue, Is.EqualTo("armed"));

            var runtime = new TriggerPlanJsonDatabase();
            runtime.LoadFromJson(TriggerAuthoringRuntimeExporter.Serialize(result.Database), "typed-blackboard-write");
            Assert.That(runtime.Records[0].Plan.Actions[0].Args["value"].Kind, Is.EqualTo(ActionArgKind.BooleanValue));
            Assert.That(runtime.Records[0].Plan.Actions[0].Args["value"].BooleanValue, Is.True);
            Assert.That(runtime.Records[0].Plan.Actions[1].Args["value"].Kind, Is.EqualTo(ActionArgKind.StringValue));
            Assert.That(runtime.Records[0].Plan.Actions[1].Args["value"].StringValue, Is.EqualTo("armed"));
        }

        [Test]
        public void Build_RejectsDynamicStringSetVariableValue()
        {
            var module = CreateModule(TypedWrite("state", TriggerValueType.String,
                Ref(TriggerValueSource.Payload, TriggerValueType.String, "state")));
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "state",
                Type = TriggerValueType.String,
                DefaultValue = new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.String,
                    StringValue = "idle"
                }
            });

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Exists(item => item.Code == "TRG2060"), Is.True, result.BuildMessage());
        }

        [Test]
        public void Build_RejectsSetVariableTargetValueTypeMismatchBeforeCompilation()
        {
            var module = CreateModule(TypedWrite("enabled", TriggerValueType.Boolean,
                new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.String,
                    StringValue = "true"
                }));
            module.Blackboard.Add(new TriggerBlackboardVariableData
            {
                Key = "enabled",
                Type = TriggerValueType.Boolean,
                DefaultValue = new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.Boolean
                }
            });

            var result = TriggerAuthoringRuntimeExporter.Build(module);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Diagnostics.Exists(item => item.Code == "TRG1315"), Is.True, result.BuildMessage());
        }

        [Test]
        public void TriggerPlanDtoBuilder_PreservesBlackboardTarget()
        {
            var target = new BlackboardWriteTarget(101, 202, BlackboardKeyType.Double, "owner");
            var action = ActionCallPlan.WithArgs(
                new AbilityKit.Triggering.Registry.ActionId(303),
                new Dictionary<string, ActionArgValue>
                {
                    ["target"] = ActionArgValue.OfBlackboardTarget(in target, "target"),
                    ["value"] = ActionArgValue.OfConst(4, "value")
                });
            var plan = new TriggerPlan<object>(0, 0, actions: new[] { action });
            var trigger = new TriggerEditorConfig { TriggerId = 1, EventId = "test.write" };

            var dto = TriggerPlanDtoBuilder.BuildTriggerPlanDto(trigger, in plan, 0, 0);
            var targetDto = dto.Actions[0].Args["target"];

            Assert.That(targetDto.Kind, Is.EqualTo("BlackboardTarget"));
            Assert.That(targetDto.BoardId, Is.EqualTo(101));
            Assert.That(targetDto.KeyId, Is.EqualTo(202));
            Assert.That(targetDto.KeyType, Is.EqualTo(BlackboardKeyType.Double));
            Assert.That(targetDto.Scope, Is.EqualTo("owner"));
        }

        [Test]
        public void ReadableRuntimeJson_RoundTripPreservesBlackboardTarget()
        {
            var database = new TriggerPlanDatabaseDto();
            database.Triggers.Add(new TriggerPlanDto
            {
                TriggerId = 1,
                EventName = "test.write",
                Actions = new List<ActionCallPlanDto>
                {
                    new ActionCallPlanDto
                    {
                        ActionId = 303,
                        Arity = 2,
                        Args = new Dictionary<string, NumericValueRefDto>
                        {
                            ["target"] = new NumericValueRefDto
                            {
                                Kind = "BlackboardTarget",
                                BoardId = 101,
                                KeyId = 202,
                                KeyType = BlackboardKeyType.Double,
                                Scope = "owner"
                            },
                            ["value"] = new NumericValueRefDto { Kind = "Const", ConstValue = 4 },
                            ["boolValue"] = new NumericValueRefDto { Kind = "Bool", BoolValue = true },
                            ["stringValue"] = new NumericValueRefDto { Kind = "String", StringValue = "armed" }
                        }
                    }
                }
            });

            var readableJson = ReadableTriggerPlanConverter.ToReadable(database);
            var roundTripped = ReadableTriggerPlanConverter.FromReadable(readableJson);
            var target = roundTripped.Triggers[0].Actions[0].Args["target"];

            StringAssert.Contains("\"Kind\": \"BlackboardTarget\"", readableJson);
            Assert.That(target.Kind, Is.EqualTo("BlackboardTarget"));
            Assert.That(target.BoardId, Is.EqualTo(101));
            Assert.That(target.KeyId, Is.EqualTo(202));
            Assert.That(target.KeyType, Is.EqualTo(BlackboardKeyType.Double));
            Assert.That(target.Scope, Is.EqualTo("owner"));
            Assert.That(roundTripped.Triggers[0].Actions[0].Args["boolValue"].BoolValue, Is.True);
            Assert.That(roundTripped.Triggers[0].Actions[0].Args["stringValue"].StringValue, Is.EqualTo("armed"));
        }

        private static TriggerAuthoringModuleData CreateModule(TriggerNodeData actions)
        {
            return new TriggerAuthoringModuleData
            {
                ModuleId = "skill.fireball",
                Triggers =
                {
                    new TriggerDefinitionData
                    {
                        Id = 1001,
                        Name = "Cast",
                        Event = "skill.cast",
                        Actions = actions
                    }
                }
            };
        }

        private static TriggerNodeData DebugLog(string message)
        {
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "debug_log",
                Arguments = { Arg("message", new TriggerValueRefData
                {
                    Source = TriggerValueSource.Constant,
                    Type = TriggerValueType.String,
                    StringValue = message
                }) }
            };
        }

        private static TriggerNodeData NumericWrite(string type, double value)
        {
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = type,
                Arguments =
                {
                    Arg("target", Ref(TriggerValueSource.LocalBlackboard, TriggerValueType.Number, "damageBoost")),
                    Arg("value", new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Number,
                        NumberValue = value
                    })
                }
            };
        }

        private static TriggerNodeData TypedWrite(string key, TriggerValueType type, TriggerValueRefData value)
        {
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Action,
                Type = "set_var",
                Arguments =
                {
                    Arg("target", Ref(TriggerValueSource.LocalBlackboard, type, key)),
                    Arg("value", value)
                }
            };
        }

        private static TriggerNodeData Compare(string type, long left, long right)
        {
            return new TriggerNodeData
            {
                Kind = TriggerNodeKind.Condition,
                Type = type,
                Arguments = { Arg("left", ConstInt(left)), Arg("right", ConstInt(right)) }
            };
        }

        private static TriggerArgumentData Arg(string name, TriggerValueRefData value)
        {
            return new TriggerArgumentData { Name = name, Value = value };
        }

        private static TriggerValueRefData ConstInt(long value)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = TriggerValueType.Integer,
                IntegerValue = value
            };
        }

        private static TriggerValueRefData Ref(TriggerValueSource source, TriggerValueType type, string path)
        {
            return new TriggerValueRefData { Source = source, Type = type, Path = path };
        }

        private static string NormalizeLineEndings(string value)
        {
            return value?.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
#endif
