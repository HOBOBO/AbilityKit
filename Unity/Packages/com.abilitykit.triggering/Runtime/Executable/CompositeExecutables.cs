using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 跨平台随机数提供器
    // ========================================================================

    /// <summary>
    /// 跨平台随机数提供器
    /// 使用 System.Random 实现，可在服务器和 Unity 环境中通用
    /// </summary>
    public static class CrossPlatformRandom
    {
        private static readonly Random _random = new Random();

        /// <summary>
        /// 返回 [0, maxValue) 范围内的整数
        /// </summary>
        public static int Range(int maxValue)
        {
            return _random.Next(maxValue);
        }

        /// <summary>
        /// 返回 [0, maxValue) 范围内的浮点数
        /// </summary>
        public static float Range(float maxValue)
        {
            return (float)(_random.NextDouble() * maxValue);
        }

        /// <summary>
        /// 返回 [minValue, maxValue) 范围内的浮点数
        /// </summary>
        public static float Range(float minValue, float maxValue)
        {
            return minValue + (float)(_random.NextDouble() * (maxValue - minValue));
        }
    }

    // ========================================================================
    // 复合行为实现
    // ========================================================================

    /// <summary>
    /// 顺序执行行为
    /// </summary>
    public sealed class SequenceExecutable : ISimpleExecutable, ISequenceExecutable, ICompositeExecutable
    {
        public string Name => "Sequence";
        public ExecutableMetadata Metadata => new(1, "Sequence", isComposite: true);

        private readonly List<ISimpleExecutable> _children = new();

        public int ChildCount => _children.Count;

        public SequenceExecutable Add(ISimpleExecutable child)
        {
            _children.Add(child);
            return this;
        }

        public SequenceExecutable AddRange(IEnumerable<ISimpleExecutable> children)
        {
            _children.AddRange(children);
            return this;
        }

        public ISimpleExecutable GetChild(int index)
            => index >= 0 && index < _children.Count ? _children[index] : null;

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
    /// 选择执行行为 (选择第一个成功的子节点)
    /// </summary>
    public sealed class SelectorExecutable : ISimpleExecutable, ISelectorExecutable, ICompositeExecutable
    {
        public string Name => "Selector";
        public ExecutableMetadata Metadata => new(10, "Selector", isComposite: true);

        private readonly List<ISimpleExecutable> _children = new();

        public int ChildCount => _children.Count;

        public SelectorExecutable Add(ISimpleExecutable child)
        {
            _children.Add(child);
            return this;
        }

        public ISimpleExecutable GetChild(int index)
            => index >= 0 && index < _children.Count ? _children[index] : null;

        public ExecutionResult Execute(object ctx)
        {
            int skippedCount = 0;
            foreach (var child in _children)
            {
                if (child == null)
                {
                    skippedCount++;
                    continue;
                }

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
    /// 并行执行行为
    /// </summary>
    public sealed class ParallelExecutable : ISimpleExecutable, IParallelExecutable, ICompositeExecutable
    {
        public string Name => "Parallel";
        public ExecutableMetadata Metadata => new(20, "Parallel", isComposite: true);
        public ECompositeMode ParallelMode { get; set; } = ECompositeMode.Parallel;
        public float TimeoutMs { get; set; } = -1f;

        private readonly List<ISimpleExecutable> _children = new();
        private readonly List<ExecutionResult> _results = new();
        private float _elapsed;

        public int ChildCount => _children.Count;

        public ParallelExecutable Add(ISimpleExecutable child)
        {
            _children.Add(child);
            _results.Add(ExecutionResult.None);
            return this;
        }

        public ISimpleExecutable GetChild(int index)
            => index >= 0 && index < _children.Count ? _children[index] : null;

        public ExecutionResult Execute(object ctx)
        {
            _elapsed = 0f;
            _results.Clear();

            for (int i = 0; i < _children.Count; i++)
            {
                _results.Add(ExecutionResult.None);
            }

            foreach (var child in _children)
            {
                if (child == null) continue;
                try
                {
                    var result = child.Execute(ctx);

                    switch (ParallelMode)
                    {
                        case ECompositeMode.ParallelSequence:
                            if (result.IsFailed) return result;
                            break;
                        case ECompositeMode.ParallelSelector:
                            if (result.IsSuccess) return result;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    return ExecutionResult.Failed($"Parallel[{child.Name}]: {ex.Message}");
                }
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

            if (TimeoutMs > 0 && _elapsed >= TimeoutMs)
            {
                return ExecutionResult.Skipped("Parallel timeout");
            }

            int completedCount = 0;
            int successCount = 0;
            int failCount = 0;

            for (int i = 0; i < _children.Count; i++)
            {
                if (_results[i].IsSuccess || _results[i].IsFailed)
                {
                    completedCount++;
                    if (_results[i].IsSuccess) successCount++;
                    if (_results[i].IsFailed) failCount++;
                    continue;
                }

                var child = _children[i];
                if (child == null)
                {
                    _results[i] = ExecutionResult.Success(0);
                    completedCount++;
                    continue;
                }

                try
                {
                    var result = child.Execute(ctx);
                    _results[i] = result;

                    switch (ParallelMode)
                    {
                        case ECompositeMode.ParallelSequence:
                            if (result.IsFailed) return result;
                            break;
                        case ECompositeMode.ParallelSelector:
                            if (result.IsSuccess) return result;
                            break;
                    }

                    completedCount++;
                    if (result.IsSuccess) successCount++;
                }
                catch (Exception ex)
                {
                    _results[i] = ExecutionResult.Failed($"Parallel[{child.Name}]: {ex.Message}");
                    failCount++;
                    completedCount++;
                }
            }

            if (completedCount == _children.Count)
            {
                return ParallelMode switch
                {
                    ECompositeMode.Parallel => ExecutionResult.Success(successCount),
                    ECompositeMode.ParallelSequence => failCount == 0 ? ExecutionResult.Success(successCount) : ExecutionResult.Failed("Some children failed"),
                    ECompositeMode.ParallelSelector => successCount > 0 ? ExecutionResult.Success(successCount) : ExecutionResult.Skipped("No child succeeded"),
                    _ => ExecutionResult.Success(successCount)
                };
            }

            return ExecutionResult.Success(successCount);
        }
    }

    /// <summary>
    /// If 行为
    /// </summary>
    public sealed class IfExecutable : ISimpleExecutable, IConditionalExecutable, ICompositeExecutable
    {
        public string Name => "If";
        public ExecutableMetadata Metadata => new(100, "If", isComposite: true);

        public ICondition Condition { get; set; }
        public ISimpleExecutable Body { get; set; }

        public int ChildCount => Body != null ? 1 : 0;

        public ISimpleExecutable GetChild(int index)
            => index == 0 ? Body : null;

        public int EvaluateConditionIndex(object ctx)
        {
            return Condition?.Evaluate(ctx).Passed == true ? 0 : -1;
        }

        public ExecutionResult Execute(object ctx)
        {
            if (Condition?.Evaluate(ctx).Passed != true)
                return ExecutionResult.Skipped("Condition not passed");

            if (Body == null)
                return ExecutionResult.Success(0);

            try
            {
                var result = Body.Execute(ctx);
                return result.IsSuccess ? ExecutionResult.Success(1) : result;
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"If.Body: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// If-ElseIf-Else 行为
    /// </summary>
    public sealed class IfElseExecutable : ISimpleExecutable, IConditionalExecutable, ICompositeExecutable
    {
        public string Name => "IfElse";
        public ExecutableMetadata Metadata => new(101, "IfElse", isComposite: true);

        private readonly List<Branch> _branches = new();
        private ISimpleExecutable _elseBody;

        public int ChildCount => _branches.Count + (_elseBody != null ? 1 : 0);

        public ISimpleExecutable GetChild(int index)
        {
            if (index < _branches.Count)
                return _branches[index].Body;
            if (index == _branches.Count && _elseBody != null)
                return _elseBody;
            return null;
        }

        public IfElseExecutable If(ICondition condition, ISimpleExecutable body)
        {
            _branches.Add(new Branch { Condition = condition, Body = body });
            return this;
        }

        public IfElseExecutable ElseIf(ICondition condition, ISimpleExecutable body)
        {
            return If(condition, body);
        }

        public IfElseExecutable Else(ISimpleExecutable body)
        {
            _elseBody = body;
            return this;
        }

        public int EvaluateConditionIndex(object ctx)
        {
            for (int i = 0; i < _branches.Count; i++)
            {
                if (_branches[i].Condition?.Evaluate(ctx).Passed == true)
                    return i;
            }
            return _elseBody != null ? _branches.Count : -1;
        }

        public ExecutionResult Execute(object ctx)
        {
            for (int i = 0; i < _branches.Count; i++)
            {
                var branch = _branches[i];
                if (branch.Condition?.Evaluate(ctx).Passed == true)
                {
                    if (branch.Body == null)
                        return ExecutionResult.Success(0);
                    try
                    {
                        return branch.Body.Execute(ctx);
                    }
                    catch (Exception ex)
                    {
                        return ExecutionResult.Failed($"IfElse[{i}].Body: {ex.Message}");
                    }
                }
            }
            if (_elseBody != null)
            {
                try
                {
                    return _elseBody.Execute(ctx);
                }
                catch (Exception ex)
                {
                    return ExecutionResult.Failed($"IfElse.Else: {ex.Message}");
                }
            }
            return ExecutionResult.Skipped("No matching branch");
        }

        private struct Branch
        {
            public ICondition Condition;
            public ISimpleExecutable Body;
        }
    }

    /// <summary>
    /// Switch 行为
    /// </summary>
    public sealed class SwitchExecutable : ISimpleExecutable, ISwitchExecutable, ICompositeExecutable
    {
        public string Name => "Switch";
        public ExecutableMetadata Metadata => new(110, "Switch", isComposite: true);

        public Func<object, int> ValueSelector { get; set; }

        private readonly Dictionary<int, ISimpleExecutable> _cases = new();
        private ISimpleExecutable _defaultCase;

        public int ChildCount => _cases.Count + (_defaultCase != null ? 1 : 0);

        public ISimpleExecutable GetChild(int index)
        {
            if (index < _cases.Count)
                return GetCaseByIndex(index);
            if (index == _cases.Count && _defaultCase != null)
                return _defaultCase;
            return null;
        }

        private ISimpleExecutable GetCaseByIndex(int index)
        {
            int i = 0;
            foreach (var kvp in _cases)
            {
                if (i == index) return kvp.Value;
                i++;
            }
            return null;
        }

        public SwitchExecutable Case(int value, ISimpleExecutable body)
        {
            _cases[value] = body;
            return this;
        }

        public SwitchExecutable Default(ISimpleExecutable body)
        {
            _defaultCase = body;
            return this;
        }

        public ExecutionResult Execute(object ctx)
        {
            int value = ValueSelector?.Invoke(ctx) ?? -1;
            ISimpleExecutable body = null;
            if (_cases.TryGetValue(value, out var caseBody))
                body = caseBody;
            else if (_defaultCase != null)
                body = _defaultCase;

            if (body == null)
                return ExecutionResult.Skipped("No matching case");

            try
            {
                return body.Execute(ctx);
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"Switch[{value}]: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 随机选择行为
    /// </summary>
    public sealed class RandomSelectorExecutable : ISimpleExecutable, ICompositeExecutable
    {
        public string Name => "RandomSelector";
        public ExecutableMetadata Metadata => new(120, "RandomSelector", isComposite: true);

        public List<ISimpleExecutable> Children { get; set; } = new();
        public float[] Weights { get; set; }

        public int ChildCount => Children.Count;

        public ISimpleExecutable GetChild(int index)
            => index >= 0 && index < Children.Count ? Children[index] : null;

        public RandomSelectorExecutable Add(ISimpleExecutable child, float weight = 1f)
        {
            Children.Add(child);
            return this;
        }

        public ExecutionResult Execute(object ctx)
        {
            if (Children.Count == 0)
                return ExecutionResult.Skipped("No children");

            int selectedIndex;
            if (Weights != null && Weights.Length == Children.Count)
            {
                selectedIndex = WeightedRandomSelect();
            }
            else
            {
                selectedIndex = CrossPlatformRandom.Range(Children.Count);
            }

            var child = Children[selectedIndex];
            if (child == null)
                return ExecutionResult.Skipped($"Child[{selectedIndex}] is null");

            try
            {
                return child.Execute(ctx);
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"Random[{selectedIndex}]: {ex.Message}");
            }
        }

        private int WeightedRandomSelect()
        {
            float totalWeight = 0f;
            foreach (var w in Weights) totalWeight += w;

            float randomValue = CrossPlatformRandom.Range(totalWeight);
            float cumulative = 0f;

            for (int i = 0; i < Weights.Length; i++)
            {
                cumulative += Weights[i];
                if (randomValue <= cumulative)
                    return i;
            }

            return Weights.Length - 1;
        }
    }

    /// <summary>
    /// 重复执行行为
    /// </summary>
    public sealed class RepeatExecutable : ISimpleExecutable, ICompositeExecutable
    {
        public string Name => "Repeat";
        public ExecutableMetadata Metadata => new(130, "Repeat", isComposite: true);

        public ISimpleExecutable Child { get; set; }
        public int Count { get; set; } = 1;
        public bool StopOnFailure { get; set; } = true;

        public int ChildCount => Child != null ? 1 : 0;

        public ISimpleExecutable GetChild(int index)
            => index == 0 ? Child : null;

        public ExecutionResult Execute(object ctx)
        {
            if (Child == null)
                return ExecutionResult.Skipped("No child to repeat");

            int successCount = 0;
            for (int i = 0; i < Count || Count < 0; i++)
            {
                try
                {
                    var result = Child.Execute(ctx);
                    if (result.IsSuccess) successCount++;
                    if (result.IsFailed && StopOnFailure) return result;
                }
                catch (Exception ex)
                {
                    return ExecutionResult.Failed($"Repeat[{i}]: {ex.Message}");
                }
            }

            return ExecutionResult.Success(successCount);
        }
    }

    /// <summary>
    /// 直到成功/失败行为
    /// </summary>
    public sealed class UntilExecutable : ISimpleExecutable, ICompositeExecutable
    {
        public string Name => "Until";
        public ExecutableMetadata Metadata => new(131, "Until", isComposite: true);

        public ISimpleExecutable Child { get; set; }
        public int MaxIterations { get; set; } = 10;
        public bool UntilSuccess { get; set; } = true;

        public int ChildCount => Child != null ? 1 : 0;

        public ISimpleExecutable GetChild(int index)
            => index == 0 ? Child : null;

        public ExecutionResult Execute(object ctx)
        {
            if (Child == null)
                return ExecutionResult.Skipped("No child");

            int iterations = 0;
            while (iterations < MaxIterations)
            {
                try
                {
                    var result = Child.Execute(ctx);
                    if (UntilSuccess && result.IsSuccess) return ExecutionResult.Success(iterations + 1);
                    if (!UntilSuccess && result.IsFailed) return ExecutionResult.Success(iterations + 1);
                }
                catch (Exception ex)
                {
                    return ExecutionResult.Failed($"Until[{iterations}]: {ex.Message}");
                }
                iterations++;
            }

            return UntilSuccess
                ? ExecutionResult.Skipped($"Max iterations {MaxIterations} reached without success")
                : ExecutionResult.Skipped($"Max iterations {MaxIterations} reached without failure");
        }
    }
}
