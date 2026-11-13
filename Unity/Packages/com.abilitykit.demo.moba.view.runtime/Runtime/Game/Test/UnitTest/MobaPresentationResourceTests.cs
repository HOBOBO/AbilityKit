using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
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

        [Serializable]
        private sealed class TagHudConfigRoot
        {
            public TagHudDTO[] Items;
        }

        [Serializable]
        private sealed class TagHudDTO
        {
            public int DefinitionId;
            public bool Visible;
            public string DisplayName;
            public string LocalizationKey;
            public int ChannelMask;
            public string HeadText;
            public int HeadTextDurationMs;
            public int VfxId;
            public int VfxDurationMs;
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
        public void TagHudPresentations_ShouldCoverSuperArmorAndAirborne()
        {
            var rows = LoadArray<TagHudConfigRoot, TagHudDTO>(
                "luban/moba/tag_hud_presentations",
                root => root.Items);

            var superArmor = Array.Find(rows, item => item != null && item.DefinitionId == 1002);
            var airborne = Array.Find(rows, item => item != null && item.DefinitionId == 1504);
            Assert.IsNotNull(superArmor, "SuperArmor needs a production tag presentation row.");
            Assert.IsNotNull(airborne, "Airborne needs a production tag presentation row.");
            Assert.AreEqual(3, superArmor.ChannelMask);
            Assert.AreEqual(3, airborne.ChannelMask);
            Assert.AreEqual("霸体", superArmor.HeadText);
            Assert.AreEqual("击飞", airborne.HeadText);
            Assert.Greater(superArmor.HeadTextDurationMs, 0);
            Assert.Greater(airborne.HeadTextDurationMs, 0);
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

                var requiredStates = RequiredAnimatorStates(models[i].Id);
                for (var stateIndex = 0; stateIndex < requiredStates.Length; stateIndex++)
                {
                    var state = requiredStates[stateIndex];
                    Assert.IsTrue(
                        HasState(controller.layers[0].stateMachine, state),
                        "Hero model Animator is missing state " + state + ": " + models[i].PrefabPath);
                }
            }
        }

        private static string[] RequiredAnimatorStates(int modelId)
        {
            // Jhin's linked controller uses the production clip names from game-res;
            // it deliberately has no placeholder Run/Attack/Stun/Die states.
            if (modelId == 3001)
            {
                return new[]
                {
                    "Idle", "AutoAttack_1", "AutoAttack_2", "AutoAttack_3", "AutoAttack_4",
                    "AutoAttack_Reload", "Spell_Q", "Spell_W", "Spell_E", "Spell_R_Deploy",
                    "Spell_R_Idle", "Spell_R_Shoot", "Spell_R_ShootLast", "Death"
                };
            }

            if (modelId == 3003)
            {
                return new[]
                {
                    "Void_Annie_Model_Idle", "Void_Annie_Model_AutoAttack",
                    "Void_Annie_Model_SpellQ", "Void_Annie_Model_SpellW",
                    "Void_Annie_Model_SpellE", "Void_Annie_Model_SpellR",
                    "Void_Annie_Model_Death"
                };
            }

            if (modelId == 3002)
            {
                return new[]
                {
                    "Idle_Minigun", "Idle_Rocket", "AA_Minigun", "AA_Rocket",
                    "Spell_Q_RocketToMini", "Spell_Q_MiniToRocket", "Spell_W", "Spell_E", "Spell_R"
                };
            }

            if (modelId == 3004)
            {
                return new[]
                {
                    "Veigar_Model_Idle", "Veigar_Model_AA", "Veigar_Model_SpellQ",
                    "Veigar_Model_SpellW", "Veigar_Model_SpellE", "Veigar_Model_SpellR"
                };
            }

            if (modelId == 3005)
            {
                return new[]
                {
                    "Gragas_Model_Idle", "Gragas_Model_AutoAttack", "Gragas_Model_AutoAttack_2",
                    "Gragas_Model_SpellQ", "Gragas_Model_SpellW", "Gragas_Model_SpellE",
                    "Gragas_Model_SpellR", "Walk"
                };
            }

            return new[] { "Idle", "Run", "Attack", "Stun", "Die" };
        }

        [Test]
        public void AuthoritativeModelCatalog_ShouldIncludeEveryConfiguredHeroModel()
        {
            var characters = LoadArray<CharacterConfigRoot, CharacterDTO>(
                "luban/moba/characters",
                root => root.Items);
            var configuredModels = LoadArray<ModelConfigRoot, ModelDTO>(
                "luban/moba/models",
                root => root.Items);
            var configuredById = new Dictionary<int, ModelDTO>(configuredModels.Length);

            for (var i = 0; i < configuredModels.Length; i++)
                configuredById.Add(configuredModels[i].Id, configuredModels[i]);

            for (var i = 0; i < characters.Length; i++)
            {
                // The Luban roster also contains minion archetypes. Cover the
                // configured heroes with formal presentation mappings.
                if ((characters[i].Id < 1001 || characters[i].Id > 1008) &&
                    characters[i].Id != 3001 && characters[i].Id != 3002 &&
                    characters[i].Id != 3003 && characters[i].Id != 3004 &&
                    characters[i].Id != 3005) continue;
                var modelId = characters[i].ModelId;
                Assert.IsTrue(configuredById.TryGetValue(modelId, out var configured),
                    "Character " + characters[i].Id + " references a missing Luban model: " + modelId);
                Assert.IsNotNull(Resources.Load<GameObject>(configured.PrefabPath),
                    "Configured hero model prefab is not available: " + configured.PrefabPath);
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

        [Test]
        public void AnnieAndTibbersLinkedResources_ShouldLoadModelsAndLifecycleVfx()
        {
            var models = LoadArray<ModelConfigRoot, ModelDTO>("luban/moba/models", root => root.Items);
            var annieModel = Array.Find(models, item => item != null && item.Id == 3003);
            var tibbersModel = Array.Find(models, item => item != null && item.Id == 30030502);
            Assert.IsNotNull(annieModel, "Annie model config is missing.");
            Assert.IsNotNull(tibbersModel, "Tibbers model config is missing.");
            Assert.IsNotNull(Resources.Load<GameObject>(annieModel.PrefabPath),
                "Annie model linked prefab is missing: " + annieModel.PrefabPath);
            Assert.IsNotNull(Resources.Load<GameObject>(tibbersModel.PrefabPath),
                "Tibbers model linked prefab is missing: " + tibbersModel.PrefabPath);
            var tibbersPrefab = Resources.Load<GameObject>(tibbersModel.PrefabPath);
            var tibbersAnimator = tibbersPrefab.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(tibbersAnimator, "Tibbers model has no Animator.");
            var tibbersController = tibbersAnimator.runtimeAnimatorController as AnimatorController;
            Assert.IsNotNull(tibbersController, "Tibbers Animator controller is not an AnimatorController asset.");
            foreach (var state in new[]
                     { "Void_Annie_Tibbers_Idle", "Void_Annie_Tibbers_Run",
                       "Void_Annie_Tibbers_Attack1", "Void_Annie_Tibbers_Attack2",
                       "Void_Annie_Tibbers_Death" })
                Assert.IsTrue(HasState(tibbersController.layers[0].stateMachine, state),
                    "Tibbers Animator is missing state " + state);

            var expectedVfx = new Dictionary<int, string>
            {
                { 90033001, "ChampionVfx/14.Void_Annie/Void_Annie_Q_Projectile" },
                { 90033002, "ChampionVfx/14.Void_Annie/Void_Annie_W_Area" },
                { 90033003, "ChampionVfx/14.Void_Annie/Void_Annie_Q_Hit" },
                { 90033004, "moba/AnnieShield" },
                { 90033005, "ChampionVfx/14.Void_Annie/Void_Annie_R_Summon" },
                { 90033006, "ChampionVfx/14.Void_Annie/Void_Annie_Tibbers_Spawn" },
            };
            var entries = LoadArray<VfxConfigRoot, VfxDTO>("vfx/vfx", root => root.Items);
            foreach (var pair in expectedVfx)
            {
                var entry = Array.Find(entries, item => item != null && item.Id == pair.Key);
                Assert.IsNotNull(entry, "Missing Annie VFX config: " + pair.Key);
                Assert.AreEqual(pair.Value, entry.Resource, "Annie VFX path mismatch: " + pair.Key);
                var prefab = Resources.Load<GameObject>(entry.Resource);
                Assert.IsNotNull(prefab, "Missing Annie linked VFX prefab: " + entry.Resource);
                if (pair.Key == 90033004)
                {
                    var shield = prefab.transform.Find("E_Shield");
                    Assert.IsNotNull(shield, "Annie's shield variant must retain its linked shield child.");
                    Assert.IsTrue(shield.gameObject.activeSelf,
                        "Annie's shield variant must enable the source prefab's disabled shield child.");
                }
            }

            var summons = LoadArray<SummonConfigRoot, SummonDTO>(
                "luban/moba/summons", root => root.Items);
            var tibbers = Array.Find(summons, item => item != null && item.Id == 30030502);
            Assert.IsNotNull(tibbers, "Tibbers summon config is missing.");
            Assert.AreEqual(30030502, tibbers.ModelId,
                "Tibbers must use the linked Tibbers model config.");
            Assert.AreEqual(90033006, tibbers.SpawnVfxId,
                "Tibbers spawn must be driven by its authored lifecycle VFX.");
            Assert.AreEqual(0, tibbers.DeathVfxId,
                "Tibbers death VFX remains unset until a dedicated linked asset exists.");
            Assert.AreEqual(0, tibbers.DespawnVfxId,
                "Tibbers despawn VFX remains unset until a dedicated linked asset exists.");
        }

        [Test]
        public void AnnieAndTibbersActionCatalog_ShouldResolveLinkedAnimatorStates()
        {
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            AssertPlayback(catalog, 3003, "life/alive/action/idle", 0, "Void_Annie_Model_Idle");
            AssertPlayback(catalog, 3003, "life/alive/action/attack.slot.1", 0, "Void_Annie_Model_AutoAttack");
            AssertPlayback(catalog, 3003, "life/alive/action/casting", 30030201, "Void_Annie_Model_SpellQ");
            AssertPlayback(catalog, 3003, "life/alive/action/casting", 30030501, "Void_Annie_Model_SpellR");
            AssertPlayback(catalog, 30030502, "life/alive/action/moving", 0, "Void_Annie_Tibbers_Run");
            AssertPlayback(catalog, 30030502, "life/alive/action/attack.slot.2", 0, "Void_Annie_Tibbers_Attack2");
        }

        [Test]
        public void VeigarActionCatalog_ShouldResolveLinkedAnimatorStates()
        {
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            AssertPlayback(catalog, 3004, "life/alive/action/idle", 0, "Veigar_Model_Idle");
            AssertPlayback(catalog, 3004, "life/alive/action/moving", 0, "Base Layer.Walk");
            AssertPlayback(catalog, 3004, "life/alive/action/casting", 30040101, "Veigar_Model_AA");
            AssertPlayback(catalog, 3004, "life/alive/action/casting", 30040201, "Veigar_Model_SpellQ");
            AssertPlayback(catalog, 3004, "life/alive/action/casting", 30040301, "Veigar_Model_SpellW");
            AssertPlayback(catalog, 3004, "life/alive/action/casting", 30040401, "Veigar_Model_SpellE");
            AssertPlayback(catalog, 3004, "life/alive/action/casting", 30040501, "Veigar_Model_SpellR");
        }

        [Test]
        public void VeigarPresentationResources_ShouldResolveFormalModelAndSkillVfx()
        {
            var models = LoadArray<ModelConfigRoot, ModelDTO>("luban/moba/models", root => root.Items);
            var model = Array.Find(models, item => item != null && item.Id == 3004);
            Assert.IsNotNull(model, "Veigar model config is missing.");
            var prefab = Resources.Load<GameObject>(model.PrefabPath);
            Assert.IsNotNull(prefab, "Veigar model prefab is missing: " + model.PrefabPath);
            Assert.IsNotNull(prefab.GetComponentInChildren<Animator>(true), "Veigar model has no Animator.");
            Assert.IsNotEmpty(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true),
                "Veigar model has no skinned mesh renderer.");

            var vfx = LoadArray<VfxConfigRoot, VfxDTO>("vfx/vfx", root => root.Items);
            var expected = new Dictionary<int, string>
            {
                { 90034001, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_AA_Projectile" },
                { 90034002, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_AA_Hit" },
                { 90034003, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_E" },
                { 90034004, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_E_Activation" },
                { 90034005, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_E_Cage" },
                { 90034008, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_Q_Projectile" },
                { 90034011, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_R_Projectile" },
                { 90034013, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_W_Cast" },
                { 90034014, "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_W_Explosion" },
            };
            foreach (var pair in expected)
            {
                var entry = Array.Find(vfx, item => item != null && item.Id == pair.Key);
                Assert.IsNotNull(entry, "Veigar VFX config is missing: " + pair.Key);
                Assert.AreEqual(pair.Value, entry.Resource, "Veigar VFX path mismatch: " + pair.Key);
                Assert.IsNotNull(Resources.Load<GameObject>(entry.Resource),
                    "Veigar VFX prefab is missing: " + entry.Resource);
            }
        }

        private static void AssertPlayback(CharacterPresentationActionCatalog catalog, int entityCode,
            string path, int skillId, string expectedAnimatorState)
        {
            var runner = new CharacterPresentationActionRunner(catalog.CreateForEntityCode(entityCode));
            var playback = runner.Evaluate(path, entityCode, 0, 0, 30, null, skillId);
            Assert.IsTrue(playback.HasValue, entityCode + " action did not resolve: " + path);
            Assert.AreEqual(expectedAnimatorState, playback.Value.AnimatorState,
                entityCode + " action resolved the wrong animator state: " + path);
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

        [Test]
        public void LinkedChampionResources_ShouldLoadModelsAndJhinAbilityPrefabs()
        {
            var requiredPrefabs = new[]
            {
                "ChampionVfx/08.Jinx_Arcanist/Jinx_Arcanist_Model",
                "ChampionVfx/09.Veigar_Hextech/Veigar_Hextech_Model",
                "ChampionVfx/10.Gragas_StarGuardian/Gragas_StarGuardian_Model",
                "ChampionVfx/11.Karthus_Project/Karthus_Project_Model",
                "ChampionVfx/12.Lux_DarkStar/Lux_DarkStar_Model",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Model",
                "ChampionVfx/14.Void_Annie/Void_Annie_Model",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_W",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_E",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Cast",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Projectile",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Last_Projectile",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Hit",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_HitLast",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q_Hit_1",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q_Hit_2",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q_Hit_3",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q_Hit_4",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_W_Hit",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_E_Hit",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_AA_Hit",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_AA_HitLast",
            };

            for (var i = 0; i < requiredPrefabs.Length; i++)
            {
                var prefab = Resources.Load<GameObject>(requiredPrefabs[i]);
                Assert.IsNotNull(prefab, "Missing linked champion prefab: " + requiredPrefabs[i]);

                var behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                for (var behaviourIndex = 0; behaviourIndex < behaviours.Length; behaviourIndex++)
                {
                    Assert.IsNotNull(behaviours[behaviourIndex],
                        "Linked champion prefab contains a Missing Script: " + requiredPrefabs[i]);
                }
            }
        }

        [Test]
        public void JhinHitVfxPrefabs_ShouldContainParticleSystems()
        {
            var requiredHitPrefabs = new[]
            {
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q_Hit_1",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q_Hit_2",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q_Hit_3",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q_Hit_4",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_W_Hit",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_E_Hit",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Hit",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_HitLast",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_AA_Hit",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_AA_HitLast",
            };

            for (var i = 0; i < requiredHitPrefabs.Length; i++)
            {
                var prefab = Resources.Load<GameObject>(requiredHitPrefabs[i]);
                Assert.IsNotNull(prefab, "Missing Jhin hit VFX prefab: " + requiredHitPrefabs[i]);
                Assert.IsNotEmpty(prefab.GetComponentsInChildren<ParticleSystem>(true),
                    "Jhin hit VFX prefab must contain a particle system: " + requiredHitPrefabs[i]);
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
