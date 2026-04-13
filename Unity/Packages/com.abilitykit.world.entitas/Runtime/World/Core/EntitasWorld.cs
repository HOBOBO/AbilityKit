using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Common.Log;

namespace AbilityKit.Ability.World
{
    /// <summary>
    /// 基于 Entitas ECS 的世界实现。
    /// 负责管理 Entitas 上下文、系统和服务的生命周期。
    /// </summary>
    public sealed class EntitasWorld : IEntitasWorld
    {
        private readonly WorldCreateOptions _options;
        private readonly IEntitasContextsFactory _contextsFactory;
        private WorldContainer _container;
        private WorldScope _scope;
        private bool _initialized;
        private IWorldClock _clock;

        private global::Entitas.IContexts _contexts;

        /// <summary>
        /// 创建 Entitas 世界实例。
        /// </summary>
        /// <param name="options">世界创建选项，包含 ID、类型和模块配置</param>
        public EntitasWorld(WorldCreateOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _contextsFactory = _options.GetEntitasContextsFactoryOrThrow();

            Id = _options.Id;
            WorldType = _options.WorldType;

            _contexts = _contextsFactory.Create();
            if (_contexts == null) throw new InvalidOperationException("[EntitasWorld] EntitasContextsFactory.Create() returned null.");
            Systems = new global::Entitas.Systems();
        }

        /// <summary>
        /// 获取世界的唯一标识。
        /// </summary>
        public WorldId Id { get; }

        /// <summary>
        /// 获取世界的类型标识。
        /// </summary>
        public string WorldType { get; }

        /// <summary>
        /// 获取 Entitas 上下文集合。
        /// </summary>
        public global::Entitas.IContexts Contexts => _contexts;

        /// <summary>
        /// 获取 Entitas 系统容器。
        /// </summary>
        public global::Entitas.Systems Systems { get; }

        /// <summary>
        /// 获取世界服务解析器。
        /// </summary>
        public IWorldResolver Services => _scope;

        /// <summary>
        /// 内部方法：设置服务的容器和作用域。
        /// </summary>
        /// <param name="container">服务容器</param>
        /// <param name="scope">服务作用域</param>
        internal void SetComposition(WorldContainer container, WorldScope scope)
        {
            _container = container;
            _scope = scope;
        }

        /// <summary>
        /// 初始化世界，注册所有系统和服务。
        /// 仅在首次调用时执行。
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                EntitasWorldComposer.Compose(this, _options);
            }
            catch
            {
                try { _scope?.Dispose(); }
                catch (Exception ex) { Log.Exception(ex); }
                _scope = null;

                try { _container?.Dispose(); }
                catch (Exception ex) { Log.Exception(ex); }
                _container = null;

                _initialized = false;
                throw;
            }
        }

        /// <summary>
        /// 执行一帧更新，驱动所有系统执行。
        /// </summary>
        /// <param name="deltaTime">距离上一帧的时间（秒）</param>
        public void Tick(float deltaTime)
        {
            if (!_initialized) return;

            Systems.Execute();
            Systems.Cleanup();

            if (_clock == null)
            {
                _clock = _scope?.Resolve<IWorldClock>();
            }
            _clock?.Tick(deltaTime);
        }

        /// <summary>
        /// 释放世界资源，包括系统和上下文。
        /// </summary>
        public void Dispose()
        {
            if (_container == null && _scope == null) return;

            try
            {
                Systems.TearDown();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }

            _scope?.Dispose();
            _scope = null;

            _container?.Dispose();
            _container = null;

            try { _contextsFactory?.Release(_contexts); }
            catch (Exception ex) { Log.Exception(ex); }
            _contexts = null;
        }
    }
}