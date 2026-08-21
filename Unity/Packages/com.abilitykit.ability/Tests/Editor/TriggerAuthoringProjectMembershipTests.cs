#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringProjectMembershipTests
    {
        private readonly List<Object> _tracked = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _tracked.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _tracked.Count; i++)
                if (_tracked[i] != null) Object.DestroyImmediate(_tracked[i]);
        }

        private TriggerAuthoringModuleAsset CreateModule()
        {
            var module = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            module.name = "TestModule";
            module.Module = new TriggerAuthoringModuleData { ModuleId = "module.test" };
            _tracked.Add(module);
            return module;
        }

        private TriggerAuthoringProjectAsset CreateProject()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            project.name = "TestProject";
            _tracked.Add(project);
            return project;
        }

        [Test]
        public void Assign_RegistersModuleInProjectAndSetsBackReference()
        {
            var module = CreateModule();
            var project = CreateProject();

            TriggerAuthoringProjectMembership.Assign(module, project);

            Assert.That(module.Project, Is.SameAs(project));
            Assert.That(project.Modules, Has.Count.EqualTo(1));
            Assert.That(project.Modules[0], Is.SameAs(module));
        }

        [Test]
        public void Assign_MovesModuleBetweenProjectsWithoutDuplicates()
        {
            var module = CreateModule();
            var first = CreateProject();
            var second = CreateProject();
            TriggerAuthoringProjectMembership.Assign(module, first);

            TriggerAuthoringProjectMembership.Assign(module, second);

            Assert.That(module.Project, Is.SameAs(second));
            Assert.That(first.Modules, Is.Empty);
            Assert.That(second.Modules, Has.Count.EqualTo(1));
            Assert.That(second.Modules[0], Is.SameAs(module));
        }

        [Test]
        public void Assign_ToNull_DetachesFromPreviousProject()
        {
            var module = CreateModule();
            var project = CreateProject();
            TriggerAuthoringProjectMembership.Assign(module, project);

            TriggerAuthoringProjectMembership.Assign(module, null);

            Assert.That(module.Project, Is.Null);
            Assert.That(project.Modules, Is.Empty);
        }

        [Test]
        public void Assign_IsIdempotentForSameProject()
        {
            var module = CreateModule();
            var project = CreateProject();

            TriggerAuthoringProjectMembership.Assign(module, project);
            TriggerAuthoringProjectMembership.Assign(module, project);

            Assert.That(module.Project, Is.SameAs(project));
            Assert.That(project.Modules, Has.Count.EqualTo(1));
        }

        [Test]
        public void Assign_RepairsBackReferenceOnlyState()
        {
            // 旧路径只写反向引用（SetProject）不登记清单；Assign 应补齐缺失的一侧。
            var module = CreateModule();
            var project = CreateProject();
            module.SetProject(project);

            TriggerAuthoringProjectMembership.Assign(module, project);

            Assert.That(project.Modules, Has.Count.EqualTo(1));
            Assert.That(project.Modules[0], Is.SameAs(module));
        }

        [Test]
        public void Detach_RemovesMembership()
        {
            var module = CreateModule();
            var project = CreateProject();
            TriggerAuthoringProjectMembership.Assign(module, project);

            TriggerAuthoringProjectMembership.Detach(module);

            Assert.That(module.Project, Is.Null);
            Assert.That(project.Modules, Is.Empty);
        }

        [Test]
        public void ProjectModuleList_AddRemoveAreDeduplicated()
        {
            var project = CreateProject();
            var module = CreateModule();

            Assert.That(project.AddModule(module), Is.True);
            Assert.That(project.AddModule(module), Is.False);
            Assert.That(project.Modules, Has.Count.EqualTo(1));
            Assert.That(project.RemoveModule(module), Is.True);
            Assert.That(project.RemoveModule(module), Is.False);
            Assert.That(project.Modules, Is.Empty);
        }

        [Test]
        public void ProjectDefaults_RegistersCoreNodeTypesAfterCatalogCleanup()
        {
            var catalog = TriggerTypeDescriptorCatalog.CreateProjectDefaults();

            TriggerTypeDescriptor descriptor;
            Assert.That(catalog.TryGet(TriggerNodeKind.Condition, "all", out descriptor), Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Condition, "any", out descriptor), Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Condition, "not", out descriptor), Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Condition, "arg_eq", out descriptor), Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Condition, "arg_gt", out descriptor), Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "seq", out descriptor), Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "debug_log", out descriptor), Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "set_var", out descriptor), Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "set_num_var", out descriptor), Is.True);
            Assert.That(descriptor.RuntimeSupported, Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "add_num_var", out descriptor), Is.True);
            Assert.That(descriptor.RuntimeSupported, Is.True);
            Assert.That(catalog.TryGet(TriggerNodeKind.Action, "set_var", out descriptor), Is.True);
            Assert.That(descriptor.RuntimeSupported, Is.True);
        }
    }
}
#endif
