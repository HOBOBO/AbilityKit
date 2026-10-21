using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Services.Triggering.Cue;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaTagPresentationCueReporter))]
    public sealed class MobaTagPresentationCueReporter : IService, IWorldInitializable, IContinuousLifecycleBinder, IDisposable
    {
        private const string EventTagAdded = "tag.added";
        private const string EventTagRemoved = "tag.removed";
        private const int NumericTagId = 1;
        private const int NumericTagDepth = 2;
        private const int NumericChangeKind = 3;
        private const int NumericSourceValue = 4;

        private readonly Dictionary<int, GameplayTagContainer> _lastTags = new Dictionary<int, GameplayTagContainer>();
        private IMobaEffectiveTagQueryService _effectiveTags;
        private DefaultContinuousManager _continuous;
        private MobaContinuousManager _mobaContinuous;
        private MobaPresentationCueSnapshotService _presentationCues;
        private long _unknownTagCount;

        public long UnknownTagCount => _unknownTagCount;

        public void OnInit(IWorldResolver services)
        {
            services?.TryResolve(out _effectiveTags);
            IContinuousManager continuous = null;
            services?.TryResolve(out continuous);
            _continuous = continuous as DefaultContinuousManager;
            _mobaContinuous = continuous as MobaContinuousManager;
            services?.TryResolve(out _presentationCues);
            if (_effectiveTags == null || _continuous == null)
                throw new InvalidOperationException("MOBA tag cues require effective tags and a lifecycle-aware continuous manager.");
            foreach (var active in _continuous.GetAllActiveContinuous())
            {
                if (!(active?.Config is IMobaContinuousTagConfig tagConfig) ||
                    tagConfig.TagRequirements?.ApplicationTags == null ||
                    tagConfig.TagRequirements.ApplicationTags.Count == 0) continue;
                var ownerId = active.Config.OwnerId;
                if (ownerId <= 0 || ownerId > int.MaxValue) continue;
                _lastTags[(int)ownerId] = _effectiveTags.GetEffectiveTags((int)ownerId);
            }
            _continuous.AddLifecycleBinder(this);
            if (_mobaContinuous != null)
                _mobaContinuous.TagRequirementsChanged += ReportRequirementsChanged;
        }

        public void Dispose()
        {
            _continuous?.RemoveLifecycleBinder(this);
            if (_mobaContinuous != null)
                _mobaContinuous.TagRequirementsChanged -= ReportRequirementsChanged;
            _lastTags.Clear();
            _continuous = null;
            _mobaContinuous = null;
            _effectiveTags = null;
            _presentationCues = null;
            _unknownTagCount = 0;
        }

        public void OnRegistered(IContinuous continuous, IContinuousManager manager) { }
        public void OnActivated(IContinuous continuous, IContinuousManager manager) => ReportChange(continuous);
        public void OnPaused(IContinuous continuous, IContinuousManager manager) => ReportChange(continuous);
        public void OnResumed(IContinuous continuous, IContinuousManager manager) => ReportChange(continuous);
        public void OnEnded(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager) => ReportChange(continuous);
        public void OnUnregistered(IContinuous continuous, ContinuousEndReason reason, IContinuousManager manager)
        {
            if (continuous != null && !continuous.IsTerminated) ReportChange(continuous);
        }

        private void ReportRequirementsChanged(IContinuous continuous)
        {
            ReportChange(continuous, allowEmptyGrant: true);
        }

        private void ReportChange(IContinuous continuous, bool allowEmptyGrant = false)
        {
            if (!(continuous?.Config is IMobaContinuousTagConfig tagConfig) ||
                !allowEmptyGrant && (tagConfig.TagRequirements?.ApplicationTags == null ||
                                     tagConfig.TagRequirements.ApplicationTags.Count == 0)) return;
            var ownerId = continuous?.Config?.OwnerId ?? 0;
            if (_effectiveTags == null || ownerId <= 0 || ownerId > int.MaxValue) return;
            var ownerActorId = (int)ownerId;
            _effectiveTags.MarkDirty(ownerActorId);
            var current = _effectiveTags.GetEffectiveTags(ownerActorId);
            if (!_lastTags.TryGetValue(ownerActorId, out var previous))
                previous = new GameplayTagContainer();
            var added = new GameplayTagContainer();
            var removed = new GameplayTagContainer();
            foreach (var tag in current)
                if (!previous.HasTagExact(tag)) added.Add(tag);
            foreach (var tag in previous)
                if (!current.HasTagExact(tag)) removed.Add(tag);
            if (current.Count == 0) _lastTags.Remove(ownerActorId);
            else _lastTags[ownerActorId] = current;

            if (_presentationCues == null) return;
            var source = continuous.Config is IMobaContinuousProjectionConfig projection
                ? projection.TagSource : GameplayTagSource.System;
            ReportTags(ownerActorId, added, source, MobaPresentationCueStage.Started, EventTagAdded, 1);
            ReportTags(ownerActorId, removed, source, MobaPresentationCueStage.Removed, EventTagRemoved, -1);
        }

        private void ReportTags(int ownerActorId, GameplayTagContainer tags, GameplayTagSource source, MobaPresentationCueStage stage, string eventId, int changeKind)
        {
            if (tags == null || tags.Count == 0) return;

            var legacyKind = PresentationCueSemantics.ToLegacyDomainName(PresentationCueDomain.Tag);

            foreach (var tag in tags)
            {
                if (!tag.IsValid) continue;

                var tagName = tag.TagName ?? string.Empty;
                if (!MobaGameplayTagCatalog.TryGetDefinitionId(tagName, out var definitionId))
                {
                    // A view cue is optional and must not interrupt simulation
                    // when an unpublished runtime tag is observed.
                    _unknownTagCount++;
                    continue;
                }
                var simpleName = tag.SimpleName ?? string.Empty;
                var instanceKey = BuildInstanceKey(ownerActorId, definitionId);

                var entry = new MobaPresentationCueSnapshotEntry
                {
                    Stage = (int)stage,
                    CueKind = legacyKind,
                    Domain = (int)PresentationCueDomain.Tag,
                    Lifetime = (int)PresentationCueLifetime.PersistentState,
                    RequestKey = instanceKey,
                    SourceActorId = ownerActorId,
                    TargetActorId = ownerActorId,
                    Targets = new[] { ownerActorId },
                    OwnerKind = legacyKind,
                    InstanceId = ComposeInstanceId(ownerActorId, definitionId),
                    InstanceKey = instanceKey,
                    LifecycleReason = changeKind,
                    ContextEventId = eventId,
                    // Tag cue 不带表现模板：颜色/缩放走缺省，由表现层按 tag 语义自行决定。
                    Scale = 1f,
                    ColorR = 1f,
                    ColorG = 1f,
                    ColorB = 1f,
                    ColorA = 1f,
                    NumericParamKeys = new[] { NumericTagId, NumericTagDepth, NumericChangeKind, NumericSourceValue },
                    NumericParamValues = new[] { (float)definitionId, tag.GetDepth(), changeKind, (float)source.Value },
                    SourceContextId = source.Value,
                    StringParamKeys = new[] { "tag", "simple", "event", "source" },
                    StringParamValues = new[] { tagName, simpleName, eventId, source.ToString() },
                    ParameterSchemaVersion = 1,
                };

                entry.Parameters = CueParameterBag.FromLegacy(
                    entry.NumericParamKeys,
                    entry.NumericParamValues,
                    entry.StringParamKeys,
                    entry.StringParamValues);

                _presentationCues.Report(in entry);
            }
        }

        private static string BuildInstanceKey(int ownerActorId, int definitionId)
        {
            return $"tag:{ownerActorId}:{definitionId}";
        }

        private static long ComposeInstanceId(int ownerActorId, int definitionId)
        {
            return ((long)ownerActorId << 32) ^ (uint)definitionId;
        }
    }
}
