using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Services.Projectile.Launch;

namespace AbilityKit.Demo.Moba.Rollback
{
    internal sealed class MobaSkillLifecycleCheckpoint
    {
        public const string FormatName = "moba-skill-lifecycle";
        public const int CurrentVersion = 11;
        [JsonProperty(Required = Required.Always)] public string Format;
        [JsonProperty(Required = Required.Always)] public int Version;
        [JsonProperty(Required = Required.Always)] public int Frame;
        [JsonProperty(Required = Required.Always)] public int[] ActorIds;
        [JsonProperty(Required = Required.Always)] public MobaSkillLifecycleSection[] Sections;
    }

    internal sealed class MobaSkillLifecycleSection
    {
        [JsonProperty(Required = Required.Always)] public string Name;
        [JsonProperty(Required = Required.Always)] public byte[] Payload;
    }

    internal sealed class MobaExecutionRegistryCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public int Version;
        [JsonProperty(Required = Required.Always)] public long NextContextId;
        [JsonProperty(Required = Required.Always)] public byte[] Nodes;
    }

    internal sealed class MobaAreaRuntimeRollbackCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public int Version;
        [JsonProperty(Required = Required.Always)] public MobaAreaRuntimeRollbackEntry[] Areas;
        [JsonProperty(Required = Required.Always)] public int[] DelayedAreaIds;
    }

    internal sealed class MobaAreaRuntimeRollbackEntry
    {
        [JsonProperty(Required = Required.Always)] public int AreaId;
        [JsonProperty(Required = Required.Always)] public int TemplateId;
        [JsonProperty(Required = Required.Always)] public int OwnerActorId;
        [JsonProperty(Required = Required.Always)] public float CenterX;
        [JsonProperty(Required = Required.Always)] public float CenterY;
        [JsonProperty(Required = Required.Always)] public float CenterZ;
        [JsonProperty(Required = Required.Always)] public float Radius;
        [JsonProperty(Required = Required.Always)] public float InnerRadius;
        [JsonProperty(Required = Required.Always)] public int ShapeKind;
        [JsonProperty(Required = Required.Always)] public float ForwardX;
        [JsonProperty(Required = Required.Always)] public float ForwardY;
        [JsonProperty(Required = Required.Always)] public float ForwardZ;
        [JsonProperty(Required = Required.Always)] public float HalfAngleDeg;
        [JsonProperty(Required = Required.Always)] public float Width;
        [JsonProperty(Required = Required.Always)] public float Length;
        [JsonProperty(Required = Required.Always)] public int CollisionLayerMask;
        [JsonProperty(Required = Required.Always)] public int MaxTargets;
        [JsonProperty(Required = Required.Always)] public int SpawnFrame;
        [JsonProperty(Required = Required.Always)] public int DelayTriggerFrame;
        [JsonProperty(Required = Required.Always)] public long SourceContextId;
        [JsonProperty(Required = Required.Always)] public long RootContextId;
        [JsonProperty(Required = Required.Always)] public long OwnerContextId;
        [JsonProperty(Required = Required.Always)] public long SkillRuntimeId;
        [JsonProperty(Required = Required.Always)] public int SkillRuntimeGeneration;
        [JsonProperty(Required = Required.Always)] public long SkillRuntimeRootContextId;
        [JsonProperty(Required = Required.Always)] public int GroupKey;
        [JsonProperty(Required = Required.Always)] public bool ClaimTargetsWithinGroup;
        [JsonProperty] public int ClaimUnitSubTypeMask;

        internal static MobaAreaRuntimeRollbackEntry From(in MobaAreaRuntimeInfo info) => new MobaAreaRuntimeRollbackEntry
        {
            AreaId = info.AreaId, TemplateId = info.TemplateId, OwnerActorId = info.OwnerActorId,
            CenterX = info.Center.X, CenterY = info.Center.Y, CenterZ = info.Center.Z,
            Radius = info.Radius, InnerRadius = info.InnerRadius, ShapeKind = info.ShapeKind,
            ForwardX = info.Forward.X, ForwardY = info.Forward.Y, ForwardZ = info.Forward.Z,
            HalfAngleDeg = info.HalfAngleDeg, Width = info.Width, Length = info.Length,
            CollisionLayerMask = info.CollisionLayerMask, MaxTargets = info.MaxTargets,
            SpawnFrame = info.SpawnFrame, DelayTriggerFrame = info.DelayTriggerFrame,
            SourceContextId = info.SourceContextId, RootContextId = info.RootContextId, OwnerContextId = info.OwnerContextId,
            SkillRuntimeId = info.SkillRuntimeHandle.RuntimeId,
            SkillRuntimeGeneration = info.SkillRuntimeHandle.Generation,
            SkillRuntimeRootContextId = info.SkillRuntimeHandle.RootContextId,
            GroupKey = info.GroupKey, ClaimTargetsWithinGroup = info.ClaimTargetsWithinGroup,
            ClaimUnitSubTypeMask = info.ClaimUnitSubTypeMask,
        };

        internal MobaAreaRuntimeInfo ToInfo()
        {
            var center = new Vec3(CenterX, CenterY, CenterZ);
            var forward = new Vec3(ForwardX, ForwardY, ForwardZ);
            var runtime = new MobaSkillCastRuntimeHandle(SkillRuntimeId, SkillRuntimeGeneration, SkillRuntimeRootContextId);
            return new MobaAreaRuntimeInfo(AreaId, TemplateId, OwnerActorId, in center, Radius,
                CollisionLayerMask, MaxTargets, SpawnFrame, DelayTriggerFrame, SourceContextId, RootContextId,
                OwnerContextId, ShapeKind, in forward, HalfAngleDeg, Width, Length, runtime,
                GroupKey, ClaimTargetsWithinGroup, InnerRadius, ClaimUnitSubTypeMask);
        }
    }

    internal sealed class MobaContinuousLocalCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public string Type;
        [JsonProperty(Required = Required.Always)] public string Id;
        [JsonProperty(Required = Required.Always)] public long OwnerId;
        [JsonProperty(Required = Required.Always)] public int State;
        [JsonProperty(Required = Required.Always)] public long ElapsedRaw;
        [JsonProperty(Required = Required.Always)] public long IntervalRaw;
        [JsonProperty(Required = Required.Always)] public int ModifierSourceId;
        [JsonProperty(Required = Required.Always)] public bool PausedByTags;
        [JsonProperty(Required = Required.AllowNull)] public string Configuration;
        [JsonProperty(Required = Required.AllowNull)] public MobaManagedMotionCheckpoint Motion;
        [JsonProperty(Required = Required.AllowNull)] public MobaProjectileLaunchContinuousRollbackState ProjectileLaunch;

        internal static MobaContinuousLocalCheckpoint Capture(Services.MobaContinuousRuntimeBase runtime)
        {
            var motion = runtime as MobaMotionContinuousRuntime;
            return new MobaContinuousLocalCheckpoint
            {
                Type = runtime.GetType().FullName, Id = runtime.Config.Id, OwnerId = runtime.Config.OwnerId,
                State = (int)runtime.State, ElapsedRaw = runtime.ElapsedRaw, IntervalRaw = runtime.IntervalRemainingRaw,
                ModifierSourceId = motion?.ModifierSourceId ?? 0,
                // Pipeline definitions have their own complete runner schema validation.
                Configuration = runtime is Services.SkillPipelineContinuousRuntime ? null : JsonConvert.SerializeObject(runtime.Config),
                Motion = motion == null ? null : MobaManagedMotionCheckpoint.Capture(motion),
                ProjectileLaunch = runtime is MobaProjectileLaunchContinuous launch ?
                    MobaProjectileLaunchContinuousRollbackState.Capture(launch) : null,
            };
        }

        internal (string, long, string, int) Key => (Type, OwnerId, Id, ModifierSourceId);
    }

    internal static class MobaSkillLifecycleCheckpointCodec
    {
        internal const int MaxBytes = 16 * 1024 * 1024;
        internal static readonly string[] SectionNames =
        {
            "context-entities", "execution-contexts", "skill-runtime", "areas", "buffs", "buff-local",
            "continuous", "trigger-modifiers", "pipelines", "owner-blackboards", "continuous-local", "locomotion-local",
            "motion-pipelines",
        };

        private static JsonSerializer CreateSerializer() => JsonSerializer.Create(new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            MaxDepth = 64,
        });

        internal static byte[] Encode<T>(T value)
        {
            var payload = Encoding.UTF8.GetBytes(JToken.FromObject(value, CreateSerializer()).ToString(Formatting.None));
            if (payload.Length > MaxBytes) throw new InvalidDataException("Lifecycle checkpoint exceeds the local size limit.");
            return payload;
        }

        internal static T Decode<T>(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > MaxBytes)
                throw new InvalidDataException("Invalid lifecycle checkpoint size.");
            using var reader = new JsonTextReader(new StringReader(Encoding.UTF8.GetString(payload)))
            {
                MaxDepth = 64, DateParseHandling = DateParseHandling.None,
            };
            var token = JToken.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("Lifecycle checkpoint has trailing content.");
            var value = token.ToObject<T>(CreateSerializer());
            if (value == null) throw new InvalidDataException("Lifecycle checkpoint is null.");
            return value;
        }

        internal static Dictionary<string, byte[]> ReadSections(MobaSkillLifecycleCheckpoint checkpoint)
        {
            if (checkpoint.Format != MobaSkillLifecycleCheckpoint.FormatName || checkpoint.Version != MobaSkillLifecycleCheckpoint.CurrentVersion ||
                checkpoint.Frame < 0 || checkpoint.ActorIds == null || checkpoint.Sections == null ||
                checkpoint.Sections.Length != SectionNames.Length)
                throw new InvalidDataException("Unsupported or incomplete lifecycle checkpoint.");
            var sections = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var section in checkpoint.Sections)
                if (section == null || Array.IndexOf(SectionNames, section.Name) < 0 ||
                    section.Payload == null || section.Payload.Length == 0 || !sections.TryAdd(section.Name, section.Payload))
                    throw new InvalidDataException("Invalid, duplicate, or unknown lifecycle section.");
            return sections;
        }
    }

    /// <summary>
    /// A detached, validated local checkpoint. Preparation never registers live objects.
    /// Committing lifecycle state is a separate operation, not implied by possessing this plan.
    /// </summary>
    public sealed class MobaSkillLifecycleRestorePlan
    {
        private readonly byte[] _checkpoint;
        public int Frame { get; }
        public int ActorCount { get; }
        public int SkillRuntimeCount { get; }
        public int BuffCount { get; }
        public int BuffContinuousCount { get; }

        internal MobaSkillLifecycleRestorePlan(byte[] checkpoint, int frame, int actors, int skills, int buffs, int continuous)
        {
            _checkpoint = (byte[])checkpoint.Clone();
            Frame = frame; ActorCount = actors; SkillRuntimeCount = skills;
            BuffCount = buffs; BuffContinuousCount = continuous;
        }

        public byte[] CopyCheckpoint() => (byte[])_checkpoint.Clone();
    }
}
