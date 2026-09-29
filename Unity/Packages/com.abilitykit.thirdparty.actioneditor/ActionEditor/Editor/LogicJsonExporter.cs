using System;
using System.IO;
using AbilityKit.ActionSchema;
using NBC.ActionEditor;
using UnityEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public static class LogicJsonExporter
    {
        private const string MobaTimelineFolder =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/action_timeline";

        [MenuItem("Tools/AbilityKit/Demos/Moba/ActionEditor/Export Selected Timeline")]
        public static void ExportSelectedTimeline()
        {
            if (!(Selection.activeObject is TextAsset textAsset))
                throw new InvalidOperationException("Select an ActionEditor timeline JSON asset first.");
            ExportMobaTimeline(textAsset);
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/AbilityKit/Demos/Moba/ActionEditor/Export All MOBA Timelines")]
        public static void ExportAllMobaTimelines()
        {
            var count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { MobaTimelineFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json", StringComparison.Ordinal) ||
                    path.EndsWith(".logic.json", StringComparison.Ordinal) ||
                    path.EndsWith(".presentation.json", StringComparison.Ordinal)) continue;
                var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                if (textAsset == null) continue;
                var asset = Json.Deserialize(typeof(Asset), textAsset.text) as Asset;
                if (!(asset is IActionTimelineRuntimeAsset marked) || !marked.ExportMobaRuntime) continue;
                ExportMobaTimeline(textAsset);
                count++;
            }
            AssetDatabase.Refresh();
            Debug.Log($"[ActionEditor] Exported {count} MOBA logic and presentation timelines.");
        }

        private static void ExportMobaTimeline(TextAsset textAsset)
        {
            var assetPath = AssetDatabase.GetAssetPath(textAsset);
            var asset = Json.Deserialize(typeof(Asset), textAsset.text) as Asset;
            if (!(asset is IActionTimelineRuntimeAsset marked) || !marked.ExportMobaRuntime)
                throw new InvalidDataException("MOBA timeline must opt in to runtime export: " + assetPath);
            asset.Init();
            ExportLogicJson(asset, Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath)));
        }

        public static void ExportLogicJson(Asset assetData, string editorJsonPath)
        {
            if (assetData == null) return;
            if (string.IsNullOrEmpty(editorJsonPath)) return;

            var dto = ToDto(assetData);

            var dir = Path.GetDirectoryName(editorJsonPath);
            var name = Path.GetFileNameWithoutExtension(editorJsonPath);
            var logicPath = Path.Combine(dir ?? string.Empty, name + ".logic.json");

            SkillAssetDto logic = null;
            SkillAssetDto presentation = null;
            if (assetData is IActionTimelineRuntimeAsset mobaAsset && mobaAsset.ExportMobaRuntime)
            {
                logic = ActionTimelinePartition.Create(dto, ActionTimelineRuntimeTypes.Logic);
                presentation = ActionTimelinePartition.Create(dto, ActionTimelineRuntimeTypes.Presentation);
            }

            if (logic != null && presentation != null)
            {
                var mobaLogicPath = Path.Combine(dir ?? string.Empty, name + ".moba.logic.json");
                var mobaPresentationPath = Path.Combine(dir ?? string.Empty, name + ".moba.presentation.json");
                File.WriteAllText(mobaLogicPath, Json.Serialize(logic));
                File.WriteAllText(mobaPresentationPath, Json.Serialize(presentation));
            }
            else
            {
                File.WriteAllText(logicPath, Json.Serialize(dto));
            }
        }

        private static SkillAssetDto ToDto(Asset asset)
        {
            var dto = new SkillAssetDto
            {
                length = asset.Length
            };

            if (asset.groups == null) return dto;

            foreach (var group in asset.groups)
            {
                if (group == null) continue;

                var g = new GroupDto
                {
                    name = group.Name,
                    actorId = group.ActorId,
                    active = group.IsActive,
                    locked = group.IsLocked,
                    collapsed = group.IsCollapsed
                };

                if (group.Tracks != null)
                {
                    foreach (var track in group.Tracks)
                    {
                        if (track == null) continue;

                        var t = new TrackDto
                        {
                            type = track.GetType().FullName,
                            name = track.Name,
                            active = track.IsActive,
                            locked = track.IsLocked
                        };

                        if (track.Clips != null)
                        {
                            foreach (var clip in track.Clips)
                            {
                                if (clip == null) continue;

                                var c = new ClipDto
                                {
                                    type = clip.GetType().FullName,
                                    runtimeType = clip is IActionTimelineRuntimeClip runtimeClip
                                        ? (runtimeClip.RuntimeKind == ActionTimelineRuntimeKind.Logic
                                            ? ActionTimelineRuntimeTypes.Logic
                                            : runtimeClip.RuntimeKind == ActionTimelineRuntimeKind.Presentation
                                                ? ActionTimelineRuntimeTypes.Presentation
                                                : null)
                                        : null,
                                    start = clip.StartTime,
                                    length = clip.Length,
                                    blendIn = clip.BlendIn,
                                    blendOut = clip.BlendOut,
                                };

                                FillClipArgs(clip, c);

                                t.clips.Add(c);
                            }
                        }

                        g.tracks.Add(t);
                    }
                }

                dto.groups.Add(g);
            }

            return dto;
        }

        private static void FillClipArgs(Clip clip, ClipDto dto)
        {
            if (clip is ILogicJsonExportable exportable)
            {
                exportable.FillLogicArgs(dto.args);
            }
        }
    }
}
