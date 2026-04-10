using AbilityKit.Core.Common.Log;
using AbilityKit.Ability.World.Abstractions;
using System;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private void TryDestroyBattleWorlds()
        {
            try
            {
                _handles.RemoteDriven.Runtime?.DestroyWorld(new WorldId(_plan.WorldId));
                _handles.Confirmed.Runtime?.DestroyWorld(new WorldId((_plan.WorldId ?? "room_1") + "__confirmed"));
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }

        private void DisposeConfirmedView()
        {
            if (_flow != null && _confirmedViewFeature != null)
            {
                _flow.Detach(_confirmedViewFeature);
                _confirmedViewFeature = null;
            }

            _confirmedViewSubLobby?.Dispose();
            _confirmedViewSubLobby = null;
            _confirmedViewSubActorTransform?.Dispose();
            _confirmedViewSubActorTransform = null;
            _confirmedViewSubStateHash?.Dispose();
            _confirmedViewSubStateHash = null;

            _confirmedViewCmdHandler?.Dispose();
            _confirmedViewPipeline?.Dispose();
            _confirmedViewSnapshots?.Dispose();
            _confirmedViewCmdHandler = null;
            _confirmedViewPipeline = null;
            _confirmedViewSnapshots = null;

            if (_confirmedViewCtx != null)
            {
                _confirmedViewCtx.FrameSnapshots = null;
                _confirmedViewCtx.SnapshotPipeline = null;
                _confirmedViewCtx.CmdHandler = null;

                if (_confirmedViewCtx.EntityNode.IsValid)
                {
                    DestroyEntityTree(_confirmedViewCtx.EntityNode);
                }
                _confirmedViewCtx.EntityLookup?.Clear();
                BattleContext.Return(_confirmedViewCtx);
                _confirmedViewCtx = null;
            }
        }

        private void DisposeRemoteDrivenWorld()
        {
            _remoteDrivenWorld = null;
            _remoteDrivenRuntime = null;
            _remoteDrivenWorlds = null;
            _remoteDrivenLastTickedFrame = 0;
            _remoteDrivenInputSource?.Dispose();
            _remoteDrivenInputSource = null;
            _remoteDrivenConsumable = null;
            _remoteDrivenSink = null;
        }

        private void DisposeConfirmedWorld()
        {
            _confirmedWorld = null;
            _confirmedRuntime = null;
            _confirmedWorlds = null;
            _confirmedLastTickedFrame = 0;
            _confirmedInputSource?.Dispose();
            _confirmedInputSource = null;
            _confirmedConsumable = null;
            _confirmedSink = null;

            _confirmedSnapshotViewAdapter?.Dispose();
            _confirmedSnapshotViewAdapter = null;

            _confirmedTriggerBridge?.Dispose();
            _confirmedTriggerBridge = null;

            _confirmedViewEventSink = null;
            _confirmedSnapshots = null;

            BattleFlowDebugProvider.ConfirmedAuthorityWorldStats = null;

            if (_ctx != null)
            {
                _ctx.PredictionStats = null;
            }
        }

        private void DisposeNetworkIoDispatcher()
        {
            try
            {
                _networkIoDispatcher?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
            finally
            {
                _networkIoDispatcher = null;
            }
        }
    }
}
