using System.Collections.Generic;

namespace AbilityKit.BattleFlow
{
    /// <summary>战斗流程文档（.battleflow）：caseId + 积木树。编辑器保存/加载、批量运行都经它。</summary>
    public sealed class BattleFlowDocument
    {
        public string CaseId { get; set; } = string.Empty;

        public List<BattleBlock> Blocks { get; set; } = new List<BattleBlock>();
    }
}
