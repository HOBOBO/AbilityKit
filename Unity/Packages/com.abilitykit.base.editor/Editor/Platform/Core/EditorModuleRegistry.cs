#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Editor.Platform.Core
{
    public sealed class EditorModuleRegistry
    {
        private readonly Dictionary<string, IEditorModule> _modules =
            new Dictionary<string, IEditorModule>(StringComparer.Ordinal);
        private readonly IEditorPlatformContext _context;

        public EditorModuleRegistry(IEditorPlatformContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public event Action ModulesChanged;

        public IReadOnlyList<IEditorModule> Modules => _modules.Values
            .OrderBy(module => module.Descriptor.Order)
            .ThenBy(module => module.Descriptor.Id, StringComparer.Ordinal)
            .ToArray();

        public IDisposable Register(IEditorModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));

            var id = module.Descriptor?.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("The module descriptor must define an id.", nameof(module));
            }

            if (_modules.ContainsKey(id))
            {
                throw new InvalidOperationException($"Editor module '{id}' is already registered.");
            }

            _modules.Add(id, module);
            try
            {
                module.OnRegister(_context);
            }
            catch
            {
                _modules.Remove(id);
                throw;
            }

            ModulesChanged?.Invoke();
            return new Registration(this, id, module);
        }

        public bool TryGet(string id, out IEditorModule module)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                module = null;
                return false;
            }

            return _modules.TryGetValue(id, out module);
        }

        public void Clear()
        {
            var modules = Modules.Reverse().ToArray();
            _modules.Clear();
            foreach (var module in modules)
            {
                module.OnUnregister();
            }

            if (modules.Length > 0) ModulesChanged?.Invoke();
        }

        private void Unregister(string id, IEditorModule expectedModule)
        {
            if (!_modules.TryGetValue(id, out var current) || !ReferenceEquals(current, expectedModule)) return;

            _modules.Remove(id);
            current.OnUnregister();
            ModulesChanged?.Invoke();
        }

        private sealed class Registration : IDisposable
        {
            private EditorModuleRegistry _registry;
            private readonly string _id;
            private readonly IEditorModule _module;

            public Registration(EditorModuleRegistry registry, string id, IEditorModule module)
            {
                _registry = registry;
                _id = id;
                _module = module;
            }

            public void Dispose()
            {
                var registry = _registry;
                if (registry == null) return;
                _registry = null;
                registry.Unregister(_id, _module);
            }
        }
    }
}
#endif
