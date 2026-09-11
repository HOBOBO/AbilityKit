#if UNITY_EDITOR
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringTestExtension : ITriggerAuthoringExtension
    {
        public const string ExtensionId = "abilitykit.tests.trigger-extension";

        public string Id => ExtensionId;

        public void Register(TriggerAuthoringExtensionContext context)
        {
            context.RegisterCondition(
                new TriggerTypeDescriptor(
                    TriggerNodeKind.Condition,
                    "test_condition",
                    "测试条件",
                    "Condition/Test"),
                new TestConditionCompiler());
            context.RegisterAction(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "test_action",
                "测试行为",
                "Action/Test",
                0,
                0,
                true,
                new TriggerParameterDescriptor("value", TriggerValueType.Number)));
            context.RegisterAction(new TriggerTypeDescriptor(
                TriggerNodeKind.Action,
                "test_output_action",
                "测试输出行为",
                "Action/Test",
                0,
                0,
                true,
                new TriggerParameterDescriptor(
                    "result",
                    TriggerValueType.Number,
                    false,
                    TriggerValueSourceMask.All,
                    TriggerParameterAccess.Output)));
            context.RegisterValueSource(new TriggerAuthoringValueSourceDescriptor(
                "gameplay:1001",
                TriggerValueType.Number,
                "测试玩法变量",
                expressionName: "gameplay.1001"));
            context.RegisterEvent(new TriggerEventDefinitionData
            {
                Id = "test.event",
                DisplayName = "测试事件",
                Category = "测试"
            });
        }

        private sealed class TestConditionCompiler : ITriggerAuthoringConditionCompiler
        {
            public void Compile(TriggerAuthoringConditionCompilerContext context)
            {
                context.EmitConstant(true);
            }
        }
    }

    public sealed class TriggerAuthoringExtensionTests
    {
        [Test]
        public void MobaEventCatalog_ExposesFieldsOwnedByEachPipelineStage()
        {
            var events = new TriggerEventDescriptorCatalog(TriggerAuthoringProjectDefaults.CreateMobaEvents());

            Assert.That(events.TryResolve("damage.calc.begin", out var calculation), Is.True);
            Assert.That(calculation.PayloadType, Is.EqualTo("AttackCalcInfo"));
            Assert.That(HasPayloadField(calculation, "raw_damage"), Is.True);
            Assert.That(HasPayloadField(calculation, "hp_damage"), Is.True);
            Assert.That(HasPayloadField(calculation, "damage_value"), Is.False);

            Assert.That(events.TryResolve("damage.apply.after", out var damageResult), Is.True);
            Assert.That(damageResult.PayloadType, Is.EqualTo("DamageResult"));
            Assert.That(HasPayloadField(damageResult, "damage_value"), Is.True);
            Assert.That(HasPayloadField(damageResult, "raw_damage"), Is.False);

            Assert.That(events.TryResolve("heal.apply.before", out var healRequest), Is.True);
            Assert.That(healRequest.PayloadType, Is.EqualTo("MobaHealRequest"));
            Assert.That(HasPayloadField(healRequest, "requested_value"), Is.True);
            Assert.That(HasPayloadField(healRequest, "applied_value"), Is.False);

            Assert.That(events.TryResolve("heal.apply.after", out var healResult), Is.True);
            Assert.That(healResult.PayloadType, Is.EqualTo("MobaHealthChangeResult"));
            Assert.That(HasPayloadField(healResult, "applied_value"), Is.True);
            Assert.That(HasPayloadField(healResult, "overheal_value"), Is.True);
        }

        [Test]
        public void ProjectCatalog_OnlyAppliesExplicitlyEnabledExtensions()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            try
            {
                var coreOnly = TriggerTypeDescriptorCatalog.CreateForProject(project);
                Assert.That(coreOnly.TryGet(
                    TriggerNodeKind.Action,
                    "test_action",
                    out _), Is.False);
                Assert.That(coreOnly.TryGet(
                    TriggerNodeKind.Action,
                    "give_damage",
                    out _), Is.False);

                project.SetExtensionIds(new[] { TriggerAuthoringTestExtension.ExtensionId });
                var extended = TriggerTypeDescriptorCatalog.CreateForProject(project);
                Assert.That(extended.TryGet(
                    TriggerNodeKind.Action,
                    "test_action",
                    out var action), Is.True);
                Assert.That(action.Parameters, Has.Count.EqualTo(1));
                Assert.That(extended.TryGet(
                    TriggerNodeKind.Condition,
                    "test_condition",
                    out _), Is.True);
                Assert.That(extended.TryGetConditionCompiler(
                    "test_condition",
                    out _), Is.True);

                var events = TriggerEventDescriptorCatalog.FromProject(project);
                Assert.That(events.TryResolve("test.event", out var eventDefinition), Is.True);
                Assert.That(eventDefinition.DisplayName, Is.EqualTo("测试事件"));
            }
            finally
            {
                Object.DestroyImmediate(project);
            }
        }

        [Test]
        public void ProjectExtensionIds_AreNormalizedDeduplicatedAndSorted()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            try
            {
                project.SetExtensionIds(new[] { " z.extension ", "a.extension", "a.extension", "" });
                Assert.That(project.ExtensionIds, Is.EqualTo(new[] { "a.extension", "z.extension" }));
            }
            finally
            {
                Object.DestroyImmediate(project);
            }
        }

        [Test]
        public void ProjectValueSources_AreIsolatedAndAvailableToParametersAndExpressions()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            try
            {
                var coreOnly = TriggerAuthoringValueSourceCatalog.CreateForProject(project);
                Assert.That(coreOnly.TryGet("gameplay:1001", out _), Is.False);

                project.SetExtensionIds(new[] { TriggerAuthoringTestExtension.ExtensionId });
                var extended = TriggerAuthoringValueSourceCatalog.CreateForProject(project);
                Assert.That(extended.TryGet("gameplay:1001", out var descriptor), Is.True);
                Assert.That(descriptor.Type, Is.EqualTo(TriggerValueType.Number));

                var editorContext = new TriggerAuthoringValueRefEditorContext { ValueSources = extended };
                var options = TriggerAuthoringValueRefEditor.CollectPathOptions(
                    TriggerValueSource.Context,
                    TriggerValueType.Number,
                    TriggerParameterAccess.Read,
                    editorContext);
                Assert.That(options.Exists(option => option.Path == "gameplay:1001"), Is.True);

                var references = TriggerAuthoringValueRefEditor.CollectExpressionReferences(editorContext);
                Assert.That(references.Exists(reference => reference.Expression == "gameplay.1001"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(project);
            }
        }

        [Test]
        public void FormulaValidation_ChecksSyntaxFunctionsAndArgumentCounts()
        {
            Assert.That(TriggerAuthoringValueRefEditor.TryValidateExpression(
                "max(payload.damage, gameplay.1001) * 2", out var validError), Is.True, validError);
            Assert.That(TriggerAuthoringValueRefEditor.TryValidateExpression(
                "unknown(payload.damage)", out _), Is.False);
            Assert.That(TriggerAuthoringValueRefEditor.TryValidateExpression(
                "clamp(payload.damage, 0)", out _), Is.False);
            Assert.That(TriggerAuthoringValueRefEditor.TryValidateExpression(
                "payload.damage +", out _), Is.False);
        }

        [Test]
        public void OutputParameter_OnlyOffersWritableBlackboards()
        {
            var descriptor = new TriggerParameterDescriptor(
                "result",
                TriggerValueType.Number,
                false,
                TriggerValueSourceMask.All,
                TriggerParameterAccess.Output);
            Assert.That(descriptor.AllowedSources, Is.EqualTo(
                TriggerValueSourceMask.LocalBlackboard | TriggerValueSourceMask.GlobalBlackboard));

            var context = new TriggerAuthoringValueRefEditorContext
            {
                Trigger = new TriggerDefinitionData
                {
                    Blackboard =
                    {
                        new TriggerBlackboardVariableData
                        {
                            Key = "writable",
                            Type = TriggerValueType.Number,
                            ReadOnly = false
                        },
                        new TriggerBlackboardVariableData
                        {
                            Key = "readOnly",
                            Type = TriggerValueType.Number,
                            ReadOnly = true
                        }
                    }
                }
            };
            var options = TriggerAuthoringValueRefEditor.CollectValueReferenceOptions(
                descriptor.Type,
                descriptor.Access,
                descriptor.AllowedSources,
                context);

            Assert.That(options.Exists(option => option.UnscopedPath == "writable"), Is.True);
            Assert.That(options.Exists(option => option.UnscopedPath == "readOnly"), Is.False);
            Assert.That(options.TrueForAll(option =>
                option.Source == TriggerValueSource.LocalBlackboard ||
                option.Source == TriggerValueSource.GlobalBlackboard), Is.True);
        }

        [Test]
        public void OutputParameter_ExportsAsBlackboardTarget()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            try
            {
                project.SetExtensionIds(new[] { TriggerAuthoringTestExtension.ExtensionId });
                var module = new TriggerAuthoringModuleData
                {
                    ModuleId = "test.output",
                    Blackboard =
                    {
                        new TriggerBlackboardVariableData
                        {
                            Key = "actualDamage",
                            Type = TriggerValueType.Number,
                            DefaultValue = new TriggerValueRefData
                            {
                                Source = TriggerValueSource.Constant,
                                Type = TriggerValueType.Number
                            }
                        }
                    },
                    Triggers =
                    {
                        new TriggerDefinitionData
                        {
                            Id = 1,
                            Event = "test.event",
                            Scope = "owner",
                            Actions = new TriggerNodeData
                            {
                                Kind = TriggerNodeKind.Action,
                                Type = "test_output_action",
                                Arguments =
                                {
                                    new TriggerArgumentData
                                    {
                                        Name = "result",
                                        Value = new TriggerValueRefData
                                        {
                                            Source = TriggerValueSource.LocalBlackboard,
                                            Type = TriggerValueType.Number,
                                            Path = TriggerAuthoringLocalBlackboardPath.Format(
                                                TriggerAuthoringLocalBlackboardScope.Module,
                                                "actualDamage")
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                var result = TriggerAuthoringRuntimeExporter.Build(
                    module,
                    new TriggerAuthoringValidationContext
                    {
                        Types = TriggerTypeDescriptorCatalog.CreateForProject(project),
                        Events = TriggerEventDescriptorCatalog.FromProject(project),
                        ValueSources = TriggerAuthoringValueSourceCatalog.CreateForProject(project)
                    });

                Assert.That(result.Success, Is.True, result.BuildMessage());
                var output = result.Database.Triggers[0].Actions[0].Args["result"];
                Assert.That(output.Kind, Is.EqualTo("BlackboardTarget"));
                Assert.That(output.KeyType, Is.EqualTo(AbilityKit.Triggering.Blackboard.BlackboardKeyType.Double));
            }
            finally
            {
                Object.DestroyImmediate(project);
            }
        }

        [Test]
        public void ExtensionConditionCompiler_ProducesRuntimePredicate()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            try
            {
                project.SetExtensionIds(new[] { TriggerAuthoringTestExtension.ExtensionId });
                var module = new TriggerAuthoringModuleData
                {
                    ModuleId = "test.extension",
                    Triggers =
                    {
                        new TriggerDefinitionData
                        {
                            Id = 1,
                            Name = "Extension condition",
                            Event = "test.event",
                            Condition = new TriggerNodeData
                            {
                                Kind = TriggerNodeKind.Condition,
                                Type = "test_condition"
                            },
                            Actions = new TriggerNodeData
                            {
                                Kind = TriggerNodeKind.Action,
                                Type = "debug_log",
                                Arguments =
                                {
                                    new TriggerArgumentData
                                    {
                                        Name = "message",
                                        Value = new TriggerValueRefData
                                        {
                                            Source = TriggerValueSource.Constant,
                                            Type = TriggerValueType.String,
                                            StringValue = "extension"
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                var result = TriggerAuthoringRuntimeExporter.Build(
                    module,
                    new TriggerAuthoringValidationContext
                    {
                        Types = TriggerTypeDescriptorCatalog.CreateForProject(project),
                        Events = TriggerEventDescriptorCatalog.FromProject(project)
                    });

                Assert.That(result.Success, Is.True, result.BuildMessage());
                Assert.That(result.Database.Triggers, Has.Count.EqualTo(1));
                Assert.That(result.Database.Triggers[0].Predicate.Nodes, Has.Count.EqualTo(1));
                Assert.That(result.Database.Triggers[0].Predicate.Nodes[0].Kind, Is.EqualTo("Const"));
                Assert.That(result.Database.Triggers[0].Predicate.Nodes[0].ConstValue, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(project);
            }
        }

        private static bool HasPayloadField(TriggerEventDefinitionData definition, string path)
        {
            return definition?.PayloadFields != null &&
                   definition.PayloadFields.Exists(field => field != null && field.Path == path);
        }
    }
}
#endif
