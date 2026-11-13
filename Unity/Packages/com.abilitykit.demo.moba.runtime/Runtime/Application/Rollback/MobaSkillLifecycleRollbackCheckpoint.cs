using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Rollback
{
    internal sealed class MobaSkillLifecycleCheckpoint
    {
        public const string FormatName = "moba-skill-lifecycle";
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

    internal sealed class MobaContinuousLocalCheckpoint
    {
        [JsonProperty(Required = Required.Always)] public string Type;
        [JsonProperty(Required = Required.Always)] public string Id;
        [JsonProperty(Required = Required.Always)] public long OwnerId;
        [JsonProperty(Required = Required.Always)] public int State;
        [JsonProperty(Required = Required.Always)] public long ElapsedRaw;
        [JsonProperty(Required = Required.Always)] public long IntervalRaw;
        [JsonProperty(Required = Required.Always)] public bool PausedByTags;
        [JsonProperty(Required = Required.AllowNull)] public string Configuration;

        internal static MobaContinuousLocalCheckpoint Capture(Services.MobaContinuousRuntimeBase runtime) => new MobaContinuousLocalCheckpoint
        {
            Type = runtime.GetType().FullName, Id = runtime.Config.Id, OwnerId = runtime.Config.OwnerId,
            State = (int)runtime.State, ElapsedRaw = runtime.ElapsedRaw, IntervalRaw = runtime.IntervalRemainingRaw,
            // Pipeline definitions have their own complete runner schema validation.
            Configuration = runtime is Services.SkillPipelineContinuousRuntime ? null : JsonConvert.SerializeObject(runtime.Config),
        };

        internal (string, long, string) Key => (Type, OwnerId, Id);
    }

    internal static class MobaSkillLifecycleCheckpointCodec
    {
        internal const int MaxBytes = 16 * 1024 * 1024;
        internal static readonly string[] SectionNames =
        {
            "context-entities", "execution-contexts", "skill-runtime", "buffs", "buff-local",
            "continuous", "trigger-modifiers", "pipelines", "owner-blackboards", "continuous-local",
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
            if (checkpoint.Format != MobaSkillLifecycleCheckpoint.FormatName || checkpoint.Version != 2 ||
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
