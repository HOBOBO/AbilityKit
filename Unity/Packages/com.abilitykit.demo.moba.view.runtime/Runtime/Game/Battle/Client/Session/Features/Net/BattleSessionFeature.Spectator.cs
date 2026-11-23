using System;
using System.Threading.Tasks;
using AbilityKit.Ability.Host.Extensions.FrameSync.Spectator;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Network.Battle;

namespace AbilityKit.Game.Flow
{
    public sealed partial class BattleSessionFeature
    {
        public SpectatorWorldDriver SpectatorDriver => _runtime.Spectator.Driver;

        public IWorld SpectatorWorld => _runtime.Spectator.World;

        public bool IsSpectating => _runtime.Spectator.IsSpectating;

        /// <summary>
        /// 结算观战无法继续时的原因；故障后保留最后推进位置，重新观战需要新绑定。
        /// </summary>
        public string SpectatorFault => _runtime.Spectator.Fault;

        /// <summary>
        /// 依据目标房间下发的战局计划选择观战协议。观战使用独立且已鉴权的连接；
        /// 断线后重新建立连接并启动观战，不切换到旧数据流。
        /// </summary>
        public async Task TryStartSpectating(
            INetworkClient gatewayClient,
            BattleStartPlan targetPlan,
            Func<IWorld> worldFactory)
        {
            HideSpectatorView();
            await _runtime.Spectator.StartAsync(gatewayClient, targetPlan, worldFactory);
        }

        public void StopSpectating()
        {
            SessionAsyncOperation.RequireCompleted(
                StopSpectatingAsync(),
                "Spectator session stop");
        }

        public async Task StopSpectatingAsync()
        {
            Exception presentationFailure = null;
            try { HideSpectatorView(); }
            catch (Exception error) { presentationFailure = error; }
            try { await _runtime.Spectator.StopAsync(); }
            catch (Exception stopFailure)
            {
                if (presentationFailure != null)
                    throw new AggregateException(presentationFailure, stopFailure);
                throw;
            }
            if (presentationFailure != null) throw presentationFailure;
        }

        public void UpdateSpectatorWorld(int stepsBudget = 10)
        {
            _runtime.Spectator.Tick(stepsBudget);
        }
    }
}
