using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;

namespace AbilityKit.Demo.Moba.Services
{
    [WorldService(typeof(MobaTagDebugSnapshotService))]
    public sealed class MobaTagDebugSnapshotService : IWorldInitializable, IService
    {
        private IContinuousManager _continuous;
        private MobaEffectiveTagQueryService _effective;
        private IMobaContinuousTagRuleService _rules;
        private IFrameTime _frameTime;

        public MobaTagDebugSnapshotService() { }

        public MobaTagDebugSnapshotService(IContinuousManager continuous,
            MobaEffectiveTagQueryService effective, IMobaContinuousTagRuleService rules = null)
        {
            _continuous = continuous;
            _effective = effective;
            _rules = rules;
        }

        public void OnInit(IWorldResolver services)
        {
            services.TryResolve(out _continuous);
            services.TryResolve(out _effective);
            services.TryResolve(out _rules);
            services.TryResolve(out _frameTime);
        }

        public void Dispose()
        {
            _continuous = null;
            _effective = null;
            _rules = null;
            _frameTime = null;
        }

        public MobaTagDebugSnapshot Capture(int actorId, string worldId = null, bool includeRules = false)
        {
            if (actorId <= 0) throw new ArgumentOutOfRangeException(nameof(actorId));
            if (_continuous == null || _effective?.DebugWorkbook == null)
                throw new InvalidOperationException("MOBA tag debug snapshot services are unavailable.");

            var workbook = _effective.DebugWorkbook;
            var effectiveTags = _effective.GetEffectiveTags(actorId);
            var flags = workbook.Evaluate(effectiveTags);
            var sources = CaptureSources(actorId, workbook);
            var tags = new List<MobaTagDebugTag>();
            foreach (var tag in effectiveTags)
            {
                if (!tag.IsValid || !workbook.TryGetTagDefinition(tag.TagName,
                        out var id, out var displayName, out var parentId)) continue;
                tags.Add(new MobaTagDebugTag
                {
                    Id = id,
                    Name = tag.TagName,
                    DisplayName = displayName,
                    ParentId = parentId,
                    SourceIndices = sources.Where(source => source.IsEffective &&
                        source.GrantedTagIds.Contains(id)).Select(source => source.Index).ToArray()
                });
            }
            tags.Sort((left, right) => left.Id.CompareTo(right.Id));

            return new MobaTagDebugSnapshot
            {
                WorldId = worldId ?? string.Empty,
                ActorId = actorId,
                Frame = _frameTime?.Frame.Value ?? 0,
                CapturedUtcTicks = DateTime.UtcNow.Ticks,
                BundleVersion = MobaGameplayTagCatalog.BundleVersion ?? string.Empty,
                ResultMask = (ulong)flags,
                ResultFlags = workbook.GetDebugResultFlags(flags),
                Tags = tags.ToArray(),
                Sources = sources,
                Gates = workbook.GetDebugGates(flags),
                Rules = includeRules ? workbook.GetDebugRules(effectiveTags, sources) : Array.Empty<MobaTagDebugRule>(),
                HasRuleDetails = includeRules
            };
        }

        private MobaTagDebugSource[] CaptureSources(int actorId, MobaTagWorkbookRuntime workbook)
        {
            var continuous = _continuous.GetOwnerContinuous(actorId);
            if (continuous == null || continuous.Count == 0) return Array.Empty<MobaTagDebugSource>();
            var sources = new List<MobaTagDebugSource>();
            for (var i = 0; i < continuous.Count; i++)
            {
                var item = continuous[i];
                if (item?.Config is not IMobaContinuousTagConfig config || config.TagRequirements == null)
                    continue;
                var granted = new List<int>();
                var grantedNames = new List<string>();
                var application = config.TagRequirements.ApplicationTags;
                if (application != null)
                    foreach (var tag in application)
                        if (tag.IsValid && workbook.TryGetTagDefinition(tag.TagName, out var id,
                                out _, out _))
                        {
                            granted.Add(id);
                            grantedNames.Add(tag.TagName);
                        }

                var debug = item is IMobaContinuousRuntimeDebugSource provider &&
                            provider.TryGetRuntimeDebugInfo(out var info) ? info : default;
                var current = _rules?.Explain(item) ?? MobaContinuousTagRuleResult.None;
                var last = _rules?.GetLastResult(item) ?? MobaContinuousTagRuleResult.None;
                var duration = item.Config is IDurationConfig timed && timed.DurationSeconds.HasValue
                    ? timed.DurationSeconds.Value : 0f;
                sources.Add(new MobaTagDebugSource
                {
                    Index = sources.Count,
                    InstanceId = item.Config.Id ?? string.Empty,
                    Kind = string.IsNullOrEmpty(debug.Kind) ? item.GetType().Name : debug.Kind,
                    ConfigId = debug.ConfigId,
                    State = item.State.ToString(),
                    IsEffective = item.IsActive && !item.IsTerminated,
                    SourceActorId = debug.SourceActorId,
                    TargetActorId = debug.TargetActorId > 0 ? debug.TargetActorId : actorId,
                    SourceContextId = debug.SourceContextId,
                    RootContextId = debug.RootContextId,
                    ElapsedSeconds = item.ElapsedSeconds,
                    RemainingSeconds = duration > 0f ? Math.Max(0f, duration - item.ElapsedSeconds) : 0f,
                    Stack = item.Config is IStackConfig stacked ? stacked.Stack : 1,
                    GrantedTagIds = granted.Distinct().OrderBy(id => id).ToArray(),
                    GrantedTagNames = grantedNames.Distinct().OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                    CurrentAction = current.Action.ToString(),
                    CurrentReason = current.Reason ?? string.Empty,
                    LastAction = last.Action.ToString(),
                    LastReason = last.Reason ?? string.Empty,
                    Requirements = CaptureRequirements(current),
                    ExecutionBlockedMask = config.TagRequirements.ExecutionBlockedResultMask
                });
            }
            return sources.ToArray();
        }

        private static MobaTagDebugRequirement[] CaptureRequirements(MobaContinuousTagRuleResult result)
        {
            if (result.Checks == null || result.Checks.Count == 0)
                return Array.Empty<MobaTagDebugRequirement>();
            return result.Checks.Select(check => new MobaTagDebugRequirement
            {
                Category = check.Category,
                Satisfied = check.Satisfied,
                RequiredSatisfied = check.RequiredSatisfied,
                BlockedSatisfied = check.BlockedSatisfied,
                RequiredTags = check.RequiredTags.Select(tag => tag.TagName).ToArray(),
                BlockedTags = check.BlockedTags.Select(tag => tag.TagName).ToArray()
            }).ToArray();
        }
    }
}
