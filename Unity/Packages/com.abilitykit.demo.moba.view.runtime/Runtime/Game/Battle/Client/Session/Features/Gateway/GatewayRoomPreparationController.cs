using System;
using System.Threading.Tasks;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 自动创建/加入房间的快速编排器。用于 Demo/Headless 场景下的极简流程：
    /// 连接 → GuestLogin → 创建或加入房间 → 开始 TimeSync。
    /// See also: <see cref="AbilityKit.Game.Flow.GatewayMultiplayerRoomSession"/> for
    /// the full formal lobby room lifecycle (PickHero, SetReady, BeginLoading, etc.).
    /// </summary>
    internal static class GatewayRoomPreparationController
    {
        public static async Task RunAsync(
            Func<BattleStartPlan> getPlan,
            Func<Task> waitForConnectionAsync,
            Func<Task> ensureSessionTokenAsync,
            Func<Task> createAndJoinRoomAsync,
            Func<Task> joinRoomAsync)
        {
            if (getPlan == null) throw new ArgumentNullException(nameof(getPlan));
            if (waitForConnectionAsync == null) throw new ArgumentNullException(nameof(waitForConnectionAsync));
            if (ensureSessionTokenAsync == null) throw new ArgumentNullException(nameof(ensureSessionTokenAsync));
            if (createAndJoinRoomAsync == null) throw new ArgumentNullException(nameof(createAndJoinRoomAsync));
            if (joinRoomAsync == null) throw new ArgumentNullException(nameof(joinRoomAsync));

            await waitForConnectionAsync();
            await ensureSessionTokenAsync();

            var gateway = getPlan().Gateway;
            if (gateway.AutoCreateRoom)
            {
                await createAndJoinRoomAsync();
                return;
            }

            if (gateway.AutoJoinRoom)
            {
                await joinRoomAsync();
            }
        }
    }
}
