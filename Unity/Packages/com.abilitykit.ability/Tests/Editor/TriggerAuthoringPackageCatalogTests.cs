#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Config.Authoring;
using AbilityKit.Ability.Editor.Packages;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor.Tests
{
    public sealed class TriggerAuthoringPackageCatalogTests
    {
        private readonly List<Object> _tracked = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _tracked.Count; i++)
                if (_tracked[i] != null) Object.DestroyImmediate(_tracked[i]);
            _tracked.Clear();
        }

        [Test]
        public void LegacyModule_UsesRuntimeKindAsDefaultDomain()
        {
            var package = CreatePackage("skill.hero", "Hero Skills", TriggerModuleKind.Ability, 2);

            Assert.That(TriggerAuthoringPackageCatalog.ResolveDomainId(package), Is.EqualTo("ability"));
        }

        [Test]
        public void Build_GroupsAndSortsPackagesByBusinessDomain()
        {
            var second = CreatePackage("ability.hero.zhaoyun", "赵云", TriggerModuleKind.Ability, 3);
            SetIdentity(second, "ability", "hero.zhaoyun");
            var first = CreatePackage("ability.hero.lianpo", "廉颇", TriggerModuleKind.Ability, 4);
            SetIdentity(first, "ability", "hero.lianpo");
            var buff = CreatePackage("buff.control", "控制效果", TriggerModuleKind.Buff, 2);
            SetIdentity(buff, "buff", "control");

            var groups = TriggerAuthoringPackageCatalog.Build(new[] { second, buff, first });

            Assert.That(groups, Has.Count.EqualTo(2));
            var ability = groups.Find(item => item.DomainId == "ability");
            Assert.That(ability, Is.Not.Null);
            Assert.That(ability.Packages, Has.Count.EqualTo(2));
            Assert.That(ability.TriggerCount, Is.EqualTo(7));
            Assert.That(ability.Packages[0], Is.SameAs(first));
        }

        [Test]
        public void IdentifierAndModuleId_AreNormalizedAndDeduplicated()
        {
            var project = Track(ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>());
            var existing = CreatePackage("ability.hero.zhaoyun", "赵云", TriggerModuleKind.Ability, 0);
            project.SetModules(new[] { existing });

            Assert.That(
                TriggerAuthoringPackageService.NormalizeIdentifier(" Ability / Hero-ZhaoYun "),
                Is.EqualTo("ability.hero.zhaoyun"));
            Assert.That(
                TriggerAuthoringPackageService.BuildModuleId("ability", "hero.zhaoyun"),
                Is.EqualTo("ability.hero.zhaoyun"));
            Assert.That(
                TriggerAuthoringPackageService.CreateUniqueModuleId(project, "ability.hero.zhaoyun"),
                Is.EqualTo("ability.hero.zhaoyun.2"));
        }

        [Test]
        public void BuildPlan_RejectsDuplicateContentIdentityInsideDomain()
        {
            var project = Track(ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>());
            var existing = CreatePackage("ability.hero.zhaoyun", "赵云", TriggerModuleKind.Ability, 0);
            SetIdentity(existing, "ability", "hero.zhaoyun");
            project.SetModules(new[] { existing });

            var plan = TriggerAuthoringPackageService.BuildPlan(new TriggerAuthoringPackageCreateRequest
            {
                Project = project,
                DomainId = "ability",
                ContentKey = "hero.zhaoyun",
                DisplayName = "赵云技能副本",
                AssetDirectory = "Assets/Triggers/Ability/ZhaoYun"
            });

            Assert.That(plan.IsValid, Is.False);
            StringAssert.Contains("已经存在", plan.Error);
        }

        private TriggerAuthoringModuleAsset CreatePackage(
            string moduleId,
            string displayName,
            TriggerModuleKind kind,
            int triggerCount)
        {
            var package = Track(ScriptableObject.CreateInstance<TriggerAuthoringModuleAsset>());
            package.Module = new TriggerAuthoringModuleData
            {
                ModuleId = moduleId,
                DisplayName = displayName,
                Kind = kind
            };
            for (var i = 0; i < triggerCount; i++)
                package.Module.Triggers.Add(new TriggerDefinitionData { Id = i + 1 });
            return package;
        }

        private static void SetIdentity(TriggerAuthoringModuleAsset package, string domainId, string contentKey)
        {
            package.PackageMetadata.SetIdentity(domainId, contentKey);
        }

        private T Track<T>(T value) where T : Object
        {
            _tracked.Add(value);
            return value;
        }
    }

    public sealed class TriggerAuthoringPackageCreationTests
    {
        private const string TestRoot = "Assets/__TriggerAuthoringPackageCreationTests";

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.CreateFolder("Assets", "__TriggerAuthoringPackageCreationTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestRoot);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Create_WritesAssetAndSourceAndRegistersProjectMembership()
        {
            var project = ScriptableObject.CreateInstance<TriggerAuthoringProjectAsset>();
            AssetDatabase.CreateAsset(project, TestRoot + "/Project.asset");

            var result = TriggerAuthoringPackageService.Create(new TriggerAuthoringPackageCreateRequest
            {
                Project = project,
                DomainId = "ability",
                ContentKey = "hero.zhaoyun",
                DisplayName = "赵云技能包",
                Kind = TriggerModuleKind.Ability,
                Owner = "combat-team",
                Tags = new[] { "hero", "moba" }
            });

            Assert.That(result.Success, Is.True, result.Error);
            Assert.That(project.Modules, Has.Count.EqualTo(1));
            Assert.That(result.Package.Project, Is.SameAs(project));
            Assert.That(result.Package.Module.ModuleId, Is.EqualTo("ability.hero.zhaoyun"));
            Assert.That(result.Package.PackageMetadata.ContentKey, Is.EqualTo("hero.zhaoyun"));
            Assert.That(AssetDatabase.GetAssetPath(result.Package), Does.StartWith(TestRoot + "/Triggers/ability/hero_zhaoyun/"));
            Assert.That(result.Package.SourceJsonPath, Is.Not.Empty);
            Assert.That(File.Exists(ToAbsolutePath(result.Package.SourceJsonPath)), Is.True);
        }

        private static string ToAbsolutePath(string projectRelativePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));
        }
    }
}
#endif
