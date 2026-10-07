using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaModifierResolutionArtifact
    {
        public string SchemaVersion { get; set; }
        public long EvictedResolutionCount { get; set; }
        public List<MobaModifierResolutionSample> RecentResolutions { get; set; } =
            new List<MobaModifierResolutionSample>();
        public List<MobaModifierProjectionSample> CurrentProjections { get; set; } =
            new List<MobaModifierProjectionSample>();
    }

    public sealed class MobaModifierResolutionSample
    {
        public int Frame { get; set; }
        public int OwnerActorId { get; set; }
        public int ParameterId { get; set; }
        public MobaParameterStage Stage { get; set; }
        public MobaModifierIdentitySample Identity { get; set; }
        public float BaseValue { get; set; }
        public float FinalValue { get; set; }
        public int MatchedCount { get; set; }
        public MobaParameterIdentityFields MissingSelectorFields { get; set; }
    }

    public sealed class MobaModifierProjectionSample
    {
        public int OwnerActorId { get; set; }
        public int ParameterId { get; set; }
        public int SourceId { get; set; }
        public int Priority { get; set; }
        public int Operation { get; set; }
        public int MagnitudeType { get; set; }
        public float MagnitudeBaseValue { get; set; }
        public MobaModifierIdentitySample Selector { get; set; }
    }

    public sealed class MobaModifierIdentitySample
    {
        public int Slot { get; set; }
        public int BaseSkillId { get; set; }
        public int EffectiveSkillId { get; set; }
        public int LauncherTemplateId { get; set; }
        public int ProjectileTemplateId { get; set; }
        public int AreaTemplateId { get; set; }
        public int SummonTemplateId { get; set; }
        public int BuffTemplateId { get; set; }

        internal static MobaModifierIdentitySample FromRecord(in MobaParameterRecord record) =>
            new MobaModifierIdentitySample
            {
                Slot = record.Slot,
                BaseSkillId = record.BaseSkillId,
                EffectiveSkillId = record.EffectiveSkillId,
                LauncherTemplateId = record.LauncherTemplateId,
                ProjectileTemplateId = record.ProjectileTemplateId,
                AreaTemplateId = record.AreaTemplateId,
                SummonTemplateId = record.SummonTemplateId,
                BuffTemplateId = record.BuffTemplateId,
            };

        internal static MobaModifierIdentitySample FromSelector(in MobaParameterSelector selector) =>
            new MobaModifierIdentitySample
            {
                Slot = selector.Slot,
                BaseSkillId = selector.BaseSkillId,
                EffectiveSkillId = selector.EffectiveSkillId,
                LauncherTemplateId = selector.LauncherTemplateId,
                ProjectileTemplateId = selector.ProjectileTemplateId,
                AreaTemplateId = selector.AreaTemplateId,
                SummonTemplateId = selector.SummonTemplateId,
                BuffTemplateId = selector.BuffTemplateId,
            };
    }

    public static class MobaModifierResolutionArtifactCodec
    {
        public const string Version = "moba-modifier-resolutions.v2";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        public static string Export(MobaSkillParamModifierService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            var artifact = new MobaModifierResolutionArtifact
            {
                SchemaVersion = Version,
                EvictedResolutionCount = service.EvictedResolutionTraceCount,
            };
            var traces = new List<MobaParameterResolutionTrace>();
            service.CopyRecentResolutionsTo(traces);
            for (var i = 0; i < traces.Count; i++)
            {
                var trace = traces[i];
                MobaSkillParamModifierKeys.TryGetParameterId(trace.Key, out var parameterId);
                var record = trace.Record;
                artifact.RecentResolutions.Add(new MobaModifierResolutionSample
                {
                    Frame = trace.Frame,
                    OwnerActorId = trace.Owner.Id,
                    ParameterId = parameterId,
                    Stage = record.Stage,
                    Identity = MobaModifierIdentitySample.FromRecord(in record),
                    BaseValue = trace.BaseValue,
                    FinalValue = trace.FinalValue,
                    MatchedCount = trace.MatchedCount,
                    MissingSelectorFields = trace.MissingSelectorFields,
                });
            }

            var current = service.CaptureRollbackSnapshot().Entries;
            for (var i = 0; i < current.Length; i++)
            {
                var entry = current[i];
                var modifier = entry.Modifier;
                MobaSkillParamModifierKeys.TryGetParameterId(modifier.Key, out var parameterId);
                var selector = entry.Selector;
                artifact.CurrentProjections.Add(new MobaModifierProjectionSample
                {
                    OwnerActorId = entry.OwnerId,
                    ParameterId = parameterId,
                    SourceId = modifier.SourceId,
                    Priority = modifier.Priority,
                    Operation = (int)modifier.Op,
                    MagnitudeType = (int)modifier.Magnitude.Type,
                    MagnitudeBaseValue = modifier.Magnitude.BaseValue,
                    Selector = MobaModifierIdentitySample.FromSelector(in selector),
                });
            }
            return JsonConvert.SerializeObject(artifact, Settings);
        }

        public static MobaModifierResolutionArtifact Import(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Artifact is empty.", nameof(json));
            var artifact = JsonConvert.DeserializeObject<MobaModifierResolutionArtifact>(json, Settings);
            if (artifact == null ||
                artifact.SchemaVersion != Version && artifact.SchemaVersion != "moba-modifier-resolutions.v1" ||
                artifact.RecentResolutions == null || artifact.CurrentProjections == null)
                throw new InvalidOperationException("Unsupported modifier resolution artifact.");
            return artifact;
        }
    }
}
