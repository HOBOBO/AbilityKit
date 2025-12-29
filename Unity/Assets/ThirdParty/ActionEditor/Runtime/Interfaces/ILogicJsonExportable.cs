using System.Collections.Generic;

namespace NBC.ActionEditor
{
    public interface ILogicJsonExportable
    {
        void FillLogicArgs(Dictionary<string, string> args);
    }
}
