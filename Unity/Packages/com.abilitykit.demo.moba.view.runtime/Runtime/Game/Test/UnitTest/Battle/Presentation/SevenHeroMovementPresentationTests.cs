using System.Linq;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Deterministic;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Flow;
using AbilityKit.Modifiers;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Tests
{
    public sealed class SevenHeroMovementPresentationTests
    {
        [TestCase(3001, "moving", "Walk", "idle", "Idle")]
        [TestCase(3002, "moving.powpow", "Walk", "weapon.powpow", "Idle_Minigun")]
        [TestCase(3002, "moving.fishbones", "Walk_Rocket", "weapon.fishbones", "Idle_Rocket")]
        [TestCase(3003, "moving", "Walk", "idle", "Void_Annie_Model_Idle")]
        [TestCase(3004, "moving", "Walk", "idle", "Veigar_Model_Idle")]
        [TestCase(3005, "moving", "Walk", "idle", "Gragas_Model_Idle")]
        [TestCase(3006, "moving", "Walk", "idle", "Karthus_Model_Idle")]
        [TestCase(3007, "moving", "Walk", "idle", "Lux_Model_Idle")]
        public void PublishedMovementLoopsChangesBonesAndReturnsToIdle(
            int modelId, string movingPath, string movingState, string idlePath, string idleState)
        {
            var loader = new ResourcesTextAssetLoader();
            var models = JArray.Parse(Resources.Load<TextAsset>("luban/moba/models").text);
            var resource = models.OfType<JObject>().Single(m => m.Value<int>("Id") == modelId).Value<string>("PrefabPath");
            var prefab = Resources.Load<GameObject>(resource);
            Assert.IsNotNull(prefab, resource);
            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator);
                Assert.IsFalse(animator.applyRootMotion, "Translation belongs to the battle simulation.");
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var rootPosition = instance.transform.localPosition;
                var definition = MobaCharacterHfsmProfile.LoadDefinition(loader, modelId);
                var parameters = new MobaCharacterHfsmParameterService();
                parameters.RegisterActorProfile(41, definition);
                if (modelId == 3002 && movingState == "Walk_Rocket")
                {
                    const int weaponMode = 4;
                    var modifier = ModifierData.Override(ModifierKey.FromPacked(weaponMode), 1f, sourceId: 41);
                    parameters.Set(41, 41, weaponMode, in modifier);
                }
                using var runtime = new MobaCharacterHfsmRuntime(41, definition, 0, Fixed64.Zero,
                    registerForInspection: false, parameters: parameters);
                var catalog = CharacterPresentationActionCatalog.Load(loader).CreateForDefinition(definition);
                var state = new BattleCharacterHfsmComponent(definition, catalog, modelId);
                var view = instance.GetComponent<MonoCharacterHfsmView>() ?? instance.AddComponent<MonoCharacterHfsmView>();
                const int tickRate = 24;
                runtime.Tick(1, Fixed64.FromDouble(1d / tickRate), true, false, true, 0, 0);
                Assert.AreEqual("life/alive/action/" + movingPath, runtime.Action.Path);
                var actionId = runtime.Action.InstanceId;
                state.ApplySnapshot(runtime.CaptureSnapshot());
                view.Bind(state, tickRate);

                var quarter = PoseAt(6);
                PoseAt(18);
                var bones = animator.GetComponentsInChildren<Transform>(true);
                Assert.Greater(bones.Select((b, i) => Quaternion.Angle(quarter[i], b.localRotation)).Max(), 2f,
                    "The actual moving model must change pose, not merely select a state hash.");
                var secondLoopQuarter = PoseAt(30);
                Assert.Less(quarter.Select((q, i) => Quaternion.Angle(q, secondLoopQuarter[i])).Max(), .1f,
                    "The same phase of consecutive walk cycles must match.");
                PoseAt(54);
                Assert.AreEqual(actionId, state.Action.InstanceId, "Continued movement must keep the action identity.");

                runtime.Tick(56, Fixed64.FromDouble(56d / tickRate), true, false, false, 0, 0);
                Assert.AreEqual("life/alive/action/" + idlePath, runtime.Action.Path);
                state.ApplySnapshot(runtime.CaptureSnapshot());
                view.Bind(state, tickRate);
                view.SeekToFrame(61, 1f / tickRate);
                Assert.AreEqual(Animator.StringToHash(idleState), animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    "Stopping must restore the correct idle, including the current weapon pose.");
                Assert.Less((instance.transform.localPosition - rootPosition).sqrMagnitude, .000001f, "Walk must not move the actor root.");
                TestContext.WriteLine($"Model {modelId}: {movingState} loops beyond 2 seconds and restores {idleState}.");

                Quaternion[] PoseAt(int frame)
                {
                    runtime.Tick(frame + 1, Fixed64.FromDouble((frame + 1d) / tickRate), true, false, true, 0, 0);
                    Assert.AreEqual("life/alive/action/" + movingPath, runtime.Action.Path);
                    Assert.AreEqual(actionId, runtime.Action.InstanceId);
                    state.ApplySnapshot(runtime.CaptureSnapshot());
                    view.Bind(state, tickRate);
                    view.SeekToFrame(frame + 1, 1f / tickRate);
                    var intent = state.Evaluate(frame + 1, tickRate).Value;
                    Assert.IsTrue(intent.Loop);
                    Assert.AreEqual(24, intent.AuthoredFrameCount);
                    Assert.AreEqual(24f, intent.AuthoredFrameRate);
                    Assert.AreEqual(1f, intent.DurationSeconds);
                    Assert.AreEqual(Animator.StringToHash(movingState), animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                    var playing = animator.GetCurrentAnimatorClipInfo(0);
                    Assert.IsTrue(playing.Length > 0 && playing[0].clip.isLooping);
                    return animator.GetComponentsInChildren<Transform>(true).Select(b => b.localRotation).ToArray();
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
}
