using System;
using System.Collections.Generic;

namespace AbilityKit.Combat.Projectile
{
    /// <summary>
    /// 投射物命中策略注册表。
    ///
    /// 约定与 targeting 的 <c>TargetRuleRegistry</c>、PlanAction 的 <c>PlanActionModuleAttribute</c> 一致：
    /// 框架包只提供注册表与两个内置策略，新增命中策略在项目侧通过
    /// <see cref="ProjectileHitPolicyAttribute"/> 或 <see cref="RegisterFactory"/> 注册，
    /// 不需要修改框架包。
    ///
    /// 未注册的 kind 一律回退到 <see cref="ExitOnHitPolicy"/>（fail-closed，不放行未知命中行为链）。
    /// </summary>
    public static class ProjectileHitPolicyFactory
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<int, Func<int, IProjectileHitPolicy>> Factories =
            new Dictionary<int, Func<int, IProjectileHitPolicy>>();
        private static readonly Dictionary<int, Type> MarkedTypes = new Dictionary<int, Type>();

        private static readonly Dictionary<int, PierceHitPolicy> PierceCache = new Dictionary<int, PierceHitPolicy>(16);
        private static readonly IProjectileHitPolicy InfinitePierce = new InfinitePierceHitPolicy();

        /// <summary>已注册的命中策略种类数量。</summary>
        public static int RegisteredKindCount
        {
            get { lock (SyncRoot) return Factories.Count; }
        }

        /// <summary>
        /// 注册一个命中策略构造入口。同一 kind 重复注册时保留首次注册的实现。
        /// </summary>
        public static void RegisterFactory(int kind, Func<int, IProjectileHitPolicy> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (IsBuiltInKind(kind))
                throw new InvalidOperationException($"Projectile hit policy kind {kind} is built in and cannot be replaced.");
            lock (SyncRoot)
            {
                if (!Factories.ContainsKey(kind)) Factories[kind] = factory;
            }
        }

        /// <summary>
        /// 注销自定义构造入口；内置策略（ExitOnHit / Pierce）不可注销。
        /// </summary>
        public static bool UnregisterFactory(int kind)
        {
            if (kind == (int)ProjectileHitPolicyKind.ExitOnHit ||
                kind == (int)ProjectileHitPolicyKind.Pierce)
                return false;
            lock (SyncRoot)
            {
                return Factories.Remove(kind);
            }
        }

        /// <summary>
        /// 扫描程序集，按 <see cref="ProjectileHitPolicyAttribute"/> 注册命中策略。
        /// </summary>
        public static void Scan(params System.Reflection.Assembly[] assemblies)
        {
            if (assemblies == null) return;
            for (var i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i] == null) continue;
                Type[] types;
                try { types = assemblies[i].GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }

                for (var t = 0; t < types.Length; t++)
                {
                    var type = types[t];
                    if (type == null || type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IProjectileHitPolicy).IsAssignableFrom(type)) continue;
                    var attr = (ProjectileHitPolicyAttribute)Attribute.GetCustomAttribute(
                        type, typeof(ProjectileHitPolicyAttribute));
                    if (attr == null) continue;
                    RegisterMarkedType(attr.Kind, type);
                }
            }
        }

        /// <summary>
        /// 显式注册一个命中策略类型（无需反射扫描）。
        /// </summary>
        public static void RegisterType(int kind, Type policyType)
        {
            if (policyType == null) throw new ArgumentNullException(nameof(policyType));
            if (!typeof(IProjectileHitPolicy).IsAssignableFrom(policyType) ||
                policyType.IsAbstract || policyType.IsInterface)
                throw new ArgumentException("Policy type must be a concrete IProjectileHitPolicy.", nameof(policyType));
            if (policyType.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException("Policy type must have a public parameterless constructor; use RegisterFactory otherwise.", nameof(policyType));
            RegisterMarkedType(kind, policyType);
        }

        /// <summary>
        /// 按 kind 创建命中策略。param 为配置里的 hit policy 参数（如穿透次数）。
        /// 未注册的 kind 回退到 <see cref="ExitOnHitPolicy"/>。
        /// </summary>
        public static IProjectileHitPolicy Create(ProjectileHitPolicyKind kind, int param)
        {
            return Create((int)kind, param);
        }

        /// <inheritdoc cref="Create(ProjectileHitPolicyKind,int)"/>
        public static IProjectileHitPolicy Create(int kind, int param)
        {
            // 内置策略优先，保证 Pierce 的缓存与无限穿透单例行为不变。
            if (kind == (int)ProjectileHitPolicyKind.Pierce)
                return CreatePierce(param);
            if (kind == (int)ProjectileHitPolicyKind.ExitOnHit)
                return ExitOnHitPolicy.Instance;

            Func<int, IProjectileHitPolicy> factory = null;
            Type markedType = null;
            lock (SyncRoot)
            {
                if (!Factories.TryGetValue(kind, out factory))
                    MarkedTypes.TryGetValue(kind, out markedType);
            }

            if (factory != null) return factory(param) ?? ExitOnHitPolicy.Instance;

            if (markedType != null && markedType.GetConstructor(Type.EmptyTypes) != null &&
                Activator.CreateInstance(markedType) is IProjectileHitPolicy created && created != null)
                return created;

            return ExitOnHitPolicy.Instance;
        }

        private static void RegisterMarkedType(int kind, Type policyType)
        {
            if (IsBuiltInKind(kind))
                throw new InvalidOperationException($"Projectile hit policy kind {kind} is built in and cannot be replaced.");
            if (!typeof(IProjectileHitPolicy).IsAssignableFrom(policyType) ||
                policyType.IsAbstract || policyType.IsInterface || policyType.GetConstructor(Type.EmptyTypes) == null)
                throw new ArgumentException("Marked policy type must be concrete, implement IProjectileHitPolicy, and have a public parameterless constructor.", nameof(policyType));
            lock (SyncRoot)
            {
                if (MarkedTypes.ContainsKey(kind)) return;
                MarkedTypes[kind] = policyType;
                if (!Factories.ContainsKey(kind)) Factories[kind] = new ActivatorFactory(policyType).Create;
            }
        }

        private static bool IsBuiltInKind(int kind)
        {
            return kind == (int)ProjectileHitPolicyKind.ExitOnHit ||
                   kind == (int)ProjectileHitPolicyKind.Pierce;
        }

        private static IProjectileHitPolicy CreatePierce(int param)
        {
            if (param < 0) return InfinitePierce;
            var n = Math.Max(1, param);
            lock (SyncRoot)
            {
                if (PierceCache.TryGetValue(n, out var cached) && cached != null) return cached;
                var created = new PierceHitPolicy(n);
                PierceCache[n] = created;
                return created;
            }
        }

        private sealed class ActivatorFactory
        {
            private readonly Type _type;

            public ActivatorFactory(Type type) { _type = type; }

            public IProjectileHitPolicy Create(int param)
            {
                if (_type.GetConstructor(Type.EmptyTypes) == null) return null;
                return Activator.CreateInstance(_type) as IProjectileHitPolicy;
            }
        }

        private sealed class InfinitePierceHitPolicy : IProjectileHitPolicy
        {
            public bool ShouldExitOnHit(in ProjectileHitEvent hit, ref int hitsRemaining)
            {
                return false;
            }
        }
    }
}
