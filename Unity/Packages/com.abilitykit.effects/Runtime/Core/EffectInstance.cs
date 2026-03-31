using System;
using AbilityKit.Effects.Core.Model;

namespace AbilityKit.Effects.Core
{
    /// <summary>
    /// 效果实例
    /// 包装效果定义并附加运行时状态（作用域、过期时间等）
    /// </summary>
    [Serializable]
    public sealed class EffectInstance : IPoolable
    {
        public string InstanceId;
        public EffectDefinition Def;

        public EffectScopeKey Scope;

        public int ExpireFrame;
        public bool IsPermanent;

        #region 工厂方法

        /// <summary>
        /// 创建永久效果实例
        /// </summary>
        public static EffectInstance Create(string instanceId, EffectDefinition def, EffectScopeKey scope)
        {
            return new EffectInstance
            {
                InstanceId = instanceId,
                Def = def,
                Scope = scope,
                ExpireFrame = 0,
                IsPermanent = true
            };
        }

        /// <summary>
        /// 创建临时效果实例
        /// </summary>
        public static EffectInstance CreateTemporary(string instanceId, EffectDefinition def, EffectScopeKey scope, int expireFrame)
        {
            return new EffectInstance
            {
                InstanceId = instanceId,
                Def = def,
                Scope = scope,
                ExpireFrame = expireFrame,
                IsPermanent = false
            };
        }

        #endregion

        #region 构造函数

        public EffectInstance() { }

        public EffectInstance(string instanceId, EffectDefinition def, EffectScopeKey scope)
        {
            InstanceId = instanceId;
            Def = def;
            Scope = scope;
            ExpireFrame = 0;
            IsPermanent = true;
        }

        #endregion

        #region 状态检查

        /// <summary>
        /// 是否有效（定义不为空）
        /// </summary>
        public bool IsValid => Def != null && !string.IsNullOrEmpty(InstanceId);

        /// <summary>
        /// 是否为空定义
        /// </summary>
        public bool IsEmpty => Def == null || Def.IsEmpty;

        /// <summary>
        /// 检查在指定帧是否已过期
        /// </summary>
        public bool IsExpiredAt(int frame)
        {
            return !IsPermanent && ExpireFrame > 0 && ExpireFrame <= frame;
        }

        /// <summary>
        /// 获取剩余生命周期帧数（永久效果返回最大值）
        /// </summary>
        public int RemainingFrames(int currentFrame)
        {
            if (IsPermanent) return int.MaxValue;
            if (ExpireFrame <= currentFrame) return 0;
            return ExpireFrame - currentFrame;
        }

        /// <summary>
        /// 是否会在指定帧之后过期
        /// </summary>
        public bool WillExpireBefore(int frame) => !IsPermanent && ExpireFrame > 0 && ExpireFrame <= frame;

        #endregion

        #region 配置方法

        /// <summary>
        /// 设置过期帧
        /// </summary>
        public void SetExpireFrame(int expireFrame)
        {
            ExpireFrame = expireFrame;
            IsPermanent = false;
        }

        /// <summary>
        /// 标记为永久
        /// </summary>
        public void SetPermanent()
        {
            IsPermanent = true;
            ExpireFrame = 0;
        }

        /// <summary>
        /// 更新作用域
        /// </summary>
        public void UpdateScope(EffectScopeKey newScope)
        {
            Scope = newScope;
        }

        #endregion

        #region 便捷访问

        /// <summary>
        /// 获取效果ID
        /// </summary>
        public string EffectId => Def?.EffectId ?? string.Empty;

        /// <summary>
        /// 获取属性项数量
        /// </summary>
        public int StatsCount => Def?.StatsCount ?? 0;

        /// <summary>
        /// 获取默认作用域
        /// </summary>
        public EffectScopeKey DefaultScope => Def?.DefaultScope ?? default;

        #endregion

        #region IPoolable 实现

        /// <summary>
        /// 重置实例状态（用于对象池回收）
        /// </summary>
        public void OnPoolReset()
        {
            InstanceId = null;
            Def = null;
            Scope = default;
            ExpireFrame = 0;
            IsPermanent = true;
        }

        /// <summary>
        /// 初始化实例（从对象池获取时调用）
        /// </summary>
        public void OnPoolInitialize()
        {
            IsPermanent = true;
        }

        #endregion

        #region 复用方法

        /// <summary>
        /// 复用此实例（从对象池取出时配置）
        /// </summary>
        public void Reuse(string instanceId, EffectDefinition def, EffectScopeKey scope)
        {
            InstanceId = instanceId;
            Def = def;
            Scope = scope;
            ExpireFrame = 0;
            IsPermanent = true;
        }

        /// <summary>
        /// 复用此实例（指定过期时间）
        /// </summary>
        public void ReuseTemporary(string instanceId, EffectDefinition def, EffectScopeKey scope, int expireFrame)
        {
            InstanceId = instanceId;
            Def = def;
            Scope = scope;
            ExpireFrame = expireFrame;
            IsPermanent = false;
        }

        #endregion

        public override string ToString() => $"EffectInst({InstanceId}, Scope={Scope}, Expires={ExpireFrame}, Permanent={IsPermanent})";
    }

    /// <summary>
    /// 对象池接口
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 重置为初始状态
        /// </summary>
        void OnPoolReset();
    }
}
