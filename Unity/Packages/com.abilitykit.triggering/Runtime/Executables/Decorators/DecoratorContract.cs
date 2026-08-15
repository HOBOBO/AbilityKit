using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Core.Markers;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Runtime.Context;

namespace AbilityKit.Triggering.Runtime.Executable
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DecoratorImplAttribute : Attribute
    {
        public Type DecoratorType { get; }
        public int Priority { get; }

        public DecoratorImplAttribute(Type decoratorType, int priority = 0)
        {
            DecoratorType = decoratorType;
            Priority = priority;
        }
    }

    public interface ICapabilityApplier
    {
        void Apply(object target, ActionContext ctx);
        void Remove(object target, ActionContext ctx);
        ICapabilityContainer GetOrCreateContainer(ActionContext ctx);
    }

    public interface ICapabilityContainer
    {
        bool Has(string capability);
        void Tick(ActionContext ctx, float deltaTimeMs);
    }

    public interface IDecorator : IComposableExecutable
    {
        Type DecoratorType { get; }
        bool IsReady { get; }
    }

    public interface IDurationDecorator : IDecorator
    {
        float DurationMs { get; set; }
        float RemainingMs { get; }
        bool IsExpired { get; }
        bool CanBeInterrupted { get; set; }
        bool AutoStart { get; set; }
        void Refresh(float additionalMs);
        bool Update(ActionContext ctx, float deltaTimeMs);
        event Action<ActionContext> OnExpired;
    }

    public interface ITagDecorator : IDecorator
    {
        ITagContainer Tags { get; set; }
        string RequiredTags { get; set; }
        string IgnoreTags { get; set; }
        void AddTag(string tagName);
        void RemoveTag(string tagName);
        event Action<ActionContext> OnTagChanged;
    }

    public interface IModifierDecorator : IDecorator
    {
        int SourceId { get; set; }
        IReadOnlyList<ModifierData> GetModifiers();
        void AddModifier(ModifierData modifier);
        bool RemoveModifier(ModifierData modifier);
        void ClearModifiers();
        IModifierApplier Applier { get; set; }
        float Level { get; set; }
        ModifierResult Calculate(float baseValue, IModifierContext context = null);
        ModifierApplyResult ApplyTo(object target, int? sourceId = null);
        event Action<ModifierData> OnModifierApplied;
        event Action<ModifierData> OnModifierRemoved;
    }

    public interface IStackDecorator : IDecorator
    {
        int Stack { get; set; }
        float BaseValue { get; set; }
        float StackMultiplier { get; set; }
        int MaxStack { get; set; }
        float CalculateEffectiveValue(float baseValue);
        void IncrementStack(int amount = 1);
        void DecrementStack(int amount = 1);
        void ResetStack();
        event Action<int, int> OnStackChanged;
    }

    public interface IHierarchyDecorator : IDecorator
    {
        int? ParentId { get; set; }
        bool CascadeOnExpire { get; set; }
        bool CascadeOnInterrupt { get; set; }
        void AddChild(int childId);
        void RemoveChild(int childId);
        IReadOnlyList<int> GetChildren();
        event Action<int, bool> OnHierarchyChanged;
    }

    public interface IContinuousDecorator : IDecorator
    {
        string ContinuationId { get; }
        void OnApplied(ActionContext ctx);
        void OnTick(ActionContext ctx, float deltaTimeMs);
        void OnRemoved(ActionContext ctx);
        bool CanCoexistWith(IContinuousDecorator other);
        bool IsTerminated { get; }
        string TerminationReason { get; }
        void RequestTermination(string reason);
    }

    public readonly struct CapabilityId : IEquatable<CapabilityId>
    {
        public readonly string Namespace;
        public readonly string Name;

        public CapabilityId(string ns, string name)
        {
            Namespace = ns ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public static CapabilityId Invalid => new(string.Empty, string.Empty);
        public static CapabilityId Vehicle => new("Ability", "Vehicle");
        public static CapabilityId Flying => new("Ability", "Flying");
        public static CapabilityId Stealth => new("Ability", "Stealth");
        public static CapabilityId Shapeshift => new("Ability", "Shapeshift");

        public bool IsValid => !string.IsNullOrEmpty(Namespace) || !string.IsNullOrEmpty(Name);
        public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
        public bool Equals(CapabilityId other) => Namespace == other.Namespace && Name == other.Name;
        public override bool Equals(object obj) => obj is CapabilityId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Namespace, Name);
        public override string ToString() => FullName;
        public static bool operator ==(CapabilityId left, CapabilityId right) => left.Equals(right);
        public static bool operator !=(CapabilityId left, CapabilityId right) => !left.Equals(right);
    }

    public interface ICapabilityDecorator : IDecorator
    {
        CapabilityId CapabilityId { get; }
        ICapabilityApplier CapabilityApplier { get; set; }
        void OnApplied(ActionContext ctx);
        void OnTick(ActionContext ctx, float deltaTimeMs);
        void OnRemoved(ActionContext ctx);
        bool CanCoexistWith(ICapabilityDecorator other);
        bool IsActive { get; }
        bool IsTerminated { get; }
        void RequestDeactivate(string reason);
    }
}
