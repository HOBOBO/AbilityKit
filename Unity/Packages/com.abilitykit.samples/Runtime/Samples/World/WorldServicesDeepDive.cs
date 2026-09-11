#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Samples.Abstractions;

namespace AbilityKit.Samples.Logic.Samples.World
{
    /// <summary>
    /// WorldServicesDeepDive - 深入讲解服务注册和解析模式
    /// 涵盖 Singleton、Transient、Scoped 三种生命周期
    /// </summary>
    [Sample]
    public sealed class WorldServicesDeepDive : SampleBase
    {
        public override string Title => "World Services Deep Dive";
        public override string Description => "深入讲解服务注册与解析：Singleton、Transient、Scoped";
        public override SampleCategory Category => SampleCategory.World;

        protected override void OnRun()
        {
            Log("=== World Services 深入讲解 ===");
            Output.Divider();

            // 1. 三种生命周期概述
            Log("【1】三种服务生命周期");
            Output.Bullet("Singleton - 单例，容器内只创建一次，全生命周期共享");
            Output.Bullet("Transient - 临时，每次 Resolve 都创建新实例");
            Output.Bullet("Scoped - 作用域，基于 WorldScope 创建，Scope 结束时销毁");
            Output.Line();

            // 2. 创建容器
            Log("【2】创建演示容器");
            var container = CreateDemoContainer();
            Log("容器创建完成");
            Output.Line();

            // 3. Singleton 生命周期演示
            Log("【3】Singleton 生命周期");
            Log("注册: builder.RegisterServiceType<ISingletonService, SingletonService>(WorldLifetime.Singleton)");
            var s1 = container.Resolve<ISingletonService>();
            var s2 = container.Resolve<ISingletonService>();
            Log($"第一次 Resolve: {s1.InstanceId}");
            Log($"第二次 Resolve: {s2.InstanceId}");
            Log($"是否同一实例: {ReferenceEquals(s1, s2)}");
            Log("  (True 表示 Singleton 正常工作)");
            Output.Line();

            // 4. Transient 生命周期演示
            Log("【4】Transient 生命周期");
            Log("注册: builder.RegisterServiceType<ITransientService, TransientService>(WorldLifetime.Transient)");
            var t1 = container.Resolve<ITransientService>();
            var t2 = container.Resolve<ITransientService>();
            Log($"第一次 Resolve: {t1.InstanceId}");
            Log($"第二次 Resolve: {t2.InstanceId}");
            Log($"是否同一实例: {ReferenceEquals(t1, t2)}");
            Log("  (False 表示 Transient 正常工作)");
            Output.Line();

            // 5. Scoped 生命周期演示
            Log("【5】Scoped 生命周期");
            Log("注册: builder.RegisterServiceType<IScopedService, ScopedService>(WorldLifetime.Scoped)");
            Log("使用: container.CreateScope() 创建作用域");

            using (var scope1 = container.CreateScope())
            {
                var sc1_a = scope1.Resolve<IScopedService>();
                var sc1_b = scope1.Resolve<IScopedService>();
                Log($"Scope1 内第一次 Resolve: {sc1_a.InstanceId}");
                Log($"Scope1 内第二次 Resolve: {sc1_b.InstanceId}");
                Log($"Scope1 内是否同一实例: {ReferenceEquals(sc1_a, sc1_b)}");

                using (var scope2 = container.CreateScope())
                {
                    var sc2 = scope2.Resolve<IScopedService>();
                    Log($"Scope2 内 Resolve: {sc2.InstanceId}");
                    Log($"不同 Scope 是否同一实例: {!ReferenceEquals(sc1_a, sc2)}");
                    Log("  (True 表示 Scoped 在不同 Scope 间隔离)");
                }
            }
            Output.Line();

            // 6. 依赖注入演示
            Log("【6】依赖注入 - 服务可注入其他服务");
            Log("注册: builder.RegisterServiceType<IDependentService, DependentService>(WorldLifetime.Singleton)");
            Log("  DependentService 构造函数需要 ISingletonService");
            var dependent = container.Resolve<IDependentService>();
            Log($"DependentService 实例: {dependent.InstanceId}");
            Log($"注入的 ISingletonService: {dependent.SingletonInstance.InstanceId}");
            Log($"注入验证成功: {dependent.SingletonInstance != null}");
            Output.Line();

            // 7. TryResolve 模式
            Log("【7】TryResolve 安全解析");
            Log("当服务未注册时，Resolve 会抛出异常");
            Log("使用 TryResolve 可以安全处理未注册的服务:");

            var container2 = new WorldContainerBuilder().Build();
            var success = container2.TryResolve<ISingletonService>(out var maybeNull);
            Log($"TryResolve 未注册服务: success={success}, instance={(maybeNull == null ? "null" : "not null")}");
            Output.Line();

            // 8. 循环依赖检测
            Log("【8】循环依赖检测");
            Log("WorldContainer 会检测循环依赖并抛出异常:");
            var cyclicContainer = CreateCyclicContainer();
            try
            {
                var _ = cyclicContainer.Resolve<ICyclicA>();
                Log("  异常: 未检测到循环依赖!");
            }
            catch (InvalidOperationException ex)
            {
                var msg = ex.Message.Length > 80 ? ex.Message.Substring(0, 80) + "..." : ex.Message;
                Log($"  捕获异常: {msg}");
                Log("  循环依赖检测正常工作");
            }

            Output.Divider();

            // Cleanup
            container.Dispose();
            container2.Dispose();
            cyclicContainer.Dispose();
        }

        private WorldContainer CreateDemoContainer()
        {
            var builder = new WorldContainerBuilder();

            // Singleton 服务
            builder.RegisterServiceType<ISingletonService, SingletonService>(WorldLifetime.Singleton);

            // Transient 服务
            builder.RegisterServiceType<ITransientService, TransientService>(WorldLifetime.Transient);

            // Scoped 服务
            builder.RegisterServiceType<IScopedService, ScopedService>(WorldLifetime.Scoped);

            // 依赖其他服务的服务
            builder.RegisterServiceType<IDependentService, DependentService>(WorldLifetime.Singleton);

            // 基础服务
            builder.RegisterServiceType<IWorldLogger, NullWorldLogger>(WorldLifetime.Singleton);

            return builder.Build();
        }

        private WorldContainer CreateCyclicContainer()
        {
            var builder = new WorldContainerBuilder();

            builder.Register<ICyclicA>(WorldLifetime.Singleton, r => new CyclicA(r.Resolve<ICyclicB>()));
            builder.Register<ICyclicB>(WorldLifetime.Singleton, r => new CyclicB(r.Resolve<ICyclicA>()));

            return builder.Build();
        }
    }

    #region Demo Service Interfaces

    public interface ISingletonService
    {
        string InstanceId { get; }
    }

    public interface ITransientService
    {
        string InstanceId { get; }
    }

    public interface IScopedService
    {
        string InstanceId { get; }
    }

    public interface IDependentService
    {
        string InstanceId { get; }
        ISingletonService SingletonInstance { get; }
    }

    public interface ICyclicA { }
    public interface ICyclicB { }

    #endregion

    #region Demo Service Implementations

    public sealed class SingletonService : ISingletonService
    {
        private static int _counter;
        private readonly int _id = ++_counter;

        public string InstanceId => $"Singleton-{_id}";
    }

    public sealed class TransientService : ITransientService
    {
        private static int _counter;
        private readonly int _id = ++_counter;

        public string InstanceId => $"Transient-{_id}";
    }

    public sealed class ScopedService : IScopedService
    {
        private static int _counter;
        private readonly int _id = ++_counter;

        public string InstanceId => $"Scoped-{_id}";
    }

    public sealed class DependentService : IDependentService
    {
        private static int _counter;
        private readonly int _id = ++_counter;

        public DependentService(ISingletonService singleton)
        {
            SingletonInstance = singleton;
        }

        public string InstanceId => $"Dependent-{_id}";
        public ISingletonService SingletonInstance { get; }
    }

    public sealed class CyclicA : ICyclicA
    {
        public CyclicA(ICyclicB b) { }
    }

    public sealed class CyclicB : ICyclicB
    {
        public CyclicB(ICyclicA a) { }
    }

    #endregion
}
