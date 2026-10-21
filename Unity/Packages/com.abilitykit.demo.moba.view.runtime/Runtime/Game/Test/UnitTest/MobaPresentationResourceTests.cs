using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.Animations;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class MobaPresentationResourceTests
    {
        [Serializable]
        private sealed class CharacterConfigRoot
        {
            public CharacterDTO[] Items;
        }

        [Serializable]
        private sealed class ModelConfigRoot
        {
            public ModelDTO[] Items;
        }

        [Serializable]
        private sealed class AoeConfigRoot
        {
            public AoeDTO[] Items;
        }

        [Serializable]
        private sealed class SummonConfigRoot
        {
            public SummonDTO[] Items;
        }

        [Serializable]
        private sealed class VfxConfigRoot
        {
            public VfxDTO[] Items;
        }

        [Serializable]
        private sealed class SkillConfigRoot
        {
            public SkillDTO[] Items;
        }

        [Serializable]
        private sealed class ProjectileConfigRoot
        {
            public ProjectileDTO[] Items;
        }

        [Serializable]
        private sealed class BuffConfigRoot
        {
            public BuffDTO[] Items;
        }

        [Serializable]
        private sealed class PresentationTemplateConfigRoot
        {
            public PresentationTemplateDTO[] Items;
        }

        [Serializable]
        private sealed class BuffHudConfigRoot
        {
            public BuffHudDTO[] Items;
        }

        [Serializable]
        private sealed class BuffHudDTO
        {
            public int Id;
            public string Name;
            public string ShortName;
            public bool ShowCountdown;
            public bool ShowDisabledHint;
        }

        [Test]
        public void SunShangxiangRollBuff_ShouldHaveExportedHudStatus()
        {
            var rows = LoadArray<BuffHudConfigRoot, BuffHudDTO>(
                "luban/moba/buff_hud_templates",
                root => root.Items);

            var row = Array.Find(rows, item => item != null && item.Id == 10070103);
            Assert.IsNotNull(row, "Sun Shangxiang's roll move-speed Buff needs a HUD display row.");
            Assert.AreEqual("Speed", row.ShortName);
            Assert.IsTrue(row.ShowCountdown, "The roll Buff should show a countdown.");
        }

        [Test]
        public void HeroModels_ShouldBeAvailableThroughResources()
        {
            var characters = LoadArray<CharacterConfigRoot, CharacterDTO>(
                "luban/moba/characters",
                root => root.Items);
            var models = LoadArray<ModelConfigRoot, ModelDTO>(
                "luban/moba/models",
                root => root.Items);
            var modelsById = new Dictionary<int, ModelDTO>(models.Length);
            for (var i = 0; i < models.Length; i++)
            {
                var model = models[i];
                Assert.IsNotNull(model, "Model config contains a null entry at index " + i + ".");
                Assert.Greater(model.Id, 0, "Model config id must be positive at index " + i + ".");
                Assert.IsFalse(modelsById.ContainsKey(model.Id), "Duplicate model config id: " + model.Id);
                modelsById.Add(model.Id, model);
            }

            for (var i = 0; i < characters.Length; i++)
            {
                var character = characters[i];
                Assert.IsNotNull(character, "Character config contains a null entry at index " + i + ".");
                Assert.IsTrue(
                    modelsById.TryGetValue(character.ModelId, out var model),
                    "Character " + character.Id + " references missing model config: " + character.ModelId);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(model.PrefabPath),
                    "Model " + model.Id + " referenced by character " + character.Id + " has an empty prefab path.");
                Assert.IsNotNull(
                    Resources.Load<GameObject>(model.PrefabPath),
                    "Missing model resource for character " + character.Id + ": " + model.PrefabPath);
            }
        }

        [Test]
        public void HeroModels_ShouldExposeFrameSeekableAnimatorStates()
        {
            var characters = LoadArray<CharacterConfigRoot, CharacterDTO>(
                "luban/moba/characters",
                root => root.Items);
            var heroModelIds = new HashSet<int>();
            for (var i = 0; i < characters.Length; i++)
                heroModelIds.Add(characters[i].ModelId);

            var models = LoadArray<ModelConfigRoot, ModelDTO>(
                "luban/moba/models",
                root => root.Items);
            var requiredStates = new[] { "Idle", "Run", "Attack", "Stun", "Die" };

            for (var i = 0; i < models.Length; i++)
            {
                if (!heroModelIds.Contains(models[i].Id)) continue;

                var prefab = Resources.Load<GameObject>(models[i].PrefabPath);
                Assert.IsNotNull(prefab, "Missing hero model prefab: " + models[i].PrefabPath);
                var animator = prefab.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator, "Hero model has no Animator: " + models[i].PrefabPath);
                Assert.IsNotNull(animator.runtimeAnimatorController,
                    "Hero model Animator has no controller: " + models[i].PrefabPath);
                var controller = animator.runtimeAnimatorController as AnimatorController;
                Assert.IsNotNull(controller,
                    "Hero model Animator controller is not an AnimatorController asset: " + models[i].PrefabPath);

                var renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.IsNotEmpty(renderers,
                    "Hero model has no skinned mesh renderer: " + models[i].PrefabPath);
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    Assert.IsNotNull(renderer.sharedMesh,
                        "Hero model skinned renderer has no mesh: " + models[i].PrefabPath);
                    Assert.IsNotEmpty(renderer.bones,
                        "Hero model skinned renderer has no bones: " + models[i].PrefabPath);
                    Assert.IsNotNull(renderer.rootBone,
                        "Hero model skinned renderer has no root bone: " + models[i].PrefabPath);
                }

                for (var stateIndex = 0; stateIndex < requiredStates.Length; stateIndex++)
                {
                    var state = requiredStates[stateIndex];
                    Assert.IsTrue(
                        HasState(controller.layers[0].stateMachine, state),
                        "Hero model Animator is missing state " + state + ": " + models[i].PrefabPath);
                }
            }
        }

        [Test]
        public void PresentationModelCatalog_ShouldIncludeEveryConfiguredHeroModel()
        {
            var characters = LoadArray<CharacterConfigRoot, CharacterDTO>(
                "luban/moba/characters",
                root => root.Items);
            var configuredModels = LoadArray<ModelConfigRoot, ModelDTO>(
                "luban/moba/models",
                root => root.Items);
            var presentationModels = LoadArray<ModelConfigRoot, ModelDTO>(
                "moba/models",
                root => root.Items);
            var configuredById = new Dictionary<int, ModelDTO>(configuredModels.Length);
            var presentationById = new Dictionary<int, ModelDTO>(presentationModels.Length);

            for (var i = 0; i < configuredModels.Length; i++)
                configuredById.Add(configuredModels[i].Id, configuredModels[i]);
            for (var i = 0; i < presentationModels.Length; i++)
                presentationById.Add(presentationModels[i].Id, presentationModels[i]);

            for (var i = 0; i < characters.Length; i++)
            {
                // The Luban roster also contains minion archetypes; this contract covers the eight hero models.
                if (characters[i].Id < 1001 || characters[i].Id > 1008) continue;
                var modelId = characters[i].ModelId;
                Assert.IsTrue(configuredById.TryGetValue(modelId, out var configured),
                    "Character " + characters[i].Id + " references a missing Luban model: " + modelId);
                Assert.IsTrue(presentationById.TryGetValue(modelId, out var presentation),
                    "Presentation model catalog omits hero model: " + modelId);
                Assert.AreEqual(configured.PrefabPath, presentation.PrefabPath,
                    "Hero model resource path differs between the Luban config and presentation catalog: " + modelId);
            }
        }

        private static bool HasState(AnimatorStateMachine stateMachine, string stateName)
        {
            if (stateMachine == null) return false;

            var states = stateMachine.states;
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == stateName) return true;
            }

            var childMachines = stateMachine.stateMachines;
            for (var i = 0; i < childMachines.Length; i++)
            {
                if (HasState(childMachines[i].stateMachine, stateName)) return true;
            }

            return false;
        }

        [Test]
        public void AoeModels_ShouldResolveAndBeAvailableThroughResources()
        {
            var aoes = LoadArray<AoeConfigRoot, AoeDTO>("moba/aoes", root => root.Items);
            var models = LoadArray<ModelConfigRoot, ModelDTO>("luban/moba/models", root => root.Items);
            var modelsById = new Dictionary<int, ModelDTO>(models.Length);
            for (var i = 0; i < models.Length; i++)
            {
                var model = models[i];
                Assert.IsNotNull(model, "Model config contains a null entry at index " + i + ".");
                Assert.IsTrue(modelsById.ContainsKey(model.Id) == false, "Duplicate model config id: " + model.Id);
                modelsById.Add(model.Id, model);
            }

            for (var i = 0; i < aoes.Length; i++)
            {
                var aoe = aoes[i];
                Assert.IsNotNull(aoe, "AOE config contains a null entry at index " + i + ".");
                Assert.Greater(aoe.ModelId, 0, "AOE " + aoe.Id + " must reference a positive model id.");
                Assert.IsTrue(
                    modelsById.TryGetValue(aoe.ModelId, out var model),
                    "AOE " + aoe.Id + " references missing model config: " + aoe.ModelId);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(model.PrefabPath),
                    "Model " + model.Id + " referenced by AOE " + aoe.Id + " has an empty prefab path.");
                Assert.IsNotNull(
                    Resources.Load<GameObject>(model.PrefabPath),
                    "Missing model resource for AOE " + aoe.Id + ": " + model.PrefabPath);
            }
        }

        [Test]
        public void ConfiguredEffects_ShouldBeAvailableThroughResources()
        {
            var entries = LoadArray<VfxConfigRoot, VfxDTO>("vfx/vfx", root => root.Items);
            var ids = new HashSet<int>();
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                Assert.IsNotNull(entry, "VFX config contains a null entry at index " + i + ".");
                Assert.Greater(entry.Id, 0, "VFX config id must be positive at index " + i + ".");
                Assert.IsTrue(ids.Add(entry.Id), "Duplicate VFX config id: " + entry.Id);
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(entry.Resource),
                    "VFX config " + entry.Id + " has an empty resource path.");
                Assert.IsNotNull(
                    Resources.Load<GameObject>(entry.Resource),
                    "Missing VFX resource for config " + entry.Id + ": " + entry.Resource);
            }
        }

        private static TItem[] LoadArray<TRoot, TItem>(
            string resourcePath,
            Func<TRoot, TItem[]> selectItems)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            Assert.IsNotNull(asset, "Missing JSON resource: " + resourcePath);

            var root = JsonUtility.FromJson<TRoot>("{\"Items\":" + asset.text + "}");
            Assert.IsNotNull(root, "Failed to parse JSON resource: " + resourcePath);
            var items = selectItems(root);
            Assert.IsNotNull(items, "JSON resource contains no array: " + resourcePath);
            Assert.IsNotEmpty(items, "JSON resource contains an empty array: " + resourcePath);
            return items;
        }

        [TestCase(1002, 10020101, 30020001, 90002011)]
        [TestCase(1004, 10040101, 30040011, 90004011)]
        [TestCase(1005, 10050101, 30050001, 90005011)]
        [TestCase(1006, 10060101, 30060001, 90006011)]
        public void RangedBasicAttackProjectiles_ShouldUseConfiguredVfx(
            int heroId,
            int activeSkillId,
            int projectileId,
            int expectedVfxId)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       skillIds: new[] { activeSkillId },
                       worldId: "presentation_resource_" + heroId,
                       heroId: heroId,
                       attributeTemplateId: heroId))
            {
                Assert.IsTrue(harness.Config.TryGetProjectile(projectileId, out var projectile), "Missing projectile config: " + projectileId);
                Assert.AreEqual(expectedVfxId, projectile.VfxId, "Basic attack projectile should reference its matching VFX.");
            }
        }

        [Test]
        public void LubanAirshipSummon_ShouldUseDedicatedModelPrefab()
        {
            var summons = LoadArray<SummonConfigRoot, SummonDTO>(
                "luban/moba/summons",
                root => root.Items);
            var models = LoadArray<ModelConfigRoot, ModelDTO>(
                "luban/moba/models",
                root => root.Items);
            var characters = LoadArray<CharacterConfigRoot, CharacterDTO>(
                "luban/moba/characters",
                root => root.Items);
            var airship = Array.Find(summons, item => item != null && item.Id == 1008);
            var luban = Array.Find(characters, item => item != null && item.Id == 1008);
            Assert.IsNotNull(airship, "Luban airship summon config is missing.");
            Assert.IsNotNull(luban, "Luban character config is missing.");
            Assert.AreNotEqual(luban.ModelId, airship.ModelId,
                "The airship summon must not reuse Luban's hero model.");

            var model = Array.Find(models, item => item != null && item.Id == airship.ModelId);
            Assert.IsNotNull(model, "Luban airship model config is missing: " + airship.ModelId);
            Assert.AreEqual("moba/airship/luban_airship", model.PrefabPath);
            Assert.IsNotNull(Resources.Load<GameObject>(model.PrefabPath),
                "Missing Luban airship model resource: " + model.PrefabPath);
        }

        [Test]
        public void SunShangxiangAndLubanPresentation_ShouldResolveDedicatedVfxPrefabs()
        {
            var expected = new Dictionary<int, string>
            {
                { 90007001, "effect/sunshangxiang_skill1_roll" },
                { 90007002, "effect/sunshangxiang_skill2_grenade" },
                { 90007003, "effect/sunshangxiang_skill2_explosion" },
                { 90007004, "effect/sunshangxiang_skill3_cannon" },
                { 90007005, "effect/sunshangxiang_buff_enhanced" },
                { 90007006, "effect/sunshangxiang_buff_mark" },
                { 90008001, "effect/luban_basic_bullet" },
                { 90008002, "effect/luban_skill1_grenade" },
                { 90008003, "effect/luban_skill1_explosion" },
                { 90008004, "effect/luban_skill2_cannon" },
                { 90008005, "effect/luban_passive_sweep" },
                { 90008006, "effect/luban_skill3_bomb" },
            };
            var entries = LoadArray<VfxConfigRoot, VfxDTO>("vfx/vfx", root => root.Items);
            var byId = new Dictionary<int, VfxDTO>();
            foreach (var entry in entries) byId.Add(entry.Id, entry);
            foreach (var pair in expected)
            {
                Assert.IsTrue(byId.TryGetValue(pair.Key, out var vfx), "Missing dedicated VFX config " + pair.Key);
                Assert.AreEqual(pair.Value, vfx.Resource, "Dedicated VFX resource path mismatch for " + pair.Key);
                Assert.IsNotNull(Resources.Load<GameObject>(vfx.Resource), "Missing dedicated VFX prefab " + vfx.Resource);
            }
        }

        [TestCase(10010101, "buff", 10010000)]
        [TestCase(10010201, "aoe", 40010201)]
        [TestCase(10010301, "aoe", 40010301)]
        [TestCase(10020101, "projectile", 30020101)]
        [TestCase(10020201, "aoe", 40020201)]
        [TestCase(10020301, "buff", 10020301)]
        [TestCase(10030101, "buff", 10030101)]
        [TestCase(10030201, "aoe", 40030201)]
        [TestCase(10030301, "aoe", 40030301)]
        [TestCase(10040101, "buff", 10040101)]
        [TestCase(10040201, "projectile", 30040201)]
        [TestCase(10040301, "aoe", 40040301)]
        [TestCase(10050101, "projectile", 30050101)]
        [TestCase(10050201, "projectile", 30050201)]
        [TestCase(10050301, "buff", 10050301)]
        [TestCase(10060101, "aoe", 40060101)]
        [TestCase(10060201, "buff", 10060201)]
        [TestCase(10060301, "projectile", 30060301)]
        [TestCase(10070101, "buff", 10070101)]
        [TestCase(10070201, "projectile", 30070202)]
        [TestCase(10070301, "projectile", 30070302)]
        [TestCase(10080101, "projectile", 30080101)]
        [TestCase(10080201, "projectile", 30080201)]
        [TestCase(10080301, "projectile", 30080401)]
        public void EightHeroActiveSkills_ShouldResolveReferencedVfx(int skillId, string referenceKind, int referenceId)
        {
            var skills = LoadArray<SkillConfigRoot, SkillDTO>("luban/moba/skills", root => root.Items);
            Assert.IsNotNull(Array.Find(skills, item => item != null && item.Id == skillId),
                "Configured active skill is missing: " + skillId);

            var vfxId = ResolveReferencedVfx(referenceKind, referenceId);
            Assert.Greater(vfxId, 0,
                "Skill " + skillId + " reference " + referenceKind + " " + referenceId + " has no VFX.");

            var entries = LoadArray<VfxConfigRoot, VfxDTO>("vfx/vfx", root => root.Items);
            var vfx = Array.Find(entries, item => item != null && item.Id == vfxId);
            Assert.IsNotNull(vfx, "Skill " + skillId + " references missing VFX config: " + vfxId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(vfx.Resource),
                "Skill " + skillId + " VFX has an empty resource path: " + vfxId);
            Assert.IsNotNull(Resources.Load<GameObject>(vfx.Resource),
                "Skill " + skillId + " VFX prefab is missing: " + vfx.Resource);
        }

        [Test]
        public void EveryConfiguredCharacterSkill_ShouldHaveLoadablePressPreviewVfx()
        {
            var characters = LoadArray<CharacterConfigRoot, CharacterDTO>(
                "luban/moba/characters", root => root.Items);
            var skills = LoadArray<SkillConfigRoot, SkillDTO>(
                "luban/moba/skills", root => root.Items);
            var vfxEntries = LoadArray<VfxConfigRoot, VfxDTO>("vfx/vfx", root => root.Items);
            var skillsById = new Dictionary<int, SkillDTO>();
            var vfxById = new Dictionary<int, VfxDTO>();
            foreach (var skill in skills) if (skill != null) skillsById[skill.Id] = skill;
            foreach (var vfx in vfxEntries) if (vfx != null) vfxById[vfx.Id] = vfx;

            foreach (var character in characters)
            {
                if (character == null || character.SkillIds == null) continue;
                foreach (var skillId in character.SkillIds)
                {
                    Assert.IsTrue(skillsById.TryGetValue(skillId, out var skill),
                        "Character " + character.Id + " references missing skill " + skillId);
                    Assert.Greater(skill.PreviewVfxId, 0,
                        "Character " + character.Id + " skill " + skillId + " has no press-preview VFX.");
                    Assert.IsTrue(vfxById.TryGetValue(skill.PreviewVfxId, out var vfx),
                        "Skill " + skillId + " references missing preview VFX " + skill.PreviewVfxId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(vfx.Resource),
                        "Skill " + skillId + " preview VFX has no resource path.");
                    Assert.IsNotNull(Resources.Load<GameObject>(vfx.Resource),
                        "Skill " + skillId + " preview prefab is missing: " + vfx.Resource);
                }
            }
        }

        private static int ResolveReferencedVfx(string referenceKind, int referenceId)
        {
            switch (referenceKind)
            {
                case "aoe":
                    var aoes = LoadArray<AoeConfigRoot, AoeDTO>("luban/moba/aoes", root => root.Items);
                    var aoe = Array.Find(aoes, item => item != null && item.Id == referenceId);
                    Assert.IsNotNull(aoe, "Missing AOE config: " + referenceId);
                    return aoe.VfxId;
                case "projectile":
                    var projectiles = LoadArray<ProjectileConfigRoot, ProjectileDTO>(
                        "luban/moba/projectiles", root => root.Items);
                    var projectile = Array.Find(projectiles, item => item != null && item.Id == referenceId);
                    Assert.IsNotNull(projectile, "Missing projectile config: " + referenceId);
                    return projectile.VfxId > 0 ? projectile.VfxId : projectile.OnHitVfxId;
                case "buff":
                    var buffs = LoadArray<BuffConfigRoot, BuffDTO>("luban/moba/buffs", root => root.Items);
                    var buff = Array.Find(buffs, item => item != null && item.Id == referenceId);
                    Assert.IsNotNull(buff, "Missing buff config: " + referenceId);
                    var templates = LoadArray<PresentationTemplateConfigRoot, PresentationTemplateDTO>(
                        "luban/moba/presentation_templates", root => root.Items);
                    var template = Array.Find(templates, item => item != null && item.Id == buff.PresentationTemplateId);
                    Assert.IsNotNull(template, "Buff " + referenceId + " references missing presentation template: " +
                        buff.PresentationTemplateId);
                    return template.AssetId > 0 ? template.AssetId : buff.PresentationTemplateId;
                default:
                    Assert.Fail("Unknown VFX reference kind: " + referenceKind);
                    return 0;
            }
        }
    }
}
