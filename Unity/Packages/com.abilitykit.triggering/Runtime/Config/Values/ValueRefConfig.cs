using System;

namespace AbilityKit.Triggering.Runtime.Config.Values
{
    /// <summary>
    /// 数值引用配置实现（静态配置数据）
    /// </summary>
    [Serializable]
    public struct ValueRefConfig : IValueRefConfig
    {
        public EValueRefKind Kind { get; set; }
        public double ConstValue { get; set; }
        public int BlackboardId { get; set; }
        public string BlackboardKey { get; set; }
        public int PayloadFieldId { get; set; }
        public string DomainId { get; set; }
        public string ExprText { get; set; }

        public static ValueRefConfig Const(double value) => new ValueRefConfig
        {
            Kind = EValueRefKind.Const,
            ConstValue = value
        };

        public static ValueRefConfig Blackboard(int boardId, string key) => new ValueRefConfig
        {
            Kind = EValueRefKind.Blackboard,
            BlackboardId = boardId,
            BlackboardKey = key
        };

        public static ValueRefConfig PayloadField(int fieldId) => new ValueRefConfig
        {
            Kind = EValueRefKind.PayloadField,
            PayloadFieldId = fieldId
        };

        public static ValueRefConfig Var(string domainId, string key) => new ValueRefConfig
        {
            Kind = EValueRefKind.Var,
            DomainId = domainId,
            BlackboardKey = key
        };

        public static ValueRefConfig Expr(string expr) => new ValueRefConfig
        {
            Kind = EValueRefKind.Expr,
            ExprText = expr
        };

        public static ValueRefConfig ContextField(int fieldIndex) => new ValueRefConfig
        {
            Kind = EValueRefKind.ContextField,
            PayloadFieldId = fieldIndex
        };

        public bool IsEmpty => Kind == EValueRefKind.Const && ConstValue == 0 && string.IsNullOrEmpty(ExprText);
    }
}