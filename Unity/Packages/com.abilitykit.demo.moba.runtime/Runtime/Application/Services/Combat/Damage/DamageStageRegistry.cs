using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Demo.Moba.Services
{
    public static class MobaDamageStageOrders
    {
        public const int Base = 1000;
        public const int Mitigation = 2000;
        public const int Shield = 3000;
        public const int Final = 4000;

        public static bool IsExtensionOrder(int order)
        {
            return (order > Base && order < Mitigation)
                || (order > Mitigation && order < Shield)
                || (order > Shield && order < Final);
        }
    }

    public readonly struct MobaDamageStageDescriptor
    {
        internal MobaDamageStageDescriptor(string id, int order, IMobaDamagePipelineStage stage, bool isCore, long sequence)
        {
            Id = id;
            Order = order;
            Stage = stage;
            IsCore = isCore;
            Sequence = sequence;
        }

        public string Id { get; }
        public int Order { get; }
        public IMobaDamagePipelineStage Stage { get; }
        public bool IsCore { get; }
        internal long Sequence { get; }
    }

    public sealed class MobaDamageStageValidationResult
    {
        private readonly List<string> _errors = new List<string>(4);

        public IReadOnlyList<string> Errors => _errors;
        public bool Succeeded => _errors.Count == 0;

        internal void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error)) _errors.Add(error);
        }
    }

    public interface IMobaDamageStageProvider
    {
        IReadOnlyList<MobaDamageStageDescriptor> GetStages();
        MobaDamageStageValidationResult Validate();
    }

    [WorldService(typeof(IMobaDamageStageProvider), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaDamageStageRegistry), WorldLifetime.Scoped)]
    public sealed class MobaDamageStageRegistry : IMobaDamageStageProvider, IService
    {
        public const string BaseStageId = "core.base";
        public const string MitigationStageId = "core.mitigation";
        public const string ShieldStageId = "core.shield";
        public const string FinalStageId = "core.final";

        private readonly List<MobaDamageStageDescriptor> _registrations = new List<MobaDamageStageDescriptor>(8);
        private IReadOnlyList<MobaDamageStageDescriptor> _stages;
        private long _nextSequence;
        private bool _frozen;

        public MobaDamageStageRegistry(
            MobaDamageMitigationService mitigation = null,
            MobaShieldService shields = null)
        {
            AddCore(BaseStageId, MobaDamageStageOrders.Base, new MobaBaseDamagePipelineStage());
            AddCore(MitigationStageId, MobaDamageStageOrders.Mitigation, new MobaDamageMitigationPipelineStage(mitigation));
            AddCore(ShieldStageId, MobaDamageStageOrders.Shield, new MobaShieldAbsorbPipelineStage(shields));
            AddCore(FinalStageId, MobaDamageStageOrders.Final, new MobaFinalDamagePipelineStage());
        }

        public void RegisterExtension(string id, int order, IMobaDamagePipelineStage stage)
        {
            if (_frozen) throw new InvalidOperationException("Damage stage registry is frozen after pipeline execution begins.");
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Damage stage id is required.", nameof(id));
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (string.IsNullOrWhiteSpace(stage.EventId)) throw new ArgumentException("Damage stage event id is required.", nameof(stage));
            if (!MobaDamageStageOrders.IsExtensionOrder(order))
            {
                throw new ArgumentOutOfRangeException(nameof(order), order, "Extension stages must execute between adjacent core stages and before the health commit boundary.");
            }

            EnsureUnique(id, stage.EventId);
            _registrations.Add(new MobaDamageStageDescriptor(id, order, stage, isCore: false, _nextSequence++));
        }

        public IReadOnlyList<MobaDamageStageDescriptor> GetStages()
        {
            if (_stages != null) return _stages;

            var validation = Validate();
            if (!validation.Succeeded)
            {
                throw new InvalidOperationException("Invalid damage stage configuration: " + string.Join("; ", validation.Errors));
            }

            var sorted = _registrations.ToArray();
            Array.Sort(sorted, CompareDescriptors);
            _stages = Array.AsReadOnly(sorted);
            _frozen = true;
            return _stages;
        }

        public MobaDamageStageValidationResult Validate()
        {
            var result = new MobaDamageStageValidationResult();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var events = new HashSet<string>(StringComparer.Ordinal);
            var coreOrders = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [BaseStageId] = MobaDamageStageOrders.Base,
                [MitigationStageId] = MobaDamageStageOrders.Mitigation,
                [ShieldStageId] = MobaDamageStageOrders.Shield,
                [FinalStageId] = MobaDamageStageOrders.Final,
            };

            for (var i = 0; i < _registrations.Count; i++)
            {
                var descriptor = _registrations[i];
                if (string.IsNullOrWhiteSpace(descriptor.Id)) result.AddError("A damage stage has an empty id.");
                else if (!ids.Add(descriptor.Id)) result.AddError("Duplicate damage stage id: " + descriptor.Id + ".");

                var eventId = descriptor.Stage?.EventId;
                if (descriptor.Stage == null) result.AddError("Damage stage '" + descriptor.Id + "' has no implementation.");
                else if (string.IsNullOrWhiteSpace(eventId)) result.AddError("Damage stage '" + descriptor.Id + "' has an empty event id.");
                else if (!events.Add(eventId)) result.AddError("Duplicate damage stage event id: " + eventId + ".");

                if (descriptor.IsCore)
                {
                    if (!coreOrders.TryGetValue(descriptor.Id, out var expectedOrder) || descriptor.Order != expectedOrder)
                    {
                        result.AddError("Core damage stage order is invalid: " + descriptor.Id + ".");
                    }
                }
                else if (!MobaDamageStageOrders.IsExtensionOrder(descriptor.Order))
                {
                    result.AddError("Extension damage stage is outside the protected core stage intervals: " + descriptor.Id + ".");
                }
            }

            foreach (var core in coreOrders)
            {
                if (!ids.Contains(core.Key)) result.AddError("Missing core damage stage: " + core.Key + ".");
            }

            return result;
        }

        private void AddCore(string id, int order, IMobaDamagePipelineStage stage)
        {
            _registrations.Add(new MobaDamageStageDescriptor(id, order, stage, isCore: true, _nextSequence++));
        }

        private void EnsureUnique(string id, string eventId)
        {
            for (var i = 0; i < _registrations.Count; i++)
            {
                var current = _registrations[i];
                if (string.Equals(current.Id, id, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Duplicate damage stage id: " + id + ".");
                }

                if (string.Equals(current.Stage?.EventId, eventId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Duplicate damage stage event id: " + eventId + ".");
                }
            }
        }

        private static int CompareDescriptors(MobaDamageStageDescriptor left, MobaDamageStageDescriptor right)
        {
            var order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : left.Sequence.CompareTo(right.Sequence);
        }

        public void Dispose()
        {
            _registrations.Clear();
            _stages = null;
            _frozen = false;
            _nextSequence = 0L;
        }
    }
}
