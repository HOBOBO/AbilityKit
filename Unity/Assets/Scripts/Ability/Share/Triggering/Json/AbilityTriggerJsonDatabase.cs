using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Triggering.Definitions;
using Newtonsoft.Json;
using UnityEngine;

namespace AbilityKit.Ability.Triggering.Json
{
    public sealed class AbilityTriggerJsonDatabase
    {
        private readonly List<TriggerDTO> _flatTriggers = new List<TriggerDTO>();

        public readonly struct TriggerRecord
        {
            public readonly int TriggerId;
            public readonly string EventId;
            public readonly TriggerDef Def;
            public readonly IReadOnlyDictionary<string, object> InitialLocalVars;

            public TriggerRecord(int triggerId, string eventId, TriggerDef def, IReadOnlyDictionary<string, object> initialLocalVars)
            {
                TriggerId = triggerId;
                EventId = eventId;
                Def = def;
                InitialLocalVars = initialLocalVars;
            }
        }

        public IEnumerable<TriggerRecord> EnumerateAll()
        {
            if (_flatTriggers != null && _flatTriggers.Count > 0)
            {
                for (int i = 0; i < _flatTriggers.Count; i++)
                {
                    var t = _flatTriggers[i];
                    if (t == null) continue;
                    if (t.TriggerId <= 0) continue;

                    var eventId = t.EventId ?? string.Empty;

                    var conditions = new List<ConditionDef>(t.Conditions != null ? t.Conditions.Count : 0);
                    if (t.Conditions != null)
                    {
                        for (int c = 0; c < t.Conditions.Count; c++)
                        {
                            var cd = t.Conditions[c];
                            if (cd == null || string.IsNullOrEmpty(cd.Type)) continue;
                            var cdef = BuildConditionDef(cd);
                            if (cdef != null) conditions.Add(cdef);
                        }
                    }

                    var actions = new List<ActionDef>(t.Actions != null ? t.Actions.Count : 0);
                    if (t.Actions != null)
                    {
                        for (int a = 0; a < t.Actions.Count; a++)
                        {
                            var ad = t.Actions[a];
                            if (ad == null || string.IsNullOrEmpty(ad.Type)) continue;
                            var adef = BuildActionDef(ad);
                            if (adef != null) actions.Add(adef);
                        }
                    }

                    var def = new TriggerDef(eventId, conditions, actions);
                    var locals = t.InitialLocalVars != null ? new Dictionary<string, object>(t.InitialLocalVars, StringComparer.Ordinal) : null;
                    yield return new TriggerRecord(t.TriggerId, eventId, def, locals);
                }
            }
        }

        private static ConditionDef BuildConditionDef(ConditionDTO dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Type)) return null;

            if (string.Equals(dto.Type, "all", StringComparison.Ordinal) || string.Equals(dto.Type, "any", StringComparison.Ordinal))
            {
                if (dto.Items == null) throw new InvalidOperationException($"Condition '{dto.Type}' requires dto.Items");

                var list = new List<ConditionDef>(dto.Items.Count);
                for (int i = 0; i < dto.Items.Count; i++)
                {
                    var child = BuildConditionDef(dto.Items[i]);
                    if (child != null) list.Add(child);
                }

                var args = new Dictionary<string, object>(StringComparer.Ordinal);
                args["items"] = list;
                return new ConditionDef(dto.Type, args);
            }

            if (string.Equals(dto.Type, "not", StringComparison.Ordinal))
            {
                if (dto.Item == null) throw new InvalidOperationException("Condition 'not' requires dto.Item");

                var child = BuildConditionDef(dto.Item);
                var args = new Dictionary<string, object>(StringComparer.Ordinal);
                args["item"] = child;
                return new ConditionDef(dto.Type, args);
            }

            return new ConditionDef(dto.Type, dto.Args != null ? new Dictionary<string, object>(dto.Args, StringComparer.Ordinal) : null);
        }

        private static ActionDef BuildActionDef(ActionDTO dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Type)) return null;

            if (string.Equals(dto.Type, "seq", StringComparison.Ordinal))
            {
                if (dto.Items == null) throw new InvalidOperationException("Action 'seq' requires dto.Items");

                var list = new List<ActionDef>(dto.Items.Count);
                for (int i = 0; i < dto.Items.Count; i++)
                {
                    var child = BuildActionDef(dto.Items[i]);
                    if (child != null) list.Add(child);
                }

                var args = new Dictionary<string, object>(StringComparer.Ordinal);
                args["items"] = list;
                return new ActionDef(dto.Type, args);
            }

            return new ActionDef(dto.Type, dto.Args != null ? new Dictionary<string, object>(dto.Args, StringComparer.Ordinal) : null);
        }

        public void LoadFromResources(string resourcesPathWithoutExt)
        {
            if (string.IsNullOrEmpty(resourcesPathWithoutExt)) throw new ArgumentException(nameof(resourcesPathWithoutExt));

            _flatTriggers.Clear();

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

            if (dto == null) return;

            if (dto.Triggers != null && dto.Triggers.Count > 0)
            {
                _flatTriggers.AddRange(dto.Triggers);
            }
        }
    }
}
