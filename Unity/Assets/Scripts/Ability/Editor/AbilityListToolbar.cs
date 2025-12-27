using Sirenix.Utilities.Editor;
using UnityEditor;

namespace AbilityKit.Ability.Editor
{
    internal static class AbilityListToolbar
    {
        public static bool Draw(
            AbilityListWindow window,
            AbilityModuleSO selected,
            bool autoExportSelected,
            out bool newAutoExportSelected
        )
        {
            SirenixEditorGUI.BeginHorizontalToolbar();

            var cmds = AbilityListToolbarCommandRegistry.GetAll();
            for (int i = 0; i < cmds.Count; i++)
            {
                var cmd = cmds[i];
                if (cmd == null) continue;
                if (!cmd.IsVisible(window, selected)) continue;

                EditorGUI.BeginDisabledGroup(!cmd.IsEnabled(window, selected));
                if (SirenixEditorGUI.ToolbarButton(cmd.Label))
                {
                    cmd.Execute(window, selected);
                }
                EditorGUI.EndDisabledGroup();
            }

            newAutoExportSelected = SirenixEditorGUI.ToolbarToggle(autoExportSelected, "Auto Export Selected");

            SirenixEditorGUI.EndHorizontalToolbar();

            return true;
        }
    }
}
