using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Json;
using AbilityKit.Ability.World.Services;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaTriggerIndexService : IService
    {
        private const string TriggerJsonResourcesPath = "ability/ability_triggers";

        private readonly ITextLoader _loader;

        public readonly struct Entry
        {
            public readonly TriggerDef Def;
            public readonly IReadOnlyDictionary<string, object> InitialLocalVars;

            public Entry(TriggerDef def, IReadOnlyDictionary<string, object> initialLocalVars)
            {
                Def = def;
                InitialLocalVars = initialLocalVars;
            }
        }

        private readonly Dictionary<string, List<Entry>> _byEventId = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly Dictionary<int, List<Entry>> _byTriggerId = new Dictionary<int, List<Entry>>();

        public MobaTriggerIndexService(ITextLoader loader)
        {
            _loader = loader;
        }

        public void LoadFromResources()
        {
            _byEventId.Clear();
            _byTriggerId.Clear();

            if (_loader == null) throw new InvalidOperationException("ITextLoader not provided.");

            var db = new AbilityTriggerJsonDatabase();
            db.Load(_loader, TriggerJsonResourcesPath);

            var triggerCount = 0;
            foreach (var r in db.EnumerateAll())
            {
                if (r.TriggerId <= 0) continue;

                if (!_byTriggerId.TryGetValue(r.TriggerId, out var idList))
                {
                    idList = new List<Entry>(4);
                    _byTriggerId[r.TriggerId] = idList;
                }
                idList.Add(new Entry(r.Def, r.InitialLocalVars));
                triggerCount++;

                if (!string.IsNullOrEmpty(r.EventId))
                {
                    if (!_byEventId.TryGetValue(r.EventId, out var evtList))
                    {
                        evtList = new List<Entry>(4);
                        _byEventId[r.EventId] = evtList;
                    }
                    evtList.Add(new Entry(r.Def, r.InitialLocalVars));
                }
            }

            Log.Info($"[MobaTriggerIndexService] Loaded triggers from json: {triggerCount}.");
        }

        public bool TryGet(string eventId, out IReadOnlyList<Entry> list)
        {
            list = null;
            if (string.IsNullOrEmpty(eventId)) return false;
            if (!_byEventId.TryGetValue(eventId, out var l) || l == null || l.Count == 0) return false;
            list = l;
            return true;
        }

        public bool TryGetByTriggerId(int triggerId, out IReadOnlyList<Entry> list)
        {
            list = null;
            if (triggerId <= 0) return false;
            if (!_byTriggerId.TryGetValue(triggerId, out var l) || l == null || l.Count == 0) return false;
            list = l;
            return true;
        }

        public void Dispose()
        {
        }
    }
}
