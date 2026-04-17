using System;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Dsl
{
    /// <summary>
    /// NumericValueRef 的流畅 API 扩展
    /// 提供更易读的数值引用构建方式
    /// </summary>
    public static class NumericValueRefDsl
    {
        /// <summary>
        /// 创建一个常量值引用
        /// </summary>
        public static NumericValueRef Const(double value)
        {
            return NumericValueRef.Const(value);
        }

        /// <summary>
        /// 创建一个常量值引用（int 重载）
        /// </summary>
        public static NumericValueRef Const(int value)
        {
            return NumericValueRef.Const((double)value);
        }

        /// <summary>
        /// 创建一个 Payload 字段引用
        /// </summary>
        public static NumericValueRef Payload(string fieldName)
        {
            var fieldId = StableStringId.Get("payload:" + fieldName);
            return NumericValueRef.PayloadField(fieldId);
        }

        /// <summary>
        /// 创建一个 Payload 字段引用（使用已解析的 fieldId）
        /// </summary>
        public static NumericValueRef Payload(int fieldId)
        {
            return NumericValueRef.PayloadField(fieldId);
        }

        /// <summary>
        /// 创建一个黑板变量引用（使用字符串格式 "board:key"）
        /// </summary>
        public static NumericValueRef Blackboard(string boardKey)
        {
            if (string.IsNullOrEmpty(boardKey))
                throw new ArgumentException("boardKey cannot be null or empty", nameof(boardKey));

            var idx = boardKey.IndexOf(':');
            if (idx <= 0 || idx >= boardKey.Length - 1)
                throw new ArgumentException($"boardKey must be in format 'board:key', got: {boardKey}", nameof(boardKey));

            var boardName = boardKey.Substring(0, idx);
            var keyName = boardKey.Substring(idx + 1);
            var boardId = StableStringId.Get("bb:" + boardName);
            var keyId = StableStringId.Get("bb:" + boardKey);

            return NumericValueRef.Blackboard(boardId, keyId);
        }

        /// <summary>
        /// 创建一个黑板变量引用（使用 boardId 和 keyId）
        /// </summary>
        public static NumericValueRef Blackboard(int boardId, int keyId)
        {
            return NumericValueRef.Blackboard(boardId, keyId);
        }

        /// <summary>
        /// 创建一个域变量引用
        /// </summary>
        public static NumericValueRef Var(string domain, string key)
        {
            return NumericValueRef.Var(domain, key);
        }

        /// <summary>
        /// 创建一个表达式引用
        /// </summary>
        public static NumericValueRef Expr(string expression)
        {
            return NumericValueRef.Expr(expression);
        }

        /// <summary>
        /// 常见常量值
        /// </summary>
        public static class Values
        {
            public static NumericValueRef Zero => NumericValueRef.Const(0);
            public static NumericValueRef One => NumericValueRef.Const(1);
            public static NumericValueRef Two => NumericValueRef.Const(2);
            public static NumericValueRef Ten => NumericValueRef.Const(10);
            public static NumericValueRef Hundred => NumericValueRef.Const(100);

            public static NumericValueRef True => NumericValueRef.Const(1);
            public static NumericValueRef False => NumericValueRef.Const(0);
        }
    }

    /// <summary>
    /// StableStringId 的辅助扩展
    /// </summary>
    public static class StableStringIdDsl
    {
        /// <summary>
        /// 创建一个事件 ID
        /// </summary>
        public static int Event(string name)
        {
            return StableStringId.Get("event:" + name);
        }

        /// <summary>
        /// 创建一个 Payload 字段 ID
        /// </summary>
        public static int Payload(string name)
        {
            return StableStringId.Get("payload:" + name);
        }

        /// <summary>
        /// 创建一个黑板域 ID
        /// </summary>
        public static int Blackboard(string name)
        {
            return StableStringId.Get("bb:" + name);
        }

        /// <summary>
        /// 创建一个黑板键 ID
        /// </summary>
        public static int BlackboardKey(string boardKey)
        {
            return StableStringId.Get("bb:" + boardKey);
        }

        /// <summary>
        /// 创建一个函数 ID
        /// </summary>
        public static int Function(string name)
        {
            return StableStringId.Get("function:" + name);
        }

        /// <summary>
        /// 创建一个动作 ID
        /// </summary>
        public static int Action(string name)
        {
            return StableStringId.Get("action:" + name);
        }
    }
}
