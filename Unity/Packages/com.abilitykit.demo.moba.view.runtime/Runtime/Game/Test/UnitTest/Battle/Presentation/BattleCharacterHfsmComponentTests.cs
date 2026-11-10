using System;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow;
using AbilityKit.Modifiers;
using NUnit.Framework;

namespace AbilityKit.Game.Tests
{
    public sealed class BattleCharacterHfsmComponentTests
    {
        [Test]
        public void PresentationActionProjectsWithoutLogicWorld()
        {
            var view = new BattleCharacterHfsmComponent();
            var action = new CharacterActionData(41, 1, 105, "life/alive/action/idle",
                ((long)41 << 32) | 100u, 100, 5, 77, 7001);
            view.ApplyPresentation(in action);

            Assert.AreEqual(105, view.SnapshotFrame);
            Assert.AreEqual(77, view.Action.CastInstanceId);
            Assert.IsNotNull(view.Evaluate(106, 30));
            Assert.IsNull(view.Evaluate(104, 30));
        }

        [Test]
        public void CharacterActionProjectionRejectsStaleVersionAndFrame()
        {
            var context = new BattleContext();
            context.ObserveActorSpawnIdentity(41, 2);
            var current = new CharacterActionData(41, 2, 105, "life/alive/action/casting",
                ((long)41 << 32) | 100u, 100, 5, 77, 7001);
            var stale = new CharacterActionData(41, 2, 104, "life/alive/action/idle",
                ((long)41 << 32) | 100u, 100, 4, 0, 0);
            context.ApplyCharacterActions(new[] { current, stale });
            Assert.IsTrue(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out var projected));
            Assert.AreEqual("life/alive/action/casting", projected.Path);

            context.ObserveActorSpawnIdentity(41, 3);
            Assert.IsFalse(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out _));
            context.ForgetActorIdentity(41);
        }

        [Test]
        public void CharacterActionProjectionWaitsForMatchingSpawnAfterReconnect()
        {
            var context = new BattleContext();
            var action = new CharacterActionData(41, 3, 105, "life/alive/action/casting",
                ((long)41 << 32) | 100u, 100, 5, 77, 7001);
            context.ApplyCharacterActions(new[] { action });

            Assert.IsFalse(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out _));
            context.ObserveActorSpawnIdentity(41, 2);
            Assert.IsFalse(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out _));
            context.ObserveActorSpawnIdentity(41, 3);
            Assert.IsTrue(((IBattleCharacterActionReadPort)context).TryGetCharacterAction(41, out var projected));
            Assert.AreEqual(105, projected.SnapshotFrame);
        }

        [Test]
        public void SnapshotRestoresCastingFrameWithoutTransitionReplay()
        {
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            using (var logic = new MobaCharacterHfsmRuntime(41, definition, 0, Fixed64.Zero))
            {
                logic.Tick(1, Fixed64.FromRaw(1000), true, false, false, 100, 7001);
                logic.Tick(2, Fixed64.FromRaw(2000), true, false, false, 100, 7001);
                var view = new BattleCharacterHfsmComponent(definition);
                view.ApplySnapshot(logic.CaptureSnapshot());
                Assert.AreEqual("life/alive/action/casting", view.Action.Path);
                Assert.AreEqual(1, view.Action.LocalFrame);
                Assert.AreEqual(logic.DefinitionHash, view.DefinitionHash);

                logic.Tick(3, Fixed64.FromRaw(3000), true, false, false, 100, 7001);
                view.ApplySnapshot(logic.CaptureSnapshot());
                Assert.AreEqual(2, view.Action.LocalFrame);
            }
        }

        [Test]
        public void JhinFourthAttackCompletionGateAndControlRecoveryUseParameters()
        {
            const int actorId = 41;
            const long slotSource = 30010604L;
            const long committedSource = 3001060401L;
            const long controlSource = 3001060402L;
            const long completionRaw = 15569256448L;
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            var parameters = new MobaCharacterHfsmParameterService();
            parameters.RegisterActorProfile(actorId, definition);

            using (var runtime = new MobaCharacterHfsmRuntime(actorId, definition, 0, Fixed64.Zero,
                       registerForInspection: false, parameters: parameters))
            {
                var slot = ModifierData.Override(ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot),
                    4f, sourceId: unchecked((int)slotSource));
                parameters.Set(actorId, slotSource, MobaCharacterHfsmTestFixture.AttackSlot, in slot);
                runtime.ReevaluateCurrentFrame();
                StringAssert.EndsWith("action/attack.slot.4", runtime.Action.Path);

                // The launch/commit result changes the next slot, but the authored fourth-shot
                // action cannot leave before its configured 87/24-second minimum.
                slot = ModifierData.Override(ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot),
                    5f, sourceId: unchecked((int)committedSource));
                parameters.Set(actorId, committedSource, MobaCharacterHfsmTestFixture.AttackSlot, in slot);
                runtime.Tick(1, Fixed64.FromRaw(completionRaw - 1), true, false, false, 0, 0);
                StringAssert.EndsWith("action/attack.slot.4", runtime.Action.Path,
                    "A fourth-shot state must honor its minimum active duration.");
                runtime.Tick(2, Fixed64.FromRaw(completionRaw), true, false, false, 0, 0);
                StringAssert.EndsWith("action/attack.slot.5", runtime.Action.Path,
                    "The committed slot must drive the post-fourth-shot transition after the gate.");
            }

            // A hard-control interruption is immediate. Once control is removed, the same
            // persistent slot parameter selects reload; no animation completion callback is used.
            // The prior scenario committed slot 5. Remove that source before setting up a fresh
            // pre-commit slot-4 scenario so the test does not retain two competing overrides.
            parameters.ClearSource(actorId, committedSource);
            using (var runtime = new MobaCharacterHfsmRuntime(actorId, definition, 0, Fixed64.Zero,
                       registerForInspection: false, parameters: parameters))
            {
                var slot = ModifierData.Override(ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot),
                    4f, sourceId: unchecked((int)slotSource));
                parameters.Set(actorId, slotSource, MobaCharacterHfsmTestFixture.AttackSlot, in slot);
                runtime.ReevaluateCurrentFrame();
                runtime.Tick(3, Fixed64.FromRaw(1), true, true, false, 0, 0);
                StringAssert.EndsWith("action/controlled", runtime.Action.Path,
                    "Control must preempt the fourth-shot action immediately.");

                slot = ModifierData.Override(ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot),
                    5f, sourceId: unchecked((int)committedSource));
                parameters.Set(actorId, committedSource, MobaCharacterHfsmTestFixture.AttackSlot, in slot);
                runtime.Tick(4, Fixed64.FromRaw(2), true, false, false, 0, 0);
                Assert.AreEqual("life/alive/action/idle", runtime.Action.Path,
                    "Control release first exits the controlled state before selecting the persisted slot.");
                runtime.Tick(5, Fixed64.FromRaw(3), true, false, false, 0, 0);
                StringAssert.EndsWith("action/attack.slot.5", runtime.Action.Path,
                    "On the next HFSM evaluation after control, the effective slot parameter must select reload automatically.");
            }

            parameters.ClearSource(actorId, slotSource);
            parameters.ClearSource(actorId, committedSource);
            parameters.ClearSource(actorId, controlSource);
        }

        [Test]
        public void JhinFourthAttackControlBeforeCommitResumesFromPersistedSlot()
        {
            const int actorId = 42;
            const long slotSource = 3001060403L;
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            var parameters = new MobaCharacterHfsmParameterService();
            parameters.RegisterActorProfile(actorId, definition);
            var slot = ModifierData.Override(ModifierKey.FromPacked(MobaCharacterHfsmTestFixture.AttackSlot),
                4f, sourceId: unchecked((int)slotSource));
            parameters.Set(actorId, slotSource, MobaCharacterHfsmTestFixture.AttackSlot, in slot);

            using (var runtime = new MobaCharacterHfsmRuntime(actorId, definition, 0, Fixed64.Zero,
                       registerForInspection: false, parameters: parameters))
            {
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/attack.slot.4", runtime.Action.Path);

                runtime.Tick(1, Fixed64.FromRaw(1), true, true, false, 0, 0);
                Assert.AreEqual("life/alive/action/controlled", runtime.Action.Path);

                // Release first exits controlled; the following HFSM evaluation uses the
                // still-persisted slot-4 parameter and does not invent a committed shot.
                runtime.Tick(2, Fixed64.FromRaw(2), true, false, false, 0, 0);
                Assert.AreEqual("life/alive/action/idle", runtime.Action.Path);
                runtime.Tick(3, Fixed64.FromRaw(3), true, false, false, 0, 0);
                Assert.AreEqual("life/alive/action/attack.slot.4", runtime.Action.Path);
            }

            parameters.ClearSource(actorId, slotSource);
        }

        [Test]
        public void JinxWeaponModeIsHeroLocalAndResumesAfterMovementAndCast()
        {
            const int actorId = 43;
            const int weaponModeParameterId = 4;
            const int modeSkillId = 30020101;
            const long modeSourceId = modeSkillId;
            const long castInstanceId = 7043L;
            var loader = new ResourcesTextAssetLoader();
            var definition = MobaCharacterHfsmProfile.ResolveDefinition(loader, 3002);
            Assert.AreEqual("moba.character.3002", definition.DefinitionId);
            Assert.IsTrue(definition.Parameters.Exists(parameter => parameter.Id == weaponModeParameterId &&
                parameter.Name == "WeaponMode" && parameter.DefaultValue == 0f));
            Assert.IsFalse(MobaCharacterHfsmTestFixture.CreateDefinition().Parameters.Exists(
                parameter => parameter.Id == weaponModeParameterId),
                "Jinx's WeaponMode option must not be added to Jhin's HFSM definition.");

            var parameters = new MobaCharacterHfsmParameterService();
            parameters.RegisterActorProfile(actorId, definition);
            using (var runtime = new MobaCharacterHfsmRuntime(actorId, definition, 0, Fixed64.Zero,
                       registerForInspection: false, parameters: parameters))
            {
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/weapon.powpow", runtime.Action.Path);

                var mode = ModifierData.Override(ModifierKey.FromPacked(weaponModeParameterId), 1f,
                    sourceId: unchecked((int)modeSourceId));
                parameters.Set(actorId, modeSourceId, weaponModeParameterId, in mode);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/weapon.fishbones", runtime.Action.Path);

                runtime.Tick(1, Fixed64.FromRaw(1000), true, false, true, 0, 0);
                Assert.AreEqual("life/alive/action/moving", runtime.Action.Path);
                mode = ModifierData.Override(ModifierKey.FromPacked(weaponModeParameterId), 0f,
                    sourceId: unchecked((int)modeSourceId));
                parameters.Set(actorId, modeSourceId, weaponModeParameterId, in mode);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/moving", runtime.Action.Path,
                    "Changing weapon mode must not interrupt movement.");
                runtime.Tick(2, Fixed64.FromRaw(2000), true, false, false, 0, 0);
                Assert.AreEqual("life/alive/action/idle", runtime.Action.Path);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/weapon.powpow", runtime.Action.Path);

                runtime.Tick(3, Fixed64.FromRaw(3000), true, false, false, castInstanceId, modeSkillId);
                Assert.AreEqual("life/alive/action/casting", runtime.Action.Path);
                mode = ModifierData.Override(ModifierKey.FromPacked(weaponModeParameterId), 1f,
                    sourceId: unchecked((int)modeSourceId));
                parameters.Set(actorId, modeSourceId, weaponModeParameterId, in mode);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/casting", runtime.Action.Path,
                    "Changing weapon mode must not interrupt an active cast.");
                runtime.Tick(4, Fixed64.FromRaw(4000), true, false, false, 0, 0);
                Assert.AreEqual("life/alive/action/idle", runtime.Action.Path);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/weapon.fishbones", runtime.Action.Path);
            }
        }

        [Test]
        public void InvalidActionDoesNotPartiallyRestoreView()
        {
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            using (var logic = new MobaCharacterHfsmRuntime(41, definition, 0, Fixed64.Zero))
            {
                var view = new BattleCharacterHfsmComponent(definition);
                view.ApplySnapshot(logic.CaptureSnapshot());
                var original = view.Action;
                logic.Tick(1, Fixed64.FromRaw(1000), true, false, true, 0, 0);
                var invalid = logic.CaptureSnapshot();
                invalid.Action = new MobaCharacterActionState(invalid.Action.Path,
                    invalid.Action.InstanceId, invalid.Action.StartFrame, 99, 0, 0);
                Assert.Throws<InvalidOperationException>(() => view.ApplySnapshot(invalid));
                Assert.AreEqual(0, view.SnapshotFrame);
                Assert.AreEqual(original.InstanceId, view.Action.InstanceId);
            }
        }

        [TestCase(30010101, "Spell_Q")]
        [TestCase(30010201, "Spell_W")]
        [TestCase(30010301, "Spell_E")]
        public void JhinSkillPresentation_UsesLinkedAnimatorState(int skillId, string expectedState)
        {
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            var view = new BattleCharacterHfsmComponent(
                MobaCharacterHfsmTestFixture.CreateDefinition(), catalog, entityCode: 3001);
            var action = new CharacterActionData(41, 1, 0, "life/alive/action/casting",
                ((long)41 << 32) | 100u, 0, 0, 77, skillId);
            view.ApplyPresentation(in action);

            var playback = view.Evaluate(0, 24);
            Assert.IsTrue(playback.HasValue);
            Assert.AreEqual(expectedState, playback.Value.AnimatorState);
        }

        [Test]
        public void JhinCurtainCallPresentation_TransitionsFromDeployToIdleAtAuthoredFrame()
        {
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            var view = new BattleCharacterHfsmComponent(
                MobaCharacterHfsmTestFixture.CreateDefinition(), catalog, entityCode: 3001);
            var action = new CharacterActionData(41, 1, 0, "life/alive/action/casting",
                ((long)41 << 32) | 100u, 0, 0, 77, 30010401);
            view.ApplyPresentation(in action);

            var deploy = view.Evaluate(0, 24);
            Assert.IsTrue(deploy.HasValue);
            Assert.AreEqual("Spell_R_Deploy", deploy.Value.AnimatorState);

            // The authored Deploy clip is 43 frames at 24 fps. Its successor
            // starts when those 43 playback frames have elapsed.
            var idle = view.Evaluate(43, 24);
            Assert.IsTrue(idle.HasValue);
            Assert.AreEqual("Spell_R_Idle", idle.Value.AnimatorState);
            Assert.IsTrue(idle.Value.Loop);
        }

        [TestCase("life/alive/action/r.aiming", "Spell_R_Idle", 60, true)]
        [TestCase("life/alive/action/r.shooting", "Spell_R_Shoot", 48, false)]
        [TestCase("life/alive/action/r.final_shot", "Spell_R_ShootLast", 62, false)]
        public void JhinCurtainCallHfsmState_UsesAuthoredAction(string path, string expectedState, int frames, bool loop)
        {
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            var view = new BattleCharacterHfsmComponent(
                MobaCharacterHfsmTestFixture.CreateDefinition(), catalog, entityCode: 3001);
            var action = new CharacterActionData(41, 1, 0, path,
                ((long)41 << 32) | 100u, 0, 0, 77, 30010401);
            view.ApplyPresentation(in action);

            var playback = view.Evaluate(0, 24);
            Assert.IsTrue(playback.HasValue);
            Assert.AreEqual(expectedState, playback.Value.AnimatorState);
            Assert.AreEqual(frames, playback.Value.FrameCount);
            Assert.AreEqual(loop, playback.Value.Loop);
        }

        [TestCase(1, "AutoAttack_1", 78)]
        [TestCase(2, "AutoAttack_2", 78)]
        [TestCase(3, "AutoAttack_3", 87)]
        [TestCase(4, "AutoAttack_4", 87)]
        public void JhinAttackSlotPresentation_UsesAuthoredSinglePlayAction(int slot, string expectedState, int frames)
        {
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            var path = "life/alive/action/attack.slot." + slot;
            var view = new BattleCharacterHfsmComponent(
                MobaCharacterHfsmTestFixture.CreateDefinition(), catalog, entityCode: 3001);
            var action = new CharacterActionData(41, 1, 0, path,
                ((long)41 << 32) | 100u, 0, 0, 0, 0);
            view.ApplyPresentation(in action);

            var playback = view.Evaluate(0, 24);
            Assert.IsTrue(playback.HasValue);
            Assert.AreEqual(expectedState, playback.Value.AnimatorState);
            Assert.AreEqual(frames, playback.Value.FrameCount);
            Assert.IsFalse(playback.Value.Loop, "A single attack clip should complete before the next action/state.");
        }

        [Test]
        public void JhinReloadSlotPresentation_UsesAuthoredReloadAction()
        {
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            var view = new BattleCharacterHfsmComponent(
                MobaCharacterHfsmTestFixture.CreateDefinition(), catalog, entityCode: 3001);
            var action = new CharacterActionData(41, 1, 0, "life/alive/action/attack.slot.5",
                ((long)41 << 32) | 100u, 0, 0, 77, 0);
            view.ApplyPresentation(in action);

            var playback = view.Evaluate(0, 24);
            Assert.IsTrue(playback.HasValue);
            Assert.AreEqual("AutoAttack_Reload", playback.Value.AnimatorState);
            Assert.AreEqual(75, playback.Value.FrameCount);
            Assert.IsFalse(playback.Value.Loop,
                "Reload is a single presentation action; HFSM parameters still own state exit.");
        }

        [Test]
        public void JhinHfsmParametersDriveStateAndModelActionGroups()
        {
            const int actorId = 41;
            const long sourceId = 30010001L;
            const int jhinEntityCode = 3001;
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            var parameters = new MobaCharacterHfsmParameterService();
            parameters.RegisterActorProfile(actorId, definition);
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            var view = new BattleCharacterHfsmComponent(definition, catalog, entityCode: jhinEntityCode);
            using (var runtime = new MobaCharacterHfsmRuntime(
                       actorId, definition, 0, Fixed64.Zero, registerForInspection: false,
                       parameters: parameters))
            {
                Assert.AreEqual("life/alive/action/idle", runtime.Action.Path);

                AssertJhinStateAndAction(parameters, runtime, view, actorId,
                    MobaCharacterHfsmTestFixture.AttackSlot, 1f, sourceId,
                    "life/alive/action/attack.slot.1", "AutoAttack_1", 78);
                AssertJhinStateAndAction(parameters, runtime, view, actorId,
                    MobaCharacterHfsmTestFixture.AttackSlot, 2f, sourceId,
                    "life/alive/action/attack.slot.2", "AutoAttack_2", 78);
                AssertJhinStateAndAction(parameters, runtime, view, actorId,
                    MobaCharacterHfsmTestFixture.AttackSlot, 3f, sourceId,
                    "life/alive/action/attack.slot.3", "AutoAttack_3", 87);
                AssertJhinStateAndAction(parameters, runtime, view, actorId,
                    MobaCharacterHfsmTestFixture.AttackSlot, 4f, sourceId,
                    "life/alive/action/attack.slot.4", "AutoAttack_4", 87);
                AssertJhinStateAndAction(parameters, runtime, view, actorId,
                    MobaCharacterHfsmTestFixture.AttackSlot, 5f, sourceId,
                    "life/alive/action/attack.slot.5", "AutoAttack_Reload", 75);

                parameters.ClearSource(actorId, sourceId);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual(0f, parameters.Resolve(actorId, MobaCharacterHfsmTestFixture.AttackSlot));
                Assert.AreEqual("life/alive/action/idle", runtime.Action.Path,
                    "Clearing the attack-slot contribution must return the HFSM to idle without an animation callback.");

                AssertJhinStateAndAction(parameters, runtime, view, actorId,
                    MobaCharacterHfsmTestFixture.RPhase,
                    MobaCharacterHfsmTestFixture.RPhaseAiming, sourceId,
                    "life/alive/action/r.aiming", "Spell_R_Idle", 60);
                AssertJhinStateAndAction(parameters, runtime, view, actorId,
                    MobaCharacterHfsmTestFixture.RPhase,
                    MobaCharacterHfsmTestFixture.RPhaseShooting, sourceId,
                    "life/alive/action/r.shooting", "Spell_R_Shoot", 48);
                AssertJhinStateAndAction(parameters, runtime, view, actorId,
                    MobaCharacterHfsmTestFixture.RPhase,
                    MobaCharacterHfsmTestFixture.RPhaseFinalShot, sourceId,
                    "life/alive/action/r.final_shot", "Spell_R_ShootLast", 62);

                parameters.ClearSource(actorId, sourceId);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual(0f, parameters.Resolve(actorId, MobaCharacterHfsmTestFixture.RPhase));
                Assert.AreEqual("life/alive/action/idle", runtime.Action.Path,
                    "Clearing the R phase contribution must leave the final-shot presentation state through HFSM evaluation.");
            }
        }

        [Test]
        public void JhinFinalRPhaseCanEnterDirectlyFromAiming()
        {
            const int actorId = 41;
            const long sourceId = 30010502L;
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            var parameters = new MobaCharacterHfsmParameterService();
            parameters.RegisterActorProfile(actorId, definition);
            using (var runtime = new MobaCharacterHfsmRuntime(
                       actorId, definition, 0, Fixed64.Zero, registerForInspection: false,
                       parameters: parameters))
            {
                var phase = ModifierData.Override(
                    ModifierKey.FromPacked((uint)MobaCharacterHfsmTestFixture.RPhase),
                    MobaCharacterHfsmTestFixture.RPhaseAiming,
                    sourceId: unchecked((int)sourceId));
                parameters.Set(actorId, sourceId, MobaCharacterHfsmTestFixture.RPhase, in phase);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/r.aiming", runtime.Action.Path);

                phase = ModifierData.Override(
                    ModifierKey.FromPacked((uint)MobaCharacterHfsmTestFixture.RPhase),
                    MobaCharacterHfsmTestFixture.RPhaseFinalShot,
                    sourceId: unchecked((int)sourceId));
                parameters.Set(actorId, sourceId, MobaCharacterHfsmTestFixture.RPhase, in phase);
                runtime.ReevaluateCurrentFrame();

                Assert.AreEqual("life/alive/action/r.final_shot", runtime.Action.Path,
                    "The final R slot is an independent cast and may be selected while still aiming.");
            }
        }

        [TestCase(2, "life/alive/action/attack.slot.2", "AutoAttack_2", 30010101, 1, "Spell_Q")]
        [TestCase(2, "life/alive/action/attack.slot.2", "AutoAttack_2", 30010201, 2, "Spell_W")]
        [TestCase(2, "life/alive/action/attack.slot.2", "AutoAttack_2", 30010301, 3, "Spell_E")]
        [TestCase(5, "life/alive/action/attack.slot.5", "AutoAttack_Reload", 30010101, 1, "Spell_Q")]
        [TestCase(5, "life/alive/action/attack.slot.5", "AutoAttack_Reload", 30010201, 2, "Spell_W")]
        [TestCase(5, "life/alive/action/attack.slot.5", "AutoAttack_Reload", 30010301, 3, "Spell_E")]
        public void JhinActiveSkillCastInterruptsAttackActionAndRestoresSelectedSlot(
            int attackSlot,
            string expectedAttackPath,
            string expectedAttackAnimator,
            int skillId,
            int skillSlot,
            string expectedSkillAnimator)
        {
            const int actorId = 42;
            const int basicAttackSlot = 5;
            const long sourceId = 30010002L;
            const long castId = 70042L;
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            var parameters = new MobaCharacterHfsmParameterService();
            parameters.RegisterActorProfile(actorId, definition);
            var attackSlotModifier = ModifierData.Override(
                ModifierKey.FromPacked((uint)MobaCharacterHfsmTestFixture.AttackSlot),
                attackSlot, sourceId: unchecked((int)sourceId));
            parameters.Set(actorId, sourceId, MobaCharacterHfsmTestFixture.AttackSlot,
                in attackSlotModifier);
            var catalog = CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader());
            var view = new BattleCharacterHfsmComponent(definition, catalog, entityCode: 3001);

            using (var runtime = new MobaCharacterHfsmRuntime(
                       actorId, definition, 0, Fixed64.Zero, registerForInspection: false,
                       parameters: parameters))
            {
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual(expectedAttackPath, runtime.Action.Path);

                runtime.Tick(1, Fixed64.FromSingle(1f / 24f), true, false, false,
                    castId, skillId, skillSlot, basicAttackSlot);
                Assert.AreEqual("life/alive/action/casting", runtime.Action.Path,
                    "An active ability must replace the selected attack action while it casts.");
                Assert.AreEqual(skillId, runtime.Action.SkillId);
                view.ApplyPresentation(new CharacterActionData(
                    actorId, 1, runtime.Frame, runtime.Action.Path, runtime.Action.InstanceId,
                    runtime.Action.StartFrame, runtime.Action.LocalFrame,
                    runtime.Action.CastInstanceId, runtime.Action.SkillId));
                var skillPlayback = view.Evaluate(runtime.Frame, 24);
                Assert.IsTrue(skillPlayback.HasValue);
                Assert.AreEqual(expectedSkillAnimator, skillPlayback.Value.AnimatorState);

                runtime.Tick(2, Fixed64.FromSingle(2f / 24f), true, false, false,
                    0, 0, 0, basicAttackSlot);
                Assert.AreEqual(expectedAttackPath, runtime.Action.Path,
                    "The AttackSlot parameter must select the post-cast action state.");
                Assert.AreEqual(0, runtime.Action.CastInstanceId);
                view.ApplyPresentation(new CharacterActionData(
                    actorId, 1, runtime.Frame, runtime.Action.Path, runtime.Action.InstanceId,
                    runtime.Action.StartFrame, runtime.Action.LocalFrame,
                    runtime.Action.CastInstanceId, runtime.Action.SkillId));
                var attackPlayback = view.Evaluate(runtime.Frame, 24);
                Assert.IsTrue(attackPlayback.HasValue);
                Assert.AreEqual(expectedAttackAnimator, attackPlayback.Value.AnimatorState);
            }
        }

        [Test]
        public void JhinBasicAttackCastKeepsCommittedAttackActionInsteadOfUsingGenericCastState()
        {
            const int actorId = 42;
            const int attackSlot = 2;
            const int basicAttackSlot = 5;
            const long castId = 70042L;
            const long sourceId = 30010002L;
            var definition = MobaCharacterHfsmTestFixture.CreateDefinition();
            var parameters = new MobaCharacterHfsmParameterService();
            parameters.RegisterActorProfile(actorId, definition);
            var attackSlotModifier = ModifierData.Override(
                ModifierKey.FromPacked((uint)MobaCharacterHfsmTestFixture.AttackSlot),
                attackSlot, sourceId: unchecked((int)sourceId));
            parameters.Set(actorId, sourceId, MobaCharacterHfsmTestFixture.AttackSlot,
                in attackSlotModifier);
            var committed = ModifierData.Override(
                ModifierKey.FromPacked((uint)MobaCharacterHfsmTestFixture.CommittedAttackSlot), attackSlot);
            parameters.Set(actorId, castId, MobaCharacterHfsmTestFixture.CommittedAttackSlot, in committed);

            using (var runtime = new MobaCharacterHfsmRuntime(
                       actorId, definition, 0, Fixed64.Zero, registerForInspection: false,
                       parameters: parameters))
            {
                runtime.ReevaluateCurrentFrame();
                runtime.Tick(1, Fixed64.FromSingle(1f / 24f), true, false, false,
                    castId, 30010602, basicAttackSlot, basicAttackSlot);

                Assert.AreEqual("life/alive/action/attack.slot.2", runtime.Action.Path);
                Assert.AreEqual(castId, runtime.Action.CastInstanceId,
                    "The attack cast must retain its own action-group presentation context.");
                Assert.AreEqual(30010602, runtime.Action.SkillId);
            }
        }

        private static void AssertJhinStateAndAction(
            MobaCharacterHfsmParameterService parameters,
            MobaCharacterHfsmRuntime runtime,
            BattleCharacterHfsmComponent view,
            int actorId,
            int parameterId,
            float value,
            long sourceId,
            string expectedPath,
            string expectedAnimatorState,
            int expectedFrameCount)
        {
            var modifier = ModifierData.Override(
                ModifierKey.FromPacked((uint)parameterId), value, sourceId: unchecked((int)sourceId));
            parameters.Set(actorId, sourceId, parameterId, in modifier);
            runtime.ReevaluateCurrentFrame();

            Assert.AreEqual(expectedPath, runtime.Action.Path,
                "HFSM state must be selected from the authored parameter condition.");
            var action = runtime.Action;
            view.ApplyPresentation(new CharacterActionData(
                actorId, 1, runtime.Frame, action.Path, action.InstanceId,
                action.StartFrame, action.LocalFrame, action.CastInstanceId, action.SkillId));
            var playback = view.Evaluate(runtime.Frame, 24);
            Assert.IsTrue(playback.HasValue, "The active Jhin HFSM state must resolve a presentation action.");
            Assert.AreEqual(expectedAnimatorState, playback.Value.AnimatorState);
            Assert.AreEqual(expectedFrameCount, playback.Value.FrameCount);
        }


        [Test]
        public void CharacterPresentationSequence_AdvancesBetweenSinglePlayActionsByFrameCount()
        {
            const string json = "{\"schemaVersion\":1,\"states\":[{" +
                "\"path\":\"life/alive/action/idle\",\"action\":{" +
                "\"id\":\"idle\",\"type\":\"sequence\",\"actions\":[" +
                "{\"id\":\"first\",\"type\":\"play\",\"animatorState\":\"First\",\"frameCount\":3,\"loop\":false}," +
                "{\"id\":\"second\",\"type\":\"play\",\"animatorState\":\"Second\",\"frameCount\":5,\"loop\":false}]}}]}";
            var catalog = CharacterPresentationActionCatalog.LoadJson(json);
            var view = new BattleCharacterHfsmComponent(
                MobaCharacterHfsmTestFixture.CreateDefinition(), catalog);
            var action = new CharacterActionData(41, 1, 0, "life/alive/action/idle",
                ((long)41 << 32) | 100u, 0, 0, 0, 0);
            view.ApplyPresentation(in action);

            Assert.AreEqual("First", view.Evaluate(2, 24).Value.AnimatorState);
            var second = view.Evaluate(3, 24);
            Assert.IsTrue(second.HasValue);
            Assert.AreEqual("Second", second.Value.AnimatorState);
            Assert.AreEqual(0, second.Value.LocalFrame);
        }

        [Test]
        public void CharacterPresentationSequence_LoopingPlayHoldsUntilStateChanges()
        {
            const string json = "{\"schemaVersion\":1,\"states\":[{" +
                "\"path\":\"life/alive/action/idle\",\"action\":{" +
                "\"id\":\"idle\",\"type\":\"sequence\",\"actions\":[" +
                "{\"id\":\"hold\",\"type\":\"play\",\"animatorState\":\"Hold\",\"frameCount\":3,\"loop\":true}," +
                "{\"id\":\"unreachable\",\"type\":\"play\",\"animatorState\":\"Next\",\"frameCount\":2,\"loop\":false}]}}]}";
            var catalog = CharacterPresentationActionCatalog.LoadJson(json);
            var view = new BattleCharacterHfsmComponent(
                MobaCharacterHfsmTestFixture.CreateDefinition(), catalog);
            var action = new CharacterActionData(41, 1, 0, "life/alive/action/idle",
                ((long)41 << 32) | 100u, 0, 0, 0, 0);
            view.ApplyPresentation(in action);

            for (var frame = 0; frame < 20; frame++)
            {
                var playback = view.Evaluate(frame, 24);
                Assert.IsTrue(playback.HasValue);
                Assert.AreEqual("Hold", playback.Value.AnimatorState);
            }
        }

        [Test]
        public void CharacterPresentationAction_ConvertsAuthored24FpsToLogicalTickRate()
        {
            const string json = "{\"schemaVersion\":1,\"states\":[{" +
                "\"path\":\"life/alive/action/idle\",\"action\":{" +
                "\"id\":\"idle\",\"type\":\"sequence\",\"actions\":[" +
                "{\"id\":\"clip\",\"type\":\"play\",\"animatorState\":\"Clip\",\"frameCount\":78,\"loop\":false}]} }]}";
            var catalog = CharacterPresentationActionCatalog.LoadJson(json);
            var view = new BattleCharacterHfsmComponent(
                MobaCharacterHfsmTestFixture.CreateDefinition(), catalog);
            var action = new CharacterActionData(41, 1, 0, "life/alive/action/idle",
                ((long)41 << 32) | 100u, 0, 0, 0, 0);
            view.ApplyPresentation(in action);

            var playback24 = view.Evaluate(77, 24).Value;
            Assert.AreEqual(78, playback24.FrameCount);
            Assert.AreEqual(78, playback24.AuthoredFrameCount);
            Assert.AreEqual(3.25f, playback24.DurationSeconds, 0.0001f);

            var playback30 = view.Evaluate(0, 30).Value;
            Assert.AreEqual(98, playback30.FrameCount);
            Assert.AreEqual(78, playback30.AuthoredFrameCount);
            Assert.AreEqual(24f, playback30.AuthoredFrameRate, 0.0001f);

            var playback60 = view.Evaluate(0, 60).Value;
            Assert.AreEqual(195, playback60.FrameCount);
            Assert.AreEqual(3.25f, playback60.DurationSeconds, 0.0001f);
        }

        [Test]
        public void CharacterPresentationCatalogValidation_IgnoresStatesOwnedByOtherHeroes()
        {
            var loader = new ResourcesTextAssetLoader();
            var catalog = CharacterPresentationActionCatalog.Load(loader);

            Assert.DoesNotThrow(() => new BattleCharacterHfsmComponent(
                MobaCharacterHfsmProfile.LoadDefinition(loader), catalog, entityCode: 1001));
            Assert.DoesNotThrow(() => new BattleCharacterHfsmComponent(
                MobaCharacterHfsmProfile.LoadDefinition(loader, 3001), catalog, entityCode: 3001));
        }

        [Test]
        public void CharacterPresentationCatalogValidation_RejectsUnknownSharedStatePath()
        {
            const string json = "{\"schemaVersion\":1,\"states\":[" +
                "{\"path\":\"life/alive/action/idle\",\"action\":{\"id\":\"idle\",\"type\":\"sequence\",\"actions\":[{" +
                "\"id\":\"idle.play\",\"type\":\"play\",\"animatorState\":\"Idle\",\"loop\":true}]}}," +
                "{\"path\":\"life/alive/action/not-real\",\"action\":{\"id\":\"unknown\",\"type\":\"sequence\",\"actions\":[{" +
                "\"id\":\"unknown.play\",\"type\":\"play\",\"animatorState\":\"Unknown\"}]}}]}";
            var catalog = CharacterPresentationActionCatalog.LoadJson(json);

            Assert.Throws<InvalidOperationException>(() => new BattleCharacterHfsmComponent(
                MobaCharacterHfsmProfile.CreateGenericDefinition(), catalog));
        }

        [Test]
        public void CharacterPresentationCatalogValidation_RejectsMissingPathOwnedByCurrentEntity()
        {
            const string json = "{\"schemaVersion\":1,\"states\":[" +
                "{\"path\":\"life/alive/action/jinx.special\",\"action\":{\"id\":\"root\",\"type\":\"sequence\",\"actions\":[{" +
                "\"id\":\"play\",\"type\":\"play\",\"animatorState\":\"Special\"}]}}]," +
                "\"variants\":[{\"entityCode\":3001,\"path\":\"life/alive/action/jinx.special\",\"actionId\":\"play\",\"action\":{\"id\":\"play\",\"type\":\"play\",\"animatorState\":\"JhinSpecial\"}}]}";
            var catalog = CharacterPresentationActionCatalog.LoadJson(json);

            Assert.Throws<InvalidOperationException>(() => new BattleCharacterHfsmComponent(
                MobaCharacterHfsmProfile.CreateGenericDefinition(), catalog, entityCode: 3001));
        }
    }
}
