using System;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// Executable 模块入口
    /// 提供类型安全的 TypeId 访问方式
    /// </summary>
    public static class ExecutableModule
    {
        /// <summary>
        /// 行为类型 ID
        /// </summary>
        public static class ExecutableTypeIds
        {
            public const int Sequence = 1;
            public const int Selector = 2;
            public const int Parallel = 3;
            public const int If = 10;
            public const int IfElse = 11;
            public const int Switch = 12;
            public const int RandomSelector = 13;
            public const int Repeat = 14;
            public const int ActionCall = 100;
            public const int Delay = 200;
            public const int Schedule = 300;
            public const int BusinessStart = 1000;
        }

        /// <summary>
        /// 条件类型 ID
        /// </summary>
        public static class ConditionTypeIds
        {
            public const int Const = 0;
            public const int And = 1;
            public const int Or = 2;
            public const int Not = 3;
            public const int NumericCompare = 10;
            public const int PayloadCompare = 11;
            public const int HasTarget = 20;
            public const int Multi = 100;
            public const int BusinessStart = 1000;
        }

        /// <summary>
        /// 初始化模块（预留）
        /// </summary>
        public static void Initialize(
            Registry.FunctionRegistry functions,
            Registry.ActionRegistry actions)
        {
        }

        /// <summary>
        /// 获取所有已注册的 Executable 类型信息
        /// </summary>
        public static (int TypeId, string TypeName)[] GetAllExecutableTypes()
        {
            return new[]
            {
                (TypeIdRegistry.Executable.Sequence, "Sequence"),
                (TypeIdRegistry.Executable.Selector, "Selector"),
                (TypeIdRegistry.Executable.Parallel, "Parallel"),
                (TypeIdRegistry.Executable.If, "If"),
                (TypeIdRegistry.Executable.IfElse, "IfElse"),
                (TypeIdRegistry.Executable.Switch, "Switch"),
                (TypeIdRegistry.Executable.RandomSelector, "RandomSelector"),
                (TypeIdRegistry.Executable.Repeat, "Repeat"),
                (TypeIdRegistry.Executable.Until, "Until"),
                (TypeIdRegistry.Executable.ActionCall, "ActionCall"),
                (TypeIdRegistry.Executable.Delay, "Delay"),
            };
        }

        /// <summary>
        /// 获取所有已注册的 Condition 类型信息
        /// </summary>
        public static (int TypeId, string TypeName)[] GetAllConditionTypes()
        {
            return new[]
            {
                (TypeIdRegistry.Condition.Const, "Const"),
                (TypeIdRegistry.Condition.And, "And"),
                (TypeIdRegistry.Condition.Or, "Or"),
                (TypeIdRegistry.Condition.Not, "Not"),
                (TypeIdRegistry.Condition.NumericCompare, "NumericCompare"),
                (TypeIdRegistry.Condition.PayloadCompare, "PayloadCompare"),
                (TypeIdRegistry.Condition.HasTarget, "HasTarget"),
                (TypeIdRegistry.Condition.Multi, "Multi"),
            };
        }
    }
}
