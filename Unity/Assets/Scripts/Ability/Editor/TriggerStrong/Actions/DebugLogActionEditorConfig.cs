using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.DebugLog, "输出日志", "行为/调试", 0)]
    public sealed class DebugLogActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.DebugLog;

        [LabelText("日志内容")]
        [TextArea]
        public string Message;

        [LabelText("输出 Args")]
        public bool DumpArgs;

        protected override string GetTitleSuffix()
        {
            return StrongEditorTitleUtil.QuoteAndTruncate(Message, 32);
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new DebugLogActionConfig
            {
                Message = Message,
                DumpArgs = DumpArgs
            };
        }
    }
}
