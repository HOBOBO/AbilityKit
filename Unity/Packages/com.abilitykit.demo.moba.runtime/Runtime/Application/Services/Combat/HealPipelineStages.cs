using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Demo.Moba.Services
{
    public interface IMobaHealPipelineStage
    {
        string Id { get; }
        int Order { get; }
        void Execute(MobaHealCalculation calculation);
    }

    public sealed class MobaHealCalculation
    {
        internal MobaHealCalculation(MobaHealTransaction transaction)
        {
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            Value = transaction.Value;
        }

        public MobaHealTransaction Transaction { get; }
        public float Value { get; private set; }

        public void SetValue(float value)
        {
            Value = value;
        }
    }

    public static class MobaHealStageOrders
    {
        public const int Base = 1000;
        public const int Final = 4000;

        public static bool IsExtensionOrder(int order)
        {
            return order > Base && order < Final;
        }
    }

    public sealed class MobaHealStageValidationResult
    {
        private readonly List<string> _errors = new List<string>(4);

        public IReadOnlyList<string> Errors => _errors;
        public bool Succeeded => _errors.Count == 0;

        internal void AddError(string error)
        {
            if (!string.IsNullOrWhiteSpace(error)) _errors.Add(error);
        }
    }

    public interface IMobaHealStageProvider
    {
        IReadOnlyList<IMobaHealPipelineStage> GetStages();
        MobaHealStageValidationResult Validate();
    }

    [WorldService(typeof(IMobaHealStageProvider), WorldLifetime.Scoped)]
    [WorldService(typeof(MobaHealStageRegistry), WorldLifetime.Scoped)]
    public sealed class MobaHealStageRegistry : IMobaHealStageProvider, IService
    {
        private readonly List<IMobaHealPipelineStage> _registrations = new List<IMobaHealPipelineStage>(4);
        private IReadOnlyList<IMobaHealPipelineStage> _stages;
        private bool _frozen;

        public MobaHealStageRegistry()
        {
            _registrations.Add(new MobaHealBaseStage());
            _registrations.Add(new MobaHealFinalStage());
        }

        public void RegisterExtension(IMobaHealPipelineStage stage)
        {
            if (_frozen) throw new InvalidOperationException("Heal stage registry is frozen after pipeline execution begins.");
            if (stage == null) throw new ArgumentNullException(nameof(stage));
            if (string.IsNullOrWhiteSpace(stage.Id)) throw new ArgumentException("Heal stage id is required.", nameof(stage));
            if (!MobaHealStageOrders.IsExtensionOrder(stage.Order))
                throw new ArgumentOutOfRangeException(nameof(stage), stage.Order,
                    "Extension heal stages must execute between the protected base and final stages.");
            EnsureUnique(stage.Id);
            _registrations.Add(stage);
        }

        public IReadOnlyList<IMobaHealPipelineStage> GetStages()
        {
            if (_stages != null) return _stages;
            var validation = Validate();
            if (!validation.Succeeded)
                throw new InvalidOperationException("Invalid heal stage configuration: " + string.Join("; ", validation.Errors));

            var sorted = _registrations.ToArray();
            Array.Sort(sorted, (left, right) =>
            {
                var order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.Id, right.Id);
            });
            _stages = Array.AsReadOnly(sorted);
            _frozen = true;
            return _stages;
        }

        public MobaHealStageValidationResult Validate()
        {
            var result = new MobaHealStageValidationResult();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var hasBase = false;
            var hasFinal = false;
            for (var i = 0; i < _registrations.Count; i++)
            {
                var stage = _registrations[i];
                if (stage == null)
                {
                    result.AddError("A heal stage has no implementation.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(stage.Id) || !ids.Add(stage.Id))
                    result.AddError("Duplicate or empty heal stage id: " + stage.Id + ".");
                if (stage is MobaHealBaseStage)
                {
                    hasBase = true;
                    if (stage.Order != MobaHealStageOrders.Base)
                        result.AddError("Core heal base stage order is invalid.");
                }
                else if (stage is MobaHealFinalStage)
                {
                    hasFinal = true;
                    if (stage.Order != MobaHealStageOrders.Final)
                        result.AddError("Core heal final stage order is invalid.");
                }
                else if (!MobaHealStageOrders.IsExtensionOrder(stage.Order))
                {
                    result.AddError("Extension heal stage is outside the protected core interval: " + stage.Id + ".");
                }
            }
            if (!hasBase) result.AddError("Missing core heal stage: core.base.");
            if (!hasFinal) result.AddError("Missing core heal stage: core.final.");
            return result;
        }

        private void EnsureUnique(string id)
        {
            for (var i = 0; i < _registrations.Count; i++)
                if (string.Equals(_registrations[i]?.Id, id, StringComparison.Ordinal))
                    throw new InvalidOperationException("Duplicate heal stage id: " + id + ".");
        }

        public void Dispose()
        {
            _registrations.Clear();
            _stages = null;
            _frozen = false;
        }
    }

    public sealed class MobaHealBaseStage : IMobaHealPipelineStage
    {
        public string Id => "core.base";
        public int Order => MobaHealStageOrders.Base;

        public void Execute(MobaHealCalculation calculation)
        {
            if (calculation == null) return;
            calculation.SetValue(IsFinitePositive(calculation.Value) ? calculation.Value : 0f);
        }

        private static bool IsFinitePositive(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    public sealed class MobaHealFinalStage : IMobaHealPipelineStage
    {
        public string Id => "core.final";
        public int Order => MobaHealStageOrders.Final;

        public void Execute(MobaHealCalculation calculation)
        {
            if (calculation == null) return;
            calculation.SetValue(calculation.Value > 0f && !float.IsNaN(calculation.Value)
                && !float.IsInfinity(calculation.Value) ? calculation.Value : 0f);
        }
    }
}
