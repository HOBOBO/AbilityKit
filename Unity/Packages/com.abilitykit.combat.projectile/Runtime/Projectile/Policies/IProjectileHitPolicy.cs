using System;

namespace AbilityKit.Combat.Projectile
{
    public interface IProjectileHitPolicy
    {
        bool ShouldExitOnHit(in ProjectileHitEvent hit, ref int hitsRemaining);
    }

    /// <summary>
    /// 标记一个 <see cref="IProjectileHitPolicy"/> 实现为可注册的命中策略扩展点。
    /// 与 targeting 的 <c>TargetRuleAttribute</c> / PlanAction 的 <c>PlanActionModuleAttribute</c>
    /// 保持同一套「特性 + 注册表」约定，使命中策略可以在项目侧扩展而不必修改框架包。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProjectileHitPolicyAttribute : Attribute
    {
        public ProjectileHitPolicyAttribute(int kind)
        {
            Kind = kind;
        }

        /// <summary>命中策略种类，对应配置里的 <c>ProjectileHitPolicyKind</c> 值。</summary>
        public int Kind { get; }
    }

}
