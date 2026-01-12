using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering.Definitions;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Ability.Triggering.Json
{
    public sealed class AbilityTriggerJsonDatabase
    {
        private readonly Dictionary<string, AbilityTriggerEntryDTO> _byAbilityId = new Dictionary<string, AbilityTriggerEntryDTO>(StringComparer.Ordinal);

        public void LoadFromResources(string resourcesPathWithoutExt)
        {
            if (string.IsNullOrEmpty(resourcesPathWithoutExt)) throw new ArgumentException(nameof(resourcesPathWithoutExt));

            _byAbilityId.Clear();

            var ta = Resources.Load<TextAsset>(resourcesPathWithoutExt);
            if (ta == null) throw new InvalidOperationException($"Trigger json not found in Resources: {resourcesPathWithoutExt}");

            var json = ta.text;
            if (string.IsNullOrEmpty(json)) throw new InvalidOperationException($"Trigger json is empty: {resourcesPathWithoutExt}");

            AbilityTriggerDatabaseDTO dto;
            try
            {
                dto = JsonConvert.DeserializeObject<AbilityTriggerDatabaseDTO>(json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse trigger json: {resourcesPathWithoutExt}", ex);
            }

            if (dto?.Abilities == null) return;
            for (int i = 0; i < dto.Abilities.Count; i++)
            {
                var e = dto.Abilities[i];
                if (e == null || string.IsNullOrEmpty(e.AbilityId)) continue;
                _byAbilityId[e.AbilityId] = e;
            }
        }

        public bool TryGet(string abilityId, out IReadOnlyList<TriggerDef> triggers, out IReadOnlyList<IReadOnlyDictionary<string, object>> initialLocalVars)
        {
            triggers = null;
            initialLocalVars = null;
            if (string.IsNullOrEmpty(abilityId)) return false;
            if (!_byAbilityId.TryGetValue(abilityId, out var entry) || entry == null) return false;

            if (entry.Triggers == null || entry.Triggers.Count == 0)
            {
                triggers = Array.Empty<TriggerDef>();
                initialLocalVars = Array.Empty<IReadOnlyDictionary<string, object>>();
                return true;
            }

            var list = new List<TriggerDef>(entry.Triggers.Count);
            var vars = new List<IReadOnlyDictionary<string, object>>(entry.Triggers.Count);

            for (int i = 0; i < entry.Triggers.Count; i++)
            {
                var t = entry.Triggers[i];
                if (t == null || string.IsNullOrEmpty(t.EventId)) continue;

                var conditions = new List<ConditionDef>(t.Conditions != null ? t.Conditions.Count : 0);
                if (t.Conditions != null)
                {
                    for (int c = 0; c < t.Conditions.Count; c++)
                    {
                        var cd = t.Conditions[c];
                        if (cd == null || string.IsNullOrEmpty(cd.Type)) continue;
                        conditions.Add(new ConditionDef(cd.Type, cd.Args != null ? new Dictionary<string, object>(cd.Args, StringComparer.Ordinal) : null));
                    }
                }

                var actions = new List<ActionDef>(t.Actions != null ? t.Actions.Count : 0);
                if (t.Actions != null)
                {
                    for (int a = 0; a < t.Actions.Count; a++)
                    {
                        var ad = t.Actions[a];
                        if (ad == null || string.IsNullOrEmpty(ad.Type)) continue;
                        actions.Add(new ActionDef(ad.Type, ad.Args != null ? new Dictionary<string, object>(ad.Args, StringComparer.Ordinal) : null));
                    }
                }

                list.Add(new TriggerDef(t.EventId, conditions, actions));
                vars.Add(t.InitialLocalVars != null ? new Dictionary<string, object>(t.InitialLocalVars, StringComparer.Ordinal) : null);
            }

            triggers = list;
            initialLocalVars = vars;
            return true;
        }
    }
}
