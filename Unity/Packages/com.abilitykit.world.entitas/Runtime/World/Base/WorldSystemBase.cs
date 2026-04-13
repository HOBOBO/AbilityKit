using System;
using AbilityKit.Ability.World.DI;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// Entitas System 的基类，封装了 Entitas 的四个生命周期接口：
    /// IInitializeSystem、IExecuteSystem、ICleanupSystem、ITearDownSystem。
    /// </summary>
    public abstract class WorldSystemBase : global::Entitas.IInitializeSystem, global::Entitas.IExecuteSystem, global::Entitas.ICleanupSystem, global::Entitas.ITearDownSystem
    {
        private bool _enabled = true;

        /// <summary>
        /// 初始化系统基类。
        /// </summary>
        /// <param name="contexts">Entitas 上下文集合</param>
        /// <param name="services">世界服务解析器</param>
        protected WorldSystemBase(global::Entitas.IContexts contexts, IWorldResolver services)
        {
            Contexts = contexts ?? throw new ArgumentNullException(nameof(contexts));
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <summary>
        /// 获取 Entitas 上下文集合。
        /// </summary>
        protected global::Entitas.IContexts Contexts { get; }

        /// <summary>
        /// 获取世界服务解析器，用于解析依赖注入的服务。
        /// </summary>
        protected IWorldResolver Services { get; }

        /// <summary>
        /// 系统的执行优先级，数值越小越先执行。
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// 系统是否启用。禁用时所有生命周期回调都会被跳过。
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        /// <summary>
        /// 初始化阶段调用，仅在 Enabled 为 true 时执行。
        /// </summary>
        public void Initialize()
        {
            if (!_enabled) return;
            OnInit();
        }

        /// <summary>
        /// 每帧执行调用，仅在 Enabled 为 true 且 CanExecute() 返回 true 时执行。
        /// </summary>
        public void Execute()
        {
            if (!_enabled) return;
            if (!CanExecute()) return;
            OnExecute();
        }

        /// <summary>
        /// 每帧清理阶段调用，仅在 Enabled 为 true 时执行。
        /// </summary>
        public void Cleanup()
        {
            if (!_enabled) return;
            OnCleanup();
        }

        /// <summary>
        /// 销毁阶段调用，始终执行，不受 Enabled 影响。
        /// </summary>
        public void TearDown()
        {
            OnTearDown();
        }

        /// <summary>
        /// 在 Execute 执行前检查是否可以执行。默认返回 true。
        /// </summary>
        /// <returns>是否可以执行</returns>
        protected virtual bool CanExecute() => true;

        /// <summary>
        /// 初始化逻辑，子类重写此方法实现初始化逻辑。
        /// </summary>
        protected virtual void OnInit() { }

        /// <summary>
        /// 每帧执行逻辑，子类重写此方法实现帧更新逻辑。
        /// </summary>
        protected virtual void OnExecute() { }

        /// <summary>
        /// 每帧清理逻辑，子类重写此方法实现清理逻辑。
        /// </summary>
        protected virtual void OnCleanup() { }

        /// <summary>
        /// 销毁逻辑，子类重写此方法实现资源释放逻辑。
        /// </summary>
        protected virtual void OnTearDown() { }
    }
}