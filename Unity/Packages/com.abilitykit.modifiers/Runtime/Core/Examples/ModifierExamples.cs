using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AbilityKit.Modifiers.Examples
{
    // ============================================================================
    // 标签修改器示例
    // ============================================================================

    /// <summary>
    /// 标签修改器目标接口。
    /// 实现此接口的对象可以接收标签修改。
    /// </summary>
    public interface ITagModifierTarget
    {
        /// <summary>添加标签</summary>
        void AddTag(string tag, int sourceId);

        /// <summary>移除标签</summary>
        void RemoveTag(string tag, int sourceId);

        /// <summary>是否拥有标签</summary>
        bool HasTag(string tag);

        /// <summary>获取所有标签（用于调试）</summary>
        IReadOnlyCollection<string> GetTags();

        /// <summary>获取标签来源映射（用于调试）</summary>
        IReadOnlyDictionary<string, int> GetTagSources();
    }

    /// <summary>
    /// 标签修改器处理器。
    /// 处理标签的添加和移除。
    ///
    /// 用法：
    /// ```csharp
    /// var target = new CharacterTags();
    /// var handler = new TagModifierHandler(target);
    ///
    /// // 添加标签
    /// var mod = ModifierData.Custom(
    ///     key: ModifierKey.Create(...),
    ///     op: ModifierOp.Custom,
    ///     customData: CustomModifierData.String("Invincible")
    /// );
    /// handler.Apply(default, mod, context);  // 添加 Invincible 标签
    ///
    /// // 移除标签
    /// var mod2 = ModifierData.Custom(
    ///     key: ModifierKey.Create(...),
    ///     op: ModifierOp.Custom + 1,  // 表示移除
    ///     customData: CustomModifierData.String("Invincible")
    /// );
    /// ```
    /// </summary>
    public struct TagModifierHandler : IModifierHandler<string>
    {
        private readonly ITagModifierTarget _target;

        public TagModifierHandler(ITagModifierTarget target)
        {
            _target = target;
        }

        /// <summary>
        /// 应用标签修改。
        /// Op.Custom 表示添加标签，Op.Custom + 1 表示移除标签。
        /// </summary>
        public string Apply(string baseValue, in ModifierData modifier, IModifierContext context)
        {
            var tag = modifier.CustomData.StringValue;
            if (string.IsNullOrEmpty(tag)) return baseValue;

            if (modifier.Op == ModifierOp.Custom)
            {
                _target?.AddTag(tag, modifier.SourceId);
            }
            else if ((int)modifier.Op == (int)ModifierOp.Custom + 1)
            {
                _target?.RemoveTag(tag, modifier.SourceId);
            }

            return baseValue;
        }

        public int Compare(string a, string b) => string.Compare(a, b, StringComparison.Ordinal);

        public string Combine(in Span<string> values) => values.Length > 0 ? values[0] : string.Empty;
    }

    // ============================================================================
    // 状态修改器示例
    // ============================================================================

    /// <summary>
    /// 状态修改器目标接口。
    /// 实现此接口的对象可以接收状态修改（保存/恢复）。
    /// </summary>
    public interface IStateModifierTarget
    {
        /// <summary>获取状态值</summary>
        object GetState(string stateKey);

        /// <summary>设置状态值</summary>
        void SetState(string stateKey, object value);

        /// <summary>保存原始状态（用于还原）</summary>
        void SaveOriginal(string stateKey, int sourceId, object originalValue);

        /// <summary>还原原始状态</summary>
        void RestoreOriginal(string stateKey, int sourceId);
    }

    /// <summary>
    /// 状态修改���结果
    /// </summary>
    public readonly struct StateModifyResult
    {
        public readonly bool Success;
        public readonly string Error;
        public readonly object OriginalValue;

        public StateModifyResult(bool success, string error, object originalValue)
        {
            Success = success;
            Error = error;
            OriginalValue = originalValue;
        }

        public static StateModifyResult Succeeded(object originalValue = null)
            => new(true, null, originalValue);

        public static StateModifyResult Failed(string error)
            => new(false, error, null);
    }

    /// <summary>
    /// 状态修改器处理器。
    /// 处理状态的保存和恢复。
    ///
    /// 用法：
    /// ```csharp
    /// var target = new CharacterState();
    /// var handler = new StateModifierHandler(target);
    ///
    /// // 设置状态（保存原始值）
    /// var mod = ModifierData.Custom(
    ///     key: ModifierKey.Create(...),
    ///     op: ModifierOp.Custom,
    ///     customData: CustomModifierData.String("MoveSpeed")
    /// );
    /// mod.Magnitude = MagnitudeStrategyData.Fixed(200f);  // 移动速度设置为 200
    /// handler.Apply(default, mod, context);
    /// ```
    /// </summary>
    public struct StateModifierHandler : IModifierHandler<object>
    {
        private readonly IStateModifierTarget _target;

        public StateModifierHandler(IStateModifierTarget target)
        {
            _target = target;
        }

        public object Apply(object baseValue, in ModifierData modifier, IModifierContext context)
        {
            var stateKey = modifier.CustomData.StringValue;
            if (string.IsNullOrEmpty(stateKey)) return baseValue;

            // 保存原始值
            var originalValue = _target?.GetState(stateKey);
            _target?.SaveOriginal(stateKey, modifier.SourceId, originalValue);

            // 设置新值
            var newValue = modifier.GetMagnitude(context?.Level ?? 1f, context);
            _target?.SetState(stateKey, newValue);

            return originalValue;
        }

        public int Compare(object a, object b)
        {
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            return a.Equals(b) ? 0 : 1;
        }

        public object Combine(in Span<object> values) => values.Length > 0 ? values[^1] : null;
    }

    // ============================================================================
    // 技能 ID 修改器示例
    // ============================================================================

    /// <summary>
    /// 技能 ID 修改器处理器。
    /// 用于强制使用特定技能。
    ///
    /// 示例：
    /// ```csharp
    /// var handler = new SkillIdModifierHandler();
    /// var mod = ModifierData.Override(
    ///     key: ModifierKey.Create(...),
    ///     value: 0,
    ///     customData: CustomModifierData.SkillId(999)  // 强制使用技能 999
    /// );
    /// int skillId = handler.Apply(default, mod, context);
    /// ```
    /// </summary>
    public struct SkillIdModifierHandler : IModifierHandler<int>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Apply(int baseValue, in ModifierData modifier, IModifierContext context)
        {
            if (modifier.Op == ModifierOp.Override && modifier.CustomData.CustomTypeId == 1)
            {
                return modifier.CustomData.IntValue;
            }
            return baseValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(int a, int b) => a.CompareTo(b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Combine(in Span<int> values) => values.Length > 0 ? values[0] : 0;
    }

    // ============================================================================
    // 预制体修改器示例
    // ============================================================================

    /// <summary>
    /// 预制体修改器处理器。
    /// 用于替换技能使用的预制体。
    ///
    /// 示例：
    /// ```csharp
    /// var handler = new PrefabModifierHandler();
    /// var mod = ModifierData.Custom(
    ///     key: ModifierKey.Create(...),
    ///     op: ModifierOp.Override,
    ///     customData: CustomModifierData.String("Assets/Prefabs/CustomBullet.prefab")
    /// );
    /// string prefabPath = handler.Apply(default, mod, context);
    /// ```
    /// </summary>
    public struct PrefabModifierHandler : IModifierHandler<string>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Apply(string baseValue, in ModifierData modifier, IModifierContext context)
        {
            if (modifier.Op == ModifierOp.Override)
            {
                var path = modifier.CustomData.StringValue;
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }
            return baseValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(string a, string b) => string.Compare(a, b, StringComparison.Ordinal);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Combine(in Span<string> values) => values.Length > 0 ? values[0] : string.Empty;
    }
}
