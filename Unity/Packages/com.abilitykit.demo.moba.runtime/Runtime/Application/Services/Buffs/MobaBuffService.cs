using System;
using System.Collections.Generic;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;
using AbilityKit.Core.Logging;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Diagnostics;

using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Buffs.Lifecycle;
using AbilityKit.Demo.Moba.Services.Buffs.Runtime;
using AbilityKit.Demo.Moba.Services.Buffs.Presentation;
using AbilityKit.Demo.Moba.Services.Buffs.Tagging;
using AbilityKit.Demo.Moba.Services.Buffs.Triggering;

namespace AbilityKit.Demo.Moba.Services.Buffs {
    /// <summary>
    /// Buff 对外入口服务：负责把外部 apply/remove 请求收敛成命令队列，并交给生命周期执行器统一处理。
    /// 这里不直接改运行时细节，避免入口层和生命周期层职责交叉。
    /// </summary>
    [WorldService(typeof(MobaBuffService))]
    public sealed class MobaBuffService : IWorldInitializable
    {
        private enum BuffCommandKind
        {
            Apply = 0,
            Remove = 1,
        }

        private struct BuffCommand
        {
            public long Seq;
            public BuffCommandKind Kind;
            public BuffApplyRequest ApplyRequest;
            public BuffRemoveRequest RemoveRequest;

            public int BuffId => Kind == BuffCommandKind.Apply ? ApplyRequest.BuffId : RemoveRequest.BuffId;
            public int TargetActorId => Kind == BuffCommandKind.Apply ? ApplyRequest.TargetActorId : RemoveRequest.TargetActorId;
        }

        [WorldInject] private MobaActorLookupService _actors = null;
        [WorldInject(required: false)] private MobaConfigDatabase _configs = null;
        [WorldInject(required: false)] private IMobaEffectiveTagQueryService _tags = null;
        [WorldInject(required: false)] private IMobaContinuousTagTemplateRegistry _tagTemplates = null;
        [WorldInject(required: false)] private IMobaBattleDiagnosticsService _diagnostics = null;
        [WorldInject(required: false)] private IMobaBattleDiagnosticEventSink _eventCollector = null;
        [WorldInject(required: false)] private IMobaBattleExceptionPolicy _exceptions = null;
        private BuffLifecycleExecutor _lifecycle;
        private long _nextCommandSeq;
        private readonly List<BuffCommand> _pending = new List<BuffCommand>(32);
        private readonly HashSet<long> _awaitedCommandSeqs = new HashSet<long>();
        private readonly Dictionary<long, bool> _commandResults = new Dictionary<long, bool>();
        private int _draining;

        public void OnInit(IWorldResolver services)
        {
            _lifecycle = BuffLifecycleExecutorFactory.Create(services);
        }

        public global::ActorEntity TryGetActorEntity(int actorId)
        {
            if (_actors != null && _actors.TryGetActorEntity(actorId, out var e) && e != null)
            {
                return e;
            }
            return null;
        }

        public bool ApplyBuffImmediate(global::ActorEntity target, int buffId, int sourceActorId, int durationOverrideMs)
        {
            if (target == null || !target.hasActorId) return false;
            return ApplyBuffImmediate(target.actorId.Value, buffId, sourceActorId, durationOverrideMs, default(BuffOriginContext));
        }

        public bool ApplyBuffImmediate(int targetActorId, int buffId, int sourceActorId, int durationOverrideMs)
        {
            return ApplyBuffImmediate(targetActorId, buffId, sourceActorId, durationOverrideMs, default(BuffOriginContext));
        }

        /// <summary>
        /// 立即申请 Buff：先入队再主动 drain，保证和延迟命令走同一条生命周期路径。
        /// </summary>
        public bool ApplyBuffImmediate(int targetActorId, int buffId, int sourceActorId, int durationOverrideMs, in BuffOriginContext origin)
        {
            if (_draining > 0) return false;
            if (!EnqueueApply(targetActorId, buffId, sourceActorId, durationOverrideMs, origin, sourceContextId: 0L, forceNewInstance: false))
            {
                return false;
            }

            return DrainImmediateCommand(_nextCommandSeq, maxCommands: 256);
        }

        public bool ApplyBuffInstanceImmediate(int targetActorId, int buffId, int sourceActorId, int durationOverrideMs, long sourceContextId, in BuffOriginContext origin)
        {
            if (sourceContextId == 0L || _draining > 0) return false;
            if (!EnqueueApply(targetActorId, buffId, sourceActorId, durationOverrideMs, origin, sourceContextId, forceNewInstance: true))
            {
                return false;
            }

            return DrainImmediateCommand(_nextCommandSeq, maxCommands: 256);
        }

        public bool RemoveBuffImmediate(global::ActorEntity target, int buffId, int sourceActorId, TraceLifecycleReason reason)
        {
            if (target == null || !target.hasActorId) return false;
            return RemoveBuffImmediate(target.actorId.Value, buffId, sourceActorId, reason);
        }

        public bool RemoveBuffImmediate(int targetActorId, int buffId, int sourceActorId, TraceLifecycleReason reason)
        {
            if (_draining > 0) return false;
            if (!EnqueueRemove(targetActorId, buffId, sourceActorId, sourceContextId: 0L, reason: reason))
            {
                return false;
            }

            return DrainImmediateCommand(_nextCommandSeq, maxCommands: 256);
        }

        public bool RemoveBuffInstanceImmediate(int targetActorId, int buffId, int sourceActorId, long sourceContextId, TraceLifecycleReason reason)
        {
            if (sourceContextId == 0L || _draining > 0) return false;
            if (!EnqueueRemove(targetActorId, buffId, sourceActorId, sourceContextId, reason))
            {
                return false;
            }

            return DrainImmediateCommand(_nextCommandSeq, maxCommands: 256);
        }

        public int RemoveBuffsImmediate(int targetActorId, int buffId, int sourceActorId, bool removeAll, TraceLifecycleReason reason)
        {
            if (targetActorId <= 0 || _draining > 0) return 0;

            var target = TryGetActorEntity(targetActorId);
            if (target == null || !target.hasBuffs || target.buffs.Active == null || target.buffs.Active.Count == 0)
            {
                return 0;
            }

            var active = target.buffs.Active;
            var commandSeqs = new List<long>();
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var runtime = active[i];
                if (runtime == null) continue;
                if (buffId > 0 && runtime.BuffId != buffId) continue;
                if (sourceActorId > 0 && runtime.SourceId != sourceActorId) continue;

                var removeSourceId = sourceActorId > 0 ? sourceActorId : runtime.SourceId;
                if (!EnqueueRemove(targetActorId, runtime.BuffId, removeSourceId, runtime.SourceContextId, reason)) continue;

                commandSeqs.Add(_nextCommandSeq);
                if (!removeAll) break;
            }

            return DrainImmediateCommands(commandSeqs, Math.Max(256, commandSeqs.Count + 32));
        }

        public int RemoveBuffsWithTagImmediate(int targetActorId, string tagName, int sourceActorId, bool removeAll, TraceLifecycleReason reason)
        {
            return RemoveBuffsWithTagImmediate(targetActorId, tagName, dispelCategory: 0, sourceActorId, removeAll, reason);
        }

        public int RemoveBuffsWithTagImmediate(int targetActorId, string tagName, int dispelCategory, int sourceActorId, bool removeAll, TraceLifecycleReason reason)
        {
            if (targetActorId <= 0 || dispelCategory < 0 || _draining > 0 || !MobaGameplayTagCatalog.TryResolve(tagName, out var tag)) return 0;

            var target = TryGetActorEntity(targetActorId);
            if (target == null || !target.hasBuffs || target.buffs.Active == null || target.buffs.Active.Count == 0 || _configs == null)
            {
                return 0;
            }

            var active = target.buffs.Active;
            var commandSeqs = new List<long>();
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var runtime = active[i];
                if (runtime == null || (sourceActorId > 0 && runtime.SourceId != sourceActorId)) continue;
                if (!_configs.TryGetBuff(runtime.BuffId, out var buff) || buff?.Tags == null || !buff.Tags.HasTag(tag)) continue;
                if (!CanDispel(targetActorId, runtime, buff, dispelCategory)) continue;

                var removeSourceId = sourceActorId > 0 ? sourceActorId : runtime.SourceId;
                if (!EnqueueRemove(targetActorId, runtime.BuffId, removeSourceId, runtime.SourceContextId, reason)) continue;

                commandSeqs.Add(_nextCommandSeq);
                if (!removeAll) break;
            }

            return DrainImmediateCommands(commandSeqs, Math.Max(256, commandSeqs.Count + 32));
        }

        private bool CanDispel(int targetActorId, BuffRuntime runtime, BuffMO buff, int dispelCategory)
        {
            if (buff.DispelPolicy == BuffDispelPolicy.Undispellable)
            {
                ReportDispelRejected("buff.dispel.undispellable", targetActorId, runtime, buff, dispelCategory);
                return false;
            }

            if (dispelCategory > 0 && buff.DispelCategory > 0 && buff.DispelCategory != dispelCategory)
            {
                ReportDispelRejected("buff.dispel.categoryMismatch", targetActorId, runtime, buff, dispelCategory);
                return false;
            }

            if (buff.DispelBlockedByTags == null || buff.DispelBlockedByTags.Count == 0) return true;
            if (_tags == null)
            {
                ReportDispelRejected("buff.dispel.tagQueryUnavailable", targetActorId, runtime, buff, dispelCategory);
                return false;
            }

            var effectiveTags = _tags.GetEffectiveTags(targetActorId);
            if (!MobaGameplayTagCatalog.HasAny(effectiveTags, buff.DispelBlockedByTags)) return true;

            ReportDispelRejected("buff.dispel.immunityBlocked", targetActorId, runtime, buff, dispelCategory);
            return false;
        }

        private void ReportDispelRejected(string code, int targetActorId, BuffRuntime runtime, BuffMO buff, int dispelCategory)
        {
            var sourceActorId = runtime != null ? runtime.SourceId : 0;
            var buffId = buff != null ? buff.Id : runtime != null ? runtime.BuffId : 0;
            var buffCategory = buff != null ? buff.DispelCategory : 0;
            var policy = buff != null ? buff.DispelPolicy.ToString() : "unknown";
            ReportRejected(
                code,
                () => $"Dispel rejected. rejectCode={code} target={targetActorId} buffId={buffId} source={sourceActorId} requestedCategory={dispelCategory} buffCategory={buffCategory} policy={policy}",
                targetActorId,
                buffId,
                sourceActorId);
        }

        /// <summary>
        /// 每帧/定期对 Actor 上的 Buff 运行时做生命周期对账：同步持续行为时间、检查标签移除、触发过期清理。
        /// </summary>
        public void ReconcileActorBuffLifecycles(global::ActorEntity target)
        {
            if (target == null || !target.hasActorId || !target.hasBuffs) return;

            var list = target.buffs.Active;
            if (list == null || list.Count == 0) return;

            for (var i = list.Count - 1; i >= 0; i--)
            {
                var runtime = list[i];
                if (runtime == null)
                {
                    list.RemoveAt(i);
                    BuffRepository.MarkDirty(list);
                    continue;
                }

                var endedByTags = ShouldEndByTags(target.actorId.Value, runtime);
                if (endedByTags)
                {
                    runtime.Remaining = 0f;
                }
                else
                {
                    SyncFromContinuous(runtime);
                }

                if (!ShouldEndRuntime(runtime, endedByTags)) continue;

                var reason = endedByTags ? TraceLifecycleReason.Interrupted : TraceLifecycleReason.Expired;
                _lifecycle?.EndRuntime(target, list, i, runtime, runtime.SourceId, reason);
            }
        }

        private bool ShouldEndByTags(int targetActorId, BuffRuntime runtime)
        {
            if (runtime == null || _configs == null) return false;
            if (!_configs.TryGetBuff(runtime.BuffId, out BuffMO buff) || buff == null) return false;

            if (runtime.TagRequirements == null)
            {
                runtime.TagRequirements = BuffTagLifecycle.ResolveRequirements(buff, _tagTemplates);
            }

            return BuffTagLifecycle.ShouldEnd(_tags, targetActorId, runtime.TagRequirements);
        }

        private static void SyncFromContinuous(BuffRuntime runtime)
        {
            if (runtime == null || runtime.Continuous == null) return;

            runtime.Remaining = runtime.Continuous.RemainingSeconds;
            runtime.IntervalRemainingSeconds = runtime.Continuous.IntervalRemainingSeconds;
        }

        private static bool ShouldEndRuntime(BuffRuntime runtime, bool endedByTags)
        {
            if (runtime == null) return false;
            if (endedByTags) return true;
            if (runtime.Continuous != null) return runtime.Continuous.IsTerminated;
            return runtime.Remaining <= 0f;
        }

        /// <summary>
        /// 消费待处理命令。执行过程中可能由效果再次申请 Buff，因此用 _draining 防止重入递归。
        /// </summary>
        public void DrainPending(int maxCommands)
        {
            if (maxCommands <= 0) return;

            // 防止 Immediate API 在效果回调中递归 drain；普通命令追加到尾部，并在本轮预算允许时继续消费。
            if (_draining > 0) return;

            var diagnostics = _diagnostics;
            var start = diagnostics != null ? diagnostics.GetTimestamp() : 0L;
            var pendingAtStart = _pending.Count;
            var executed = 0;

            _draining++;
            try
            {
                var cursor = 0;
                while (cursor < _pending.Count)
                {
                    if (executed >= maxCommands)
                    {
                        var message = $"[MobaBuffService] DrainPending exceeded maxCommands={maxCommands}. pending={_pending.Count}.";
                        if (diagnostics != null)
                        {
                            diagnostics.Warning("buff.drain.maxCommands", message);
                            diagnostics.Counter("moba.buff.drain.maxCommandsExceeded");
                        }
                        else
                        {
                            Log.Warning(message);
                        }
                        break;
                    }

                    var cmd = _pending[cursor++];
                    var succeeded = false;

                    try
                    {
                        switch (cmd.Kind)
                        {
                            case BuffCommandKind.Apply:
                                succeeded = ExecuteApply(cmd.ApplyRequest);
                                break;
                            case BuffCommandKind.Remove:
                                succeeded = ExecuteRemove(cmd.RemoveRequest);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        var exceptions = _exceptions;
                        if (exceptions != null)
                        {
                            exceptions.Handle(
                                ex,
                                new MobaBattleExceptionContext(
                                    MobaBattleExceptionDomain.Buff,
                                    "command.execute",
                                    actorId: cmd.TargetActorId,
                                    detail: $"kind={cmd.Kind} buffId={cmd.BuffId}"),
                                MobaBattleExceptionSeverity.Recoverable);
                        }
                        else if (diagnostics != null)
                        {
                            diagnostics.Exception(
                                "buff.command.execute",
                                ex,
                                $"Execute buff command failed. kind={cmd.Kind} buffId={cmd.BuffId}");
                            diagnostics.Counter("moba.buff.command.exceptions");
                        }
                        else
                        {
                            Log.Exception(ex, $"[MobaBuffService] Execute buff command failed. kind={cmd.Kind} buffId={cmd.BuffId}");
                        }
                    }

                    if (_awaitedCommandSeqs.Remove(cmd.Seq))
                    {
                        _commandResults[cmd.Seq] = succeeded;
                    }

                    executed++;
                }

                if (cursor > 0)
                {
                    _pending.RemoveRange(0, cursor);
                }
            }
            finally
            {
                _draining--;
                if (diagnostics != null)
                {
                    diagnostics.Sample("moba.buff.drain.pending", pendingAtStart);
                    diagnostics.Sample("moba.buff.drain.executed", executed);
                    diagnostics.Gauge("moba.buff.pending", _pending.Count);
                    diagnostics.RecordDuration(
                        MobaBattleDiagnosticMetric.BuffDrain,
                        start,
                        MobaBattleDiagnosticsDefaults.BuffDrainWarnMs);
                }
            }
        }

        private bool DrainImmediateCommand(long commandSeq, int maxCommands)
        {
            _awaitedCommandSeqs.Add(commandSeq);
            DrainPending(maxCommands);
            var hasResult = _commandResults.TryGetValue(commandSeq, out var succeeded);
            _awaitedCommandSeqs.Remove(commandSeq);
            _commandResults.Remove(commandSeq);
            if (!hasResult) RemovePendingCommand(commandSeq);
            return hasResult && succeeded;
        }

        private int DrainImmediateCommands(List<long> commandSeqs, int maxCommands)
        {
            if (commandSeqs == null || commandSeqs.Count == 0) return 0;

            for (var i = 0; i < commandSeqs.Count; i++)
            {
                _awaitedCommandSeqs.Add(commandSeqs[i]);
            }

            DrainPending(maxCommands);
            var succeededCount = 0;
            for (var i = 0; i < commandSeqs.Count; i++)
            {
                var commandSeq = commandSeqs[i];
                if (_commandResults.TryGetValue(commandSeq, out var succeeded) && succeeded)
                {
                    succeededCount++;
                }
                else if (!_commandResults.ContainsKey(commandSeq))
                {
                    RemovePendingCommand(commandSeq);
                }

                _awaitedCommandSeqs.Remove(commandSeq);
                _commandResults.Remove(commandSeq);
            }

            return succeededCount;
        }

        private void RemovePendingCommand(long commandSeq)
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Seq != commandSeq) continue;
                _pending.RemoveAt(i);
                return;
            }
        }

        private bool EnqueueApply(int targetActorId, int buffId, int sourceActorId, int durationOverrideMs, in BuffOriginContext origin, long sourceContextId, bool forceNewInstance)
        {
            if (targetActorId <= 0) return false;
            if (buffId <= 0) return false;

            _pending.Add(new BuffCommand
            {
                Seq = ++_nextCommandSeq,
                Kind = BuffCommandKind.Apply,
                ApplyRequest = new BuffApplyRequest
                {
                    TargetActorId = targetActorId,
                    BuffId = buffId,
                    SourceActorId = sourceActorId,
                    DurationOverrideMs = durationOverrideMs,
                    SourceContextId = sourceContextId,
                    ForceNewInstance = forceNewInstance,
                    Origin = origin,
                },
            });
            return true;
        }

        private bool EnqueueRemove(int targetActorId, int buffId, int sourceActorId, long sourceContextId, TraceLifecycleReason reason)
        {
            if (targetActorId <= 0) return false;
            if (buffId <= 0) return false;

            _pending.Add(new BuffCommand
            {
                Seq = ++_nextCommandSeq,
                Kind = BuffCommandKind.Remove,
                RemoveRequest = new BuffRemoveRequest
                {
                    TargetActorId = targetActorId,
                    BuffId = buffId,
                    SourceActorId = sourceActorId,
                    SourceContextId = sourceContextId,
                    Reason = reason,
                },
            });
            return true;
        }

        private bool ExecuteApply(in BuffApplyRequest request)
        {
            if (!request.IsValid)
            {
                ReportRejected("buff.apply.invalidRequest", () => "Apply buff request rejected: request is invalid.", request.TargetActorId, request.BuffId, request.SourceActorId);
                return false;
            }

            var ok = _lifecycle != null && _lifecycle.Apply(in request);
            if (!ok)
            {
                var reject = _lifecycle != null ? _lifecycle.LastReject : BuffLifecycleRejectResult.None;
                var rejectCode = FormatRejectCode(reject.Code);
                var hasLifecycle = _lifecycle != null;
                var requestSnapshot = request;
                ReportRejected(rejectCode, () => FormatApplyRejectedMessage(requestSnapshot, hasLifecycle, rejectCode, reject.Message), request.TargetActorId, request.BuffId, request.SourceActorId);
            }

            return ok;
        }

        private bool ExecuteRemove(in BuffRemoveRequest request)
        {
            if (!request.IsValid)
            {
                ReportRejected("buff.remove.invalidRequest", () => "Remove buff request rejected: request is invalid.", request.TargetActorId, request.BuffId, request.SourceActorId);
                return false;
            }

            var ok = _lifecycle != null && _lifecycle.Remove(in request);
            if (!ok)
            {
                var reject = _lifecycle != null ? _lifecycle.LastReject : BuffLifecycleRejectResult.None;
                var rejectCode = FormatRejectCode(reject.Code);
                var hasLifecycle = _lifecycle != null;
                var requestSnapshot = request;
                ReportRejected(rejectCode, () => FormatRemoveRejectedMessage(requestSnapshot, hasLifecycle, rejectCode, reject.Message), request.TargetActorId, request.BuffId, request.SourceActorId);
            }

            return ok;
        }

        private static string FormatApplyRejectedMessage(BuffApplyRequest request, bool hasLifecycle, string rejectCode, string rejectReason)
        {
            return $"Apply buff rejected by lifecycle. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId} sourceContextId={request.SourceContextId} forceNewInstance={request.ForceNewInstance} durationOverrideMs={request.DurationOverrideMs} hasLifecycle={hasLifecycle} rejectCode={rejectCode} reason={FormatRejectReason(rejectReason)}";
        }

        private static string FormatRemoveRejectedMessage(BuffRemoveRequest request, bool hasLifecycle, string rejectCode, string rejectReason)
        {
            return $"Remove buff rejected by lifecycle. target={request.TargetActorId} buffId={request.BuffId} source={request.SourceActorId} sourceContextId={request.SourceContextId} reason={request.Reason} hasLifecycle={hasLifecycle} rejectCode={rejectCode} lifecycleReason={FormatRejectReason(rejectReason)}";
        }

        private static string FormatRejectReason(string reason)
        {
            return string.IsNullOrEmpty(reason) ? "unknown" : reason;
        }

        private static string FormatRejectCode(string code)
        {
            return string.IsNullOrEmpty(code) ? "buff.lifecycle.rejected" : code;
        }

        private static string SanitizeMetricSuffix(string value)
        {
            if (string.IsNullOrEmpty(value)) return "unknown";

            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-') continue;
                chars[i] = '_';
            }

            return new string(chars);
        }

        internal static MobaBattleDiagnosticEventDraft CreateBuffAddedDraft(in BuffApplyRequest request)
        {
            var origin = request.Origin;
            origin.TryGetOrigin(out var resolvedOrigin);
            var handle = request.SkillRuntimeHandle;
            var runtime = handle.IsValid
                ? new BattleDiagnosticRuntimeHandle(handle.RuntimeId, handle.Generation)
                : default;
            var rootContextId = resolvedOrigin.EffectiveRootContextId != 0L
                ? resolvedOrigin.EffectiveRootContextId
                : request.SourceContextId;
            var contextId = request.SourceContextId != 0L
                ? request.SourceContextId
                : resolvedOrigin.ImmediateContextId;
            var summary = request.DurationOverrideMs > 0
                ? $"buffId={request.BuffId}, durationMs={request.DurationOverrideMs}, forceNew={request.ForceNewInstance}"
                : $"buffId={request.BuffId}, forceNew={request.ForceNewInstance}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.BuffAdded,
                BattleDiagnosticEventChannel.Buff,
                BattleDiagnosticEventOutcome.Succeeded,
                request.SourceActorId,
                request.TargetActorId,
                request.BuffId,
                rootContextId,
                contextId,
                runtime,
                summary: summary);
        }

        internal static MobaBattleDiagnosticEventDraft CreateBuffRemovedDraft(in BuffRemoveRequest request)
        {
            var contextId = request.SourceContextId;
            var summary = $"buffId={request.BuffId}, reason={request.Reason}";

            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.BuffRemoved,
                BattleDiagnosticEventChannel.Buff,
                BattleDiagnosticEventOutcome.Succeeded,
                request.SourceActorId,
                request.TargetActorId,
                request.BuffId,
                rootContextId: 0L,
                contextId,
                skillRuntime: default,
                summary: summary);
        }

        private void CollectBuffAdded(in BuffApplyRequest request)
        {
            try
            {
                var collector = _eventCollector;
                if (collector == null) return;

                var draft = CreateBuffAddedDraft(in request);
                collector.TryCollect(in draft);
            }
            catch
            {
                // 诊断采集异常不得影响 Buff 主流程。
            }
        }

        private void CollectBuffRemoved(in BuffRemoveRequest request)
        {
            try
            {
                var collector = _eventCollector;
                if (collector == null) return;

                var draft = CreateBuffRemovedDraft(in request);
                collector.TryCollect(in draft);
            }
            catch
            {
                // 诊断采集异常不得影响 Buff 主流程。
            }
        }

        private void ReportRejected(string key, Func<string> messageFactory, int targetActorId, int buffId, int sourceActorId)
        {
            var diagnostics = _diagnostics;
            if (diagnostics != null)
            {
                diagnostics.Warning(key, messageFactory);
                diagnostics.Counter("moba.buff.command.rejected");
                diagnostics.Counter("moba.buff.command.rejected." + SanitizeMetricSuffix(key));
                if (buffId > 0) diagnostics.Counter("moba.buff.command.rejected.buff." + buffId);
                if (sourceActorId > 0) diagnostics.Counter("moba.buff.command.rejected.source." + sourceActorId);
                return;
            }

            var message = messageFactory != null ? messageFactory() : key;
            Log.Warning($"[MobaBuffService] {message} target={targetActorId} buffId={buffId} source={sourceActorId}");
        }

        public void Dispose()
        {
            _pending.Clear();
            _awaitedCommandSeqs.Clear();
            _commandResults.Clear();
        }
    }
}

