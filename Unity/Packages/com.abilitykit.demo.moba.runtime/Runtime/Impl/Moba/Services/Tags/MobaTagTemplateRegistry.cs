using System;
using System.Collections.Generic;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config;
using AbilityKit.Ability.Share.Common.TagSystem;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class MobaTagTemplateRegistry : ITagTemplateRegistry
    {
        private readonly MobaConfigDatabase _db;
        private readonly Dictionary<int, TagTemplateRuntime> _cache = new Dictionary<int, TagTemplateRuntime>();

        public MobaTagTemplateRegistry(MobaConfigDatabase db)
        {
            _db = db;
        }

        public bool TryGet(int templateId, out TagTemplateRuntime template)
        {
            template = null;
            if (templateId <= 0) return false;

            if (_cache.TryGetValue(templateId, out template) && template != null)
            {
                return true;
            }

            if (_db == null) return false;
            if (!_db.TryGetTagTemplate(templateId, out var mo) || mo == null) return false;

            var required = ToContainer(mo.RequiredTags);
            var blocked = ToContainer(mo.BlockedTags);
            var grant = ToContainer(mo.GrantTags);
            var remove = ToContainer(mo.RemoveTags);

            var req = new GameplayTagRequirements(required, blocked, exact: false);
            template = new TagTemplateRuntime(mo.Id, mo.Name, req, grant, remove);
            _cache[templateId] = template;
            return true;
        }

        private static GameplayTagContainer ToContainer(IReadOnlyList<int> ids)
        {
            if (ids == null || ids.Count == 0) return null;

            var c = new GameplayTagContainer();
            for (int i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                if (id <= 0) continue;
                c.Add(GameplayTag.FromId(id));
            }

            return c.Count > 0 ? c : null;
        }
    }
}
