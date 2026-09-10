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
    }
}
#endif
