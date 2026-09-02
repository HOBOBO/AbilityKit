#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.Editor.Platform.Core
{
    /// <summary>
    /// Describes an independently registered editor module without coupling the platform
    /// to the module's domain model or canvas implementation.
    /// </summary>
    public sealed class EditorModuleDescriptor
    {
        public EditorModuleDescriptor(string id, string displayNameKey, int order = 0)
        {
            Id = RequireValue(id, nameof(id));
            DisplayNameKey = RequireValue(displayNameKey, nameof(displayNameKey));
            Order = order;
        }

        public string Id { get; }
        public string DisplayNameKey { get; }
        public int Order { get; }

        private static string RequireValue(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value;
        }
    }

    public interface IEditorModule
    {
        EditorModuleDescriptor Descriptor { get; }
        void OnRegister(IEditorPlatformContext context);
        void OnUnregister();
    }

    public interface IEditorPlatformContext
    {
        EditorServiceRegistry Services { get; }
        EditorContributionRegistry<EditorMenuContribution> Menus { get; }
        EditorContributionRegistry<EditorPanelContribution> Panels { get; }
    }

    public sealed class EditorPlatformContext : IEditorPlatformContext
    {
        public EditorPlatformContext(
            EditorServiceRegistry services,
            EditorContributionRegistry<EditorMenuContribution> menus = null,
            EditorContributionRegistry<EditorPanelContribution> panels = null)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Menus = menus ?? new EditorContributionRegistry<EditorMenuContribution>();
            Panels = panels ?? new EditorContributionRegistry<EditorPanelContribution>();
        }

        public EditorServiceRegistry Services { get; }
        public EditorContributionRegistry<EditorMenuContribution> Menus { get; }
        public EditorContributionRegistry<EditorPanelContribution> Panels { get; }
    }

    public sealed class EditorContributionRegistry<TContribution>
        where TContribution : class, IEditorContribution
    {
        private readonly Dictionary<string, TContribution> _items =
            new Dictionary<string, TContribution>(StringComparer.Ordinal);

        public event Action Changed;

        public IReadOnlyList<TContribution> Items => _items.Values
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();

        public IDisposable Register(TContribution contribution)
        {
            if (contribution == null) throw new ArgumentNullException(nameof(contribution));
            if (_items.ContainsKey(contribution.Id))
            {
                throw new InvalidOperationException($"Editor contribution '{contribution.Id}' is already registered.");
            }

            _items.Add(contribution.Id, contribution);
            Changed?.Invoke();
            return new ContributionRegistration(this, contribution);
        }

        public bool TryGet(string id, out TContribution contribution)
        {
            return _items.TryGetValue(id, out contribution);
        }

        private void Unregister(TContribution contribution)
        {
            if (!_items.TryGetValue(contribution.Id, out var current) || !ReferenceEquals(current, contribution)) return;
            _items.Remove(contribution.Id);
            Changed?.Invoke();
        }

        private sealed class ContributionRegistration : IDisposable
        {
            private EditorContributionRegistry<TContribution> _owner;
            private readonly TContribution _contribution;

            public ContributionRegistration(EditorContributionRegistry<TContribution> owner, TContribution contribution)
            {
                _owner = owner;
                _contribution = contribution;
            }

            public void Dispose()
            {
                var owner = _owner;
                if (owner == null) return;
                _owner = null;
                owner.Unregister(_contribution);
            }
        }
    }

    /// <summary>
    /// Small explicit service registry for editor infrastructure. It deliberately does not
    /// perform assembly scanning or instantiate domain modules implicitly.
    /// </summary>
    public sealed class EditorServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Register<TService>(TService service) where TService : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var type = typeof(TService);
            if (_services.ContainsKey(type))
            {
                throw new InvalidOperationException($"Editor service '{type.FullName}' is already registered.");
            }

            _services.Add(type, service);
        }

        public bool TryResolve<TService>(out TService service) where TService : class
        {
            if (_services.TryGetValue(typeof(TService), out var value))
            {
                service = (TService)value;
                return true;
            }

            service = null;
            return false;
        }

        public TService Resolve<TService>() where TService : class
        {
            if (TryResolve<TService>(out var service)) return service;
            throw new KeyNotFoundException($"Editor service '{typeof(TService).FullName}' is not registered.");
        }

        public bool Unregister<TService>() where TService : class
        {
            return _services.Remove(typeof(TService));
        }

        public void Clear()
        {
            _services.Clear();
        }
    }
}
#endif
