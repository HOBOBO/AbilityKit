using System.Collections.Generic;

namespace NBC.ActionEditor
{
    public enum ActionTimelineRuntimeKind
    {
        Logic = 1,
        Presentation = 2
    }

    public interface IActionTimelineRuntimeClip : ILogicJsonExportable
    {
        ActionTimelineRuntimeKind RuntimeKind { get; }
    }

    public interface IActionTimelineRuntimeAsset
    {
        bool ExportMobaRuntime { get; }
    }

    public interface ILogicJsonExportable
    {
        void FillLogicArgs(Dictionary<string, string> args);
    }
}
