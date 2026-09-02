#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;

namespace AbilityKit.Editor.Platform.Localization
{
    public interface IEditorLocalization
    {
        event Action LanguageChanged;
        string CurrentLanguage { get; }
        string ProjectDefaultLanguage { get; set; }
        string UserLanguageOverride { get; set; }
        string Get(string key);
        string Format(string key, params object[] arguments);
    }

    public interface IEditorLocalizationSource
    {
        string ModuleId { get; }
        bool TryGet(string language, string key, out string value);
    }

    public sealed class DictionaryEditorLocalizationSource : IEditorLocalizationSource
    {
        private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _tables;

        public DictionaryEditorLocalizationSource(
            string moduleId,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables)
        {
            if (string.IsNullOrWhiteSpace(moduleId)) throw new ArgumentException("Module id is required.", nameof(moduleId));
            if (tables == null) throw new ArgumentNullException(nameof(tables));

            ModuleId = moduleId;
            _tables = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in tables)
            {
                _tables[pair.Key] = pair.Value;
            }
        }

        public string ModuleId { get; }

        public bool TryGet(string language, string key, out string value)
        {
            value = null;
            return !string.IsNullOrWhiteSpace(language)
                   && !string.IsNullOrWhiteSpace(key)
                   && _tables.TryGetValue(language, out var table)
                   && table != null
                   && table.TryGetValue(key, out value);
        }
    }

    public sealed class EditorLocalizationService : IEditorLocalization
    {
        public const string EnglishLanguage = "en";

        private readonly List<IEditorLocalizationSource> _sources = new List<IEditorLocalizationSource>();
        private string _projectDefaultLanguage = EnglishLanguage;
        private string _userLanguageOverride = string.Empty;

        public event Action LanguageChanged;

        public string CurrentLanguage => string.IsNullOrWhiteSpace(_userLanguageOverride)
            ? NormalizeLanguage(_projectDefaultLanguage)
            : NormalizeLanguage(_userLanguageOverride);

        public string ProjectDefaultLanguage
        {
            get => _projectDefaultLanguage;
            set
            {
                var normalized = NormalizeLanguage(value);
                if (string.Equals(_projectDefaultLanguage, normalized, StringComparison.OrdinalIgnoreCase)) return;
                _projectDefaultLanguage = normalized;
                LanguageChanged?.Invoke();
            }
        }

        public string UserLanguageOverride
        {
            get => _userLanguageOverride;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : NormalizeLanguage(value);
                if (string.Equals(_userLanguageOverride, normalized, StringComparison.OrdinalIgnoreCase)) return;
                _userLanguageOverride = normalized;
                LanguageChanged?.Invoke();
            }
        }

        public IDisposable RegisterSource(IEditorLocalizationSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            _sources.Add(source);
            LanguageChanged?.Invoke();
            return new SourceRegistration(this, source);
        }

        public string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            var current = CurrentLanguage;
            if (TryGet(current, key, out var value)) return value;

            var project = NormalizeLanguage(_projectDefaultLanguage);
            if (!string.Equals(project, current, StringComparison.OrdinalIgnoreCase)
                && TryGet(project, key, out value)) return value;

            if (!string.Equals(EnglishLanguage, current, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(EnglishLanguage, project, StringComparison.OrdinalIgnoreCase)
                && TryGet(EnglishLanguage, key, out value)) return value;

            return key;
        }

        public string Format(string key, params object[] arguments)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), arguments ?? Array.Empty<object>());
        }

        private bool TryGet(string language, string key, out string value)
        {
            for (var i = _sources.Count - 1; i >= 0; i--)
            {
                if (_sources[i].TryGet(language, key, out value)) return true;
            }

            value = null;
            return false;
        }

        private static string NormalizeLanguage(string language)
        {
            return string.IsNullOrWhiteSpace(language) ? EnglishLanguage : language.Trim();
        }

        private void UnregisterSource(IEditorLocalizationSource source)
        {
            if (!_sources.Remove(source)) return;
            LanguageChanged?.Invoke();
        }

        private sealed class SourceRegistration : IDisposable
        {
            private EditorLocalizationService _owner;
            private readonly IEditorLocalizationSource _source;

            public SourceRegistration(EditorLocalizationService owner, IEditorLocalizationSource source)
            {
                _owner = owner;
                _source = source;
            }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null) return;
                _owner = null;
                owner.UnregisterSource(_source);
            }
        }
    }
}
#endif
