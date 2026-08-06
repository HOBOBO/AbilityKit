using AbilityKit.Network.Battle.Projection;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        private PredictionViewBridge _predictionViewBridge;

        private void TickRemoteDrivenLocalSim(float deltaTime)
        {
            _remoteDrivenLastTickedFrame = RemoteDrivenWorldTickDriver.Tick(new RemoteDrivenWorldTickOptions(
                _plan,
                _handles.RemoteDriven,
                _worldCatchUp,
                _snapshots,
                _remoteDrivenLastTickedFrame,
                GetFixedDeltaSeconds(),
                SessionSimRuntimeTuning.MaxCatchUpStepsPerUpdate,
                _lastServerAckFrame));

            // P0-3 FIX: 推送预测 world 的本地玩家状态到 view EntityWorld。
            // 在预测 tick 完成后，本地玩家的 transform 从预测 world 覆盖到 view EntityWorld，
            // 让玩家看到的本地英雄位置是"预测的即时位置"而非"等服务器确认的位置"。
            // 远程玩家继续走 snapshot 插值（BattleSyncFeature 负责），不受此影响。
            // 投影规范：通过 IActorProjectionProducer 提取（注册在预测 world 的 Services），
            // 与 snapshot 通道共享同一份字段提取逻辑。
            if (_plan.Authority.EnableClientPrediction && _ctx != null)
            {
                var world = _handles.RemoteDriven.World;
                if (world?.Services == null) return;
                if (!world.Services.TryResolve<IActorProjectionProducer>(out var producer) || producer == null) return;

                _predictionViewBridge ??= new PredictionViewBridge(_ctx.EntityWorld, _ctx.EntityLookup);
                _predictionViewBridge.SyncLocalPlayer(producer, _ctx.LocalActorId);
            }
        }
    }
}
