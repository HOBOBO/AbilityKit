using System;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Core.Snapshots.Routing;
using AbilityKit.World.ECS;

namespace AbilityKit.Game.Flow
{
    internal interface ISessionPresentationPort
    {
        FrameSnapshotDispatcher ConfirmedSnapshots { get; }

        void EnsureConfirmedViewInstalled(
            BattleContext sourceContext,
            GameFlowDomain flow,
            WorldId authWorldId,
            bool enabled,
            Action<IEntity> destroyEntityTree);

        void DisposeConfirmedView(
            GameFlowDomain flow,
            Action<IEntity> destroyEntityTree);
        void DisposeProjectionViews();
    }

    /// <summary>
    /// Provides the simulation lifecycle with a narrow presentation boundary.
    /// </summary>
    internal sealed class SessionPresentationController : ISessionPresentationPort
    {
        private readonly BattlePresentationSessionResources _resources;

        internal SessionPresentationController(BattlePresentationSessionResources resources)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public FrameSnapshotDispatcher ConfirmedSnapshots => _resources.ConfirmedSnapshots;

        public void EnsureConfirmedViewInstalled(
            BattleContext sourceContext,
            GameFlowDomain flow,
            WorldId authWorldId,
            bool enabled,
            Action<IEntity> destroyEntityTree)
        {
            _resources.EnsureConfirmedViewInstalled(
                sourceContext,
                flow,
                authWorldId,
                enabled,
                destroyEntityTree);
        }

        public void DisposeConfirmedView(
            GameFlowDomain flow,
            Action<IEntity> destroyEntityTree)
        {
            _resources.DisposeConfirmedView(flow, destroyEntityTree);
        }

        public void DisposeProjectionViews() => _resources.DisposeProjectionViews();
    }
}
