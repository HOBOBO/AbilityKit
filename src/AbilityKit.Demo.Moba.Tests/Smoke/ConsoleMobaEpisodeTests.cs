using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console.AutoTest;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Protocol.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class ConsoleMobaEpisodeTests
{
    [Fact, Trait("Gate", "MobaConsoleSmoke")]
    public void Reset_and_step_drive_real_inputs_without_carrying_over_state()
    {
        using var episode = new ConsoleMobaEpisode(_ => CreateBattle(false), maxSteps: 100);
        Assert.Throws<InvalidOperationException>(() => episode.Step());
        var initial = episode.Reset(101);
        Assert.Equal(0, initial.Frame);
        Assert.False(initial.Done);
        Assert.Throws<ArgumentOutOfRangeException>(() => episode.Step(new(Move: new(float.NaN, 0))));

        var moving = episode.Step(new(Move: new(1, 0)));
        Assert.Equal(1, moving.Frame);
        for (var frame = 0; frame < 8; frame++) moving = episode.Step();
        Assert.True(moving.Player.Position.X > initial.Player.Position.X);

        var reset = episode.Reset(101);
        Assert.Equal(initial.Seed, reset.Seed);
        Assert.Equal(initial.Frame, reset.Frame);
        Assert.Equal(initial.Player.Position, reset.Player.Position);
        Assert.Equal(initial.Opponent.Hp, reset.Opponent.Hp);
        Assert.Equal(initial.Player.Skills.ToArray(), reset.Player.Skills.ToArray());

        var aim = reset.Opponent.Position;
        var result = episode.Step(new(Move: new(0, 0), Skill: new(1, SkillInputPhase.Press,
            AimPosition: aim, AimDirection: new Vec3(0, 0, 1))));
        while (!result.Done) result = episode.Step();
        Assert.Equal(100, result.Frame);
        Assert.True(result.Truncated);
        Assert.False(result.Terminated);
        Assert.True(result.Opponent.Hp < reset.Opponent.Hp);
        Assert.InRange(Assert.Single(result.Player.Skills, skill => skill.Slot == 1).RemainingCooldownMs, 1, 6000);
        Assert.Throws<InvalidOperationException>(() => episode.Step());
        Assert.Equal(102, episode.Reset(102).Seed);
    }

    [Fact, Trait("Gate", "MobaConsoleSmoke")]
    public void Rule_ai_batch_moves_and_deals_damage_and_repeats_same_seed()
    {
        const int steps = 150;
        var samples = ConsoleMobaEpisode.SampleRuleAi(_ => CreateBattle(true), new[] { 101, 202, 101 }, steps).ToArray();
        var episodes = samples.GroupBy(sample => sample.Episode).Select(group => group.ToArray()).ToArray();
        Assert.Equal(3, episodes.Length);
        Assert.Equal(new[] { 101, 202, 101 }, episodes.Select(episode => episode[0].Before.Seed));
        Assert.All(episodes, episode =>
        {
            var initial = episode[0].Before;
            Assert.True(initial.Opponent.HasBrain);
            Assert.False(initial.Player.HasBrain);
            Assert.Equal(0, initial.Frame);
            Assert.Equal(Enumerable.Range(1, episode.Length), episode.Select(sample => sample.After.Frame));
            Assert.True(episode[^1].After.Done);
            Assert.Contains(episode, sample => !sample.After.Opponent.Position.Equals(initial.Opponent.Position));
            Assert.Contains(episode, sample => sample.After.Player.Hp < initial.Player.Hp);
        });
        Assert.Equal(episodes[0].Length, episodes[2].Length);
        foreach (var (first, repeated) in episodes[0].Zip(episodes[2]))
        {
            Assert.Equal(first.After.Player.Hp, repeated.After.Player.Hp);
            Assert.Equal(first.After.Opponent.Position, repeated.After.Opponent.Position);
            Assert.Equal(first.After.Opponent.Skills.ToArray(), repeated.After.Opponent.Skills.ToArray());
        }
    }

    private static BattleStartConfig CreateBattle(bool enableOpponentBrain) => new()
    {
        PlayerId = "p1",
        Players = new List<PlayerConfig>
        {
            new()
            {
                PlayerId = "p1", TeamId = 1, HeroId = 3004, AttributeTemplateId = 3004, Level = 1,
                MainType = 1, UnitSubType = 1, PositionX = 7, PositionZ = -4, BasicAttackSkillId = 30040101,
                SkillIds = new List<int> { 30040201, 30040301, 30040401, 30040501 }
            },
            new()
            {
                PlayerId = "p2", TeamId = 2, HeroId = 3005, AttributeTemplateId = 3005, Level = 1,
                MainType = 1, UnitSubType = 1, PositionX = 7, PositionZ = enableOpponentBrain ? 2 : 1,
                BrainId = 100, EnableBrainOnSpawn = enableOpponentBrain, BasicAttackSkillId = 30050101,
                SkillIds = new List<int> { 30050201, 30050301, 30050401, 30050501 }
            }
        }
    };
}
