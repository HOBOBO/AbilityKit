#if UNITY_EDITOR
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Utilities;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    /// <summary>
    /// Golden example 验收（设计文档 §12 P0）：编辑样例 -> 校验 -> canonical Runtime 编译全绿；
    /// 顺带验收引用搜索。此测试是 golden 内容与校验器/导出器契约的联动哨兵——
    /// 任何一侧漂移都会在这里先红。
    /// </summary>
    public sealed class TriggerAuthoringGoldenExamplesTests
    {
        private readonly List<Object> _tracked = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _tracked.Count; i++)
                if (_tracked[i] != null) Object.DestroyImmediate(_tracked[i]);
        }

        [Test]
        public void GoldenExamples_ValidateCleanAgainstMobaCatalogs()
        {
            var context = CreateMobaContext();
            var modules = TriggerAuthoringGoldenExamples.BuildAll();

            Assert.That(modules.Count, Is.EqualTo(3));
            foreach (var module in modules)
            {
                var diagnostics = TriggerAuthoringValidator.Validate(module, context);
                Assert.That(
                    TriggerAuthoringValidator.HasErrors(diagnostics),
                    Is.False,
                    module.ModuleId + ": " + FormatDiagnostics(diagnostics));
            }
        }

        [Test]
        public void GoldenExamples_RuntimeExportSucceeds()
        {
            var context = CreateMobaContext();
            var modules = TriggerAuthoringGoldenExamples.BuildAll();

            foreach (var module in modules)
            {
                var result = TriggerAuthoringRuntimeExporter.Build(module, context);
                Assert.That(result.Success, Is.True, module.ModuleId + ": " + result.BuildMessage());
                Assert.That(result.ExportedTriggerCount, Is.GreaterThan(0), module.ModuleId);
                Assert.That(result.Database, Is.Not.Null, module.ModuleId);
            }
        }

        [Test]
        public void GoldenExamples_ProjectExportProducesLoadableRuntimePlans()
        {
            var tempRoot = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "golden-runtime-export-" + System.Guid.NewGuid().ToString("N"));
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            var eventCatalog = ScriptableObject.CreateInstance<TriggerEventCatalogAsset>();
            var blackboardCatalog = ScriptableObject.CreateInstance<TriggerGlobalBlackboardCatalogAsset>();
            var templateCatalog = ScriptableObject.CreateInstance<TriggerAuthoringTemplateCatalogAsset>();
            eventCatalog.Events = TriggerAuthoringProjectDefaults.CreateMobaEvents();
            blackboardCatalog.Keys = TriggerAuthoringProjectDefaults.CreateMobaBlackboardKeys();
            project.SetCatalogs(eventCatalog, blackboardCatalog, templateCatalog);
            _tracked.Add(project);
            _tracked.Add(eventCatalog);
            _tracked.Add(blackboardCatalog);
            _tracked.Add(templateCatalog);

            var expectedRecords = 0;
            foreach (var data in TriggerAuthoringGoldenExamples.BuildAll())
            {
                var module = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
                module.name = data.ModuleId;
                module.Module = data;
                _tracked.Add(module);
                TriggerAuthoringProjectMembership.Assign(module, project);
                expectedRecords += data.Triggers.Count;
            }

            try
            {
                project.SetRuntimeOutputRoot(tempRoot);
                var result = TriggerAuthoringProjectExport.ExportAll(project);

                Assert.That(result.Success, Is.True, result.BuildMessage());
                Assert.That(result.ExportedFiles.Count, Is.EqualTo(3), result.BuildMessage());

                var loadedRecords = 0;
                for (var i = 0; i < result.ExportedFiles.Count; i++)
                {
                    var path = result.ExportedFiles[i];
                    Assert.That(System.IO.File.Exists(path), Is.True, path);
                    var database = new AbilityKit.Triggering.Runtime.Plan.Json.TriggerPlanJsonDatabase();
                    var json = System.IO.File.ReadAllText(path);
                    Assert.DoesNotThrow(() => database.LoadFromJson(json, path), path);
                    loadedRecords += database.Records.Count;
                }
                Assert.That(loadedRecords, Is.EqualTo(expectedRecords),
                    "导出文件应可被运行时数据库逐个加载且触发器数守恒");
            }
            finally
            {
                if (System.IO.Directory.Exists(tempRoot)) System.IO.Directory.Delete(tempRoot, true);
            }
        }

        [Test]
        public void ReferenceFinder_LocatesEventGroupAndGlobalKeyReferences()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            var skill = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            var passive = ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>();
            _tracked.Add(project);
            _tracked.Add(skill);
            _tracked.Add(passive);
            skill.Module = TriggerAuthoringGoldenExamples.BuildSkillModule();
            passive.Module = TriggerAuthoringGoldenExamples.BuildPassiveModule();
            project.SetModules(new[] { skill, passive });

            var eventRefs = TriggerAuthoringReferenceFinder.FindEventReferences(project, "skill.cast.complete");
            var groupRefs = TriggerAuthoringReferenceFinder.FindGroupReferences(project, "condition_group_low_health");
            var keyRefs = TriggerAuthoringReferenceFinder.FindGlobalKeyReferences(project, "skill.decayFactor");
            var missingRefs = TriggerAuthoringReferenceFinder.FindEventReferences(project, "buff.nonexistent");

            Assert.That(eventRefs.Count, Is.EqualTo(1), "skill.cast.complete 应被技能模块引用");
            Assert.That(eventRefs[0].ModuleId, Is.EqualTo("module.golden_skill"));
            Assert.That(groupRefs.Count, Is.EqualTo(1), "低血量条件组应被被动模块引用");
            Assert.That(groupRefs[0].ModuleId, Is.EqualTo("module.golden_passive"));
            Assert.That(keyRefs.Count, Is.EqualTo(1), "全局键 skill.decayFactor 应被技能模块引用");
            Assert.That(missingRefs.Count, Is.EqualTo(0));
        }

        private static TriggerAuthoringValidationContext CreateMobaContext()
        {
            return new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                Events = new TriggerEventDescriptorCatalog(TriggerAuthoringProjectDefaults.CreateMobaEvents()),
                GlobalBlackboard = new TriggerGlobalBlackboardDescriptorCatalog(
                    TriggerAuthoringProjectDefaults.CreateMobaBlackboardKeys())
            };
        }

        private static string FormatDiagnostics(IReadOnlyList<TriggerAuthoringDiagnostic> diagnostics)
        {
            var builder = new System.Text.StringBuilder();
            for (var i = 0; i < diagnostics.Count; i++)
            {
                builder.AppendLine();
                builder.Append(diagnostics[i].Severity).Append(' ').Append(diagnostics[i].Code).Append(' ')
                    .Append(diagnostics[i].Path).Append(": ").Append(diagnostics[i].Message);
            }
            return builder.ToString();
        }
    }
}
#endif
