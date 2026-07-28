using System;
using AbilityKit.Demo.Shooter.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterBotAiRuntimeSmokeTests
{
    [Fact]
    public void PlayerLevelBotAiChasesVisiblePlayerThroughRuntimeTick()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "bot-ai-chase-smoke",
            30,
            9001,
            new[]
            {
                new ShooterStartPlayer(1, "Bot", 0f, 0f),
                new ShooterStartPlayer(2, "Target", 8f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.TryGetPlayer(1, out var before));
        Assert.True(runtime.MountBotAi(new ShooterBotAiMountOptions(1, ShooterBotAiProfile.SimpleBattle, "simple-battle")));
        Assert.Equal(1, runtime.BotAiCount);

        for (var i = 0; i < 8; i++)
        {
            Assert.True(runtime.Tick(1f / 30f));
        }

        Assert.True(runtime.TryGetPlayer(1, out var after));
        Assert.True(after.X > before.X, $"Expected bot to chase target on X axis. Before: {before.X}, After: {after.X}");
    }

    [Fact]
    public void PlayerLevelBotAiFiresWhenTargetIsInAttackRange()
    {
        var runtime = new ShooterBattleRuntimePort();
        var start = new ShooterStartGamePayload(
            "bot-ai-attack-smoke",
            30,
            9002,
            new[]
            {
                new ShooterStartPlayer(1, "Bot", 0f, 0f),
                new ShooterStartPlayer(2, "Target", 3f, 0f)
            });

        Assert.True(runtime.StartGame(in start));
        Assert.True(runtime.MountBotAi(new ShooterBotAiMountOptions(1, ShooterBotAiProfile.SimpleBattle, "simple-battle")));

        var fired = false;
        for (var i = 0; i < 30; i++)
        {
            Assert.True(runtime.Tick(1f / 30f));
            var frame = runtime.GetSnapshot();
            fired |= frame.Bullets.Length > 0;
            fired |= Array.Exists(frame.Events, static evt => evt.EventType == (int)ShooterEventType.Fire);
        }

        Assert.True(fired, "Expected the bot's held attack cadence to align with a primary-fire window.");
    }
}
