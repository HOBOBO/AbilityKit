using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// Tick计数系统。
    /// 每帧递增 WorldTickCounter.TickCount，并在首帧输出日志。
    /// </summary>
    internal sealed class TickCounterSystem : global::Entitas.IExecuteSystem
    {
        private readonly IWorldContext _ctx;
        private readonly WorldTickCounter _counter;
        private IWorldLogger _logger;

        /// <summary>
        /// 创建 TickCounterSystem 实例。
        /// </summary>
        /// <param name="ctx">世界上下文</param>
        /// <param name="counter">Tick计数器</param>
        public TickCounterSystem(IWorldContext ctx, WorldTickCounter counter)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _counter = counter ?? throw new ArgumentNullException(nameof(counter));
        }

        /// <summary>
        /// 每帧执行：递增Tick计数并在首帧输出日志。
        /// </summary>
        public void Execute()
        {
            _counter.TickCount++;

            if (_logger == null)
            {
                _logger = _ctx.Services.Resolve<IWorldLogger>();
            }

            if (_counter.TickCount == 1)
            {
                _logger.Info("World tick started");
            }
        }
    }
}