using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Share.Config;

namespace AbilityKit.Demo.Moba.Gameplay.Structures
{
    public enum MobaStructureKind
    {
        Base = 1,
        Tower = 2,
    }

    public sealed class MobaMapStructureResource
    {
        public int Version { get; set; } = 1;
        public int MapId { get; set; }
        public List<MobaMapStructureDefinition> Structures { get; set; } = new List<MobaMapStructureDefinition>();
    }

    public sealed class MobaMapStructureDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public int TeamId { get; set; }
        public int AttributeTemplateId { get; set; }
        public int ModelId { get; set; }
        public MapVector3DTO Position { get; set; }
        public float YawDegrees { get; set; }
        public float Radius { get; set; } = 1f;

        [JsonIgnore]
        public MobaStructureKind ParsedKind => Enum.TryParse(Kind, true, out MobaStructureKind value) ? value : 0;
    }

    public sealed class MobaMapStructureResourceLoader
    {
        public static MobaMapStructureResource LoadResource(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Map structure resource is empty.");
            var resource = JsonConvert.DeserializeObject<MobaMapStructureResource>(json);
            if (resource == null) throw new InvalidOperationException("Map structure resource is invalid.");
            if (resource.Version <= 0) throw new InvalidOperationException("Map structure resource requires a positive Version.");
            if (resource.MapId <= 0) throw new InvalidOperationException("Map structure resource requires a positive MapId.");
            if (resource.Structures == null) resource.Structures = new List<MobaMapStructureDefinition>();
            var ids = new HashSet<int>();
            for (var i = 0; i < resource.Structures.Count; i++)
            {
                var item = resource.Structures[i];
                if (item == null || item.Id <= 0 || !ids.Add(item.Id))
                    throw new InvalidOperationException("Map structure ids must be positive and unique.");
                if (!Enum.IsDefined(typeof(MobaStructureKind), item.ParsedKind))
                    throw new InvalidOperationException($"Map structure kind is invalid. id={item.Id}, kind={item.Kind}");
                if (item.TeamId <= 0) throw new InvalidOperationException($"Map structure team is invalid. id={item.Id}");
                if (item.Radius <= 0f || float.IsNaN(item.Radius) || float.IsInfinity(item.Radius))
                    throw new InvalidOperationException($"Map structure radius must be finite and positive. id={item.Id}");
                if (item.AttributeTemplateId <= 0) throw new InvalidOperationException($"Map structure attribute template is required. id={item.Id}");
                if (float.IsNaN(item.YawDegrees) || float.IsInfinity(item.YawDegrees))
                    throw new InvalidOperationException($"Map structure yaw must be finite. id={item.Id}");
                if (item.Position != null &&
                    (float.IsNaN(item.Position.X) || float.IsInfinity(item.Position.X) ||
                     float.IsNaN(item.Position.Y) || float.IsInfinity(item.Position.Y) ||
                     float.IsNaN(item.Position.Z) || float.IsInfinity(item.Position.Z)))
                    throw new InvalidOperationException($"Map structure position must be finite. id={item.Id}");
            }
            return resource;
        }
    }

    public static class MobaStructurePayloadFields
    {
        public const string StructureActorId = MobaStructureEvents.StructureActorId;
        public const string StructureId = MobaStructureEvents.StructureId;
        public const string Kind = MobaStructureEvents.Kind;
        public const string TeamId = MobaStructureEvents.TeamId;
        public const string SourceActorId = MobaStructureEvents.SourceActorId;
        public const string Damage = MobaStructureEvents.Damage;
        public const string RemainingHp = MobaStructureEvents.RemainingHp;
    }

    public static class MobaStructureEvents
    {
        public const string Damaged = "structure.damaged";
        public const string Destroyed = "structure.destroyed";
        public const string StructureActorId = "structure_actor_id";
        public const string StructureId = "structure_id";
        public const string Kind = "structure_kind";
        public const string TeamId = "team_id";
        public const string SourceActorId = "source_actor_id";
        public const string Damage = "damage";
        public const string RemainingHp = "remaining_hp";
    }

    public sealed class MobaStructureEventArgs
    {
        public MobaStructureEventArgs(int structureActorId, int structureId, MobaStructureKind kind,
            int teamId, int sourceActorId, float damage, float remainingHp)
        {
            StructureActorId = structureActorId;
            StructureId = structureId;
            Kind = kind;
            TeamId = teamId;
            SourceActorId = sourceActorId;
            Damage = damage;
            RemainingHp = remainingHp;
        }

        public int StructureActorId { get; }
        public int StructureId { get; }
        public MobaStructureKind Kind { get; }
        public int TeamId { get; }
        public int SourceActorId { get; }
        public float Damage { get; }
        public float RemainingHp { get; }
    }
}
