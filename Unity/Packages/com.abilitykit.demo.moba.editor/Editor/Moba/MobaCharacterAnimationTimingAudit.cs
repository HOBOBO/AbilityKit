using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Read-only imported animation timing evidence; does not publish or change assets.</summary>
    public static class MobaCharacterAnimationTimingAudit
    {
        public static void Capture()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, "-mobaAnimationAuditHeroes");
            if (index < 0 || index + 1 >= arguments.Length)
                throw new ArgumentException("Provide -mobaAnimationAuditHeroes with comma-separated model IDs.");
            var identities = new HashSet<int>(arguments[index + 1].Split(',').Select(int.Parse));
            var rows = JArray.Parse(Resources.Load<TextAsset>("luban/moba/models").text);
            var models = new List<object>();
            foreach (var row in rows.OfType<JObject>().Where(row => identities.Contains(row.Value<int>("Id"))))
            {
                var path = row.Value<string>("PrefabPath");
                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null) throw new InvalidDataException("Missing model: " + path);
                var animator = prefab.GetComponentInChildren<Animator>(true);
                if (animator == null || !(animator.runtimeAnimatorController is AnimatorController controller))
                    throw new InvalidDataException("Missing inspectable controller: " + path);
                var states = new List<object>();
                foreach (var layer in controller.layers)
                    Collect(layer.stateMachine, layer.name, states);
                models.Add(new { id = row.Value<int>("Id"), resource = path,
                    controller = AssetDatabase.GetAssetPath(controller), states });
                if (arguments.Contains("-mobaAnimationAuditSamplePoses"))
                    CapturePoses(prefab, controller, row.Value<int>("Id"));
            }
            if (models.Count != identities.Count)
                throw new InvalidDataException("Not all requested model IDs were found.");
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../local/Logs/character-animation-timing"));
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, "imported-clips.json");
            File.WriteAllText(destination, JsonConvert.SerializeObject(models, Formatting.Indented));
            Debug.Log("[MobaCharacterAnimationTimingAudit] Read-only capture: " + destination);
        }

        private static void Collect(AnimatorStateMachine machine, string prefix, List<object> output)
        {
            foreach (var entry in machine.states)
            {
                var state = entry.state;
                var clip = state.motion as AnimationClip;
                var settings = clip != null ? AnimationUtility.GetAnimationClipSettings(clip) : null;
                output.Add(new
                {
                    path = prefix + "/" + state.name, state = state.name,
                    clip = clip != null ? AssetDatabase.GetAssetPath(clip) : null,
                    seconds = clip != null ? clip.length : 0f,
                    frameRate = clip != null ? clip.frameRate : 0f,
                    frameCount = clip != null ? clip.length * clip.frameRate : 0f,
                    settingsStartTime = settings != null ? settings.startTime : 0f,
                    settingsStopTime = settings != null ? settings.stopTime : 0f,
                    settingsLoopTime = settings != null && settings.loopTime,
                    speed = state.speed, speedParameterActive = state.speedParameterActive,
                    speedParameter = state.speedParameter,
                    transitions = state.transitions.Select(transition => new
                    {
                        destination = transition.destinationState != null ? transition.destinationState.name : null,
                        transition.hasExitTime, transition.exitTime, transition.duration,
                        transition.hasFixedDuration,
                        conditions = transition.conditions.Select(condition => new
                        { condition.parameter, mode = condition.mode.ToString(), condition.threshold }).ToArray()
                    }).ToArray()
                });
            }
            foreach (var child in machine.stateMachines)
                Collect(child.stateMachine, prefix + "/" + child.stateMachine.name, output);
        }

        private static void CapturePoses(GameObject prefab, AnimatorController controller, int modelId)
        {
            var reports = new List<object>();
            foreach (var clip in controller.animationClips.Distinct())
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                instance.hideFlags = HideFlags.HideAndDontSave;
                try
                {
                    var animator = instance.GetComponentInChildren<Animator>(true);
                    animator.enabled = false;
                    var bones = animator.GetComponentsInChildren<Transform>(true);
                    clip.SampleAnimation(animator.gameObject, 0f);
                    var rotations = bones.Select(bone => bone.localRotation).ToArray();
                    var positions = bones.Select(bone => bone.localPosition).ToArray();
                    var scales = bones.Select(bone => bone.localScale).ToArray();
                    var samples = new List<object>();
                    var count = Mathf.CeilToInt(clip.length * 60f);
                    var lastChange = 0f;
                    for (var i = 1; i <= count; i++)
                    {
                        var seconds = Mathf.Min(i / 60f, clip.length);
                        clip.SampleAnimation(animator.gameObject, seconds);
                        var angle = 0f;
                        var distance = 0f;
                        var scaleDelta = 0f;
                        string anglePath = null, positionPath = null;
                        for (var b = 0; b < bones.Length; b++)
                        {
                            var boneAngle = Quaternion.Angle(rotations[b], bones[b].localRotation);
                            var boneDistance = Vector3.Distance(positions[b], bones[b].localPosition);
                            if (boneAngle > angle) { angle = boneAngle; anglePath = AnimationUtility.CalculateTransformPath(bones[b], animator.transform); }
                            if (boneDistance > distance) { distance = boneDistance; positionPath = AnimationUtility.CalculateTransformPath(bones[b], animator.transform); }
                            scaleDelta = Mathf.Max(scaleDelta, Vector3.Distance(scales[b], bones[b].localScale));
                            rotations[b] = bones[b].localRotation;
                            positions[b] = bones[b].localPosition;
                            scales[b] = bones[b].localScale;
                        }
                        // Diagnostic thresholds only. This is not an automatic trim/cancel marker.
                        if (angle > .1f || distance > .0001f || scaleDelta > .0001f) lastChange = seconds;
                        samples.Add(new { seconds, maxAngleDegrees = angle,
                            maxPositionDelta = distance, maxScaleDelta = scaleDelta,
                            maxAnglePath = anglePath, maxPositionPath = positionPath });
                    }
                    reports.Add(new { clip = AssetDatabase.GetAssetPath(clip), clip.length, clip.frameRate,
                        sampleRate = 60, lastObservedTransformChange = lastChange, samples });
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../local/Logs/character-animation-timing"));
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, "sampled-poses-" + modelId + ".json");
            File.WriteAllText(destination, JsonConvert.SerializeObject(reports, Formatting.Indented));
            Debug.Log("[MobaCharacterAnimationTimingAudit] Isolated model pose sampling: " + destination);
        }
    }
}
