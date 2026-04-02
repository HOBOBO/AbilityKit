using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Modifiers;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 修饰器默认实现 — 最小化占位，业务包可替换
    //
    //  所有实现均标记 [DecoratorImpl]，可通过 DecoratorRegistry 自动发现
    //  如果业务包注册了相同接口的实现，优先使用业务包的
    // ========================================================================

    // ========================================================================
    // 标签系统默认实现
    // 注意: 不同项目的 Tag 系统 (如 GameplayTag, FGameplayTag, 自定义 Tag) 差异很大
    // 这些只是核心包提供的最基础实现，业务项目应替换为自己的实现
    // ========================================================================

    internal sealed class DefaultGameplayTagImpl : IGameplayTag
    {
        public string FullName { get; }
        public IGameplayTag Parent { get; }

        public DefaultGameplayTagImpl(string fullName)
        {
            FullName = fullName;
            var lastDot = fullName.LastIndexOf('.');
            if (lastDot > 0)
                Parent = new DefaultGameplayTagImpl(fullName.Substring(0, lastDot));
        }

        public bool Matches(IGameplayTag other, ETagQueryMode mode = ETagQueryMode.IncludeParent)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (FullName == other.FullName) return true;
            if (mode == ETagQueryMode.IncludeParent && Parent != null)
                return Parent.Matches(other, mode);
            return false;
        }
    }

    internal sealed class DefaultTagContainerImpl : ITagContainer
    {
        private readonly HashSet<DefaultGameplayTagImpl> _tags = new();

        public int Count => _tags.Count;
        public event Action<TagEventData> OnTagChanged;

        public bool Has(IGameplayTag tag, ETagQueryMode mode = ETagQueryMode.IncludeParent)
        {
            if (tag == null) return false;
            foreach (var t in _tags)
                if (t.Matches(tag, mode)) return true;
            return false;
        }

        public bool HasAny(IEnumerable<IGameplayTag> tags)
        {
            foreach (var tag in tags)
                if (Has(tag)) return true;
            return false;
        }

        public bool HasAll(IEnumerable<IGameplayTag> tags)
        {
            foreach (var tag in tags)
                if (!Has(tag)) return false;
            return true;
        }

        public void Add(IGameplayTag tag)
        {
            if (tag is DefaultGameplayTagImpl dt && _tags.Add(dt))
                OnTagChanged?.Invoke(new TagEventData(tag, ETagEvent.Added));
        }

        public void Remove(IGameplayTag tag)
        {
            if (tag is DefaultGameplayTagImpl dt && _tags.Remove(dt))
                OnTagChanged?.Invoke(new TagEventData(tag, ETagEvent.Removed));
        }

        public IEnumerable<IGameplayTag> GetAll() => _tags;
    }

    // ========================================================================
    // 持续时间修饰器默认实现
    // ========================================================================

    [DecoratorImpl(typeof(IDurationDecorator))]
    internal sealed class DefaultDurationDecorator : IDurationDecorator
    {
        private float _elapsedMs;
        private float _remainingMs;

        public string Name => $"Duration({Inner?.Name ?? "null"}, {_remainingMs:F0}ms)";
        public ExecutableMetadata Metadata => new(3000, "Duration");
        public Type DecoratorType => typeof(IDurationDecorator);
        public bool IsReady => !IsExpired;

        public float DurationMs { get; set; } = -1;
        public float RemainingMs => Math.Max(0, _remainingMs - _elapsedMs);
        public bool IsExpired => _remainingMs >= 0 && _elapsedMs >= _remainingMs;
        public bool CanBeInterrupted { get; set; } = true;
        public bool AutoStart { get; set; } = true;
        public ISimpleExecutable Inner { get; set; }
        public event Action<object> OnExpired;
        internal event Action<object, float> OnTickInternal;

        public DefaultDurationDecorator() { }
        public DefaultDurationDecorator(float durationMs) { DurationMs = durationMs; _remainingMs = durationMs; }

        public bool OnBeforeExecute(object ctx)
        {
            if (!AutoStart && _remainingMs < 0) return !IsExpired;
            if (_remainingMs < 0) _remainingMs = DurationMs;
            return !IsExpired;
        }

        public void OnAfterExecute(object ctx, ref ExecutionResult result)
        {
            OnTickInternal?.Invoke(ctx, 0);
        }

        public bool Update(object ctx, float deltaTimeMs)
        {
            if (!AutoStart && _remainingMs < 0) _remainingMs = DurationMs;
            _elapsedMs += deltaTimeMs;
            OnTickInternal?.Invoke(ctx, deltaTimeMs);
            if (IsExpired) { OnExpired?.Invoke(ctx); return true; }
            return false;
        }

        public void Refresh(float additionalMs)
        {
            if (CanBeInterrupted || _remainingMs < 0)
            {
                if (_remainingMs < 0) _remainingMs = 0;
                _remainingMs = Math.Min(_remainingMs + additionalMs, DurationMs >= 0 ? DurationMs : float.MaxValue);
                _elapsedMs = 0;
            }
        }

        public ExecutionResult Execute(object ctx)
        {
            if (!OnBeforeExecute(ctx)) return ExecutionResult.Skipped("Duration expired or not started");
            var result = Inner?.Execute(ctx) ?? ExecutionResult.Success();
            OnAfterExecute(ctx, ref result);
            return result;
        }
    }

    // ========================================================================
    // 标签修饰器默认实现
    // 注意: DefaultGameplayTagImpl 只是最基础的字符串 Tag 实现
    // 业务项目应实现自己的 IGameplayTag / ITagContainer 并替换此默认实现
    // ========================================================================

    [DecoratorImpl(typeof(ITagDecorator))]
    internal sealed class DefaultTagDecorator : ITagDecorator
    {
        private ITagContainer _container = new DefaultTagContainerImpl();

        public string Name => $"Tag({Inner?.Name ?? "null"}, {_container?.Count ?? 0})";
        public ExecutableMetadata Metadata => new(3001, "Tag");
        public Type DecoratorType => typeof(ITagDecorator);
        public bool IsReady => !RequiredTags.Matches(_container) ? false : !IgnoreTags.Matches(_container);

        public ITagContainer Tags { get => _container; set => _container = value ?? new DefaultTagContainerImpl(); }
        public TagQuery RequiredTags { get; set; }
        public TagQuery IgnoreTags { get; set; }
        public ISimpleExecutable Inner { get; set; }
        public event Action<TagEventData> OnTagChanged;

        public DefaultTagDecorator() { }
        public DefaultTagDecorator(params string[] tagNames)
        {
            foreach (var name in tagNames) AddTag(name);
        }

        public void AddTag(string tagName) => _container.Add(new DefaultGameplayTagImpl(tagName));
        public void RemoveTag(string tagName) => _container.Remove(new DefaultGameplayTagImpl(tagName));

        public bool OnBeforeExecute(object ctx) => IsReady;

        public void OnAfterExecute(object ctx, ref ExecutionResult result)
        {
            _container.OnTagChanged += data => OnTagChanged?.Invoke(data);
        }

        public ExecutionResult Execute(object ctx)
        {
            if (!OnBeforeExecute(ctx)) return ExecutionResult.Skipped("Tag condition not met");
            var result = Inner?.Execute(ctx) ?? ExecutionResult.Success();
            OnAfterExecute(ctx, ref result);
            return result;
        }
    }

    // ========================================================================
    // 修改器修饰器默认实现
    // 集成 modifiers 包的完整能力：ModifierCalculator、ModifierStacking、等级缩放等
    // ========================================================================

    [DecoratorImpl(typeof(IModifierDecorator))]
    internal sealed class DefaultModifierDecorator : IModifierDecorator
    {
        private readonly List<ModifierData> _modifiers = new();
        private readonly ModifierCalculator _calculator = new();

        public string Name => $"Modifier({Inner?.Name ?? "null"}, {_modifiers.Count})";
        public ExecutableMetadata Metadata => new(3002, "Modifier");
        public Type DecoratorType => typeof(IModifierDecorator);
        public bool IsReady => true;

        public int SourceId { get; set; }
        public ISimpleExecutable Inner { get; set; }
        public float Level { get; set; } = 1f;
        public IModifierApplier Applier { get; set; }

        public event Action<ModifierData> OnModifierApplied;
        public event Action<ModifierData> OnModifierRemoved;

        public DefaultModifierDecorator() { }

        public DefaultModifierDecorator(params ModifierData[] modifiers)
        {
            _modifiers.AddRange(modifiers);
        }

        public DefaultModifierDecorator(IModifierApplier applier, params ModifierData[] modifiers)
        {
            Applier = applier;
            _modifiers.AddRange(modifiers);
        }

        public IReadOnlyList<ModifierData> GetModifiers() => _modifiers;

        public void AddModifier(ModifierData modifier)
        {
            if (modifier.SourceId == 0)
                modifier.SourceId = SourceId;
            _modifiers.Add(modifier);
        }

        public bool RemoveModifier(ModifierData modifier)
        {
            if (_modifiers.Remove(modifier))
            {
                OnModifierRemoved?.Invoke(modifier);
                return true;
            }
            return false;
        }

        public void ClearModifiers()
        {
            foreach (var mod in _modifiers)
                OnModifierRemoved?.Invoke(mod);
            _modifiers.Clear();
        }

        /// <summary>
        /// 计算修改器对基础值的影响
        /// 使用 ModifierCalculator 进行计算
        /// </summary>
        public ModifierResult Calculate(float baseValue, IModifierContext context = null)
        {
            if (_modifiers.Count == 0)
                return ModifierResult.Empty(baseValue);

            var level = context?.Level ?? Level;
            float GetAttribute(ModifierKey key) => context?.GetAttribute(key) ?? 0f;

            return _calculator.Calculate(_modifiers.ToArray(), baseValue, level);
        }

        /// <summary>
        /// 计算修改器对基础值的影响（使用内置的简单上下文）
        /// </summary>
        public ModifierResult Calculate(float baseValue, float level)
        {
            return _calculator.Calculate(_modifiers.ToArray(), baseValue, level);
        }

        /// <summary>
        /// 尝试获取指定的修改器应用器
        /// 优先级：1. 自身 Applier  2. ModifierApplierRegistry 默认  3. 内置默认应用器
        /// </summary>
        private IModifierApplier ResolveApplier()
        {
            if (Applier != null)
                return Applier;

            var registryApplier = ModifierApplierRegistry.Default._defaultApplier;
            if (registryApplier != null)
                return registryApplier;

            return DefaultModifierApplier.Instance;
        }

        /// <summary>
        /// 直接应用修改器到目标
        /// </summary>
        public ModifierApplyResult ApplyTo(object target, int? sourceId = null)
        {
            if (_modifiers.Count == 0)
                return ModifierApplyResult.Succeeded();

            var applier = ResolveApplier();
            if (applier == null)
                return ModifierApplyResult.Failed("No modifier applier available");

            var actualSourceId = sourceId ?? SourceId;
            var result = applier.ApplyModifiers(target, _modifiers.ToArray(), actualSourceId);

            if (result.Success)
            {
                foreach (var mod in _modifiers)
                    OnModifierApplied?.Invoke(mod);
            }

            return result;
        }

        public bool OnBeforeExecute(object ctx) => true;

        public void OnAfterExecute(object ctx, ref ExecutionResult result)
        {
            if (_modifiers.Count == 0) return;

            var applier = ResolveApplier();
            if (applier == null) return;

            if (ctx == null) return;

            var applyResult = applier.ApplyModifiers(ctx, _modifiers.ToArray(), SourceId);
            if (applyResult.Success)
            {
                foreach (var mod in _modifiers)
                    OnModifierApplied?.Invoke(mod);
            }
        }

        public ExecutionResult Execute(object ctx)
        {
            if (!OnBeforeExecute(ctx)) return ExecutionResult.Skipped("Modifier condition not met");
            var result = Inner?.Execute(ctx) ?? ExecutionResult.Success();
            OnAfterExecute(ctx, ref result);
            return result;
        }
    }

    // ========================================================================
    // 内置默认修改器应用器
    // 当没有注册外部应用器时使用此实现
    // ========================================================================

    /// <summary>
    /// 内置默认修改器应用器
    /// 提供最基础的修改器应用逻辑，业务项目应注册自己的实现
    /// </summary>
    [ModifierApplier(int.MinValue)]
    public sealed class DefaultModifierApplier : IModifierApplier
    {
        public static DefaultModifierApplier Instance { get; } = new();

        private DefaultModifierApplier() { }

        public ModifierApplyResult ApplyModifiers(object target, ReadOnlySpan<ModifierData> modifiers, int sourceId)
        {
            // 基础实现不做任何实际操作，只是返回成功
            // 业务项目需要注册自己的 IModifierApplier 实现来真正应用修改器
            if (modifiers.Length == 0)
                return ModifierApplyResult.Succeeded();

            float totalMagnitude = 0f;
            foreach (var mod in modifiers)
            {
                totalMagnitude += mod.GetMagnitude();
            }

            return ModifierApplyResult.Succeeded(totalMagnitude);
        }
    }

    // ========================================================================
    // 层数修饰器默认实现
    // ========================================================================

    [DecoratorImpl(typeof(IStackDecorator))]
    internal sealed class DefaultStackDecorator : IStackDecorator
    {
        private int _stack = 1;

        public string Name => $"Stack({Inner?.Name ?? "null"}, x{Stack})";
        public ExecutableMetadata Metadata => new(3003, "Stack");
        public Type DecoratorType => typeof(IStackDecorator);
        public bool IsReady => true;

        public int Stack { get => _stack; set { if (value != _stack) { var old = _stack; _stack = Math.Max(1, value); OnStackChanged?.Invoke(old, _stack); } } }
        public float BaseValue { get; set; } = 1f;
        public float StackMultiplier { get; set; } = 1f;
        public int MaxStack { get; set; } = 0;
        public ISimpleExecutable Inner { get; set; }
        public event Action<int, int> OnStackChanged;

        public DefaultStackDecorator() { }
        public DefaultStackDecorator(int initialStack, float stackMultiplier) { _stack = Math.Max(1, initialStack); StackMultiplier = stackMultiplier; }

        public float CalculateEffectiveValue(float baseValue) => baseValue * BaseValue * (1f + (Stack - 1) * StackMultiplier);
        public void IncrementStack(int amount = 1) { var newStack = _stack + amount; if (MaxStack > 0) newStack = Math.Min(newStack, MaxStack); Stack = newStack; }
        public void DecrementStack(int amount = 1) => Stack = Math.Max(1, _stack - amount);
        public void ResetStack() => Stack = 1;

        public bool OnBeforeExecute(object ctx) => true;
        public void OnAfterExecute(object ctx, ref ExecutionResult result) { }

        public ExecutionResult Execute(object ctx)
        {
            if (!OnBeforeExecute(ctx)) return ExecutionResult.Skipped("Stack condition not met");
            var result = Inner?.Execute(ctx) ?? ExecutionResult.Success();
            OnAfterExecute(ctx, ref result);
            return result;
        }
    }

    // ========================================================================
    // 层级修饰器默认实现
    // ========================================================================

    [DecoratorImpl(typeof(IHierarchyDecorator))]
    internal sealed class DefaultHierarchyDecorator : IHierarchyDecorator
    {
        private readonly List<int> _children = new();

        public string Name => $"Hierarchy({Inner?.Name ?? "null"})";
        public ExecutableMetadata Metadata => new(3004, "Hierarchy");
        public Type DecoratorType => typeof(IHierarchyDecorator);
        public bool IsReady => true;

        public int? ParentId { get; set; }
        public bool CascadeOnExpire { get; set; } = true;
        public bool CascadeOnInterrupt { get; set; } = true;
        public ISimpleExecutable Inner { get; set; }
        public event Action<int, bool> OnHierarchyChanged;
        public event Action OnParentExpired;
        public event Action<string> OnParentInterrupted;

        public DefaultHierarchyDecorator() { }
        public DefaultHierarchyDecorator(int? parentId) => ParentId = parentId;

        public void AddChild(int childId) { if (!_children.Contains(childId)) { _children.Add(childId); OnHierarchyChanged?.Invoke(childId, true); } }
        public void RemoveChild(int childId) { if (_children.Remove(childId)) OnHierarchyChanged?.Invoke(childId, false); }
        public IReadOnlyList<int> GetChildren() => _children;

        public void NotifyParentExpired() { if (CascadeOnExpire) OnParentExpired?.Invoke(); }
        public void NotifyParentInterrupted(string reason) { if (CascadeOnInterrupt) OnParentInterrupted?.Invoke(reason); }

        public bool OnBeforeExecute(object ctx) => true;
        public void OnAfterExecute(object ctx, ref ExecutionResult result) { }

        public ExecutionResult Execute(object ctx)
        {
            if (!OnBeforeExecute(ctx)) return ExecutionResult.Skipped("Hierarchy condition not met");
            var result = Inner?.Execute(ctx) ?? ExecutionResult.Success();
            OnAfterExecute(ctx, ref result);
            return result;
        }
    }
}
