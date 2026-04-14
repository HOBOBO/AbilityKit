namespace AbilityKit.Triggering.Runtime.Config.Values
{
    /// <summary>
    /// 数值引用配置（静态配置数据，支持网络传输）
    /// 描述如何获取一个数值，而不是数值本身
    /// </summary>
    public interface IValueRefConfig
    {
        EValueRefKind Kind { get; }
        double ConstValue { get; }
        int BlackboardId { get; }
        string BlackboardKey { get; }
        int PayloadFieldId { get; }
        string DomainId { get; }
        string ExprText { get; }
        bool IsEmpty { get; }
    }

    public enum EValueRefKind
    {
        /// <summary>常量值</summary>
        Const,
        /// <summary>黑板变量引用</summary>
        Blackboard,
        /// <summary>事件载荷字段引用</summary>
        PayloadField,
        /// <summary>域变量引用</summary>
        Var,
        /// <summary>表达式</summary>
        Expr,
    }
}