#if UNITY_EDITOR
using AbilityKit.BattleFlow;
using UnityEditor;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>把 MOBA 断言积木注册进战斗流程编辑器调色板。</summary>
    [InitializeOnLoad]
    public static class MobaBattleFlowBlocks
    {
        static MobaBattleFlowBlocks()
        {
            BattleBlockPalette.Register(new AssertTraceBlock { Id = "assert-trace", DisplayName = "断言·trace" });
            BattleBlockPalette.Register(new AssertNoTraceBlock { Id = "assert-no-trace", DisplayName = "断言·禁trace" });
            BattleBlockPalette.Register(new AssertStateBlock { Id = "assert-state", DisplayName = "断言·状态" });
            BattleBlockPalette.Register(new AssertContextBlock { Id = "assert-context", DisplayName = "断言·上下文" });
            BattleBlockPalette.Register(new AssertRelationshipBlock { Id = "assert-relationship", DisplayName = "断言·因果" });
        }
    }
}
#endif
