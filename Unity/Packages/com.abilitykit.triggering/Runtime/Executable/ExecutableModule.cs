using System;

namespace AbilityKit.Triggering.Runtime.Executable
{
    /// <summary>
    /// 行为类型 ID 常量
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
    /// 条件类型 ID 常量
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
    /// Executable 模块入口
    /// </summary>
    public static class ExecutableModule
    {
        public static void Initialize(
            Registry.FunctionRegistry functions,
            Registry.ActionRegistry actions)
        {
        }
    }
}
