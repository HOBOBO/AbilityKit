using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Demo.Moba.Systems.Bootstrap.Flow
{
    /// <summary>
    /// Stage 静态初始化器
    /// 在模块加载时自动发现和注册所有 Stage
    /// </summary>
    public static class MobaBootstrapStageInitializer
    {
        private static bool _initialized;
        private static bool _initializing;
        private static readonly object InitializationLock = new object();

        /// <summary>
        /// 初始化所有 Stage
        /// </summary>
        public static void Initialize()
        {
            lock (InitializationLock)
            {
                if (_initialized) return;
                if (_initializing)
                {
                    throw new InvalidOperationException("Recursive MOBA bootstrap stage initialization is not supported.");
                }

                _initializing = true;
                var registeredBefore = MobaBootstrapStageRegistry.Count;
                try
                {
                    InitializeCore();
                    _initialized = true;
                }
                catch
                {
                    MobaBootstrapStageRegistry.RollbackPendingTo(registeredBefore);
                    throw;
                }
                finally
                {
                    _initializing = false;
                }
            }
        }

        private static void InitializeCore()
        {
            if (MobaGeneratedBootstrapStageManifest.RegisterAll() > 0)
            {
                return;
            }

            if (AppContext.TryGetSwitch(
                    "AbilityKit.Moba.DisableBootstrapStageReflectionFallback",
                    out var reflectionFallbackDisabled) && reflectionFallbackDisabled)
            {
                throw new InvalidOperationException(
                    "The generated MOBA bootstrap stage manifest is empty and reflection fallback is disabled.");
            }

            var assembly = typeof(MobaBootstrapStageInitializer).Assembly;
            var stageTypes = assembly.GetTypes()
                .Where(t =>
                    !t.IsAbstract
                    && t.IsSubclassOf(typeof(MobaBootstrapStageBase))
                    && t.GetCustomAttribute<MobaBootstrapStageAttribute>() != null)
                .ToArray();

            if (stageTypes.Length == 0)
                throw new InvalidOperationException("No MOBA bootstrap stages were discovered.");

            foreach (var type in stageTypes)
            {
                try
                {
                    var ctor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(c => c.GetParameters().Length == 0);

                    MobaBootstrapStageBase stage;
                    if (ctor != null)
                    {
                        stage = (MobaBootstrapStageBase)ctor.Invoke(null);
                    }
                    else
                    {
                        stage = (MobaBootstrapStageBase)Activator.CreateInstance(type);
                    }

                    MobaBootstrapStageRegistry.Register(stage);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to register reflected MOBA bootstrap stage: {type.FullName}", ex);
                }
            }
        }
    }

    /// <summary>
    /// 静态初始化钩子
    /// 在模块被引用时自动初始化
    /// </summary>
    public static class MobaBootstrapFlowModule
    {
        static MobaBootstrapFlowModule()
        {
            MobaBootstrapStageInitializer.Initialize();
        }

        /// <summary>
        /// 确保 Stage 已注册
        /// </summary>
        public static void EnsureInitialized()
        {
        }
    }
}
