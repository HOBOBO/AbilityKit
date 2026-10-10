using System;
using System.Collections.Generic;
using AbilityKit.Core.Logging;
using AbilityKit.Game.Flow.Battle.Modules;
using AbilityKit.Game.Flow.Modules;

namespace AbilityKit.Game.Flow
{
    internal static class SessionSubFeaturePipeline
    {
        internal static readonly IReadOnlyList<string> StandardModuleIds =
            MobaBattleFlowProfile.CreateStandard().Resolve().SessionModuleIds;

        internal static bool IsBuiltIn(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            for (var i = 0; i < StandardModuleIds.Count; i++)
                if (string.Equals(StandardModuleIds[i], id, StringComparison.Ordinal)) return true;
            return false;
        }

        internal static void ValidateCapabilitiesForPlan(
            IReadOnlyList<string> moduleIds, BattleStartPlan plan,
            Func<string, IReadOnlyList<string>> getCapabilities = null)
        {
            var required = new List<string> { "events", "plan" };
            if (plan.RunModeOptions.RunMode != BattleRunMode.Replay)
            {
                required.AddRange(new[] { "snapshots", "dispatch", "lifecycle", "tick" });
                if (plan.HostMode == BattleHostMode.GatewayRemote && plan.Gateway.UseGatewayTransport)
                    required.Add("network");
                if (GatewayRoomPreparationHelper.ShouldPrepareGatewayRoom(plan))
                    required.Add("room");
                if (plan.RunModeOptions.RunMode == BattleRunMode.Record)
                    required.Add("replay");
            }
            MobaBattleFlowProfile.ValidateSessionCapabilities(
                moduleIds,
                getCapabilities ?? MobaBattleFlowProfile.GetBuiltInModuleCapabilities,
                required);
        }

        internal static void AddConfiguredSessionSubFeatures(
            List<ISessionSubFeature<ISessionRuntimeFeature>> subFeatures,
            IReadOnlyList<string> moduleIds,
            Func<string, ISessionSubFeature<ISessionRuntimeFeature>> customFactory = null)
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));
            if (moduleIds == null) throw new ArgumentNullException(nameof(moduleIds));
            for (var i = 0; i < moduleIds.Count; i++)
            {
                var id = moduleIds[i];
                var module = customFactory?.Invoke(id) ?? CreateBuiltIn(id);
                if (module == null)
                    throw new InvalidOperationException("Unregistered battle session module: " + id);
                if (!(module is IGameModuleId identified) ||
                    !string.Equals(identified.Id, id, StringComparison.Ordinal))
                    throw new InvalidOperationException("Battle session module factory returned the wrong id: " + id);
                subFeatures.Add(module);
            }
        }

        private static ISessionSubFeature<ISessionRuntimeFeature> CreateBuiltIn(string id)
        {
            switch (id)
            {
                case "session_events": return new SessionEventsSubFeature();
                case "gateway_room": return new SessionGatewayRoomSubFeature();
                case "snapshot_routing": return new SessionSnapshotRoutingSubFeature();
                case "session_dispatchers": return new SessionDispatchersSubFeature();
                case "session_editor_hooks": return new SessionEditorHooksSubFeature();
                case "session_lifecycle": return new SessionLifecycleSubFeature();
                case "session_net_adapter": return new SessionNetAdapterSubFeature();
                case "session_replay": return new SessionReplaySubFeature();
                case "session_tick_loop": return new SessionTickLoopSubFeature();
                case "session_plan": return new SessionPlanSubFeature();
                default: return null;
            }
        }

        internal static ModuleHost<FeatureModuleContext<ISessionRuntimeFeature>, ISessionSubFeature<ISessionRuntimeFeature>> CreateHost(
            List<ISessionSubFeature<ISessionRuntimeFeature>> subFeatures,
            Action<string> fail)
        {
            if (subFeatures == null) throw new ArgumentNullException(nameof(subFeatures));

            fail ??= message => Log.Error($"[SessionSubFeaturePipeline] {message}");

            return new ModuleHost<FeatureModuleContext<ISessionRuntimeFeature>, ISessionSubFeature<ISessionRuntimeFeature>>(
                subFeatures,
                fail);
        }
    }
}
