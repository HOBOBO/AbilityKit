using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Validation
{
    /// <summary>
    /// Validates formal TriggerPlan executable trees before runtime execution.
    /// </summary>
    public sealed class TriggerPlanExecutableValidator
    {
        public ValidationResult Validate(ITriggerPlanExecutable root, string path = "$.executionRoot")
        {
            var result = ValidationResult.Success;
            ValidateNode(root, path, ref result, requireNode: true);
            return result;
        }

        private static void ValidateNode(ITriggerPlanExecutable node, string path, ref ValidationResult result, bool requireNode)
        {
            if (node == null)
            {
                if (requireNode)
                {
                    result.AddError(
                        ValidationErrorCodes.INVALID_EXECUTION_NODE,
                        "执行节点不能为空。",
                        path);
                }

                return;
            }

            if (node.Weight <= 0f)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    $"执行节点权重必须大于 0: {node.Weight}",
                    $"{path}.weight");
            }

            switch (node)
            {
                case ActionCallTriggerPlanExecutable action:
                    ValidateAction(action.Action, path, ref result);
                    break;
                case CompositeTriggerPlanExecutableBase composite:
                    ValidateComposite(composite, path, ref result);
                    break;
                case RepeatTriggerPlanExecutable repeat:
                    ValidateRepeat(repeat, path, ref result);
                    break;
                case UntilTriggerPlanExecutable until:
                    ValidateUntil(until, path, ref result);
                    break;
                case ForEachTriggerPlanExecutable forEach:
                    ValidateForEach(forEach, path, ref result);
                    break;
                case IfTriggerPlanExecutable branch:
                    ValidateIf(branch, path, ref result);
                    break;
                case ScheduledTriggerPlanExecutable scheduled:
                    ValidateScheduled(scheduled, path, ref result);
                    break;
                case InvertTriggerPlanExecutable invert:
                    ValidateNode(invert.Child, $"{path}.child", ref result, requireNode: true);
                    break;
                case SucceedTriggerPlanExecutable succeed:
                    ValidateNode(succeed.Child, $"{path}.child", ref result, requireNode: false);
                    break;
                case FailTriggerPlanExecutable fail:
                    ValidateNode(fail.Child, $"{path}.child", ref result, requireNode: false);
                    break;
                case MetadataTriggerPlanExecutable metadata:
                    ValidateNode(metadata.Child, $"{path}.child", ref result, requireNode: true);
                    break;
            }
        }

        private static void ValidateAction(in ActionCallPlan action, string path, ref ValidationResult result)
        {
            if (action.ScheduleMode == EActionScheduleMode.Timeline)
            {
                result.AddError(
                    ValidationErrorCodes.UNSUPPORTED_ACTION_SCHEDULE,
                    "Timeline Action 当前没有正式的子 Action 时间线计划结构，不能作为执行树 Action 节点使用。",
                    $"{path}.action.scheduleMode");
            }

            if (action.MaxExecutions < -1 || action.MaxExecutions == 0)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_ACTION_SCHEDULE,
                    $"Action 最大执行次数必须为 -1 或正数: {action.MaxExecutions}",
                    $"{path}.action.maxExecutions");
            }
        }

        private static void ValidateComposite(CompositeTriggerPlanExecutableBase composite, string path, ref ValidationResult result)
        {
            if (composite.Children == null || composite.Children.Count == 0)
            {
                result.AddWarning(
                    ValidationErrorCodes.EMPTY_EXECUTION_NODE,
                    $"{composite.Name} 执行节点没有子节点。",
                    $"{path}.children");
                return;
            }

            for (int i = 0; i < composite.Children.Count; i++)
            {
                ValidateNode(composite.Children[i], $"{path}.children[{i}]", ref result, requireNode: true);
            }
        }

        private static void ValidateRepeat(RepeatTriggerPlanExecutable repeat, string path, ref ValidationResult result)
        {
            if (repeat.Count <= 0)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    $"Repeat 执行次数必须大于 0: {repeat.Count}",
                    $"{path}.count");
            }

            ValidateNode(repeat.Child, $"{path}.child", ref result, requireNode: true);
        }

        private static void ValidateUntil(UntilTriggerPlanExecutable until, string path, ref ValidationResult result)
        {
            if (until.MaxIterations <= 0)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    $"Until 最大迭代次数必须大于 0: {until.MaxIterations}",
                    $"{path}.maxIterations");
            }

            if (until.UntilCondition == null)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    "Until 执行节点必须配置 UntilCondition。",
                    $"{path}.untilCondition");
            }

            ValidateNode(until.Child, $"{path}.child", ref result, requireNode: true);
        }

        private static void ValidateForEach(ForEachTriggerPlanExecutable forEach, string path, ref ValidationResult result)
        {
            if (forEach.MaxIterations <= 0)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    $"ForEach maxIterations must be greater than zero: {forEach.MaxIterations}",
                    $"{path}.maxIterations");
            }
            if (forEach.ItemTarget.BoardId == 0 || forEach.ItemTarget.KeyId == 0 ||
                forEach.ItemTarget.KeyType == AbilityKit.Triggering.Blackboard.BlackboardKeyType.Unknown)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    "ForEach itemTarget must reference a typed Blackboard key.",
                    $"{path}.itemTarget");
            }

            ValidateNode(forEach.Child, $"{path}.child", ref result, requireNode: true);
        }

        private static void ValidateScheduled(ScheduledTriggerPlanExecutable scheduled, string path, ref ValidationResult result)
        {
            if (!System.Enum.IsDefined(typeof(EScheduleMode), scheduled.ScheduleMode) || scheduled.ScheduleMode == EScheduleMode.Transient)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    $"Scheduled 执行节点调度模式非法: {scheduled.ScheduleMode}",
                    $"{path}.scheduleMode");
            }

            if ((scheduled.ScheduleMode == EScheduleMode.Timed || scheduled.ScheduleMode == EScheduleMode.Periodic || scheduled.ScheduleMode == EScheduleMode.Continuous) && scheduled.IntervalMs < 0f)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    $"Scheduled 执行节点间隔不能为负数: {scheduled.IntervalMs}",
                    $"{path}.intervalMs");
            }

            if ((scheduled.ScheduleMode == EScheduleMode.Periodic || scheduled.ScheduleMode == EScheduleMode.Continuous) && scheduled.IntervalMs <= 0f)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    $"Scheduled 周期/持续执行节点间隔必须大于 0: {scheduled.IntervalMs}",
                    $"{path}.intervalMs");
            }

            if (scheduled.MaxExecutions < -1 || scheduled.MaxExecutions == 0)
            {
                result.AddError(
                    ValidationErrorCodes.INVALID_EXECUTION_NODE,
                    $"Scheduled 最大执行次数必须为 -1 或正数: {scheduled.MaxExecutions}",
                    $"{path}.maxExecutions");
            }

            ValidateNode(scheduled.Child, $"{path}.child", ref result, requireNode: true);
        }

        private static void ValidateIf(IfTriggerPlanExecutable branch, string path, ref ValidationResult result)
        {
            if (branch.BranchCondition == null)
            {
                result.AddWarning(
                    ValidationErrorCodes.AMBIGUOUS_EXECUTION_NODE,
                    "If 执行节点未配置分支条件，将始终走 Then 分支。",
                    $"{path}.condition");
            }

            ValidateNode(branch.ThenBranch, $"{path}.then", ref result, requireNode: true);
            ValidateNode(branch.ElseBranch, $"{path}.else", ref result, requireNode: false);
        }
    }
}
