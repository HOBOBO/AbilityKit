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
        private const string XiaoQiaoAssetPath =
            "Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba/action_timeline/skill_10020101.json";

        [MenuItem("Tools/AbilityKit/Demos/Moba/ActionEditor/Export XiaoQiao Skill 1")]
        public static void ExportXiaoQiaoSkillOne()
        {
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(XiaoQiaoAssetPath);
            if (textAsset == null) throw new FileNotFoundException("ActionEditor skill asset is missing", XiaoQiaoAssetPath);
            var asset = Json.Deserialize(typeof(Asset), textAsset.text) as Asset;
            if (!(asset is IActionTimelineRuntimeAsset marked) || !marked.ExportMobaRuntime)
                throw new InvalidDataException("XiaoQiao skill asset must opt in to MOBA runtime export.");
            asset.Init();
            ExportLogicJson(asset, Path.GetFullPath(Path.Combine(Application.dataPath, "..", XiaoQiaoAssetPath)));
            AssetDatabase.Refresh();
            Debug.Log("[ActionEditor] Exported XiaoQiao skill 1 logic and presentation timelines.");
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

            var json = Json.Serialize(dto);
            File.WriteAllText(logicPath, json);

            if (logic != null && presentation != null)
            {
                var mobaLogicPath = Path.Combine(dir ?? string.Empty, name + ".moba.logic.json");
                var mobaPresentationPath = Path.Combine(dir ?? string.Empty, name + ".moba.presentation.json");
                File.WriteAllText(mobaLogicPath, Json.Serialize(logic));
                File.WriteAllText(mobaPresentationPath, Json.Serialize(presentation));
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
