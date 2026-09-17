using System;
using NBC.ActionEditor;

namespace AbilityKit.ActionEditorImpl
{
    [Name("角色技能")]
    [Serializable]
    public class SkillAsset : Asset, IActionTimelineRuntimeAsset
    {
        [MenuName("导出 MOBA 运行数据")] public bool exportMobaRuntime;

        public bool ExportMobaRuntime => exportMobaRuntime;
    }
}
