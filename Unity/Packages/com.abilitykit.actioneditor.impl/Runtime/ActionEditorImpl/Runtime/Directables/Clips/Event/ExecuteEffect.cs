using System.Collections.Generic;
using NBC.ActionEditor;

namespace AbilityKit.ActionEditorImpl
{
    [Name("执行技能效果")]
    [Description("在逻辑层执行已配置的效果 ID")]
    [Color(0.86f, 0.28f, 0.19f)]
    [Attachable(typeof(SignalTrack))]
    public class ExecuteEffect : ClipSignal, IActionTimelineRuntimeClip
    {
        [MenuName("效果 ID")] public int effectId;

        [MenuName("Event Tag")] public string eventTag = "";

        public ActionTimelineRuntimeKind RuntimeKind => ActionTimelineRuntimeKind.Logic;
        public override bool IsValid => effectId > 0;
        public override string Info => "效果 " + effectId;

        public void FillLogicArgs(Dictionary<string, string> args)
        {
            if (args == null) return;
            args["effectId"] = effectId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            args["eventTag"] = eventTag ?? string.Empty;
        }
    }
}
