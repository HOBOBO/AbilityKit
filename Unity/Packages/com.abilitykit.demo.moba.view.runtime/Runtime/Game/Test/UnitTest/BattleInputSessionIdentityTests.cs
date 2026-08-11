using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.Moba.CreateWorld;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleInputSessionIdentityTests
    {
        [Test]
        public void TryResolveLocalTrainingOpponent_SelectsFirstPlayerOnAnotherTeam()
        {
            var primaryPlayerId = new PlayerId("p1");
            var context = CreateContext(
                BattleHostMode.Local,
                CreateLoadout("p1", teamId: 1),
                CreateLoadout("p1_ally", teamId: 1),
                CreateLoadout("p2", teamId: 2),
                CreateLoadout("p3", teamId: 3));

            try
            {
                var resolved = BattleInputSessionIdentity.TryResolveLocalTrainingOpponent(
                    context,
                    primaryPlayerId,
                    out var opponentPlayerId);

                Assert.IsTrue(resolved);
                Assert.AreEqual("p2", opponentPlayerId.Value);
            }
            finally
            {
                BattleContext.Return(context);
            }
        }

        [Test]
        public void TryResolveLocalTrainingOpponent_ReturnsFalse_WhenNoOpponentIsConfigured()
        {
            var primaryPlayerId = new PlayerId("p1");
            var context = CreateContext(
                BattleHostMode.Local,
                CreateLoadout("p1", teamId: 1),
                CreateLoadout("p1_ally", teamId: 1));

            try
            {
                var resolved = BattleInputSessionIdentity.TryResolveLocalTrainingOpponent(
                    context,
                    primaryPlayerId,
                    out var opponentPlayerId);

                Assert.IsFalse(resolved);
                Assert.AreEqual(default(PlayerId), opponentPlayerId);
            }
            finally
            {
                BattleContext.Return(context);
            }
        }

        [Test]
        public void TryResolveLocalTrainingOpponent_ReturnsFalse_ForGatewayRemoteMode()
        {
            var primaryPlayerId = new PlayerId("p1");
            var context = CreateContext(
                BattleHostMode.GatewayRemote,
                CreateLoadout("p1", teamId: 1),
                CreateLoadout("p2", teamId: 2));

            try
            {
                var resolved = BattleInputSessionIdentity.TryResolveLocalTrainingOpponent(
                    context,
                    primaryPlayerId,
                    out var opponentPlayerId);

                Assert.IsFalse(resolved);
                Assert.AreEqual(default(PlayerId), opponentPlayerId);
            }
            finally
            {
                BattleContext.Return(context);
            }
        }

        private static BattleContext CreateContext(
            BattleHostMode hostMode,
            params MobaPlayerLoadout[] players)
        {
            var primaryPlayerId = new PlayerId("p1");
            var launchSpec = new MobaBattleLaunchSpec(
                battleId: "battle_input_identity_test",
                matchId: "battle_input_identity_test",
                worldId: "battle_input_identity_test",
                worldType: "battle",
                clientId: "battle_input_identity_test_client",
                localPlayerId: primaryPlayerId,
                mapId: 1,
                gameplayId: 1,
                ruleSetId: 0,
                configVersion: 0,
                protocolVersion: 0,
                randomSeed: 1,
                tickRate: 30,
                inputDelayFrames: 0,
                launchMode: MobaBattleLaunchMode.ViewFastEnter,
                syncMode: MobaBattleLaunchSyncMode.FrameSync,
                authorityMode: MobaBattleLaunchAuthorityMode.LocalAuthority,
                players: players,
                enterGamePayload: Array.Empty<byte>());
            var context = BattleContext.Rent();
            context.Plan = BattleStartPlanBuilder
                .ForWorld(
                    "battle_input_identity_test",
                    "battle",
                    "battle_input_identity_test_client",
                    primaryPlayerId.Value,
                    tickRate: 30,
                    inputDelayFrames: 0)
                .WithHostMode(hostMode)
                .WithLaunchSpec(in launchSpec)
                .Build();
            return context;
        }

        private static MobaPlayerLoadout CreateLoadout(string playerId, int teamId)
        {
            return new MobaPlayerLoadout(
                playerId: new PlayerId(playerId),
                teamId: teamId,
                heroId: 1,
                attributeTemplateId: 1001,
                level: 1,
                basicAttackSkillId: 0,
                skillIds: Array.Empty<int>(),
                spawnIndex: 0,
                unitSubType: 1,
                mainType: 1,
                hasSpawnPosition: 0,
                spawnX: 0f,
                spawnY: 0f,
                spawnZ: 0f);
        }
    }
}
