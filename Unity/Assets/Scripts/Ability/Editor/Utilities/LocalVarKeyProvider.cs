using System;
using System.Collections.Generic;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering;

namespace AbilityKit.Ability.Editor
{
    internal sealed class LocalVarKeyProvider : IVarKeyProvider
    {
        public int Order => 0;

        public bool CanProvide(in VarKeyQuery query)
        {
            if (!query.IncludeLocal) return false;
            if (query.Scope.HasValue && query.Scope.Value != VarScope.Local) return false;
            return true;
        }

        public void CollectKeys(in VarKeyQuery query, List<string> output)
        {
            if (output == null) return;

            var expected = query.ExpectedKind;
            var filterByKind = expected != ArgValueKind.None;
            var assign = query.Usage == VarKeyUsage.Assign;

            var currentTrigger = AbilityEditorVarKeyContext.CurrentTrigger;
            if (currentTrigger != null && currentTrigger.LocalVars != null)
            {
                for (int i = 0; i < currentTrigger.LocalVars.Count; i++)
                {
                    var e = currentTrigger.LocalVars[i];
                    if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                    if (assign && e.ReadOnly) continue;
                    if (filterByKind && e.Kind != expected) continue;
                    output.Add(e.Key);
                }

                return;
            }

            var skill = AbilityEditorVarKeyContext.CurrentModule;
            var triggers = skill != null ? skill.Triggers : null;
            if (triggers == null) return;

            for (int i = 0; i < triggers.Count; i++)
            {
                var t = triggers[i];
                if (t == null || t.LocalVars == null) continue;

                for (int j = 0; j < t.LocalVars.Count; j++)
                {
                    var e = t.LocalVars[j];
                    if (e == null || string.IsNullOrEmpty(e.Key)) continue;
                    if (assign && e.ReadOnly) continue;
                    if (filterByKind && e.Kind != expected) continue;
                    output.Add(e.Key);
                }
            }
        }
    }
}
