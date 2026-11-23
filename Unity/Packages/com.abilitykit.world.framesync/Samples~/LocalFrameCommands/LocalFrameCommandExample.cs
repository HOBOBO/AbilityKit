using System.Collections.Generic;

namespace AbilityKit.Ability.FrameSync.Samples
{
    /// <summary>
    /// 用类型化命令演示按帧消费与主动裁剪，不接入任何会话。
    /// </summary>
    public static class LocalFrameCommandExample
    {
        /// <summary>
        /// 验证已知空帧仍推进、缺失帧不推进，以及同帧命令的稳定执行顺序。
        /// </summary>
        public static bool Run()
        {
            var commands = new FrameCommandBuffer<int, int>(retainedFrameWindow: 2,
                commandComparer: Comparer<int>.Default);
            var ready = new HashSet<int> { 1, 2 };
            var stepCommands = new List<int>();

            commands.SubmitCommand(2, 1, 9);
            commands.SubmitCommand(2, 0, 3);
            commands.SubmitCommand(3, 0, 4);
            var frame = 0;
            var value = 0;
            while (ready.Contains(frame + 1))
            {
                frame++;
                commands.CopyFrameCommands(frame, stepCommands);
                value++;
                foreach (var command in stepCommands)
                    value = value * 10 + command;
            }

            commands.SubmitCommand(5, 0, 8);
            commands.TrimToWindow(5);
            return frame == 2 && value == 239 && !commands.TryGetFrameCommands(2, out _) &&
                commands.TryGetCommand(3, 0, out var retained) && retained == 4;
        }
    }
}
