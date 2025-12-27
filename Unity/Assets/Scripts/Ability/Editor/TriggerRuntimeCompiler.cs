using System;
using System.Collections.Generic;
using AbilityKit.Configs;
using AbilityKit.Triggering.Definitions;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Ability.Editor
{
    internal static class TriggerRuntimeCompiler
    {
        public static TriggerRuntimeConfig Compile(TriggerEditorConfig editor)
        {
            var rt = new TriggerRuntimeConfig();
            if (editor == null) return rt;

            rt.EventId = editor.EventId;

            if (editor.LocalVars != null && editor.LocalVars.Count > 0)
            {
                rt.LocalVars = new List<ArgRuntimeEntry>(editor.LocalVars.Count);
                for (int i = 0; i < editor.LocalVars.Count; i++)
                {
                    var e = editor.LocalVars[i];
                    if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                    rt.LocalVars.Add(e.ToArgRuntimeEntry());
                }
            }

            if (editor.ConditionsStrong != null && editor.ConditionsStrong.Count > 0)
            {
                rt.ConditionsStrong = new List<ConditionRuntimeConfigBase>(editor.ConditionsStrong.Count);
                for (int i = 0; i < editor.ConditionsStrong.Count; i++)
                {
                    var c = editor.ConditionsStrong[i];
                    if (c == null) continue;
                    rt.ConditionsStrong.Add(c.ToRuntimeStrong());
                }
            }

            if (editor.ActionsStrong != null && editor.ActionsStrong.Count > 0)
            {
                rt.ActionsStrong = new List<ActionRuntimeConfigBase>(editor.ActionsStrong.Count);
                for (int i = 0; i < editor.ActionsStrong.Count; i++)
                {
                    var a = editor.ActionsStrong[i];
                    if (a == null) continue;
                    rt.ActionsStrong.Add(a.ToRuntimeStrong());
                }
            }

            return rt;
        }
    }
}
