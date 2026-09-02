using System.Collections.Generic;
using AbilityKit.Scenario;

namespace AbilityKit.BattleFlow
{
    /// <summary>把积木树编译成玩法中立的 <see cref="TestScenario"/>（线性：按序编译，复合积木展平子积木）。</summary>
    public static class BattleFlowCompiler
    {
        /// <summary>按序编译一组积木（复合积木递归展平）成 <see cref="TestScenario"/>。</summary>
        public static TestScenario Compile(string caseId, IReadOnlyList<BattleBlock> blocks)
        {
            var builder = new BattleFlowBuilder { CaseId = caseId };
            foreach (var block in blocks) CompileBlock(block, builder);
            return builder.Build();
        }

        private static void CompileBlock(BattleBlock? block, BattleFlowBuilder builder)
        {
            switch (block)
            {
                case null:
                    break;
                case BattleAtomicBlock atomic:
                    atomic.Compile(builder);
                    break;
                case BattleCompositeBlock composite:
                    foreach (var child in composite.Children) CompileBlock(child, builder);
                    break;
            }
        }
    }
}
