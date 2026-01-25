using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.ECS;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Ability.Impl.Triggering
{
    public sealed class AddBuffAction : ITriggerAction
    {
        private readonly List<int> _buffIds;

        public AddBuffAction(List<int> buffIds)
        {
            _buffIds = buffIds;
        }

        public static AddBuffAction FromDef(ActionDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var ids = new List<int>();

            void TryAdd(object v)
            {
                if (v == null) return;
                try
                {
                    if (v is int i)
                    {
                        if (i > 0) ids.Add(i);
                        return;
                    }

                    if (v is long l)
                    {
                        var i2 = (int)l;
                        if (i2 > 0) ids.Add(i2);
                        return;
                    }

                    if (v is JValue jv)
                    {
                        TryAdd(jv.Value);
                        return;
                    }

                    if (v is string s)
                    {
                        if (string.IsNullOrWhiteSpace(s)) return;

                        // Support formats like:
                        // - "100001"
                        // - "100001\n100002"
                        // - "[\n 100001,\n 100002\n]"
                        // - "100001,100002"
                        // Use digit-run scanning to avoid regex allocations.
                        var value = s.Trim();
                        var any = false;
                        var acc = 0;
                        var hasDigits = false;
                        for (int idx = 0; idx < value.Length; idx++)
                        {
                            var ch = value[idx];
                            if (ch >= '0' && ch <= '9')
                            {
                                hasDigits = true;
                                acc = acc * 10 + (ch - '0');
                                continue;
                            }

                            if (hasDigits)
                            {
                                if (acc > 0) ids.Add(acc);
                                any = true;
                                acc = 0;
                                hasDigits = false;
                            }
                        }

                        if (hasDigits && acc > 0)
                        {
                            ids.Add(acc);
                            any = true;
                        }

                        // Backward compatible: if it was a plain number string but no digit-run found (unlikely), try parse.
                        if (!any && int.TryParse(value, out var parsed) && parsed > 0)
                        {
                            ids.Add(parsed);
                        }

                        return;
                    }
                }
                catch
                {
                    // ignored
                }
            }

            var args = def.Args;
            if (args != null && args.TryGetValue("buffIds", out var obj) && obj != null)
            {
                if (obj is IEnumerable<int> ints)
                {
                    foreach (var i in ints) if (i > 0) ids.Add(i);
                }
                else if (obj is JArray jarr)
                {
                    for (int i = 0; i < jarr.Count; i++)
                    {
                        TryAdd(jarr[i]);
                    }
                }
                else if (obj is IEnumerable enumerable)
                {
                    foreach (var v in enumerable)
                    {
                        TryAdd(v);
                    }
                }
                else
                {
                    TryAdd(obj);
                }
            }

            return new AddBuffAction(ids);
        }

        public void Execute(TriggerContext context)
        {
            if (_buffIds == null || _buffIds.Count == 0) return;

            var buffSvc = context?.Services?.GetService(typeof(MobaBuffService)) as MobaBuffService;
            if (buffSvc == null)
            {
                Log.Warning("[Trigger] add_buff cannot resolve MobaBuffService from DI");
                return;
            }

            if (!global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryResolveActorId(context?.Target, out var targetActorId) || targetActorId <= 0)
            {
                Log.Warning("[Trigger] add_buff requires context.Target with valid actorId");
                return;
            }

            global::AbilityKit.Ability.Impl.Triggering.TriggerActionArgUtil.TryResolveActorId(context?.Source, out var sourceActorId);

            var target = buffSvc.TryGetActorEntity(targetActorId);
            if (target == null)
            {
                Log.Warning($"[Trigger] add_buff cannot resolve target ActorEntity: actorId={targetActorId}");
                return;
            }

            object originSource = null;
            object originTarget = null;
            if (context?.Event.Args != null)
            {
                context.Event.Args.TryGetValue(EffectTriggering.Args.OriginSource, out originSource);
                context.Event.Args.TryGetValue(EffectTriggering.Args.OriginTarget, out originTarget);
            }

            for (int i = 0; i < _buffIds.Count; i++)
            {
                var buffId = _buffIds[i];
                if (buffId <= 0) continue;
                buffSvc.ApplyBuffImmediate(target, buffId, sourceActorId, durationOverrideMs: 0, originSource: originSource, originTarget: originTarget);
            }
        }
    }
}
