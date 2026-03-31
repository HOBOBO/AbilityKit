using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AbilityKit.Effects.Core
{
    /// <summary>
    /// 效果实例注册中心
    /// 提供高效的效果实例管理和查询功能
    /// </summary>
    public sealed class EffectRegistry
    {
        private readonly Dictionary<EffectScopeKey, List<EffectInstance>> _instancesByScope = new();
        private static readonly List<EffectInstance> EmptyList = new List<EffectInstance>(0);

        /// <summary>
        /// 注册效果实例
        /// </summary>
        public void Register(EffectInstance instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            if (!_instancesByScope.TryGetValue(instance.Scope, out var list))
            {
                list = new List<EffectInstance>();
                _instancesByScope.Add(instance.Scope, list);
            }

            list.Add(instance);
        }

        /// <summary>
        /// 注销效果实例
        /// </summary>
        public bool Unregister(EffectInstance instance)
        {
            if (instance == null) return false;

            if (!_instancesByScope.TryGetValue(instance.Scope, out var list)) return false;

            return list.Remove(instance);
        }

        /// <summary>
        /// 获取指定作用域的效果实例列表（只读）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IReadOnlyList<EffectInstance> GetInstances(in EffectScopeKey scope)
        {
            if (_instancesByScope.TryGetValue(scope, out var list))
            {
                return list;
            }

            return EmptyList;
        }

        /// <summary>
        /// 获取指定作用域的效果实例数量
        /// </summary>
        public int GetInstanceCount(in EffectScopeKey scope)
        {
            if (_instancesByScope.TryGetValue(scope, out var list))
            {
                return list.Count;
            }
            return 0;
        }

        /// <summary>
        /// 检查指定作用域是否包含任何效果实例
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasInstances(in EffectScopeKey scope)
        {
            return _instancesByScope.TryGetValue(scope, out var list) && list.Count > 0;
        }

        /// <summary>
        /// 获取指定作用域的有效（非过期永久）效果实例数量
        /// </summary>
        public int GetActiveInstanceCount(in EffectScopeKey scope, int currentFrame)
        {
            if (!_instancesByScope.TryGetValue(scope, out var list)) return 0;

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var inst = list[i];
                if (inst.IsPermanent || inst.ExpireFrame > currentFrame)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 获取所有已注册的作用域
        /// </summary>
        public IEnumerable<EffectScopeKey> GetAllScopes()
        {
            return _instancesByScope.Keys;
        }

        /// <summary>
        /// 获取所有已注册的作用域数量
        /// </summary>
        public int ScopeCount => _instancesByScope.Count;

        /// <summary>
        /// 获取总实例数量
        /// </summary>
        public int TotalInstanceCount
        {
            get
            {
                int count = 0;
                foreach (var list in _instancesByScope.Values)
                {
                    count += list.Count;
                }
                return count;
            }
        }

        /// <summary>
        /// 清理所有过期实例
        /// </summary>
        /// <returns>清理的实例数量</returns>
        public int CleanupExpired(int currentFrame)
        {
            int removed = 0;
            var toRemove = new List<EffectInstance>();

            foreach (var kvp in _instancesByScope)
            {
                var list = kvp.Value;
                toRemove.Clear();

                for (int i = 0; i < list.Count; i++)
                {
                    var inst = list[i];
                    if (!inst.IsPermanent && inst.ExpireFrame <= currentFrame)
                    {
                        toRemove.Add(inst);
                    }
                }

                foreach (var inst in toRemove)
                {
                    list.Remove(inst);
                    removed++;
                }
            }

            return removed;
        }

        /// <summary>
        /// 移除指定作用域下的所有实例
        /// </summary>
        public void ClearScope(in EffectScopeKey scope)
        {
            _instancesByScope.Remove(scope);
        }

        /// <summary>
        /// 清空所有注册
        /// </summary>
        public void ClearAll()
        {
            _instancesByScope.Clear();
        }

        /// <summary>
        /// 遍历所有实例（用于调试）
        /// </summary>
        public void ForEach(Action<EffectInstance> action)
        {
            foreach (var list in _instancesByScope.Values)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    action(list[i]);
                }
            }
        }

        /// <summary>
        /// 尝试获取指定作用域的实例列表用于直接遍历
        /// </summary>
        public bool TryGetScopeList(in EffectScopeKey scope, out List<EffectInstance> list)
        {
            return _instancesByScope.TryGetValue(scope, out list);
        }
    }
}
