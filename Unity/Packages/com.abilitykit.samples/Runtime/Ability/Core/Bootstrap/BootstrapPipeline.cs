#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AbilityKit.Samples.Logic.Ability.Core.Bootstrap
{
    /// <summary>
    /// Bootstrap 管线，管理游戏世界的启动流程。
    /// </summary>
    public sealed class BootstrapPipeline
    {
        private readonly List<IBootstrapStage> _stages;

        public BootstrapPipeline()
        {
            _stages = new List<IBootstrapStage>();
        }

        /// <summary>
        /// 添加启动阶段。
        /// </summary>
        public void AddStage(IBootstrapStage stage)
        {
            if (stage == null)
            {
                throw new ArgumentNullException(nameof(stage));
            }

            _stages.Add(stage);
            _stages.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        /// <summary>
        /// 移除启动阶段。
        /// </summary>
        public bool RemoveStage(string stageId)
        {
            var index = _stages.FindIndex(s => s.StageId == stageId);
            if (index < 0)
            {
                return false;
            }

            _stages.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// 获取所有阶段。
        /// </summary>
        public IReadOnlyList<IBootstrapStage> GetStages()
        {
            return _stages.AsReadOnly();
        }

        /// <summary>
        /// 执行启动流程。
        /// </summary>
        public async Task ExecuteAsync(WorldBlueprint blueprint)
        {
            foreach (var stage in _stages)
            {
                await stage.ExecuteAsync(blueprint);
            }
        }
    }
}
