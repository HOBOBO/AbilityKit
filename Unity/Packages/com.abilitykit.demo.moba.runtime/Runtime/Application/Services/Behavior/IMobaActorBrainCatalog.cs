using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.World.Services;
using Newtonsoft.Json;

namespace AbilityKit.Demo.Moba.Services.Behavior
{
    public static class MobaBrainSourceKinds
    {
        public const int BattleTemplate = 1;
        public const int Summon = 2;
    }

    public enum MobaBrainDriverKind
    {
        BTree = 0,
        Hfsm = 1,
    }

    public readonly struct MobaActorBrainDefinition
    {
        public MobaActorBrainDefinition(
            int brainId,
            MobaBrainDriverKind driverKind,
            string decisionName,
            MobaBrainSkillSelectionPolicy skillSelectionPolicy = MobaBrainSkillSelectionPolicy.FirstReady)
        {
            if (brainId <= 0) throw new ArgumentOutOfRangeException(nameof(brainId));
            if (string.IsNullOrWhiteSpace(decisionName))
                throw new ArgumentException("A brain decision name is required.", nameof(decisionName));
 
            BrainId = brainId;
            DriverKind = driverKind;
            DecisionName = decisionName;
            SkillSelectionPolicy = skillSelectionPolicy;
        }
 
        public int BrainId { get; }
 
        public MobaBrainDriverKind DriverKind { get; }
 
        public string DecisionName { get; }
 
        public MobaBrainSkillSelectionPolicy SkillSelectionPolicy { get; }
    }

    public interface IMobaActorBrainCatalog : IService
    {
        IReadOnlyList<MobaActorBrainDefinition> Definitions { get; }

        bool TryGet(int brainId, out MobaActorBrainDefinition definition);
    }

    public sealed class MobaActorBrainCatalog : IMobaActorBrainCatalog
    {
        private readonly Dictionary<int, MobaActorBrainDefinition> _definitions = new();

        public IReadOnlyList<MobaActorBrainDefinition> Definitions
        {
            get
            {
                var definitions = new List<MobaActorBrainDefinition>(_definitions.Values);
                definitions.Sort((left, right) => left.BrainId.CompareTo(right.BrainId));
                return definitions;
            }
        }

        public void Register(in MobaActorBrainDefinition definition)
        {
            if (_definitions.ContainsKey(definition.BrainId))
                throw new InvalidOperationException($"MOBA brain id '{definition.BrainId}' is duplicated.");
            _definitions.Add(definition.BrainId, definition);
        }

        public bool TryGet(int brainId, out MobaActorBrainDefinition definition)
        {
            if (brainId > 0) return _definitions.TryGetValue(brainId, out definition);
            definition = default;
            return false;
        }

        public void Dispose()
        {
            _definitions.Clear();
        }
    }

    public static class MobaActorBrainCatalogJsonLoader
    {
        public const string DefaultResourcePath = "moba/brains";

        public static int Load(
            ITextAssetLoader loader,
            MobaActorBrainCatalog catalog,
            string resourcePath = DefaultResourcePath)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (!loader.TryLoadText(resourcePath, out var json) || string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException($"MOBA brain catalog resource '{resourcePath}' was not found.");
            return LoadJson(json, catalog);
        }

        public static int LoadJson(string json, MobaActorBrainCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("MOBA brain catalog JSON is required.", nameof(json));

            var definitions = JsonConvert.DeserializeObject<List<BrainDefinition>>(
                json,
                new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error })
                ?? throw new InvalidOperationException("MOBA brain catalog JSON must be an array.");

            for (var i = 0; i < definitions.Count; i++)
            {
                var source = definitions[i]
                    ?? throw new InvalidOperationException($"MOBA brain definition at index {i} is null.");
                var definition = new MobaActorBrainDefinition(
                    source.BrainId,
                    ParseDriverKind(source.DriverKind),
                    source.DecisionName,
                    ParseSkillSelectionPolicy(source.SkillSelectionPolicy));
                catalog.Register(in definition);
            }

            return definitions.Count;
        }

        private static MobaBrainDriverKind ParseDriverKind(string driverKind)
        {
            return driverKind switch
            {
                "behaviorTree" => MobaBrainDriverKind.BTree,
                "hfsm" => MobaBrainDriverKind.Hfsm,
                _ => throw new InvalidOperationException($"Unsupported MOBA brain driver kind '{driverKind}'."),
            };
        }

        private static MobaBrainSkillSelectionPolicy ParseSkillSelectionPolicy(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy)) return MobaBrainSkillSelectionPolicy.FirstReady;
            return policy switch
            {
                "firstReady" => MobaBrainSkillSelectionPolicy.FirstReady,
                "highestRange" => MobaBrainSkillSelectionPolicy.HighestRange,
                _ => throw new InvalidOperationException($"Unsupported MOBA brain skill selection policy '{policy}'."),
            };
        }

        private sealed class BrainDefinition
        {
            public int BrainId { get; set; }
            public string DriverKind { get; set; }
            public string DecisionName { get; set; }
            public string SkillSelectionPolicy { get; set; }
        }
    }
}
