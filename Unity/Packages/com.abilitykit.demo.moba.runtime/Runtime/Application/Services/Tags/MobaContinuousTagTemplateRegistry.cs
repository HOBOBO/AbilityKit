using System.Collections.Generic;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class MobaContinuousTagTemplateRegistry : IMobaContinuousTagTemplateRegistry
    {
        private readonly MobaConfigDatabase _db;
        private readonly Dictionary<int, ContinuousTagRequirements> _cacheById = new Dictionary<int, ContinuousTagRequirements>();
        private readonly Dictionary<string, ContinuousTagRequirements> _cacheByName = new Dictionary<string, ContinuousTagRequirements>();
        private readonly Dictionary<int, string> _revisionsById = new Dictionary<int, string>();

        public MobaContinuousTagTemplateRegistry(MobaConfigDatabase db)
        {
            _db = db;
        }

        internal long SourceVersion => _db?.Version ?? 0L;

        internal string GetDefinitionRevision(int id)
        {
            if (_revisionsById.TryGetValue(id, out var revision)) return revision;
            return _db != null && _db.TryGetContinuousTagTemplate(id, out var template) ? template?.DefinitionRevision : null;
        }

        public bool TryGet(int templateId, out ContinuousTagRequirements requirements)
        {
            requirements = null;
            if (templateId <= 0) return false;

            if (_cacheById.TryGetValue(templateId, out var cached) && cached != null)
            {
                requirements = CopyRequirements(cached);
                return true;
            }

            if (_db == null) return false;
            if (!_db.TryGetContinuousTagTemplate(templateId, out var mo) || mo == null) return false;

            cached = CreateRequirements(mo);
            _cacheById[templateId] = cached;
            _revisionsById[templateId] = mo.DefinitionRevision;
            requirements = CopyRequirements(cached);
            return true;
        }

        public bool TryGet(string name, out ContinuousTagRequirements requirements)
        {
            requirements = null;
            if (string.IsNullOrEmpty(name)) return false;

            if (_cacheByName.TryGetValue(name, out var cached) && cached != null)
            {
                requirements = CopyRequirements(cached);
                return true;
            }

            if (_db == null) return false;
            if (!_db.TryGetContinuousTagTemplateByName(name, out var mo) || mo == null) return false;

            cached = CreateRequirements(mo);
            _cacheByName[name] = cached;
            _cacheById[mo.Id] = cached;
            _revisionsById[mo.Id] = mo.DefinitionRevision;
            requirements = CopyRequirements(cached);
            return true;
        }

        private static ContinuousTagRequirements CopyRequirements(ContinuousTagRequirements source)
        {
            return new ContinuousTagRequirements
            {
                ActivationRequired = CopyCondition(source.ActivationRequired),
                ApplicationTags = CopyContainer(source.ApplicationTags),
                RemovalRequired = CopyCondition(source.RemovalRequired),
                OngoingRequired = CopyCondition(source.OngoingRequired),
                ExecutionBlockedResultMask = source.ExecutionBlockedResultMask
            };
        }

        private static GameplayTagRequirements CopyCondition(GameplayTagRequirements source)
        {
            return new GameplayTagRequirements(CopyContainer(source.Required), CopyContainer(source.Blocked),
                source.Exact, source.Query);
        }

        internal static ContinuousTagRequirements CreateRequirements(ContinuousTagTemplateMO mo)
        {
            return new ContinuousTagRequirements
            {
                ActivationRequired = new GameplayTagRequirements(mo.ActivationRequiredTags, mo.ActivationBlockedTags,
                    query: mo.ActivationCondition),
                ApplicationTags = CopyContainer(mo.ApplicationTags),
                RemovalRequired = new GameplayTagRequirements(mo.RemovalRequiredTags, mo.RemovalBlockedTags,
                    query: mo.RemovalCondition),
                OngoingRequired = new GameplayTagRequirements(mo.OngoingRequiredTags, mo.OngoingBlockedTags,
                    query: mo.OngoingCondition),
                ExecutionBlockedResultMask = mo.ExecutionBlockedResultMask
            };
        }

        private static GameplayTagContainer CopyContainer(GameplayTagContainer source)
        {
            var copy = new GameplayTagContainer();
            copy.AppendTags(source);
            return copy;
        }
    }
}
