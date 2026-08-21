#if UNITY_EDITOR
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringProjectValidatorTests
    {
        [Test]
        public void Validate_AcceptsExplicitModuleCatalogAndRuntimeAggregate()
        {
            var fixture = CreateFixture();
            try
            {
                var result = TriggerAuthoringProjectValidator.Validate(fixture.Project);

                Assert.That(result.Success, Is.True, result.BuildMessage());
                Assert.That(result.ModuleCount, Is.EqualTo(1));
            }
            finally
            {
                fixture.Dispose();
            }
        }

        [Test]
        public void Validate_RejectsCrossModuleTriggerIdCollision()
        {
            var fixture = CreateFixture();
            var second = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            try
            {
                second.name = "SecondModule";
                second.SetProject(fixture.Project);
                second.Module = CreateModule("skill.second", 1001);
                fixture.Project.SetModules(new[] { fixture.Module, second });

                var result = TriggerAuthoringProjectValidator.Validate(fixture.Project);

                Assert.That(result.Success, Is.False);
                Assert.That(result.Diagnostics.Exists(item => item.Code == "TRG3050"), Is.True, result.BuildMessage());
            }
            finally
            {
                Object.DestroyImmediate(second);
                fixture.Dispose();
            }
        }

        private static Fixture CreateFixture()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            project.name = "TestProject";
            var events = ScriptableObject.CreateInstance<TriggerEventCatalogAsset>();
            events.Events.Add(new TriggerEventDefinitionData
            {
                Id = "skill.cast",
                MatchMode = TriggerEventMatchMode.Exact,
                DisplayName = "Skill Cast"
            });
            var blackboards = ScriptableObject.CreateInstance<TriggerGlobalBlackboardCatalogAsset>();
            var templates = ScriptableObject.CreateInstance<TriggerAuthoringTemplateCatalogAsset>();
            project.SetCatalogs(events, blackboards, templates);

            var module = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            module.name = "FirstModule";
            module.SetProject(project);
            module.Module = CreateModule("skill.first", 1001);
            project.SetModules(new[] { module });
            return new Fixture(project, events, blackboards, templates, module);
        }

        private static TriggerAuthoringModuleData CreateModule(string moduleId, int triggerId)
        {
            return new TriggerAuthoringModuleData
            {
                ModuleId = moduleId,
                Triggers =
                {
                    new TriggerDefinitionData
                    {
                        Id = triggerId,
                        Event = "skill.cast",
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
                                        StringValue = moduleId
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private sealed class Fixture
        {
            public Fixture(
                TriggerAuthoringProjectAsset project,
                TriggerEventCatalogAsset events,
                TriggerGlobalBlackboardCatalogAsset blackboards,
                TriggerAuthoringTemplateCatalogAsset templates,
                TriggerAuthoringModuleAsset module)
            {
                Project = project;
                Events = events;
                Blackboards = blackboards;
                Templates = templates;
                Module = module;
            }

            public TriggerAuthoringProjectAsset Project { get; }
            public TriggerEventCatalogAsset Events { get; }
            public TriggerGlobalBlackboardCatalogAsset Blackboards { get; }
            public TriggerAuthoringTemplateCatalogAsset Templates { get; }
            public TriggerAuthoringModuleAsset Module { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Module);
                Object.DestroyImmediate(Templates);
                Object.DestroyImmediate(Blackboards);
                Object.DestroyImmediate(Events);
                Object.DestroyImmediate(Project);
            }
        }
    }
}
#endif
