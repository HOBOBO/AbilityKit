using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Collections;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// Iterates an execution-scoped collection with an explicit hard limit and writes
    /// the current element to a Blackboard target before executing the child branch.
    /// </summary>
    public sealed class ForEachTriggerPlanExecutable : TriggerPlanExecutableBase
    {
        private readonly ITriggerPlanExecutable _child;
        private readonly NumericValueRef _collection;
        private readonly BlackboardWriteTarget _itemTarget;
        private readonly int _maxIterations;

        public override string Name => "ForEach";
        public override ETriggerPlanExecutableKind Kind => ETriggerPlanExecutableKind.ForEach;
        public ITriggerPlanExecutable Child => _child;
        public NumericValueRef Collection => _collection;
        public BlackboardWriteTarget ItemTarget => _itemTarget;
        public int MaxIterations => _maxIterations;

        public ForEachTriggerPlanExecutable(
            NumericValueRef collection,
            in BlackboardWriteTarget itemTarget,
            ITriggerPlanExecutable child,
            int maxIterations,
            ITriggerPlanCondition condition = null,
            float weight = 1f)
            : base(condition, weight)
        {
            _collection = collection;
            _itemTarget = itemTarget;
            _child = child;
            _maxIterations = maxIterations;
        }

        protected override TriggerPlanExecutionResult ExecuteCore<TCtx>(object args, in ExecCtx<TCtx> ctx)
        {
            if (_child == null)
                return TriggerPlanExecutionResult.Skipped("ForEach child is empty");
            if (_maxIterations <= 0)
                return TriggerPlanExecutionResult.Failed("ForEach maxIterations must be greater than zero");
            if (ctx.Collections == null)
                return TriggerPlanExecutionResult.Failed("ForEach collection resolver is unavailable");
            if (!NumericValueRefResolver.TryResolve(in _collection, in args, in ctx, out var handleNumber) ||
                !TriggerCollectionHandle.TryFromNumber(handleNumber, out var handle))
                return TriggerPlanExecutionResult.Failed("ForEach collection handle could not be resolved");
            if (!ctx.Collections.TryResolve(handle, out var collection) || collection == null)
                return TriggerPlanExecutionResult.Failed($"ForEach collection was not found: {handle}");

            var result = TriggerPlanExecutionResult.None;
            var count = collection.Count < _maxIterations ? collection.Count : _maxIterations;
            for (var i = 0; i < count; i++)
            {
                if (ShouldStop(in ctx)) return result;
                if (!collection.TryGetValue(i, out var value))
                    return TriggerPlanExecutionResult.Failed($"ForEach collection failed to read index {i}");
                if (!TryWriteCurrentValue(in ctx, in value, out var writeError))
                    return TriggerPlanExecutionResult.Failed("ForEach item write failed: " + writeError);

                var childResult = _child.Execute(args, in ctx);
                if (childResult.IsFailed) return childResult;
                result = result.Merge(childResult);
            }

            return result;
        }

        private bool TryWriteCurrentValue<TCtx>(
            in ExecCtx<TCtx> ctx,
            in TriggerCollectionValue value,
            out string error)
        {
            switch (value.Kind)
            {
                case TriggerCollectionValueKind.Number:
                    return BlackboardMutation.TrySetNumeric(ctx.Blackboards, in _itemTarget, value.Number, out error);
                case TriggerCollectionValueKind.Boolean:
                    return BlackboardMutation.TrySetBool(ctx.Blackboards, in _itemTarget, value.Boolean, out error);
                case TriggerCollectionValueKind.String:
                    return BlackboardMutation.TrySetString(ctx.Blackboards, in _itemTarget, value.String, out error);
                default:
                    error = $"Unsupported collection value kind: {value.Kind}.";
                    return false;
            }
        }
    }
}
