using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Diagnostics;

namespace AbilityKit.Game.Editor
{
    [Flags]
    internal enum BattleDebugModuleSourceSupport
    {
        None = 0,
        Live = 1 << 0,
        Offline = 1 << 1,
        All = Live | Offline
    }

    [Flags]
    internal enum BattleDebugModuleSelectionSupport
    {
        None = 0,
        Frame = 1 << 0,
        Actor = 1 << 1,
        Event = 1 << 2,
        Trace = 1 << 3,
        RuntimeObject = 1 << 4,
        Config = 1 << 5,
        Any = Frame | Actor | Event | Trace | RuntimeObject | Config
    }

    internal enum BattleDebugWidgetSlot
    {
        Navigator = 0,
        Primary = 1,
        Secondary = 2,
        Inspector = 3
    }

    internal enum BattleDebugWidgetRefreshPolicy
    {
        OnDemand = 0,
        WorkspaceChanged = 1,
        Periodic = 2
    }

    internal static class BattleDebugModuleIds
    {
        public const string DiagnosticEvents = "diagnostics.events";
        public const string DiagnosticTrace = "diagnostics.trace";
        public const string RuntimeObjects = "diagnostics.runtime-objects";
        public const string FrameSyncOverview = "diagnostics.framesync.overview";
        public const string FrameSyncPrediction = "diagnostics.framesync.prediction";
        public const string FrameSyncRollback = "diagnostics.framesync.rollback";
        public const string FrameSyncReconcile = "diagnostics.framesync.reconcile";
        public const string FrameSyncTime = "diagnostics.framesync.time";
        public const string FrameSyncNetwork = "diagnostics.framesync.network";
    }

    internal static class BattleDebugWidgetIds
    {
        public const string EventsOverview = "diagnostics.events.overview";
        public const string EventsList = "diagnostics.events.list";
        public const string EventsDetails = "diagnostics.events.details";
        public const string TraceTree = "diagnostics.trace.tree";
        public const string TraceWaterfall = "diagnostics.trace.waterfall";
        public const string TraceDetails = "diagnostics.trace.details";
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    internal sealed class BattleDebugModuleAttribute : Attribute
    {
        public BattleDebugModuleAttribute(string stableId, string category)
        {
            StableId = stableId ?? string.Empty;
            Category = category ?? string.Empty;
        }

        public string StableId { get; }
        public string Category { get; }
        public BattleDiagnosticCapabilities RequiredCapabilities { get; set; }
        public BattleDebugModuleSourceSupport Sources { get; set; } = BattleDebugModuleSourceSupport.All;
        public BattleDebugModuleSelectionSupport Selections { get; set; } = BattleDebugModuleSelectionSupport.Any;
        public BattleDebugWidgetSlot DefaultSlot { get; set; } = BattleDebugWidgetSlot.Primary;
        public BattleDebugWidgetRefreshPolicy RefreshPolicy { get; set; } = BattleDebugWidgetRefreshPolicy.Periodic;
    }

    internal readonly struct BattleDebugModuleDescriptor
    {
        public BattleDebugModuleDescriptor(
            string stableId,
            string displayName,
            string category,
            int order,
            BattleDiagnosticCapabilities requiredCapabilities,
            BattleDebugModuleSourceSupport sources,
            BattleDebugModuleSelectionSupport selections,
            BattleDebugWidgetSlot defaultSlot,
            BattleDebugWidgetRefreshPolicy refreshPolicy)
        {
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
            Order = order;
            RequiredCapabilities = requiredCapabilities;
            Sources = sources;
            Selections = selections;
            DefaultSlot = defaultSlot;
            RefreshPolicy = refreshPolicy;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public int Order { get; }
        public BattleDiagnosticCapabilities RequiredCapabilities { get; }
        public BattleDebugModuleSourceSupport Sources { get; }
        public BattleDebugModuleSelectionSupport Selections { get; }
        public BattleDebugWidgetSlot DefaultSlot { get; }
        public BattleDebugWidgetRefreshPolicy RefreshPolicy { get; }

        public bool SupportsSource(bool isOffline)
        {
            var source = isOffline
                ? BattleDebugModuleSourceSupport.Offline
                : BattleDebugModuleSourceSupport.Live;
            return (Sources & source) != 0;
        }
    }

    internal interface IBattleDebugWidget
    {
        BattleDebugModuleDescriptor Descriptor { get; }
        string StableId { get; }
        string DisplayName { get; }
        bool OwnsScrollView { get; }
        bool IsAvailable(in BattleDebugContext context);
        void Draw(in BattleDebugContext context);
    }

    internal interface IBattleDebugWidgetProvider
    {
        System.Collections.Generic.IReadOnlyList<IBattleDebugWidget> Widgets { get; }
    }

    internal sealed class BattleDebugPanelWidgetAdapter : IBattleDebugWidget
    {
        private readonly IBattleDebugPanel _panel;

        public BattleDebugPanelWidgetAdapter(IBattleDebugPanel panel)
        {
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
            Descriptor = BattleDebugModuleCatalog.Describe(panel);
        }

        public BattleDebugModuleDescriptor Descriptor { get; }
        public string StableId => Descriptor.StableId + ".full";
        public string DisplayName => Descriptor.DisplayName;
        public IBattleDebugPanel Panel => _panel;
        public bool OwnsScrollView =>
            _panel is IBattleDebugPanelLayout layout && layout.OwnsScrollView;

        public bool IsAvailable(in BattleDebugContext context)
        {
            return Descriptor.SupportsSource(context.IsOffline) && _panel.IsVisible(in context);
        }

        public void Draw(in BattleDebugContext context)
        {
            _panel.Draw(in context);
        }
    }

    internal static class BattleDebugModuleCatalog
    {
        private static readonly Dictionary<Type, BattleDebugModuleDescriptor> DescriptorCache =
            new Dictionary<Type, BattleDebugModuleDescriptor>();

        public static BattleDebugModuleDescriptor Describe(IBattleDebugPanel panel)
        {
            if (panel == null) throw new ArgumentNullException(nameof(panel));

            var type = panel.GetType();
            if (DescriptorCache.TryGetValue(type, out var cached))
            {
                return cached;
            }

            var metadata = (BattleDebugModuleAttribute)Attribute.GetCustomAttribute(
                type,
                typeof(BattleDebugModuleAttribute));
            var stableId = metadata != null && !string.IsNullOrEmpty(metadata.StableId)
                ? metadata.StableId
                : type.FullName ?? type.Name;
            var category = metadata != null && !string.IsNullOrEmpty(metadata.Category)
                ? metadata.Category
                : ResolveWorkspace(panel).ToString();

            var descriptor = new BattleDebugModuleDescriptor(
                stableId,
                panel.Name,
                category,
                panel.Order,
                metadata?.RequiredCapabilities ?? BattleDiagnosticCapabilities.None,
                metadata?.Sources ?? BattleDebugModuleSourceSupport.All,
                metadata?.Selections ?? BattleDebugModuleSelectionSupport.Any,
                metadata?.DefaultSlot ?? BattleDebugWidgetSlot.Primary,
                metadata?.RefreshPolicy ?? BattleDebugWidgetRefreshPolicy.Periodic);
            DescriptorCache[type] = descriptor;
            return descriptor;
        }

        public static string GetStableId(IBattleDebugPanel panel)
        {
            return Describe(panel).StableId;
        }

        private static BattleDebugWorkspace ResolveWorkspace(IBattleDebugPanel panel)
        {
            return panel is IBattleDebugPanelLayout layout
                ? layout.Workspace
                : BattleDebugWorkspace.Actor;
        }
    }

    internal readonly struct BattleDebugWorkspacePreset
    {
        public BattleDebugWorkspacePreset(
            string stableId,
            string displayName,
            string primaryModuleId,
            string primaryWidgetId,
            string secondaryModuleId,
            string secondaryWidgetId)
        {
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            PrimaryModuleId = primaryModuleId ?? string.Empty;
            PrimaryWidgetId = primaryWidgetId ?? string.Empty;
            SecondaryModuleId = secondaryModuleId ?? string.Empty;
            SecondaryWidgetId = secondaryWidgetId ?? string.Empty;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string PrimaryModuleId { get; }
        public string PrimaryWidgetId { get; }
        public string SecondaryModuleId { get; }
        public string SecondaryWidgetId { get; }
        public bool ShowsSecondary => !string.IsNullOrEmpty(SecondaryModuleId);
    }

    internal static class BattleDebugWorkspacePresets
    {
        private static readonly BattleDebugWorkspacePreset[] BuiltIn =
        {
            new BattleDebugWorkspacePreset(
                "combat-investigation",
                "战斗调查",
                BattleDebugModuleIds.DiagnosticEvents,
                BattleDebugWidgetIds.EventsList,
                BattleDebugModuleIds.DiagnosticTrace,
                BattleDebugWidgetIds.TraceWaterfall),
            new BattleDebugWorkspacePreset(
                "runtime-integrity",
                "对象完整性",
                BattleDebugModuleIds.RuntimeObjects,
                string.Empty,
                BattleDebugModuleIds.DiagnosticEvents,
                BattleDebugWidgetIds.EventsOverview),
            new BattleDebugWorkspacePreset(
                "frame-sync",
                "帧同步",
                BattleDebugModuleIds.FrameSyncPrediction,
                string.Empty,
                BattleDebugModuleIds.FrameSyncNetwork,
                string.Empty)
        };

        public static System.Collections.Generic.IReadOnlyList<BattleDebugWorkspacePreset> All => BuiltIn;

        public static bool TryGet(string stableId, out BattleDebugWorkspacePreset preset)
        {
            for (var i = 0; i < BuiltIn.Length; i++)
            {
                if (string.Equals(BuiltIn[i].StableId, stableId, StringComparison.Ordinal))
                {
                    preset = BuiltIn[i];
                    return true;
                }
            }

            preset = default;
            return false;
        }
    }
}
