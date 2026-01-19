using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.LogAttacker, "输出攻击者名字", "行为/调试", 10)]
    public sealed class LogAttackerNameActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.LogAttacker;

        [LabelText("格式")]
        public string Format = "{0}发动了攻击";

        protected override string GetTitleSuffix()
        {
            return StrongEditorTitleUtil.QuoteAndTruncate(Format, 32);
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new LogAttackerNameActionConfig
            {
                Format = Format
            };
        }
    }
}
