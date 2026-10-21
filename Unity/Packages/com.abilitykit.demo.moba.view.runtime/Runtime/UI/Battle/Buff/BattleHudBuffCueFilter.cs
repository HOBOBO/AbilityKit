using System;
using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using UnityEngine;

namespace AbilityKit.Game.Flow.Battle.Hud
{
    [Flags]
    internal enum BattleHudCueChannel
    {
        None = 0,
        HudStatus = 1,
        HeadText = 2,
        Vfx = 4,
    }

    internal sealed class BattleHudTagPresentationCatalog
    {
        private readonly Dictionary<int, BattleHudTagPresentation> _items;

        private BattleHudTagPresentationCatalog(Dictionary<int, BattleHudTagPresentation> items)
        {
            _items = items;
        }

        public static BattleHudTagPresentationCatalog Load()
        {
            var items = new Dictionary<int, BattleHudTagPresentation>();
            var asset = Resources.Load<TextAsset>("luban/moba/tag_hud_presentation");
            if (asset == null || string.IsNullOrWhiteSpace(asset.text))
                return new BattleHudTagPresentationCatalog(items);
            try
            {
                var wrapper = JsonUtility.FromJson<PresentationList>("{\"Items\":" + asset.text + "}");
                if (wrapper?.Items != null)
                    for (var i = 0; i < wrapper.Items.Length; i++)
                    {
                        var row = wrapper.Items[i];
                        if (row == null || row.DefinitionId <= 0) continue;
                        items[row.DefinitionId] = row.ToValue();
                    }
            }
            catch (Exception)
            {
                items.Clear();
            }
            return new BattleHudTagPresentationCatalog(items);
        }

        public bool TryGet(int definitionId, out BattleHudTagPresentation presentation)
        {
            return _items.TryGetValue(definitionId, out presentation) && presentation.Visible;
        }

        [Serializable] private sealed class PresentationList { public PresentationRow[] Items; }

        [Serializable] private sealed class PresentationRow
        {
            public int DefinitionId;
            public bool Visible;
            public string DisplayName;
            public string LocalizationKey;
            public float ColorR = 1f;
            public float ColorG = 1f;
            public float ColorB = 1f;
            public float ColorA = 1f;
            public string IconResource;
            public int Priority;
            public string Group;
            public int ChannelMask = (int)BattleHudCueChannel.HudStatus;
            public string HeadText;
            public int HeadTextDurationMs;
            public int VfxId;
            public int VfxDurationMs;

            public BattleHudTagPresentation ToValue() => new BattleHudTagPresentation(
                DefinitionId, Visible, DisplayName, LocalizationKey,
                ColorR, ColorG, ColorB, ColorA, IconResource, Priority, Group,
                ChannelMask > 0 ? ChannelMask : (int)BattleHudCueChannel.HudStatus,
                HeadText, HeadTextDurationMs, VfxId, VfxDurationMs);
        }
    }

    internal readonly struct BattleHudTagPresentation
    {
        public int DefinitionId { get; }
        public bool Visible { get; }
        public string DisplayName { get; }
        public string LocalizationKey { get; }
        public float ColorR { get; }
        public float ColorG { get; }
        public float ColorB { get; }
        public float ColorA { get; }
        public string IconResource { get; }
        public int Priority { get; }
        public string Group { get; }
        public int ChannelMask { get; }
        public string HeadText { get; }
        public int HeadTextDurationMs { get; }
        public int VfxId { get; }
        public int VfxDurationMs { get; }

        public BattleHudTagPresentation(int definitionId, bool visible, string displayName, string localizationKey,
            float colorR, float colorG, float colorB, float colorA, string iconResource, int priority, string group,
            int channelMask, string headText, int headTextDurationMs, int vfxId, int vfxDurationMs)
        {
            DefinitionId = definitionId;
            Visible = visible;
            DisplayName = displayName;
            LocalizationKey = localizationKey;
            ColorR = colorR;
            ColorG = colorG;
            ColorB = colorB;
            ColorA = colorA;
            IconResource = iconResource;
            Priority = priority;
            Group = group;
            ChannelMask = channelMask;
            HeadText = headText;
            HeadTextDurationMs = headTextDurationMs;
            VfxId = vfxId;
            VfxDurationMs = vfxDurationMs;
        }
    }

    /// <summary>Shared status-cue policy for Buff and gameplay-tag HUD entries.</summary>
    internal static class BattleHudStatusCueFilter
    {
        public const string BuffOwnerKind = "Buff";
        public const string TagOwnerKind = "Tag";
        private const int TagDefinitionNumericKey = 1;
        private static readonly BattleHudTagPresentationCatalog TagPresentations =
            BattleHudTagPresentationCatalog.Load();

        public static bool IsBuffCue(in PresentationCueData entry)
        {
            return string.Equals(entry.OwnerKind, BuffOwnerKind, StringComparison.Ordinal) &&
                   entry.TargetActorId > 0 && !string.IsNullOrEmpty(entry.InstanceKey);
        }

        public static bool IsTagCue(in PresentationCueData entry)
        {
            if (entry.Domain != PresentationCueDomain.Tag &&
                !string.Equals(entry.OwnerKind, TagOwnerKind, StringComparison.Ordinal) &&
                !string.Equals(entry.CueKind, TagOwnerKind, StringComparison.Ordinal))
                return false;
            if (entry.TargetActorId <= 0 || string.IsNullOrEmpty(entry.InstanceKey)) return false;
            return TryResolveTagPresentation(in entry, out var presentation) &&
                   (presentation.ChannelMask & (int)BattleHudCueChannel.HudStatus) != 0;
        }

        public static bool IsHudStatusCue(in PresentationCueData entry) =>
            IsBuffCue(in entry) || IsTagCue(in entry);

        public static bool TryResolveHeadText(
            in PresentationCueData entry,
            out string text,
            out Color color,
            out int durationMs)
        {
            text = null;
            color = Color.white;
            durationMs = 0;
            if (!TryResolveTagPresentation(in entry, out var presentation) ||
                (presentation.ChannelMask & (int)BattleHudCueChannel.HeadText) == 0)
                return false;

            text = string.IsNullOrWhiteSpace(presentation.HeadText)
                ? presentation.DisplayName
                : presentation.HeadText;
            if (string.IsNullOrWhiteSpace(text)) text = presentation.LocalizationKey;
            color = new Color(presentation.ColorR, presentation.ColorG, presentation.ColorB, presentation.ColorA);
            durationMs = presentation.HeadTextDurationMs;
            return !string.IsNullOrWhiteSpace(text);
        }

        public static bool TryResolveTagVfx(in PresentationCueData entry, out int vfxId, out int durationMs)
        {
            vfxId = 0;
            durationMs = 0;
            if (!TryResolveTagPresentation(in entry, out var presentation) ||
                (presentation.ChannelMask & (int)BattleHudCueChannel.Vfx) == 0)
                return false;
            vfxId = presentation.VfxId;
            durationMs = presentation.VfxDurationMs;
            return vfxId > 0;
        }

        public static bool TryResolveTagPresentation(
            in PresentationCueData entry,
            out BattleHudTagPresentation presentation)
        {
            presentation = default;
            if (!TryGetTagDefinitionId(in entry, out var definitionId)) return false;
            return TagPresentations.TryGet(definitionId, out presentation);
        }

        public static string ResolveTagLabel(in PresentationCueData entry)
        {
            if (TryResolveTagPresentation(in entry, out var presentation))
            {
                if (!string.IsNullOrWhiteSpace(presentation.DisplayName)) return presentation.DisplayName;
                if (!string.IsNullOrWhiteSpace(presentation.LocalizationKey)) return presentation.LocalizationKey;
            }
            return "状态";
        }

        public static bool TryGetTagDefinitionId(in PresentationCueData entry, out int definitionId)
        {
            var count = Math.Min(entry.NumericParamKeys?.Count ?? 0,
                entry.NumericParamValues?.Count ?? 0);
            for (var i = 0; i < count; i++)
            {
                if (entry.NumericParamKeys[i] != TagDefinitionNumericKey) continue;
                var value = entry.NumericParamValues[i];
                if (value > 0f && value <= int.MaxValue)
                {
                    definitionId = (int)value;
                    return true;
                }
            }

            var key = entry.InstanceKey;
            if (!string.IsNullOrEmpty(key) && key.StartsWith("tag:", StringComparison.Ordinal))
            {
                var lastSeparator = key.LastIndexOf(':');
                if (lastSeparator >= 0 &&
                    int.TryParse(key.Substring(lastSeparator + 1), out definitionId) &&
                    definitionId > 0)
                    return true;
            }
            definitionId = 0;
            return false;
        }

        public static bool IsStatusActiveStage(PresentationCueStage stage)
        {
            var transition = PresentationCueSemantics.Classify(stage);
            return transition == PresentationCueTransition.Start ||
                   transition == PresentationCueTransition.KeepActive;
        }

        public static bool IsStatusRemoveStage(PresentationCueStage stage) =>
            PresentationCueSemantics.Classify(stage) == PresentationCueTransition.Stop;

        public static void CollectStatusCues(
            IReadOnlyList<PresentationCueData> source,
            List<PresentationCueData> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            if (source == null) return;
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (IsHudStatusCue(in entry)) buffer.Add(entry);
            }
        }
    }

    /// <summary>Compatibility facade for existing Buff-named consumers.</summary>
    internal static class BattleHudBuffCueFilter
    {
        public const string BuffOwnerKind = BattleHudStatusCueFilter.BuffOwnerKind;
        public const string TagOwnerKind = BattleHudStatusCueFilter.TagOwnerKind;
        public static bool IsBuffCue(in PresentationCueData entry) => BattleHudStatusCueFilter.IsBuffCue(in entry);
        public static bool IsTagCue(in PresentationCueData entry) => BattleHudStatusCueFilter.IsTagCue(in entry);
        public static bool IsHudStatusCue(in PresentationCueData entry) => BattleHudStatusCueFilter.IsHudStatusCue(in entry);
        public static string ResolveTagLabel(in PresentationCueData entry) => BattleHudStatusCueFilter.ResolveTagLabel(in entry);
        public static bool TryResolveHeadText(in PresentationCueData entry, out string text, out Color color, out int durationMs) =>
            BattleHudStatusCueFilter.TryResolveHeadText(in entry, out text, out color, out durationMs);
        public static bool IsBuffActiveStage(PresentationCueStage stage) => BattleHudStatusCueFilter.IsStatusActiveStage(stage);
        public static bool IsBuffRemoveStage(PresentationCueStage stage) => BattleHudStatusCueFilter.IsStatusRemoveStage(stage);
        public static void CollectBuffCues(IReadOnlyList<PresentationCueData> source, List<PresentationCueData> buffer) =>
            CollectBuffOnly(source, buffer);

        private static void CollectBuffOnly(IReadOnlyList<PresentationCueData> source, List<PresentationCueData> buffer)
        {
            if (buffer == null) return;
            buffer.Clear();
            if (source == null) return;
            for (var i = 0; i < source.Count; i++)
            {
                var entry = source[i];
                if (BattleHudStatusCueFilter.IsBuffCue(in entry)) buffer.Add(entry);
            }
        }
    }

    /// <summary>
    /// Tracks the actors and status keys represented by one active baseline.
    /// Only actors observed in that baseline are reconciled.
    /// </summary>
    internal sealed class BattleHudStatusBaselineSession
    {
        private readonly Dictionary<int, HashSet<string>> _keysByActor =
            new Dictionary<int, HashSet<string>>();
        private readonly HashSet<int> _actors = new HashSet<int>();

        public bool IsActive { get; private set; }
        public IEnumerable<int> Actors => _actors;

        public void Begin()
        {
            _keysByActor.Clear();
            _actors.Clear();
            IsActive = true;
        }

        public void Observe(in PresentationCueData entry)
        {
            if (!IsActive || entry.TargetActorId <= 0) return;
            _actors.Add(entry.TargetActorId);
            if (!BattleHudStatusCueFilter.IsHudStatusCue(in entry) || string.IsNullOrEmpty(entry.InstanceKey)) return;
            if (!_keysByActor.TryGetValue(entry.TargetActorId, out var keys))
                _keysByActor.Add(entry.TargetActorId, keys = new HashSet<string>(StringComparer.Ordinal));
            keys.Add(entry.InstanceKey);
        }

        public bool Contains(int actorId, string instanceKey)
        {
            return _keysByActor.TryGetValue(actorId, out var keys) && keys.Contains(instanceKey);
        }

        public void End(Action<int, ISet<string>> reconcile)
        {
            if (!IsActive) return;
            if (reconcile != null)
            {
                foreach (var actorId in _actors)
                {
                    if (!_keysByActor.TryGetValue(actorId, out var keys))
                        keys = new HashSet<string>(StringComparer.Ordinal);
                    reconcile(actorId, keys);
                }
            }
            _keysByActor.Clear();
            _actors.Clear();
            IsActive = false;
        }
    }
}
