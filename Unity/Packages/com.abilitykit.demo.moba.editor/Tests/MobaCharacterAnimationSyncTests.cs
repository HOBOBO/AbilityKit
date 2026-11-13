#if UNITY_EDITOR
using System;
using System.Linq;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaCharacterAnimationSyncTests
    {
        private string _folder;
        private string _resource;
        private AnimationClip _clip;
        private AnimatorController _controller;
        private AnimatorState _state;
        private GameObject _model;

        [SetUp]
        public void SetUp()
        {
            _resource = "__AnimationSyncFixture_" + Guid.NewGuid().ToString("N");
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            _folder = "Assets/Resources/" + _resource;
            AssetDatabase.CreateFolder("Assets/Resources", _resource);
            _clip = new AnimationClip { name = "DifferentClipName", frameRate = 24 };
            _clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
            AssetDatabase.CreateAsset(_clip, _folder + "/Clip.anim");
            _controller = AnimatorController.CreateAnimatorControllerAtPath(_folder + "/Model.controller");
            _state = _controller.layers[0].stateMachine.AddState("Cast");
            _state.motion = _clip;
            var instance = new GameObject("Model");
            instance.AddComponent<Animator>().runtimeAnimatorController = _controller;
            _model = PrefabUtility.SaveAsPrefabAsset(instance, _folder + "/Model.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(_folder);

        private JObject Snapshot()
        {
            return JObject.Parse("{\"schemaVersion\":1,\"fingerprints\":{},\"bindingHash\":\"missing\"," +
                "\"bindings\":{\"schemaVersion\":1,\"bindings\":[]},\"models\":[],\"actors\":[],\"actions\":[]}")
                .With(value =>
                {
                    ((JArray)value["models"]).Add(new JObject { ["Id"] = 71, ["PrefabPath"] = _resource + "/Model" });
                    ((JArray)value["actors"]).Add(new JObject { ["Id"] = 17, ["ModelId"] = 71 });
                    ((JArray)value["actions"]).Add(new JObject { ["target"] = new JObject {
                        ["catalogRowId"] = 1, ["entityCode"] = 17, ["path"] = "life/alive/casting", ["skillId"] = 0,
                        ["actionId"] = "phase", ["nodeIds"] = new JArray("phase", "play") },
                        ["animation"] = new JObject { ["animatorState"] = "Cast" }, ["loop"] = false });
                });
        }

        [Test]
        public void ModelExportNeedsNoActorOrHfsmAndAcceptsAnExplicitActionKey()
        {
            var snapshot = Snapshot();
            snapshot["actors"] = new JArray();
            snapshot["actions"] = new JArray();
            snapshot["modelAnimations"] = new JArray();
            var scan = MobaCharacterAnimationSync.ScanModelAuthoring(snapshot, 71);
            var row = scan.Rows.Single();
            Assert.That(row.Error, Is.Null);
            Assert.That(row.Selected, Is.False, "New actions require explicit export selection.");
            row.Target["animationKey"] = "attack.primary";
            var request = MobaCharacterAnimationSync.BuildRequest(scan, scan.Rows);
            Assert.That(request.Value<string>("mode"), Is.EqualTo("model"));
            Assert.That(request["changes"].Single()["target"].Value<string>("animationKey"), Is.EqualTo("attack.primary"));
            Assert.That(request["changes"].Single()["target"]["path"], Is.Null);
            Assert.That(request["changes"].Single()["after"].Value<int>("frameCount"), Is.EqualTo(24));
        }

        [Test]
        public void ModelBindingSurvivesRenameWithoutChangingItsStableActionKey()
        {
            var snapshot = Snapshot();
            snapshot["modelAnimations"] = new JArray(new JObject { ["Id"] = 1, ["ModelId"] = 71,
                ["ActionKey"] = "attack.primary", ["AnimatorState"] = "Base Layer.Cast",
                ["FrameCount"] = 24, ["FrameRate"] = 24, ["Layer"] = 0 });
            var first = MobaCharacterAnimationSync.ScanModelAuthoring(snapshot, 71);
            snapshot["bindings"] = MobaCharacterAnimationSync.BuildRequest(first, first.Rows)["bindings"];
            _state.name = "Renamed";
            EditorUtility.SetDirty(_controller);
            AssetDatabase.SaveAssets();
            var row = MobaCharacterAnimationSync.ScanModelAuthoring(snapshot, 71).Rows.Single();
            Assert.That(row.Target.Value<string>("animationKey"), Is.EqualTo("attack.primary"));
            Assert.That(row.Bound, Is.True);
            Assert.That(row.After.Value<string>("animatorState"), Is.EqualTo("Base Layer.Renamed"));
        }

        [TestCase(1, 24, 24)]
        [TestCase(1, 60, 60)]
        [TestCase(1.54166675, 60, 93)]
        [TestCase(3.208333333333333, 24, 77)]
        public void CountsDurationIntervalsWithoutExtraEndpoint(double seconds, double rate, int expected) =>
            Assert.That(MobaCharacterAnimationSync.DurationFrames(seconds, rate), Is.EqualTo(expected));

        [Test]
        public void ResolvesAnimatorStateInsteadOfAssumingClipFilename()
        {
            var scan = MobaCharacterAnimationSync.ScanAuthoring(Snapshot(), 17);
            var row = scan.Rows.Single();
            Assert.That(row.Error, Is.Null);
            Assert.That(row.After.Value<string>("animatorState"), Is.EqualTo("Base Layer.Cast"));
            Assert.That(row.After.Value<int>("frameCount"), Is.EqualTo(24));
            Assert.That(row.State.Clip, Is.SameAs(_clip));
        }

        [Test]
        public void StableBindingSurvivesStateAndClipRename()
        {
            var snapshot = Snapshot();
            var first = MobaCharacterAnimationSync.ScanAuthoring(snapshot, 17);
            snapshot["bindings"] = MobaCharacterAnimationSync.BuildRequest(first, first.Rows)["bindings"];
            _state.name = "RenamedCast";
            EditorUtility.SetDirty(_controller);
            Assert.That(AssetDatabase.RenameAsset(_folder + "/Clip.anim", "RenamedClip"), Is.Empty);
            AssetDatabase.SaveAssets();
            var second = MobaCharacterAnimationSync.ScanAuthoring(snapshot, 17).Rows.Single();
            Assert.That(second.Bound, Is.True);
            Assert.That(second.Error, Is.Null);
            Assert.That(second.After.Value<string>("animatorState"), Is.EqualTo("Base Layer.RenamedCast"));
        }

        [Test]
        public void ReplacingClipDoesNotSilentlyRebindToSameNamedMotion()
        {
            var snapshot = Snapshot();
            var first = MobaCharacterAnimationSync.ScanAuthoring(snapshot, 17);
            snapshot["bindings"] = MobaCharacterAnimationSync.BuildRequest(first, first.Rows)["bindings"];
            var replacement = UnityEngine.Object.Instantiate(_clip);
            replacement.name = _clip.name;
            AssetDatabase.CreateAsset(replacement, _folder + "/Replacement.anim");
            _state.motion = replacement;
            EditorUtility.SetDirty(_controller);
            AssetDatabase.SaveAssets();
            var row = MobaCharacterAnimationSync.ScanAuthoring(snapshot, 17).Rows.Single();
            Assert.That(row.Error, Does.Contain("失效"));
            Assert.That(row.After, Is.Null);
        }

        [Test]
        public void DuplicateShortStateNamesRequireExplicitSelection()
        {
            var child = _controller.layers[0].stateMachine.AddStateMachine("Nested");
            child.AddState("Cast").motion = _clip;
            AssetDatabase.SaveAssets();
            var scan = MobaCharacterAnimationSync.ScanAuthoring(Snapshot(), 17);
            var row = scan.Rows.Single();
            Assert.That(row.Error, Does.Contain("唯一匹配"));
            MobaCharacterAnimationSync.Select(scan, row, row.Choices.Single(item => item.FullName == "Base Layer.Nested.Cast"));
            Assert.That(row.Error, Is.Null);
            Assert.That(row.After.Value<string>("animatorState"), Is.EqualTo("Base Layer.Nested.Cast"));
        }

        [Test]
        public void TimingWarningsRequireManualSelectionAndNeverRewriteSpeedOrLoop()
        {
            _state.speed = 2;
            EditorUtility.SetDirty(_controller);
            AssetDatabase.SaveAssets();
            var scan = MobaCharacterAnimationSync.ScanAuthoring(Snapshot(), 17);
            var row = scan.Rows.Single();
            Assert.That(row.Warnings.Any(message => message.Contains("速度")), Is.True);
            Assert.That(row.Selected, Is.False);
            var request = MobaCharacterAnimationSync.BuildRequest(scan, scan.Rows);
            var after = request["changes"].Single()["after"];
            Assert.That(after["loop"], Is.Null);
            Assert.That(after["speed"], Is.Null);
            Assert.That(after.Value<int>("frameCount"), Is.EqualTo(24));
            Assert.That(_state.speed, Is.EqualTo(2));
        }

        [Test]
        public void PlaybackRangeMismatchWarnsWithoutChangingTheClipOrSelectingTheRow()
        {
            var settings = AnimationUtility.GetAnimationClipSettings(_clip);
            settings.startTime = .125f;
            settings.stopTime = .75f;
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(_clip, settings);
            AssetDatabase.SaveAssets();
            var scan = MobaCharacterAnimationSync.ScanAuthoring(Snapshot(), 17);
            var row = scan.Rows.Single();
            Assert.That(row.Error, Is.Null);
            Assert.That(row.Warnings.Any(message => message.Contains("播放范围不一致") &&
                message.Contains("0.125") && message.Contains("0.75")), Is.True);
            Assert.That(row.Selected, Is.False);
            var after = MobaCharacterAnimationSync.BuildRequest(scan, scan.Rows)["changes"].Single()["after"];
            Assert.That(after.Value<int>("frameCount"), Is.EqualTo(24));
            Assert.That(after["startTime"], Is.Null);
            Assert.That(after["stopTime"], Is.Null);
            var unchanged = AnimationUtility.GetAnimationClipSettings(_clip);
            Assert.That(unchanged.startTime, Is.EqualTo(.125f));
            Assert.That(unchanged.stopTime, Is.EqualTo(.75f));
            Assert.That(unchanged.loopTime, Is.True);
        }

        [Test]
        public void FullClipRangeDoesNotProduceRangeWarning()
        {
            var settings = AnimationUtility.GetAnimationClipSettings(_clip);
            settings.startTime = 0;
            settings.stopTime = _clip.length;
            AnimationUtility.SetAnimationClipSettings(_clip, settings);
            AssetDatabase.SaveAssets();
            var row = MobaCharacterAnimationSync.ScanAuthoring(Snapshot(), 17).Rows.Single();
            Assert.That(row.Warnings.Any(message => message.Contains("播放范围不一致")), Is.False);
        }

        [Test]
        public void MissingActorMappingIsNotInferredFromEntityIdentity()
        {
            var snapshot = Snapshot();
            snapshot["actors"] = new JArray();
            var row = MobaCharacterAnimationSync.ScanAuthoring(snapshot, 17).Rows.Single();
            Assert.That(row.Error, Does.Contain("不按实体 ID 猜测"));
        }

        [Test]
        public void RepeatedMetadataScanIsStable()
        {
            var snapshot = Snapshot();
            var first = MobaCharacterAnimationSync.ScanAuthoring(snapshot, 17);
            var request = MobaCharacterAnimationSync.BuildRequest(first, first.Rows);
            snapshot["bindings"] = request["bindings"];
            snapshot["actions"].Single()["animation"] = request["changes"].Single()["after"];
            var row = MobaCharacterAnimationSync.ScanAuthoring(snapshot, 17).Rows.Single();
            Assert.That(row.Bound, Is.True);
            Assert.That(row.Changed, Is.False);
        }

        [Test]
        public void UnsavedClipIsRejectedBeforeMetadataCanBeApplied()
        {
            _clip.frameRate = 60;
            EditorUtility.SetDirty(_clip);
            var row = MobaCharacterAnimationSync.ScanAuthoring(Snapshot(), 17).Rows.Single();
            Assert.That(row.Error, Does.Contain("未保存"));
        }

        [Test]
        public void ChangedSourceFingerprintRejectsPreviouslyPreviewedRequest()
        {
            var scan = MobaCharacterAnimationSync.ScanAuthoring(Snapshot(), 17);
            scan.Fingerprints[System.IO.Path.GetFullPath(_folder + "/Clip.anim")] = "stale";
            Assert.Throws<System.IO.InvalidDataException>(() => MobaCharacterAnimationSync.BuildRequest(scan, scan.Rows));
        }

        [TestCase(3001)]
        [TestCase(3002)]
        public void ProductionFullStatePathsCanBeSampledByActualModelAnimator(int entityCode)
        {
            var scan = entityCode == 3002 ? MobaCharacterAnimationSync.ScanModel(MobaCharacterAnimationSync.ModelForEntity(entityCode)) :
                MobaCharacterAnimationSync.Scan(entityCode);
            Assert.That(scan.Errors, Is.Empty);
            Assert.That(scan.Rows, Is.Not.Empty);
            foreach (var row in scan.Rows)
            {
                Assert.That(row.Error, Is.Null, row.Label);
                var model = UnityEngine.Object.Instantiate(row.State.Model);
                try
                {
                    var animator = model.GetComponentInChildren<Animator>(true);
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind();
                    var layer = row.After.Value<int>("layer");
                    var hash = Animator.StringToHash(row.After.Value<string>("animatorState"));
                    Assert.That(animator.HasState(layer, hash), Is.True, row.Label);
                    animator.Play(hash, layer, .5f);
                    animator.Update(0);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(layer).fullPathHash, Is.EqualTo(hash), row.Label);
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
        }

        [Test]
        public void JinxFormalPlaybackRangesMatchImportedClipsAndActualAnimatorDurations()
        {
            var scan = MobaCharacterAnimationSync.ScanModel(MobaCharacterAnimationSync.ModelForEntity(3002));
            Assert.That(scan.Errors, Is.Empty);
            Assert.That(scan.Rows.Count(row => row.Bound), Is.EqualTo(9));
            foreach (var row in scan.Rows.GroupBy(item => item.State.FullName).Select(group => group.First()))
            {
                Assert.That(row.Error, Is.Null, row.Label);
                Assert.That(row.Warnings.Any(message => message.Contains("播放范围不一致")), Is.False, row.Label);
                var settings = AnimationUtility.GetAnimationClipSettings(row.State.Clip);
                Assert.That(settings.startTime, Is.Zero, row.Label);
                Assert.That(settings.stopTime, Is.EqualTo(row.State.Clip.length).Within(.0001), row.Label);
                var model = UnityEngine.Object.Instantiate(row.State.Model);
                try
                {
                    var animator = model.GetComponentInChildren<Animator>(true);
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    animator.Rebind();
                    animator.Play(Animator.StringToHash(row.State.FullName), row.State.Layer, 0);
                    animator.Update(0);
                    var state = animator.GetCurrentAnimatorStateInfo(row.State.Layer);
                    Assert.That(state.length * state.speed * state.speedMultiplier,
                        Is.EqualTo(row.State.Clip.length).Within(.001), row.Label);
                }
                finally { UnityEngine.Object.DestroyImmediate(model); }
            }
        }
    }

    internal static class AnimationSyncTestObjectExtensions
    {
        internal static T With<T>(this T value, Action<T> initialize) { initialize(value); return value; }
    }
}
#endif
