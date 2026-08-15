using System;
using System.Collections.Generic;
using AbilityKit.Continuous;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Runtime.Context;

namespace AbilityKit.Triggering.Runtime.Executable
{
    internal abstract class DefaultDecoratorBase : IDecorator
    {
        public abstract string Name { get; }
        public abstract ExecutableMetadata Metadata { get; }
        public abstract Type DecoratorType { get; }
        public virtual bool IsReady => true;
        public ISimpleExecutable Inner { get; set; }
        public virtual bool OnBeforeExecute(ActionContext ctx) => true;
        public virtual void OnAfterExecute(ActionContext ctx, ref ExecutionResult result) { }
        public virtual ExecutionResult Execute(ActionContext ctx) => Inner?.Execute(ctx) ?? ExecutionResult.Success();
    }

    [DecoratorImpl(typeof(IDurationDecorator))]
    internal sealed class DefaultDurationDecorator : DefaultDecoratorBase, IDurationDecorator
    {
        public DefaultDurationDecorator() { }

        public DefaultDurationDecorator(float durationMs)
        {
            DurationMs = durationMs;
        }

        public override string Name => "Duration(" + (Inner?.Name ?? "null") + ")";
        public override ExecutableMetadata Metadata => new(3000, "Duration");
        public override Type DecoratorType => typeof(IDurationDecorator);
        public float DurationMs { get; set; } = -1;
        public float RemainingMs => DurationMs;
        public bool IsExpired => false;
        public bool CanBeInterrupted { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public event Action<ActionContext> OnExpired;
        public bool Update(ActionContext ctx, float deltaTimeMs) => false;
        public void Refresh(float additionalMs)
        {
            DurationMs += additionalMs;
        }
    }

    [DecoratorImpl(typeof(ITagDecorator))]
    internal sealed class DefaultTagDecorator : DefaultDecoratorBase, ITagDecorator
    {
        private readonly List<string> _tagNames = new();

        public DefaultTagDecorator() { }

        public DefaultTagDecorator(params string[] tagNames)
        {
            if (tagNames == null) return;
            foreach (var tagName in tagNames)
            {
                AddTag(tagName);
            }
        }

        public override string Name => "Tag(" + (Inner?.Name ?? "null") + ")";
        public override ExecutableMetadata Metadata => new(3001, "Tag");
        public override Type DecoratorType => typeof(ITagDecorator);
        public ITagContainer Tags { get; set; }
        public string RequiredTags { get; set; } = string.Empty;
        public string IgnoreTags { get; set; } = string.Empty;
        public event Action<ActionContext> OnTagChanged;
        public void AddTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName) || _tagNames.Contains(tagName)) return;
            _tagNames.Add(tagName);
            OnTagChanged?.Invoke(null);
        }

        public void RemoveTag(string tagName)
        {
            if (!_tagNames.Remove(tagName)) return;
            OnTagChanged?.Invoke(null);
        }
    }

    [DecoratorImpl(typeof(IModifierDecorator))]
    internal sealed class DefaultModifierDecorator : DefaultDecoratorBase, IModifierDecorator
    {
        private readonly List<ModifierData> _modifiers = new();

        public DefaultModifierDecorator() { }

        public DefaultModifierDecorator(params ModifierData[] modifiers)
        {
            AddRange(modifiers);
        }

        public DefaultModifierDecorator(IModifierApplier applier, params ModifierData[] modifiers)
        {
            Applier = applier;
            AddRange(modifiers);
        }

        public override string Name => "Modifier(" + (Inner?.Name ?? "null") + ", " + _modifiers.Count + ")";
        public override ExecutableMetadata Metadata => new(3002, "Modifier");
        public override Type DecoratorType => typeof(IModifierDecorator);
        public int SourceId { get; set; }
        public event Action<ModifierData> OnModifierApplied;
        public event Action<ModifierData> OnModifierRemoved;
        public IModifierApplier Applier { get; set; }
        public float Level { get; set; } = 1f;
        public IReadOnlyList<ModifierData> GetModifiers() => _modifiers;
        public void AddModifier(ModifierData modifier)
        {
            _modifiers.Add(modifier);
            OnModifierApplied?.Invoke(modifier);
        }

        public bool RemoveModifier(ModifierData modifier)
        {
            var removed = _modifiers.Remove(modifier);
            if (removed) OnModifierRemoved?.Invoke(modifier);
            return removed;
        }

        public void ClearModifiers()
        {
            _modifiers.Clear();
        }

        public ModifierResult Calculate(float baseValue, IModifierContext context = null) => default;
        public ModifierApplyResult ApplyTo(object target, int? sourceId = null) => default;

        private void AddRange(ModifierData[] modifiers)
        {
            if (modifiers == null) return;
            foreach (var modifier in modifiers)
            {
                AddModifier(modifier);
            }
        }
    }

    [DecoratorImpl(typeof(IStackDecorator))]
    internal sealed class DefaultStackDecorator : DefaultDecoratorBase, IStackDecorator
    {
        public DefaultStackDecorator() { }

        public DefaultStackDecorator(int initialStack, float stackMultiplier)
        {
            Stack = initialStack;
            StackMultiplier = stackMultiplier;
        }

        public override string Name => "Stack(" + (Inner?.Name ?? "null") + ", " + Stack + ")";
        public override ExecutableMetadata Metadata => new(3003, "Stack");
        public override Type DecoratorType => typeof(IStackDecorator);
        public int Stack { get; set; } = 1;
        public float BaseValue { get; set; }
        public float StackMultiplier { get; set; } = 1f;
        public int MaxStack { get; set; } = int.MaxValue;
        public event Action<int, int> OnStackChanged;
        public float CalculateEffectiveValue(float baseValue) => baseValue * (1f + Math.Max(0, Stack - 1) * StackMultiplier);
        public void IncrementStack(int amount = 1) => SetStack(Stack + amount);
        public void DecrementStack(int amount = 1) => SetStack(Stack - amount);
        public void ResetStack() => SetStack(0);

        private void SetStack(int value)
        {
            var old = Stack;
            Stack = Math.Max(0, Math.Min(MaxStack, value));
            if (old != Stack) OnStackChanged?.Invoke(old, Stack);
        }
    }

    [DecoratorImpl(typeof(IHierarchyDecorator))]
    internal sealed class DefaultHierarchyDecorator : DefaultDecoratorBase, IHierarchyDecorator
    {
        private readonly List<int> _children = new();

        public DefaultHierarchyDecorator() { }

        public DefaultHierarchyDecorator(int? parentId)
        {
            ParentId = parentId;
        }

        public override string Name => "Hierarchy(" + (Inner?.Name ?? "null") + ")";
        public override ExecutableMetadata Metadata => new(3004, "Hierarchy");
        public override Type DecoratorType => typeof(IHierarchyDecorator);
        public int? ParentId { get; set; }
        public bool CascadeOnExpire { get; set; } = true;
        public bool CascadeOnInterrupt { get; set; } = true;
        public event Action<int, bool> OnHierarchyChanged;
        public void AddChild(int childId)
        {
            if (_children.Contains(childId)) return;
            _children.Add(childId);
            OnHierarchyChanged?.Invoke(childId, true);
        }

        public void RemoveChild(int childId)
        {
            if (!_children.Remove(childId)) return;
            OnHierarchyChanged?.Invoke(childId, false);
        }

        public IReadOnlyList<int> GetChildren() => _children;
    }

    [DecoratorImpl(typeof(IContinuousDecorator))]
    internal sealed class DefaultContinuousDecorator : DefaultDecoratorBase, IContinuousDecorator
    {
        public DefaultContinuousDecorator() { }

        public DefaultContinuousDecorator(string continuationId)
        {
            ContinuationId = continuationId ?? string.Empty;
        }

        public override string Name => "Continuous(" + (Inner?.Name ?? "null") + ")";
        public override ExecutableMetadata Metadata => new(3005, "Continuous");
        public override Type DecoratorType => typeof(IContinuousDecorator);
        public string ContinuationId { get; set; }
        public bool IsActive { get; set; }
        public bool IsTerminated { get; private set; }
        public string TerminationReason { get; private set; }
        public void OnApplied(ActionContext ctx)
        {
            IsActive = true;
        }

        public void OnTick(ActionContext ctx, float deltaTimeMs)
        {
            IsActive = true;
        }

        public void OnRemoved(ActionContext ctx)
        {
            IsActive = false;
        }

        public bool CanCoexistWith(IContinuousDecorator other) => true;
        public void RequestTermination(string reason)
        {
            IsTerminated = true;
            TerminationReason = reason;
        }
    }

    [DecoratorImpl(typeof(ICapabilityDecorator))]
    internal sealed class DefaultCapabilityDecorator : DefaultDecoratorBase, ICapabilityDecorator
    {
        public DefaultCapabilityDecorator() { }

        public DefaultCapabilityDecorator(CapabilityId capabilityId)
        {
            CapabilityId = capabilityId;
        }

        public override string Name => "Capability(" + CapabilityId.FullName + ")";
        public override ExecutableMetadata Metadata => new(3006, "Capability");
        public override Type DecoratorType => typeof(ICapabilityDecorator);
        public CapabilityId CapabilityId { get; private set; }
        public ICapabilityApplier CapabilityApplier { get; set; }
        public bool IsActive { get; private set; }
        public bool IsTerminated { get; private set; }
        public string DeactivationReason { get; private set; }

        public void OnApplied(ActionContext ctx)
        {
            IsActive = true;
            IsTerminated = false;
            CapabilityApplier?.Apply(null, ctx);
        }

        public void OnTick(ActionContext ctx, float deltaTimeMs)
        {
            CapabilityApplier?.GetOrCreateContainer(ctx)?.Tick(ctx, deltaTimeMs);
        }

        public void OnRemoved(ActionContext ctx)
        {
            IsActive = false;
            CapabilityApplier?.Remove(null, ctx);
        }

        public bool CanCoexistWith(ICapabilityDecorator other)
        {
            return other == null || other.CapabilityId != CapabilityId;
        }

        public void RequestDeactivate(string reason)
        {
            IsActive = false;
            IsTerminated = true;
            DeactivationReason = reason;
        }
    }
}
