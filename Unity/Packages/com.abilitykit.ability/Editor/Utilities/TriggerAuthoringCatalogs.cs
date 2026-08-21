using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerEventDescriptorCatalog
    {
        private readonly List<TriggerEventDefinitionData> _definitions = new List<TriggerEventDefinitionData>();
        private readonly Dictionary<string, TriggerEventDefinitionData> _exact =
            new Dictionary<string, TriggerEventDefinitionData>(StringComparer.Ordinal);
        private readonly List<TriggerEventDefinitionData> _prefixes = new List<TriggerEventDefinitionData>();

        public TriggerEventDescriptorCatalog(IEnumerable<TriggerEventDefinitionData> definitions)
        {
            if (definitions == null) return;
            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) continue;
                _definitions.Add(definition);
                if (definition.MatchMode == TriggerEventMatchMode.Prefix)
                    _prefixes.Add(definition);
                else
                    _exact[definition.Id] = definition;
            }

            _prefixes.Sort((left, right) => right.Id.Length.CompareTo(left.Id.Length));
        }

        public IReadOnlyList<TriggerEventDefinitionData> Definitions => _definitions;

        public bool TryResolve(string eventId, out TriggerEventDefinitionData definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(eventId)) return false;
            if (_exact.TryGetValue(eventId, out definition)) return true;
            for (var i = 0; i < _prefixes.Count; i++)
            {
                var candidate = _prefixes[i];
                if (!eventId.StartsWith(candidate.Id, StringComparison.Ordinal)) continue;
                definition = candidate;
                return true;
            }
            return false;
        }

        public static TriggerEventDescriptorCatalog FromAsset(TriggerEventCatalogAsset asset)
        {
            return asset == null ? null : new TriggerEventDescriptorCatalog(asset.Events);
        }
    }

    internal sealed class TriggerGlobalBlackboardDescriptorCatalog
    {
        private readonly List<TriggerGlobalBlackboardKeyData> _definitions =
            new List<TriggerGlobalBlackboardKeyData>();
        private readonly Dictionary<string, TriggerGlobalBlackboardKeyData> _keys =
            new Dictionary<string, TriggerGlobalBlackboardKeyData>(StringComparer.Ordinal);

        public TriggerGlobalBlackboardDescriptorCatalog(IEnumerable<TriggerGlobalBlackboardKeyData> keys)
        {
            if (keys == null) return;
            foreach (var key in keys)
            {
                if (key == null || string.IsNullOrWhiteSpace(key.Key)) continue;
                _definitions.Add(key);
                _keys[key.Key] = key;
            }
        }

        public IReadOnlyList<TriggerGlobalBlackboardKeyData> Definitions => _definitions;

        public bool TryGet(string key, out TriggerGlobalBlackboardKeyData definition)
        {
            return _keys.TryGetValue(key ?? string.Empty, out definition);
        }

        public static TriggerGlobalBlackboardDescriptorCatalog FromAsset(TriggerGlobalBlackboardCatalogAsset asset)
        {
            return asset == null ? null : new TriggerGlobalBlackboardDescriptorCatalog(asset.Keys);
        }
    }

    internal sealed class TriggerTemplateDescriptorCatalog
    {
        private readonly List<TriggerAuthoringTemplateAsset> _definitions =
            new List<TriggerAuthoringTemplateAsset>();
        private readonly Dictionary<string, TriggerAuthoringTemplateAsset> _templates =
            new Dictionary<string, TriggerAuthoringTemplateAsset>(StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguous = new HashSet<string>(StringComparer.Ordinal);

        public TriggerTemplateDescriptorCatalog(IEnumerable<TriggerAuthoringTemplateAsset> templates)
        {
            if (templates == null) return;
            foreach (var asset in templates)
            {
                if (asset == null) continue;
                _definitions.Add(asset);
                var id = asset.Template != null ? asset.Template.TemplateId : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (_templates.ContainsKey(id)) _ambiguous.Add(id);
                else _templates.Add(id, asset);
            }
        }

        public IReadOnlyList<TriggerAuthoringTemplateAsset> Definitions => _definitions;

        public bool TryGet(string templateId, out TriggerAuthoringTemplateAsset asset)
        {
            return _templates.TryGetValue(templateId ?? string.Empty, out asset) &&
                   !_ambiguous.Contains(templateId ?? string.Empty);
        }

        public bool IsAmbiguous(string templateId)
        {
            return _ambiguous.Contains(templateId ?? string.Empty);
        }

        public static TriggerTemplateDescriptorCatalog FromAsset(TriggerAuthoringTemplateCatalogAsset asset)
        {
            return asset == null ? null : new TriggerTemplateDescriptorCatalog(asset.Templates);
        }
    }

    internal sealed class TriggerAuthoringValidationContext
    {
        public TriggerTypeDescriptorCatalog Types;
        public TriggerEventDescriptorCatalog Events;
        public TriggerGlobalBlackboardDescriptorCatalog GlobalBlackboard;
        public TriggerTemplateDescriptorCatalog Templates;

        public static TriggerAuthoringValidationContext Create(TriggerAuthoringModuleAsset asset)
        {
            var project = asset != null ? asset.Project : null;
            return new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                Events = TriggerEventDescriptorCatalog.FromAsset(project != null ? project.EventCatalog : null),
                GlobalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(
                    project != null ? project.GlobalBlackboardCatalog : null),
                Templates = TriggerTemplateDescriptorCatalog.FromAsset(
                    project != null ? project.TemplateCatalog : null)
            };
        }

        public static TriggerAuthoringValidationContext Create(TriggerAuthoringTemplateAsset asset)
        {
            var project = asset != null ? asset.Project : null;
            return new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                Events = TriggerEventDescriptorCatalog.FromAsset(project != null ? project.EventCatalog : null),
                GlobalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(
                    project != null ? project.GlobalBlackboardCatalog : null),
                Templates = TriggerTemplateDescriptorCatalog.FromAsset(
                    project != null ? project.TemplateCatalog : null)
            };
        }
    }
}
