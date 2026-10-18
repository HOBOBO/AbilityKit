using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Agent;

namespace AbilityKit.Game.Flow
{
    public enum BattleActorViewCommandKind
    {
        Upsert = 1,
        Remove = 2,
    }

    public readonly struct BattleActorViewCommand
    {
        public BattleActorViewCommand(BattleActorViewCommandKind kind, int actorId, GatewayStateSyncActorSnapshot actor)
        {
            Kind = kind;
            ActorId = actorId;
            Actor = actor;
        }

        public BattleActorViewCommandKind Kind { get; }
        public int ActorId { get; }
        public GatewayStateSyncActorSnapshot Actor { get; }
    }

    public readonly struct BattleActorViewBatch
    {
        public BattleActorViewBatch(ulong worldId, int frame, BattleActorViewCommand[] commands)
        {
            WorldId = worldId;
            Frame = frame;
            Commands = Array.AsReadOnly(commands ?? Array.Empty<BattleActorViewCommand>());
        }

        public ulong WorldId { get; }
        public int Frame { get; }
        public IReadOnlyList<BattleActorViewCommand> Commands { get; }
    }

    public interface IBattleActorViewPort
    {
        IEnumerable<int> GetActorIds();
        void Upsert(in GatewayStateSyncActorSnapshot actor);
        void Remove(int actorId);
    }

    public sealed class BattleActorViewProjectionWorkspace
    {
        internal readonly List<BattleActorViewCommand> Commands = new List<BattleActorViewCommand>();
        internal readonly HashSet<int> PresentActorIds = new HashSet<int>();
        internal readonly HashSet<int> RemovedActorIds = new HashSet<int>();

        internal void Clear()
        {
            Commands.Clear();
            PresentActorIds.Clear();
            RemovedActorIds.Clear();
        }
    }

    public static class BattleActorViewProjector
    {
        public static BattleActorViewBatch Project(
            in GatewayStateSyncSnapshot snapshot,
            int excludedActorId,
            IEnumerable<int> existingActorIds = null)
        {
            var workspace = new BattleActorViewProjectionWorkspace();
            Fill(in snapshot, excludedActorId, existingActorIds, workspace);
            return CreateBatch(in snapshot, workspace);
        }

        public static BattleActorViewBatch Apply(
            IBattleActorViewPort port,
            in GatewayStateSyncSnapshot snapshot,
            int excludedActorId)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            var workspace = new BattleActorViewProjectionWorkspace();
            Fill(in snapshot, excludedActorId,
                snapshot.IsFullSnapshot ? port.GetActorIds() : null, workspace);
            Dispatch(port, workspace);
            return CreateBatch(in snapshot, workspace);
        }

        public static void ApplyWithoutBatch(
            IBattleActorViewPort port,
            in GatewayStateSyncSnapshot snapshot,
            int excludedActorId,
            BattleActorViewProjectionWorkspace workspace)
        {
            if (port == null) throw new ArgumentNullException(nameof(port));
            if (workspace == null) throw new ArgumentNullException(nameof(workspace));
            Fill(in snapshot, excludedActorId,
                snapshot.IsFullSnapshot ? port.GetActorIds() : null, workspace);
            Dispatch(port, workspace);
        }

        private static BattleActorViewBatch CreateBatch(
            in GatewayStateSyncSnapshot snapshot,
            BattleActorViewProjectionWorkspace workspace)
        {
            return new BattleActorViewBatch(snapshot.WorldId, snapshot.Frame, workspace.Commands.ToArray());
        }

        private static void Fill(
            in GatewayStateSyncSnapshot snapshot,
            int excludedActorId,
            IEnumerable<int> existingActorIds,
            BattleActorViewProjectionWorkspace workspace)
        {
            workspace.Clear();
            var actors = snapshot.Actors ?? Array.Empty<GatewayStateSyncActorSnapshot>();
            var commands = workspace.Commands;
            var present = workspace.PresentActorIds;
            for (var i = 0; i < actors.Length; i++)
            {
                var actor = actors[i];
                if (actor.ActorId <= 0 || !present.Add(actor.ActorId)) continue;
                if (actor.ActorId == excludedActorId) continue;
                commands.Add(new BattleActorViewCommand(BattleActorViewCommandKind.Upsert, actor.ActorId, actor));
            }

            if (snapshot.IsFullSnapshot)
            {
                if (existingActorIds != null)
                {
                    foreach (var actorId in existingActorIds)
                        AddRemoval(actorId, excludedActorId, workspace);
                }
            }
            else
            {
                var removedActorIds = snapshot.RemovedActorIds ?? Array.Empty<int>();
                for (var i = 0; i < removedActorIds.Length; i++)
                    AddRemoval(removedActorIds[i], excludedActorId, workspace);
            }
        }

        private static void AddRemoval(
            int actorId,
            int excludedActorId,
            BattleActorViewProjectionWorkspace workspace)
        {
            if (actorId <= 0 || actorId == excludedActorId ||
                workspace.PresentActorIds.Contains(actorId) ||
                !workspace.RemovedActorIds.Add(actorId)) return;
            workspace.Commands.Add(new BattleActorViewCommand(BattleActorViewCommandKind.Remove, actorId, default));
        }

        private static void Dispatch(
            IBattleActorViewPort port,
            BattleActorViewProjectionWorkspace workspace)
        {
            foreach (var command in workspace.Commands)
            {
                if (command.Kind == BattleActorViewCommandKind.Upsert)
                {
                    var actor = command.Actor;
                    port.Upsert(in actor);
                }
                else if (command.Kind == BattleActorViewCommandKind.Remove)
                {
                    port.Remove(command.ActorId);
                }
            }

        }
    }
}
