#nullable enable

using System;
using System.Threading.Tasks;

namespace AbilityKit.Samples.Logic.Ability.Core.Bootstrap
{
    /// <summary>
    /// 启动阶段的接口定义。
    /// 定义世界初始化过程中的各个阶段。
    /// </summary>
    public interface IBootstrapStage
    {
        /// <summary>
        /// 阶段的唯一标识符。
        /// </summary>
        string StageId { get; }

        /// <summary>
        /// 阶段的执行顺序，数值越小越先执行。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 阶段的显示名称。
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// 执行阶段初始化逻辑。
        /// </summary>
        /// <param name="blueprint">世界蓝图</param>
        Task ExecuteAsync(WorldBlueprint blueprint);
    }
}
