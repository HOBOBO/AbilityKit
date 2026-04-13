using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Core.Common.Log;
using AbilityKit.Core.Common.Pool;
using AbilityKit.Core.Math;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Common.Event;
using AbilityKit.Triggering.Eventing;

namespace AbilityKit.Ability.Impl.Triggering
{
    using AbilityKit.Ability;
    public sealed class PlayPresentationAction : ITriggerAction
    {
        public static class Events
        {
            public const string Play = "presentation.play";
            public const string Stop = "presentation.stop";
        }

        private static readonly ObjectPool<List<int>> _intListPool = Pools.GetPool(
            createFunc: () => new List<int>(16),
            onRelease: list => list.Clear(),
            defaultCapacity: 64,
            maxSize: 1024,
            collectionCheck: false);

        private static readonly ObjectPool<List<Vec3>> _vec3ListPool = Pools.GetPool(
            createFunc: () => new List<Vec3>(8),
            onRelease: list => list.Clear(),
            defaultCapacity: 32,
            maxSize: 512,
            collectionCheck: false);

        private readonly ActionDef _def;

        private PlayPresentationAction(ActionDef def)
        {
            _def = def;
        }

        public static PlayPresentationAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            return new PlayPresentationAction(def);
        }

        public void Execute(TriggerContext context)
        {
            if (context == null) return;

            var args = _def != null ? _def.Args : null;
            if (args == null)
            {
                Log.Warning("[Trigger] play_presentation requires args");
                return;
            }

            var templateId = ReadInt(args, "templateId", 0);
            if (templateId <= 0)
            {
                Log.Warning("[Trigger] play_presentation requires templateId > 0");
                return;
            }

            var mode = (PresentationTargetMode)ReadInt(args, "targetMode", (int)PresentationTargetMode.Target);

            var requestKey = ReadString(args, "requestKey", null);
            var durationMs = ReadInt(args, "durationMs", 0);

            var stop = ReadBool(args, "stop", false);

            var posKey = ReadString(args, "posKey", null);
            var explicitPos = ReadVec3(args, "pos");

            var bus = ResolveEventBus(context);
            if (bus == null)
            {
                Log.Warning("[Trigger] play_presentation cannot resolve plan IEventBus");
                return;
            }

            var targetActorIds = _intListPool.Get();
            var positions = _vec3ListPool.Get();
            try
            {
                if (!ResolveTargets(context, args, mode, posKey, explicitPos, targetActorIds, positions))
                {
                    return;
                }

                var evtId = stop ? Events.Stop : Events.Play;
                var scid = ResolveSourceContextId(context);

                var payload = new PresentationEventArgs
                {
                    EventId = evtId,
                    TemplateId = templateId,
                    RequestKey = requestKey,
                    DurationMsOverride = durationMs,
                    Targets = targetActorIds.Count > 0 ? targetActorIds.ToArray() : null,
                    Positions = positions.Count > 0 ? positions.ToArray() : null,
                    SourceContextId = scid,
                    Scale = args.TryGetValue("scale", out var scaleObj) ? scaleObj : null,
                    Radius = args.TryGetValue("radius", out var radiusObj) ? radiusObj : null,
                    Color = args.TryGetValue("color", out var colorObj) ? colorObj : null,
                };

                var eid = AbilityKit.Ability.Share.Impl.Moba.Services.TriggeringIdUtil.GetEventEid(evtId);
                bus.Publish(new EventKey<PresentationEventArgs>(eid), in payload);
                object boxed = payload;
                bus.Publish(new EventKey<object>(eid), in boxed);
            }
            finally
            {
                _intListPool.Release(targetActorIds);
                _vec3ListPool.Release(positions);
            }
        }

        private static AbilityKit.Triggering.Eventing.IEventBus ResolveEventBus(TriggerContext context)
        {
            if (context == null) return null;
            return context.Services?.GetService(typeof(AbilityKit.Triggering.Eventing.IEventBus)) as AbilityKit.Triggering.Eventing.IEventBus;
        }

        private static long ResolveSourceContextId(TriggerContext context)
        {
            if (context == null) return 0;
            var payload = context.Event.Payload;

            if (payload is AbilityKit.Ability.Share.Impl.Moba.Services.SkillCastContext scc)
            {
                return scc.SourceContextId;
            }

            if (payload is AbilityKit.Ability.IAbilityPipelineContext pipelineCtx)
            {
                if (pipelineCtx.SharedData != null && pipelineCtx.SharedData.TryGetValue("effect.sourceContextId", out var v) && v != null)
                {
                    if (v is long l) return l;
                    if (v is int i) return i;
                }
            }

            if (context.Event.Args != null && context.Event.Args.TryGetValue("effect.sourceContextId", out var v2) && v2 != null)
            {
                if (v2 is long l2) return l2;
                if (v2 is int i2) return i2;
            }

            return 0;
        }

        private static bool ResolveTargets(
            TriggerContext context,
            IReadOnlyDictionary<string, object> args,
            PresentationTargetMode mode,
            string posKey,
            Vec3? explicitPos,
            List<int> targetActorIds,
            List<Vec3> positions)
        {
            if (mode == PresentationTargetMode.Position)
            {
                if (explicitPos.HasValue)
                {
                    positions.Add(explicitPos.Value);
                    return true;
                }

                if (!string.IsNullOrEmpty(posKey) && context.TryGetVar(posKey, out Vec3 v))
                {
                    positions.Add(v);
                    return true;
                }

                // Fallback: use aimPos from pipeline payload if present
                var payload = context.Event.Payload;
                if (payload is AbilityKit.Ability.Share.Impl.Moba.Services.SkillCastContext scc)
                {
                    positions.Add(scc.AimPos);
                    return true;
                }

                return false;
            }

            if (mode == PresentationTargetMode.Self)
            {
                if (TryResolveActorId(context.Source, out var selfId) && selfId > 0)
                {
                    targetActorIds.Add(selfId);
                    return true;
                }
                return false;
            }

            if (mode == PresentationTargetMode.Source)
            {
                if (TryResolveActorId(context.Source, out var sid) && sid > 0)
                {
                    targetActorIds.Add(sid);
                    return true;
                }
                return false;
            }

            if (mode == PresentationTargetMode.Target)
            {
                if (TryResolveActorId(context.Target, out var tid) && tid > 0)
                {
                    targetActorIds.Add(tid);
                    return true;
                }
                return false;
            }

            if (mode == PresentationTargetMode.PayloadTarget)
            {
                var payload = context.Event.Payload;
                if (payload is AbilityKit.Ability.Share.Impl.Moba.Services.SkillCastContext scc && scc.TargetActorId > 0)
                {
                    targetActorIds.Add(scc.TargetActorId);
                    return true;
                }
                return false;
            }

            if (mode == PresentationTargetMode.PayloadAttacker)
            {
                var payload = context.Event.Payload;
                if (payload is AbilityKit.Ability.Share.Impl.Moba.Services.SkillCastContext scc && scc.CasterActorId > 0)
                {
                    targetActorIds.Add(scc.CasterActorId);
                    return true;
                }
                return false;
            }

            if (mode == PresentationTargetMode.QueryTemplate)
            {
                var qid = ReadInt(args, "queryTemplateId", 0);
                if (qid <= 0) return false;

                var search = context.Services != null ? context.Services.GetService(typeof(AbilityKit.Ability.Share.Impl.Moba.Services.SearchTargetService)) as AbilityKit.Ability.Share.Impl.Moba.Services.SearchTargetService : null;
                if (search == null) return false;

                // Caster/AimPos come from pipeline payload if possible
                var casterId = 0;
                var aimPos = Vec3.Zero;

                var payload = context.Event.Payload;
                if (payload is AbilityKit.Ability.Share.Impl.Moba.Services.SkillCastContext scc)
                {
                    casterId = scc.CasterActorId;
                    aimPos = scc.AimPos;
                }

                if (casterId <= 0 && TryResolveActorId(context.Source, out var sid) && sid > 0) casterId = sid;

                var explicitTarget = 0;
                TryResolveActorId(context.Target, out explicitTarget);

                return search.TrySearchActorIds(qid, casterId, in aimPos, explicitTarget, targetActorIds);
            }

            // Explicit
            if (args != null && args.TryGetValue("target", out var tobj) && tobj != null && TryResolveActorId(tobj, out var eid) && eid > 0)
            {
                targetActorIds.Add(eid);
                return true;
            }

            // fallback to context.Target
            if (TryResolveActorId(context.Target, out var fid) && fid > 0)
            {
                targetActorIds.Add(fid);
                return true;
            }

            return false;
        }

        private static bool TryResolveActorId(object obj, out int actorId)
        {
            actorId = 0;
            if (obj == null) return false;
            if (obj is int i)
            {
                actorId = i;
                return true;
            }
            if (obj is long l)
            {
                actorId = (int)l;
                return true;
            }
            if (obj is string s && !string.IsNullOrEmpty(s) && int.TryParse(s, out var parsed))
            {
                actorId = parsed;
                return true;
            }

            // Reuse existing util if present
            try
            {
                return TriggerActionArgUtil.TryResolveActorId(obj, out actorId);
            }
            catch
            {
                return false;
            }
        }

        private static int ReadInt(IReadOnlyDictionary<string, object> args, string key, int def)
        {
            if (args == null || key == null) return def;
            if (!args.TryGetValue(key, out var obj) || obj == null) return def;
            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is float f) return (int)f;
            if (obj is string s && int.TryParse(s, out var parsed)) return parsed;
            try { return Convert.ToInt32(obj); } catch { return def; }
        }

        private static bool ReadBool(IReadOnlyDictionary<string, object> args, string key, bool def)
        {
            if (args == null || key == null) return def;
            if (!args.TryGetValue(key, out var obj) || obj == null) return def;
            if (obj is bool b) return b;
            if (obj is int i) return i != 0;
            if (obj is long l) return l != 0;
            if (obj is string s)
            {
                if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
                if (int.TryParse(s, out var parsed)) return parsed != 0;
            }
            return def;
        }

        private static string ReadString(IReadOnlyDictionary<string, object> args, string key, string def)
        {
            if (args == null || key == null) return def;
            if (args.TryGetValue(key, out var obj) && obj is string s && !string.IsNullOrEmpty(s)) return s;
            return def;
        }

        private static Vec3? ReadVec3(IReadOnlyDictionary<string, object> args, string key)
        {
            if (args == null || key == null) return null;
            if (!args.TryGetValue(key, out var obj) || obj == null) return null;
            if (obj is Vec3 v) return v;
            return null;
        }
    }
}
