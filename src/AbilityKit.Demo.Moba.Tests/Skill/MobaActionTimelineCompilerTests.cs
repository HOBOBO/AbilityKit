using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.ActionSchema;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.ActionTimeline;
using AbilityKit.Game.Flow.Battle.Presentation.Timeline;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaActionTimelineCompilerTests
{
    [Fact]
    public void Partition_keeps_runtime_clips_separate_and_rejects_cross_runtime_loading()
    {
        var source = CreateSource();
        var logic = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Logic);
        var presentation = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Presentation);

        Assert.Equal("AbilityKit.ActionEditorImpl.ExecuteEffect", Assert.Single(Assert.Single(Assert.Single(logic.groups).tracks).clips).type);
        Assert.Equal("AbilityKit.ActionEditorImpl.PlayAnimation", Assert.Single(Assert.Single(Assert.Single(presentation.groups).tracks).clips).type);

        var json = Newtonsoft.Json.JsonConvert.SerializeObject(presentation);
        Assert.Throws<InvalidDataException>(() => ActionTimelineJson.LoadForRuntime(json, ActionTimelineRuntimeTypes.Logic));
        Assert.Throws<InvalidDataException>(() => new MobaTimelinePlayer(presentation, MobaDefaultClipHandlers.CreateRegistry(), null));
        Assert.Equal(1, ActionTimelineJson.LoadForRuntime(json, ActionTimelineRuntimeTypes.Presentation).schemaVersion);
        logic.groups[0].tracks[0].clips[0].args["effectId"] = "99";
        Assert.Equal("10", source.groups[0].tracks[0].clips[0].args["effectId"]);
    }

    [Fact]
    public void Active_clip_without_a_runtime_kind_cannot_be_published()
    {
        var source = CreateSource();
        source.groups[0].tracks[0].clips[0].runtimeType = null;
        Assert.Throws<InvalidDataException>(() => ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Logic));
    }

    [Fact]
    public void Logic_compilation_rejects_unsupported_actor_binding()
    {
        var source = CreateSource();
        source.groups[0].actorId = 2;
        var logic = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Logic);
        Assert.Throws<InvalidDataException>(() => MobaActionTimelineCompiler.CompileLogicPhase(logic));
    }

    [Fact]
    public void Logic_compilation_quantizes_and_orders_effects_without_changing_tie_order()
    {
        var source = CreateSource();
        var clips = source.groups[0].tracks[0].clips;
        clips[0].start = 0.5f;
        clips[0].args["effectId"] = "11";
        clips.Add(new ClipDto
        {
            type = "AbilityKit.ActionEditorImpl.ExecuteEffect",
            runtimeType = ActionTimelineRuntimeTypes.Logic,
            start = 0f,
            args = new Dictionary<string, string> { ["effectId"] = "22" }
        });
        clips.Add(new ClipDto
        {
            type = "AbilityKit.ActionEditorImpl.ExecuteEffect",
            runtimeType = ActionTimelineRuntimeTypes.Logic,
            start = 0.5f,
            args = new Dictionary<string, string> { ["effectId"] = "33" }
        });

        var partition = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Logic);
        var phase = MobaActionTimelineCompiler.CompileLogicPhase(partition);

        Assert.Equal(1000, phase.DurationMs);
        Assert.Equal(new[] { 22, 11, 33 }, Array.ConvertAll(phase.Events, e => e.EffectId));
        Assert.Equal(new[] { 0, 500, 500 }, Array.ConvertAll(phase.Events, e => e.AtMs));
        Assert.All(phase.Events, e => Assert.Equal(0, e.ExecuteMode));
    }

    [Fact]
    public void Logic_compilation_rejects_invalid_effect_and_visual_payload()
    {
        var source = CreateSource();
        var logic = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Logic);
        logic.groups[0].tracks[0].clips[0].args["effectId"] = "0";
        Assert.Throws<InvalidDataException>(() => MobaActionTimelineCompiler.CompileLogicPhase(logic));

        var visual = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Presentation);
        Assert.Throws<InvalidDataException>(() => MobaActionTimelineCompiler.CompileLogicPhase(visual));
    }

    [Fact]
    public void Presentation_runtime_recovers_active_animation_and_stops_by_skill_instance()
    {
        var source = CreateSource();
        var presentation = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Presentation);
        var clip = presentation.groups[0].tracks[0].clips[0];
        clip.start = 0.2f;
        clip.length = 0.5f;
        var sink = new RecordingPresentationSink();
        var runtime = new MobaSkillPresentationTimelineRuntime(77, presentation, sink);

        runtime.StartAt(0.4f);
        Assert.Equal(77, Assert.Single(sink.Starts).InstanceId);
        Assert.InRange(sink.Starts[0].Offset, 0.19f, 0.21f);
        runtime.Tick(0.5f);
        Assert.Single(sink.Starts);
        runtime.Stop();
        runtime.Stop();
        Assert.Equal(77, Assert.Single(sink.Stops));
    }

    [Fact]
    public void Presentation_runtime_does_not_replay_expired_clips_or_accept_logic_payloads()
    {
        var source = CreateSource();
        var sink = new RecordingPresentationSink();
        var visual = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Presentation);
        visual.groups[0].tracks[0].clips[0].length = 0.2f;
        var runtime = new MobaSkillPresentationTimelineRuntime(77, visual, sink);
        runtime.StartAt(0.5f);
        Assert.Empty(sink.Starts);

        var logic = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Logic);
        Assert.Throws<InvalidDataException>(() => new MobaSkillPresentationTimelineRuntime(77, logic, sink));
    }

    [Fact]
    public void XiaoQiao_skill_one_export_matches_existing_authoritative_flow()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../Unity/Packages/com.abilitykit.demo.moba.view.runtime/Resources/moba"));
        var logicJson = File.ReadAllText(Path.Combine(root, "action_timeline/skill_10020101.moba.logic.json"));
        var presentationJson = File.ReadAllText(Path.Combine(root, "action_timeline/skill_10020101.moba.presentation.json"));
        var editorAsset = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(
            Path.Combine(root, "action_timeline/skill_10020101.json")));
        Assert.True((bool?)editorAsset["exportMobaRuntime"]);
        Assert.Equal("AbilityKit.ActionEditorImpl.SkillAsset", (string?)editorAsset["$type"]);
        Assert.Contains("4fc1964cb86d4e2c91e70badea48c720", File.ReadAllText(
            Path.Combine(root, "../../Configs/Moba/SkillFlowCO.asset")));
        var logic = MobaActionTimelineCompiler.CompileLogicPhase(logicJson);
        Assert.Equal(600, logic.DurationMs);
        var effect = Assert.Single(logic.Events);
        Assert.Equal(10020101, effect.EffectId);
        Assert.Equal(0, effect.AtMs);
        Assert.Equal("xq_skill_1_cast", effect.EventTag);

        var flow = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(
            Path.Combine(root, "skill_flows/skill_flows_10020101.json")));
        var phases = (Newtonsoft.Json.Linq.JArray)flow["Phases"]!;
        Assert.Equal("skill_10020101_release", (string?)phases[0]?["PhaseId"]);
        Assert.Equal("skill_10020101_commit", (string?)phases[1]?["PhaseId"]);
        var timeline = phases[2]?["Timeline"]!;
        Assert.Equal(logic.DurationMs, (int)timeline["DurationMs"]!);
        var flowEffect = Assert.Single((Newtonsoft.Json.Linq.JArray)timeline["Events"]!);
        Assert.Equal(effect.EffectId, (int)flowEffect["EffectId"]!);
        Assert.Equal(effect.AtMs, (int)flowEffect["AtMs"]!);
        Assert.Equal(effect.EventTag, (string?)flowEffect["EventTag"]);
        var aggregate = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(
            Path.Combine(root, "skill_flows.json")));
        var aggregateFlow = Assert.Single(aggregate,
            entry => (int?)entry?["Id"] == 10020101);
        Assert.True(Newtonsoft.Json.Linq.JToken.DeepEquals(timeline,
            aggregateFlow["Phases"]?[2]?["Timeline"]));

        Assert.Throws<InvalidDataException>(() => MobaActionTimelineCompiler.CompileLogicPhase(presentationJson));
        var sink = new RecordingPresentationSink();
        var driver = new MobaSkillPresentationCastDriver(presentationJson, sink);
        driver.Seek((long)int.MaxValue + 17, 0f);
        Assert.Equal("AbilityKit.ActionEditorImpl.PlayParticle", Assert.Single(sink.Starts).Type);
        Assert.Equal((long)int.MaxValue + 17, sink.Starts[0].InstanceId);
        driver.Seek((long)int.MaxValue + 17, .4f);
        driver.Seek((long)int.MaxValue + 18, 0f);
        Assert.Equal(2, sink.Starts.Count);
        Assert.Equal((long)int.MaxValue + 17, Assert.Single(sink.Stops));
        driver.Seek((long)int.MaxValue + 18, 0f);
        Assert.Equal(2, sink.Starts.Count);
        driver.Stop();
        Assert.Equal(2, sink.Stops.Count);
    }

    [Fact]
    public void Presentation_cast_driver_restarts_on_rollback_and_skips_expired_visuals_on_reconnect()
    {
        var source = CreateSource();
        source.groups[0].tracks[0].clips[1].type = "AbilityKit.ActionEditorImpl.PlayParticle";
        source.groups[0].tracks[0].clips[1].args = new Dictionary<string, string> { ["resourceKey"] = "effect/test" };
        source.groups[0].tracks[0].clips[1].length = .3f;
        var visual = ActionTimelinePartition.Create(source, ActionTimelineRuntimeTypes.Presentation);
        var sink = new RecordingPresentationSink();
        var driver = new MobaSkillPresentationCastDriver(Newtonsoft.Json.JsonConvert.SerializeObject(visual), sink);
        driver.Seek(10, .5f);
        Assert.Empty(sink.Starts);
        driver.Seek(10, .1f);
        Assert.Single(sink.Starts);
        Assert.Equal(10, Assert.Single(sink.Stops));
        driver.Seek(0, 0);
        Assert.Equal(2, sink.Stops.Count);
    }

    private sealed class RecordingPresentationSink : IMobaSkillPresentationTimelineSink
    {
        public List<(long InstanceId, float Offset, string Type)> Starts = new();
        public List<long> Stops = new();

        public void OnClipStart(long skillInstanceId, GroupDto group, ClipDto clip, float offsetSeconds)
        {
            Starts.Add((skillInstanceId, offsetSeconds, clip.type));
        }

        public void OnTimelineStop(long skillInstanceId)
        {
            Stops.Add(skillInstanceId);
        }
    }

    private static SkillAssetDto CreateSource()
    {
        return new SkillAssetDto
        {
            length = 1f,
            groups = new List<GroupDto>
            {
                new GroupDto
                {
                    name = "caster",
                    active = true,
                    tracks = new List<TrackDto>
                    {
                        new TrackDto
                        {
                            name = "signals",
                            active = true,
                            clips = new List<ClipDto>
                            {
                                new ClipDto
                                {
                                    type = "AbilityKit.ActionEditorImpl.ExecuteEffect",
                                    runtimeType = ActionTimelineRuntimeTypes.Logic,
                                    args = new Dictionary<string, string> { ["effectId"] = "10" }
                                },
                                new ClipDto
                                {
                                    type = "AbilityKit.ActionEditorImpl.PlayAnimation",
                                    runtimeType = ActionTimelineRuntimeTypes.Presentation,
                                    args = new Dictionary<string, string> { ["clipKey"] = "attack" }
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}
