using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;

namespace AbilityKit.Ability.Host.Extensions.FrameSync
{
    /// <summary>
    /// 本地输入按目标帧排队，维持过期重定向、同类覆盖和队列淘汰规则。
    /// </summary>
    internal static class ClientPredictionLocalInputQueue
    {
        internal static void ScheduleLocalInputs(ClientPredictionWorldContext ctx, ClientPredictionDriverState state, int maxLocalDelayQueueDepth, LocalPlayerInputEvent[] events)
        {
            if (ctx == null || events == null || events.Length == 0) return;
            ctx.Inputs.PendingLocalInputs ??= new SortedDictionary<int, List<LocalPlayerInputEvent>>();

            var droppedStaleInput = false;
            for (var i = 0; i < events.Length; i++)
            {
                var input = events[i];
                var targetFrame = input.Frame.Value > 0
                    ? input.Frame.Value
                    : ctx.Progress.PredictedFrame.Value + 1;
                if (targetFrame <= ctx.Progress.PredictedFrame.Value)
                {
                    if (input.CanRetargetIfStale)
                    {
                        targetFrame = ctx.Progress.PredictedFrame.Value + 1;
                    }
                    else
                    {
                        droppedStaleInput = true;
                        continue;
                    }
                }

                if (!ctx.Inputs.PendingLocalInputs.TryGetValue(targetFrame, out var frameInputs))
                {
                    frameInputs = new List<LocalPlayerInputEvent>(2);
                    ctx.Inputs.PendingLocalInputs[targetFrame] = frameInputs;
                }

                var replaced = false;
                if (input.CanRetargetIfStale)
                {
                    for (var pendingIndex = 0; pendingIndex < frameInputs.Count; pendingIndex++)
                    {
                        var pending = frameInputs[pendingIndex];
                        if (pending.CanRetargetIfStale &&
                            pending.OpCode == input.OpCode &&
                            pending.PlayerId.Value == input.PlayerId.Value)
                        {
                            frameInputs[pendingIndex] = input;
                            replaced = true;
                            break;
                        }
                    }
                }

                if (!replaced)
                {
                    frameInputs.Add(input);
                }
            }

            if (droppedStaleInput)
            {
                state.TotalLocalDelayQueueDroppedBatches++;
            }

            while (ctx.Inputs.PendingLocalInputs.Count > maxLocalDelayQueueDepth)
            {
                var firstKey = int.MaxValue;
                foreach (var pending in ctx.Inputs.PendingLocalInputs)
                {
                    firstKey = pending.Key;
                    break;
                }
                if (firstKey == int.MaxValue) break;
                ctx.Inputs.PendingLocalInputs.Remove(firstKey);
                state.TotalLocalDelayQueueDroppedBatches++;
            }
        }

        internal static LocalPlayerInputEvent[] TakeLocalInputs(ClientPredictionWorldContext ctx, FrameIndex frame)
        {
            if (ctx?.Inputs.PendingLocalInputs == null ||
                !ctx.Inputs.PendingLocalInputs.TryGetValue(frame.Value, out var inputs) ||
                inputs == null ||
                inputs.Count == 0)
            {
                return Array.Empty<LocalPlayerInputEvent>();
            }

            ctx.Inputs.PendingLocalInputs.Remove(frame.Value);
            return inputs.ToArray();
        }
    }
}
