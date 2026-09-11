#nullable enable

using System;
using AbilityKit.Samples.Logic.Ability.Core.Bootstrap;

namespace AbilityKit.Samples.Logic.Ability.Samples.Bootstrap
{
    /// <summary>
    /// 游戏世界的蓝图定义。
    /// 定义游戏启动时需要初始化的所有模块。
    /// </summary>
    public sealed class GameWorldBlueprint
    {
        private readonly WorldBlueprint _blueprint;

        public GameWorldBlueprint()
        {
            _blueprint = new WorldBlueprint();
        }

        /// <summary>
        /// 获取基础蓝图。
        /// </summary>
        public WorldBlueprint Blueprint => _blueprint;

        /// <summary>
        /// 配置游戏世界的模块。
        /// </summary>
        public void ConfigureModules()
        {
            _blueprint.RegisterModule(new GameBootstrapModule("core", "Core Module", 0));
            _blueprint.RegisterModule(new GameBootstrapModule("combat", "Combat Module", 10));
            _blueprint.RegisterModule(new GameBootstrapModule("ability", "Ability Module", 20));
            _blueprint.RegisterModule(new GameBootstrapModule("rendering", "Rendering Module", 100));
        }

        /// <summary>
        /// 构建并初始化世界。
        /// </summary>
        public void Build()
        {
            ConfigureModules();
            _blueprint.InitializeModules();
        }

        /// <summary>
        /// 销毁世界。
        /// </summary>
        public void Destroy()
        {
            _blueprint.DestroyModules();
        }
    }
}
