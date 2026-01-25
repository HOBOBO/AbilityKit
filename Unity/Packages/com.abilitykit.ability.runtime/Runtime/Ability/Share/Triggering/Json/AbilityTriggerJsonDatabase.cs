using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.HotReload;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Triggering.Json
{
    public sealed class AbilityTriggerJsonDatabase
    {
        private const string ConfigKey = "ability.trigger";

        private List<TriggerDTO> _flatTriggers = new List<TriggerDTO>();
        private HashSet<int> _triggerIdSnapshot = new HashSet<int>();
        private long _version;

        public long Version => _version;

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

                    var def = new TriggerDef(eventId, conditions, actions, allowExternal: t.AllowExternal);
                    var locals = t.InitialLocalVars != null ? new Dictionary<string, object>(t.InitialLocalVars, StringComparer.Ordinal) : null;
                    yield return new TriggerRecord(t.TriggerId, eventId, def, locals);
                }
            }
        }

        private static ConditionDef BuildConditionDef(ConditionDTO dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Type)) return null;

            if (string.Equals(dto.Type, TriggerConditionTypes.All, StringComparison.Ordinal) || string.Equals(dto.Type, TriggerConditionTypes.Any, StringComparison.Ordinal))
            {
                if (dto.Items == null) throw new InvalidOperationException($"Condition '{dto.Type}' requires dto.Items");

                var list = new List<ConditionDef>(dto.Items.Count);
                for (int i = 0; i < dto.Items.Count; i++)
                {
                    var child = BuildConditionDef(dto.Items[i]);
                    if (child != null) list.Add(child);
                }

                var args = new Dictionary<string, object>(StringComparer.Ordinal);
                args[TriggerDefArgKeys.Items] = list;
                return new ConditionDef(dto.Type, args);
            }

            if (string.Equals(dto.Type, TriggerConditionTypes.Not, StringComparison.Ordinal))
            {
                if (dto.Item == null) throw new InvalidOperationException("Condition 'not' requires dto.Item");

                var child = BuildConditionDef(dto.Item);
                var args = new Dictionary<string, object>(StringComparer.Ordinal);
                args[TriggerDefArgKeys.Item] = child;
                return new ConditionDef(dto.Type, args);
            }

            return new ConditionDef(dto.Type, dto.Args != null ? new Dictionary<string, object>(dto.Args, StringComparer.Ordinal) : null);
        }

        private static ActionDef BuildActionDef(ActionDTO dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Type)) return null;

            if (string.Equals(dto.Type, TriggerActionTypes.Seq, StringComparison.Ordinal))
            {
                if (dto.Items == null) throw new InvalidOperationException("Action 'seq' requires dto.Items");

                var list = new List<ActionDef>(dto.Items.Count);
                for (int i = 0; i < dto.Items.Count; i++)
                {
                    var child = BuildActionDef(dto.Items[i]);
                    if (child != null) list.Add(child);
                }

                var args = new Dictionary<string, object>(StringComparer.Ordinal);
                args[TriggerDefArgKeys.Items] = list;
                return new ActionDef(dto.Type, args);
            }

            return new ActionDef(dto.Type, dto.Args != null ? new Dictionary<string, object>(dto.Args, StringComparer.Ordinal) : null);
        }

        public void LoadFromResources(string resourcesPathWithoutExt)
        {
            throw new NotSupportedException("Use Load(ITextLoader, id) or LoadFromJson(json) instead.");
        }

        public void Load(ITextLoader loader, string id)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException(nameof(id));

            if (!loader.TryLoad(id, out var json) || string.IsNullOrEmpty(json))
            {
                throw new InvalidOperationException($"Trigger json not found or empty: {id}");
            }

            ReloadFromJson(json, id);
        }

        public void LoadFromJson(string json, string sourceName = null)
        {
            ReloadFromJson(json, sourceName);
        }

        public ConfigReloadResult Reload(ITextLoader loader, string id)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException(nameof(id));

            if (!loader.TryLoad(id, out var json) || string.IsNullOrEmpty(json))
            {
                var fail = ConfigReloadResult.Fail(ConfigKey, _version, $"Trigger json not found or empty: {id}");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            return ReloadFromJson(json, id);
        }

        public ConfigReloadResult ReloadFromJson(string json, string sourceName = null)
        {
            if (string.IsNullOrEmpty(json))
            {
                var fail = ConfigReloadResult.Fail(ConfigKey, _version, $"Trigger json is empty: {sourceName ?? "<json>"}");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            AbilityTriggerDatabaseDTO dto;
            try
            {
                dto = JsonConvert.DeserializeObject<AbilityTriggerDatabaseDTO>(json);
            }
            catch (Exception ex)
            {
                var fail = ConfigReloadResult.Fail(ConfigKey, _version, $"Failed to parse trigger json: {sourceName ?? "<json>"}. {ex.Message}");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            var nextFlatTriggers = new List<TriggerDTO>();
            var nextIds = new HashSet<int>();

            if (dto != null && dto.Triggers != null && dto.Triggers.Count > 0)
            {
                nextFlatTriggers.AddRange(dto.Triggers);
                for (int i = 0; i < dto.Triggers.Count; i++)
                {
                    var t = dto.Triggers[i];
                    if (t == null) continue;
                    if (t.TriggerId <= 0) continue;
                    nextIds.Add(t.TriggerId);
                }
            }

            var changed = BuildChangedIds(_triggerIdSnapshot, nextIds);

            _flatTriggers = nextFlatTriggers;
            _triggerIdSnapshot = nextIds;
            _version++;

            var ok = ConfigReloadResult.Success(ConfigKey, _version, fullReload: true, changedIds: changed);
            ConfigReloadBus.Publish(ok);
            return ok;
        }

        private static List<int> BuildChangedIds(HashSet<int> prev, HashSet<int> next)
        {
            if (prev == null || prev.Count == 0)
            {
                if (next == null || next.Count == 0) return null;
                return new List<int>(next);
            }

            if (next == null || next.Count == 0)
            {
                return prev.Count > 0 ? new List<int>(prev) : null;
            }

            var changed = new List<int>();
            foreach (var id in prev)
            {
                if (!next.Contains(id)) changed.Add(id);
            }

            foreach (var id in next)
            {
                if (!prev.Contains(id)) changed.Add(id);
            }

            return changed.Count > 0 ? changed : null;
        }
    }
}
