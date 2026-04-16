using System;
using System.Collections.Generic;
using AbilityKit.Core.Common.Marker;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// Executable 类型 ID Attribute
    /// 标记在 IExecutable 实现类上，自动注册到 ExecutableRegistry
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ExecutableTypeIdAttribute : MarkerAttribute
    {
        public int TypeId { get; }
        public string TypeName { get; }
        public bool IsComposite { get; }

        public ExecutableTypeIdAttribute(int typeId, string typeName, bool isComposite = false)
        {
            TypeId = typeId;
            TypeName = typeName;
            IsComposite = isComposite;
        }

        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            if (implType == null || !typeof(IExecutable).IsAssignableFrom(implType)) return;
            var executableRegistry = registry as ExecutableRegistry;
            executableRegistry?.RegisterByAttribute(this, implType);
        }
    }

    /// <summary>
    /// Condition 类型 ID Attribute
    /// 标记在 ICondition 实现类上，自动注册到 ExecutableRegistry
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ConditionTypeIdAttribute : MarkerAttribute
    {
        public int TypeId { get; }
        public string TypeName { get; }

        public ConditionTypeIdAttribute(int typeId, string typeName)
        {
            TypeId = typeId;
            TypeName = typeName;
        }

        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            if (implType == null || !typeof(ICondition).IsAssignableFrom(implType)) return;
            var executableRegistry = registry as ExecutableRegistry;
            executableRegistry?.RegisterConditionByAttribute(this, implType);
        }
    }

    // ========================================================================
    // TypeId 注册表 - 基于 Attribute 自动发现
    // ========================================================================

    /// <summary>
    /// TypeId 注册表 - 基于 Attribute 的类型 ID 管理
    /// 框架包内部使用，包外扩展通过在类型上标记 Attribute 实现
    /// </summary>
    public static class TypeIdRegistry
    {
        /// <summary>
        /// Executable 类型 ID 常量（保留用于扩展）
        /// </summary>
        public static class Executable
        {
            public const int Sequence = 1;
            public const int Selector = 2;
            public const int Parallel = 3;
            public const int If = 10;
            public const int IfElse = 11;
            public const int Switch = 12;
            public const int RandomSelector = 13;
            public const int Repeat = 14;
            public const int Until = 15;
            public const int ActionCall = 100;
            public const int Delay = 200;
            public const int Schedule = 300;
            public const int BusinessStart = 1000;
        }

        /// <summary>
        /// Condition 类型 ID 常量（保留用于扩展）
        /// </summary>
        public static class Condition
        {
            public const int Const = 0;
            public const int And = 1;
            public const int Or = 2;
            public const int Not = 3;
            public const int NumericCompare = 10;
            public const int PayloadCompare = 11;
            public const int HasTarget = 20;
            public const int Multi = 100;
            public const int BusinessStart = 1000;
        }
    }

    // ========================================================================
    // 跨平台随机数提供器
    // ========================================================================

    /// <summary>
    /// 跨平台随机数提供器
    /// </summary>
    public static class CrossPlatformRandom
    {
        private static readonly Random _random = new Random();

        public static int Range(int maxValue) => _random.Next(maxValue);
        public static float Range(float maxValue) => (float)(_random.NextDouble() * maxValue);
        public static float Range(float minValue, float maxValue) => minValue + (float)(_random.NextDouble() * (maxValue - minValue));
    }

    // ========================================================================
    // 行为类型实现
    // ========================================================================

    /// <summary>
    /// Sequence - 顺序执行行为
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.Sequence, "Sequence", isComposite: true)]
    public sealed class SequenceExecutable : ISimpleExecutable, ISequenceExecutable, ICompositeExecutable
    {
        public string Name => "Sequence";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.Sequence, "Sequence", isComposite: true);

        private readonly List<ISimpleExecutable> _children = new();
        public int ChildCount => _children.Count;

        public SequenceExecutable Add(ISimpleExecutable child) { _children.Add(child); return this; }
        public SequenceExecutable AddRange(IEnumerable<ISimpleExecutable> children) { _children.AddRange(children); return this; }
        public ISimpleExecutable GetChild(int index) => index >= 0 && index < _children.Count ? _children[index] : null;

        public ExecutionResult Execute(object ctx)
        {
            int executedCount = 0;
            foreach (var child in _children)
            {
                if (child == null) continue;
                try
                {
                    var result = child.Execute(ctx);
                    if (result.IsSuccess) executedCount++;
                    if (result.IsFailed) return result;
                }
                catch (Exception ex)
                {
                    return ExecutionResult.Failed($"Sequence[{child.Name}]: {ex.Message}");
                }
            }
            return ExecutionResult.Success(executedCount);
        }
    }

    /// <summary>
    /// Selector - 选择第一个成功的子节点执行
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.Selector, "Selector", isComposite: true)]
    public sealed class SelectorExecutable : ISimpleExecutable, ISelectorExecutable, ICompositeExecutable
    {
        public string Name => "Selector";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.Selector, "Selector", isComposite: true);

        private readonly List<ISimpleExecutable> _children = new();
        public int ChildCount => _children.Count;

        public SelectorExecutable Add(ISimpleExecutable child) { _children.Add(child); return this; }
        public ISimpleExecutable GetChild(int index) => index >= 0 && index < _children.Count ? _children[index] : null;

        public ExecutionResult Execute(object ctx)
        {
            int skippedCount = 0;
            foreach (var child in _children)
            {
                if (child == null) { skippedCount++; continue; }
                try
                {
                    var result = child.Execute(ctx);
                    if (result.IsSuccess) return result;
                    if (result.IsFailed) return result;
                    skippedCount++;
                }
                catch (Exception ex)
                {
                    return ExecutionResult.Failed($"Selector[{child.Name}]: {ex.Message}");
                }
            }
            return ExecutionResult.Skipped($"All {skippedCount} children skipped/failed");
        }
    }

    /// <summary>
    /// Parallel - 并行执行行为
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.Parallel, "Parallel", isComposite: true)]
    public sealed class ParallelExecutable : ISimpleExecutable, IParallelExecutable, ICompositeExecutable
    {
        public string Name => "Parallel";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.Parallel, "Parallel", isComposite: true);
        public ECompositeMode ParallelMode { get; set; } = ECompositeMode.Parallel;
        public float TimeoutMs { get; set; } = -1f;

        private readonly List<ISimpleExecutable> _children = new();
        private readonly List<ExecutionResult> _results = new();
        private float _elapsed;
        public int ChildCount => _children.Count;

        public ParallelExecutable Add(ISimpleExecutable child) { _children.Add(child); _results.Add(ExecutionResult.None); return this; }
        public ISimpleExecutable GetChild(int index) => index >= 0 && index < _children.Count ? _children[index] : null;

        public ExecutionResult Execute(object ctx)
        {
            _elapsed = 0f; _results.Clear();
            for (int i = 0; i < _children.Count; i++) _results.Add(ExecutionResult.None);
            foreach (var child in _children)
            {
                if (child == null) continue;
                try
                {
                    var result = child.Execute(ctx);
                    if (ParallelMode == ECompositeMode.ParallelSequence && result.IsFailed) return result;
                    if (ParallelMode == ECompositeMode.ParallelSelector && result.IsSuccess) return result;
                }
                catch (Exception ex) { return ExecutionResult.Failed($"Parallel[{child.Name}]: {ex.Message}"); }
            }
            return ParallelMode switch
            {
                ECompositeMode.Parallel => ExecutionResult.Success(_children.Count),
                ECompositeMode.ParallelSequence => ExecutionResult.Success(_children.Count),
                ECompositeMode.ParallelSelector => ExecutionResult.Skipped("No child succeeded"),
                _ => ExecutionResult.Success(_children.Count)
            };
        }

        public ExecutionResult ExecuteWithUpdate(object ctx, float deltaTimeMs)
        {
            _elapsed += deltaTimeMs;
            if (TimeoutMs > 0 && _elapsed >= TimeoutMs) return ExecutionResult.Skipped("Parallel timeout");
            int completed = 0, success = 0, fail = 0;
            for (int i = 0; i < _children.Count; i++)
            {
                if (_results[i].IsSuccess || _results[i].IsFailed) { completed++; if (_results[i].IsSuccess) success++; if (_results[i].IsFailed) fail++; continue; }
                var child = _children[i];
                if (child == null) { _results[i] = ExecutionResult.Success(0); completed++; continue; }
                try
                {
                    var result = child.Execute(ctx);
                    _results[i] = result;
                    if (ParallelMode == ECompositeMode.ParallelSequence && result.IsFailed) return result;
                    if (ParallelMode == ECompositeMode.ParallelSelector && result.IsSuccess) return result;
                    completed++; if (result.IsSuccess) success++;
                }
                catch (Exception ex) { _results[i] = ExecutionResult.Failed($"Parallel[{child.Name}]: {ex.Message}"); fail++; completed++; }
            }
            if (completed == _children.Count)
                return ParallelMode switch
                {
                    ECompositeMode.Parallel => ExecutionResult.Success(success),
                    ECompositeMode.ParallelSequence => fail == 0 ? ExecutionResult.Success(success) : ExecutionResult.Failed("Some children failed"),
                    ECompositeMode.ParallelSelector => success > 0 ? ExecutionResult.Success(success) : ExecutionResult.Skipped("No child succeeded"),
                    _ => ExecutionResult.Success(success)
                };
            return ExecutionResult.Success(success);
        }
    }

    /// <summary>
    /// If - 条件分支行为
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.If, "If", isComposite: true)]
    public sealed class IfExecutable : ISimpleExecutable, IConditionalExecutable, ICompositeExecutable
    {
        public string Name => "If";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.If, "If", isComposite: true);

        public ICondition Condition { get; set; }
        public ISimpleExecutable Body { get; set; }
        public int ChildCount => Body != null ? 1 : 0;
        public ISimpleExecutable GetChild(int index) => index == 0 ? Body : null;

        public int EvaluateConditionIndex(object ctx) => Condition?.Evaluate(ctx).Passed == true ? 0 : -1;

        public ExecutionResult Execute(object ctx)
        {
            if (Condition?.Evaluate(ctx).Passed != true) return ExecutionResult.Skipped("Condition not passed");
            if (Body == null) return ExecutionResult.Success(0);
            try { var result = Body.Execute(ctx); return result.IsSuccess ? ExecutionResult.Success(1) : result; }
            catch (Exception ex) { return ExecutionResult.Failed($"If.Body: {ex.Message}"); }
        }
    }

    /// <summary>
    /// IfElse - If-ElseIf-Else 分支行为
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.IfElse, "IfElse", isComposite: true)]
    public sealed class IfElseExecutable : ISimpleExecutable, IConditionalExecutable, ICompositeExecutable
    {
        public string Name => "IfElse";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.IfElse, "IfElse", isComposite: true);

        private readonly List<Branch> _branches = new();
        private ISimpleExecutable _elseBody;
        public int ChildCount => _branches.Count + (_elseBody != null ? 1 : 0);
        public ISimpleExecutable GetChild(int index)
        {
            if (index < _branches.Count) return _branches[index].Body;
            if (index == _branches.Count && _elseBody != null) return _elseBody;
            return null;
        }

        public IfElseExecutable If(ICondition condition, ISimpleExecutable body) { _branches.Add(new Branch { Condition = condition, Body = body }); return this; }
        public IfElseExecutable ElseIf(ICondition condition, ISimpleExecutable body) => If(condition, body);
        public IfElseExecutable Else(ISimpleExecutable body) { _elseBody = body; return this; }

        public int EvaluateConditionIndex(object ctx)
        {
            for (int i = 0; i < _branches.Count; i++)
                if (_branches[i].Condition?.Evaluate(ctx).Passed == true) return i;
            return _elseBody != null ? _branches.Count : -1;
        }

        public ExecutionResult Execute(object ctx)
        {
            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i];
                if (branch.Condition?.Evaluate(ctx).Passed == true)
                {
                    if (branch.Body == null) return ExecutionResult.Success(0);
                    try { return branch.Body.Execute(ctx); }
                    catch (Exception ex) { return ExecutionResult.Failed($"IfElse[{i}].Body: {ex.Message}"); }
                }
            }
            if (_elseBody != null)
            {
                try { return _elseBody.Execute(ctx); }
                catch (Exception ex) { return ExecutionResult.Failed($"IfElse.Else: {ex.Message}"); }
            }
            return ExecutionResult.Skipped("No matching branch");
        }

        private struct Branch { public ICondition Condition; public ISimpleExecutable Body; }
    }

    /// <summary>
    /// Switch - 多分支选择行为
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.Switch, "Switch", isComposite: true)]
    public sealed class SwitchExecutable : ISimpleExecutable, ISwitchExecutable, ICompositeExecutable
    {
        public string Name => "Switch";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.Switch, "Switch", isComposite: true);

        public Func<object, int> ValueSelector { get; set; }
        private readonly Dictionary<int, ISimpleExecutable> _cases = new();
        private ISimpleExecutable _defaultCase;
        public int ChildCount => _cases.Count + (_defaultCase != null ? 1 : 0);

        public ISimpleExecutable GetChild(int index)
        {
            if (index < _cases.Count) { int i = 0; foreach (var kvp in _cases) { if (i == index) return kvp.Value; i++; } }
            if (index == _cases.Count && _defaultCase != null) return _defaultCase;
            return null;
        }

        public SwitchExecutable Case(int value, ISimpleExecutable body) { _cases[value] = body; return this; }
        public SwitchExecutable Default(ISimpleExecutable body) { _defaultCase = body; return this; }

        public ExecutionResult Execute(object ctx)
        {
            int value = ValueSelector?.Invoke(ctx) ?? -1;
            ISimpleExecutable body = _cases.TryGetValue(value, out var caseBody) ? caseBody : _defaultCase;
            if (body == null) return ExecutionResult.Skipped("No matching case");
            try { return body.Execute(ctx); }
            catch (Exception ex) { return ExecutionResult.Failed($"Switch[{value}]: {ex.Message}"); }
        }
    }

    /// <summary>
    /// RandomSelector - 随机选择行为
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.RandomSelector, "RandomSelector", isComposite: true)]
    public sealed class RandomSelectorExecutable : ISimpleExecutable, ICompositeExecutable
    {
        public string Name => "RandomSelector";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.RandomSelector, "RandomSelector", isComposite: true);
        public List<ISimpleExecutable> Children { get; set; } = new();
        public float[] Weights { get; set; }
        public int ChildCount => Children.Count;
        public ISimpleExecutable GetChild(int index) => index >= 0 && index < Children.Count ? Children[index] : null;
        public RandomSelectorExecutable Add(ISimpleExecutable child, float weight = 1f) { Children.Add(child); return this; }

        public ExecutionResult Execute(object ctx)
        {
            if (Children.Count == 0) return ExecutionResult.Skipped("No children");
            int selected = Weights != null && Weights.Length == Children.Count ? WeightedSelect() : CrossPlatformRandom.Range(Children.Count);
            var child = Children[selected];
            if (child == null) return ExecutionResult.Skipped($"Child[{selected}] is null");
            try { return child.Execute(ctx); }
            catch (Exception ex) { return ExecutionResult.Failed($"Random[{selected}]: {ex.Message}"); }
        }

        private int WeightedSelect()
        {
            float total = 0f; foreach (var w in Weights) total += w;
            float r = CrossPlatformRandom.Range(total), cum = 0f;
            for (int i = 0; i < Weights.Length; i++) { cum += Weights[i]; if (r <= cum) return i; }
            return Weights.Length - 1;
        }
    }

    /// <summary>
    /// Repeat - 重复执行行为
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.Repeat, "Repeat", isComposite: true)]
    public sealed class RepeatExecutable : ISimpleExecutable, ICompositeExecutable
    {
        public string Name => "Repeat";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.Repeat, "Repeat", isComposite: true);
        public ISimpleExecutable Child { get; set; }
        public int Count { get; set; } = 1;
        public bool StopOnFailure { get; set; } = true;
        public int ChildCount => Child != null ? 1 : 0;
        public ISimpleExecutable GetChild(int index) => index == 0 ? Child : null;

        public ExecutionResult Execute(object ctx)
        {
            if (Child == null) return ExecutionResult.Skipped("No child to repeat");
            int success = 0;
            for (int i = 0; i < Count || Count < 0; i++)
            {
                try
                {
                    var result = Child.Execute(ctx);
                    if (result.IsSuccess) success++;
                    if (result.IsFailed && StopOnFailure) return result;
                }
                catch (Exception ex) { return ExecutionResult.Failed($"Repeat[{i}]: {ex.Message}"); }
            }
            return ExecutionResult.Success(success);
        }
    }

    /// <summary>
    /// Until - 直到成功/失败行为
    /// </summary>
    [ExecutableTypeId(TypeIdRegistry.Executable.Until, "Until", isComposite: true)]
    public sealed class UntilExecutable : ISimpleExecutable, ICompositeExecutable
    {
        public string Name => "Until";
        public ExecutableMetadata Metadata => new(TypeIdRegistry.Executable.Until, "Until", isComposite: true);
        public ISimpleExecutable Child { get; set; }
        public int MaxIterations { get; set; } = 10;
        public bool UntilSuccess { get; set; } = true;
        public int ChildCount => Child != null ? 1 : 0;
        public ISimpleExecutable GetChild(int index) => index == 0 ? Child : null;

        public ExecutionResult Execute(object ctx)
        {
            if (Child == null) return ExecutionResult.Skipped("No child");
            int iter = 0;
            while (iter < MaxIterations)
            {
                try
                {
                    var result = Child.Execute(ctx);
                    if (UntilSuccess && result.IsSuccess) return ExecutionResult.Success(iter + 1);
                    if (!UntilSuccess && result.IsFailed) return ExecutionResult.Success(iter + 1);
                }
                catch (Exception ex) { return ExecutionResult.Failed($"Until[{iter}]: {ex.Message}"); }
                iter++;
            }
            return UntilSuccess
                ? ExecutionResult.Skipped($"Max iterations {MaxIterations} reached without success")
                : ExecutionResult.Skipped($"Max iterations {MaxIterations} reached without failure");
        }
    }
}
