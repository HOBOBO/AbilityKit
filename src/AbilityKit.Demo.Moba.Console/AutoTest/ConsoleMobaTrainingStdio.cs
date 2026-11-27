using System.Text.Json;
using System.Text.Json.Serialization;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Protocol.Moba;

namespace AbilityKit.Demo.Moba.Console.AutoTest;

internal static class ConsoleMobaTrainingStdio
{
    private const int Version = 1;
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    internal static int Run(TextReader input, TextWriter output, TextWriter errors, string[] args)
    {
        if (args.Length > 1)
        {
            errors.WriteLine("Usage: --training-stdio [match.json]");
            return 2;
        }
        var configurationPath = args.Length == 1 ? args[0] : Path.Combine(AppContext.BaseDirectory, "Configs", "training", "duel.json");
        string configuration;
        try { configuration = File.ReadAllText(configurationPath); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            errors.WriteLine(error.Message);
            return 2;
        }

        ConsoleMobaEpisode? episode = null;
        try
        {
            while (input.ReadLine() is { } line)
            {
                try
                {
                    var request = JsonSerializer.Deserialize<TrainingRequest>(line, WireJson)
                        ?? throw new ArgumentException("????? JSON ???");
                    switch (request.Op)
                    {
                        case "reset" when request.Seed is int seed && request.MaxSteps is > 0 && request.Action == null:
                        {
                            episode?.Dispose();
                            episode = new ConsoleMobaEpisode(_ => ReadConfig(configuration), request.MaxSteps.Value);
                            Reply(new { version = Version, ok = true, observation = Encode(episode.Reset(seed)) });
                            break;
                        }
                        case "step" when request.Seed == null && request.MaxSteps == null:
                        {
                            if (episode == null) throw new InvalidOperationException("???? reset?");
                            var observation = episode.Step(Decode(request.Action));
                            Reply(new { version = Version, ok = true, observation = Encode(observation) });
                            break;
                        }
                        case "close" when request.Seed == null && request.MaxSteps == null && request.Action == null:
                            Reply(new { version = Version, ok = true });
                            return 0;
                        default:
                            throw new ArgumentException("??????? reset(seed, maxSteps)?step(action) ? close?");
                    }
                }
                catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
                {
                    var code = error is InvalidOperationException ? "invalid_state" : "invalid_request";
                    Reply(new { version = Version, ok = false, error = new { code, message = error.Message } });
                }
            }
            return 0;
        }
        finally { episode?.Dispose(); }

        void Reply<T>(T response)
        {
            output.WriteLine(JsonSerializer.Serialize(response, WireJson));
            output.Flush();
        }
    }

    private static BattleStartConfig ReadConfig(string json) =>
        JsonSerializer.Deserialize<BattleStartConfig>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        }) ?? throw new ArgumentException("???????????");

    private static ConsoleMobaAction Decode(TrainingAction? action)
    {
        ConsoleMobaMove? move = action?.Move is { } axis ? new(axis.X, axis.Z) : null;
        ConsoleMobaSkillInput? skill = null;
        if (action?.Skill is { } cast)
        {
            var phase = cast.Phase switch
            {
                "press" => SkillInputPhase.Press,
                "hold" => SkillInputPhase.Hold,
                "release" => SkillInputPhase.Release,
                "cancel" => SkillInputPhase.Cancel,
                _ => throw new ArgumentException("skill.phase ??? press?hold?release ? cancel?")
            };
            skill = new(cast.Slot, phase, cast.TargetActorId, cast.AimPosition?.ToVector() ?? default,
                cast.AimDirection?.ToVector() ?? default);
        }
        return new(move, skill);
    }

    private static object Encode(ConsoleMobaObservation observation) => new
    {
        observation.Seed, observation.Frame,
        Player = EncodeActor(observation.Player), Opponent = EncodeActor(observation.Opponent),
        observation.Terminated, observation.Truncated
    };

    private static object EncodeActor(ConsoleMobaActorObservation actor) => new
    {
        actor.ActorId, Position = new { actor.Position.X, actor.Position.Y, actor.Position.Z },
        actor.Hp, actor.MaxHp, actor.Mana, actor.HasBrain, actor.Skills
    };

    private sealed record TrainingRequest(string Op, int? Seed, int? MaxSteps, TrainingAction? Action);
    private sealed record TrainingAction(TrainingMove? Move, TrainingSkill? Skill);
    private sealed record TrainingMove(float X, float Z);
    private sealed record TrainingSkill(int Slot, string Phase, int TargetActorId, TrainingVector? AimPosition, TrainingVector? AimDirection);
    private sealed record TrainingVector(float X, float Y, float Z)
    {
        public Vec3 ToVector() => new(X, Y, Z);
    }
}
