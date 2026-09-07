#if UNITY_EDITOR
using AbilityKit.BattleFlow;
using AbilityKit.Demo.Moba.EnvironmentModel;
using UnityEditor;

namespace AbilityKit.Demo.Moba.Editor.BattleFlow
{
    /// <summary>把 MOBA 断言积木注册进战斗流程编辑器调色板（断言积木类本身在 demo.moba.environment，纯 C# 共用）。</summary>
    [InitializeOnLoad]
    public static class MobaBattleFlowBlocks
    {
        static MobaBattleFlowBlocks()
        {
            BattleBlockPalette.Register("断言", new AssertTraceBlock { Id = "assert-trace", DisplayName = "断言·trace" });
            BattleBlockPalette.Register("断言", new AssertNoTraceBlock { Id = "assert-no-trace", DisplayName = "断言·禁trace" });
            BattleBlockPalette.Register("断言", new AssertStateBlock { Id = "assert-state", DisplayName = "断言·状态" });
            BattleBlockPalette.Register("断言", new AssertContextBlock { Id = "assert-context", DisplayName = "断言·上下文" });
            BattleBlockPalette.Register("断言", new AssertRelationshipBlock { Id = "assert-relationship", DisplayName = "断言·因果" });
        }
    }
}
#endif
