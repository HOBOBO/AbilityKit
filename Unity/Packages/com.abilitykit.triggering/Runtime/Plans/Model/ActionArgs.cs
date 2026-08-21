using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Runtime.Plan
{
    public enum ActionArgKind
    {
        NumericValue = 0,
        BlackboardTarget = 1,
        BooleanValue = 2,
        StringValue = 3
    }

    public readonly struct BlackboardWriteTarget : IEquatable<BlackboardWriteTarget>
    {
        public readonly int BoardId;
        public readonly int KeyId;
        public readonly BlackboardKeyType KeyType;
        public readonly string Scope;

        public BlackboardWriteTarget(int boardId, int keyId, BlackboardKeyType keyType, string scope)
        {
            BoardId = boardId;
            KeyId = keyId;
            KeyType = keyType;
            Scope = scope ?? string.Empty;
        }

        public bool Equals(BlackboardWriteTarget other) =>
            BoardId == other.BoardId && KeyId == other.KeyId && KeyType == other.KeyType && Scope == other.Scope;

        public override bool Equals(object obj) => obj is BlackboardWriteTarget other && Equals(other);
        public override int GetHashCode() => unchecked(
            (((((BoardId * 397) ^ KeyId) * 397) ^ (int)KeyType) * 397) ^
            (Scope != null ? Scope.GetHashCode() : 0));
        public override string ToString() => $"board={BoardId}, key={KeyId}, type={KeyType}, scope={Scope}";
    }

    /// <summary>
    /// Action 调用中的单个具名参数值
    /// </summary>
    public readonly struct ActionArgValue : IEquatable<ActionArgValue>
    {
        public readonly ActionArgKind Kind;

        /// <summary>
        /// 参数的值引用（常量、黑板、Payload 等）
        /// </summary>
        public readonly NumericValueRef Ref;

        public readonly BlackboardWriteTarget BlackboardTarget;
        public readonly bool BooleanValue;
        public readonly string StringValue;

        /// <summary>
        /// 参数名（用于调试信息和 Schema 匹配）
        /// </summary>
        public readonly string Name;

        public ActionArgValue(NumericValueRef @ref, string name)
        {
            Kind = ActionArgKind.NumericValue;
            Ref = @ref;
            BlackboardTarget = default;
            BooleanValue = false;
            StringValue = null;
            Name = name ?? string.Empty;
        }

        private ActionArgValue(in BlackboardWriteTarget target, string name)
        {
            Kind = ActionArgKind.BlackboardTarget;
            Ref = default;
            BlackboardTarget = target;
            BooleanValue = false;
            StringValue = null;
            Name = name ?? string.Empty;
        }

        private ActionArgValue(bool value, string name)
        {
            Kind = ActionArgKind.BooleanValue;
            Ref = default;
            BlackboardTarget = default;
            BooleanValue = value;
            StringValue = null;
            Name = name ?? string.Empty;
        }

        private ActionArgValue(string value, string name)
        {
            Kind = ActionArgKind.StringValue;
            Ref = default;
            BlackboardTarget = default;
            BooleanValue = false;
            StringValue = value ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public static ActionArgValue Of(NumericValueRef @ref, string name)
            => new ActionArgValue(@ref, name);

        public static ActionArgValue OfConst(double value, string name)
            => new ActionArgValue(NumericValueRef.Const(value), name);

        public static ActionArgValue OfBlackboardTarget(in BlackboardWriteTarget target, string name)
            => new ActionArgValue(in target, name);

        public static ActionArgValue OfBool(bool value, string name)
            => new ActionArgValue(value, name);

        public static ActionArgValue OfString(string value, string name)
            => new ActionArgValue(value, name);

        public bool Equals(ActionArgValue other) =>
            Kind == other.Kind && Ref.Equals(other.Ref) && BlackboardTarget.Equals(other.BlackboardTarget) &&
            BooleanValue == other.BooleanValue && StringValue == other.StringValue && Name == other.Name;
        public override bool Equals(object obj) => obj is ActionArgValue other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)Kind;
                hash = (hash * 397) ^ Ref.GetHashCode();
                hash = (hash * 397) ^ BlackboardTarget.GetHashCode();
                hash = (hash * 397) ^ (BooleanValue ? 1 : 0);
                hash = (hash * 397) ^ (StringValue != null ? StringValue.GetHashCode() : 0);
                return (hash * 397) ^ (Name != null ? Name.GetHashCode() : 0);
            }
        }
        public override string ToString() => Kind == ActionArgKind.BlackboardTarget
            ? $"[{Name}]=>{BlackboardTarget}"
            : Kind == ActionArgKind.BooleanValue
                ? $"[{Name}]={BooleanValue}"
                : Kind == ActionArgKind.StringValue
                    ? $"[{Name}]='{StringValue}'"
                    : $"[{Name}]={Ref}";
    }

    /// <summary>
    /// Action 参数 Schema 接口
    /// 每个 Action 模块实现此接口，定义自己的参数结构体和解析逻辑
    /// TActionArgs: 该 Action 的强类型参数结构体
    /// TCtx: 执行上下文类型
    /// </summary>
    public interface IActionSchema<TActionArgs, TCtx>
    {
        /// <summary>
        /// 该 Schema 对应的 Action ID
        /// </summary>
        ActionId ActionId { get; }

        /// <summary>
        /// 参数结构体的运行时类型
        /// </summary>
        Type ArgsType { get; }

        /// <summary>
        /// 将具名参数字典解析为强类型结构体对象
        /// </summary>
        /// <param name="namedArgs">参数字典，key 为参数名（如 "damage_value"）</param>
        /// <param name="ctx">执行上下文，用于解析变量引用</param>
        /// <returns>强类型参数结构体实例</returns>
        TActionArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<TCtx> ctx);

        /// <summary>
        /// 验证参数是否合法（必填项、类型范围等）
        /// </summary>
        /// <param name="args">参数字典</param>
        /// <param name="error">错误信息（验证失败时输出）</param>
        /// <returns>验证是否通过</returns>
        bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error);
    }

    public readonly struct TriggerActionParseContext
    {
        public TriggerActionParseContext(object triggerArgs)
        {
            TriggerArgs = triggerArgs;
        }

        public object TriggerArgs { get; }
        public bool HasTriggerArgs => TriggerArgs != null;
    }

    public interface ITriggerActionParseContextAwareSchema<TActionArgs, TCtx> : IActionSchema<TActionArgs, TCtx>
    {
        TActionArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<TCtx> ctx, in TriggerActionParseContext parseContext);
    }

    public interface ITriggerArgsAwareActionSchema<TActionArgs, TCtx> : IActionSchema<TActionArgs, TCtx>
    {
        TActionArgs ParseArgs(Dictionary<string, ActionArgValue> namedArgs, ExecCtx<TCtx> ctx, object triggerArgs);
    }

    /// <summary>
    /// 非泛型 IActionSchema 基接口（用于注册表存储）
    /// </summary>
    public interface IActionSchema
    {
        ActionId ActionId { get; }
        Type ArgsType { get; }
        object ParseArgs(Dictionary<string, ActionArgValue> namedArgs, object ctx);
        bool TryValidateArgs(ReadOnlySpan<KeyValuePair<string, ActionArgValue>> args, out string error);
    }

    /// <summary>
    /// Plan Action 模块注册接口
    /// 所有 PlanAction Module（无论位置参数版还是具名参数版）都需要实现此接口
    /// </summary>
    public interface IPlanActionModule
    {
        void Register(ActionRegistry actions, IWorldResolver services);
    }
}
