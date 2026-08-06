using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Markers;

/// <summary>
/// 文件名称: MobaSnapshotEmitterRegistry.cs
/// 
/// 功能描述: 扫描并解析基于 Attribute 注册的快照输出器。
/// 
/// 创建日期: 2026-05-27
/// 修改日期: 2026-05-27
/// </summary>
namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// 快照输出器注册表。
    /// </summary>
    public sealed class MobaSnapshotEmitterRegistry : MobaMarkerRegistryBase<IMobaSnapshotEmitter>
    {
        public MobaSnapshotEmitterRegistry() : base(8)
        {
        }

        /// <summary>
        /// 创建默认注册表并扫描已加载程序集。
        /// </summary>
        public static MobaSnapshotEmitterRegistry CreateDefault()
        {
            MobaSnapshotEmitterRegistry registry = new MobaSnapshotEmitterRegistry();
            if (RegisterGeneratedAndExternalEmitters(registry) == 0)
            {
                if (AppContext.TryGetSwitch(
                        "AbilityKit.Moba.DisableSnapshotEmitterReflectionFallback",
                        out var reflectionFallbackDisabled) && reflectionFallbackDisabled)
                {
                    throw new InvalidOperationException(
                        "The generated MOBA snapshot emitter manifest is empty and reflection fallback is disabled.");
                }

                MarkerScanner<MobaSnapshotEmitterAttribute>.Scan(
                    new[] { typeof(MobaSnapshotEmitterRegistry).Assembly },
                    registry);
                return registry;
            }

            return registry;
        }

        private static int RegisterGeneratedAndExternalEmitters(MobaSnapshotEmitterRegistry registry)
        {
            var runtimeAssembly = typeof(MobaSnapshotEmitterRegistry).Assembly;
            var generatedCount = MobaGeneratedSnapshotEmitterManifest.Register(registry);
            var assemblies = MobaRegistryAssemblyDiscovery.GetExternalAssemblies(runtimeAssembly);
            for (int i = 0; i < assemblies.Length; i++)
            {
                MarkerScanner<MobaSnapshotEmitterAttribute>.Scan(new[] { assemblies[i] }, registry);
            }

            return generatedCount;
        }

        /// <summary>
        /// 注册指定优先级的输出器类型。
        /// </summary>
        public void Register(int priority, Type implType)
        {
            TryRegister(key: priority, implType);
        }

        internal bool TryRegisterGenerated(int priority, Type implType)
        {
            return TryRegister(key: priority, implType);
        }

        internal bool ContainsRegistration(int priority, Type implType)
        {
            var entries = GetEntriesSnapshot(sortByKey: false);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key == priority && entries[i].ImplType == implType)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从世界服务容器解析所有输出器实例。
        /// </summary>
        public List<IMobaSnapshotEmitter> ResolveEmitters(IWorldResolver services)
        {
            List<Entry> sorted = GetEntriesSnapshot(sortByKey: true);

            List<IMobaSnapshotEmitter> emitters = new List<IMobaSnapshotEmitter>(sorted.Count);
            for (int i = 0; i < sorted.Count; i++)
            {
                Entry entry = sorted[i];
                if (services != null && services.TryResolve(entry.ImplType, out object service) && service is IMobaSnapshotEmitter emitter)
                {
                    emitters.Add(emitter);
                }
            }

            return emitters;
        }
    }
}
