using System;
using System.IO;
using AbilityKit.ActionSchema;
using NBC.ActionEditor;
using UnityEngine;

namespace NBC.ActionEditor
{
    public static class LogicJsonExporter
    {
        public static void ExportLogicJson(Asset assetData, string editorJsonPath)
        {
            if (assetData == null) return;
            if (string.IsNullOrEmpty(editorJsonPath)) return;

            var dto = ToDto(assetData);

            var dir = Path.GetDirectoryName(editorJsonPath);
            var name = Path.GetFileNameWithoutExtension(editorJsonPath);
            var logicPath = Path.Combine(dir ?? string.Empty, name + ".logic.json");

            var json = Json.Serialize(dto);
            File.WriteAllText(logicPath, json);
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
