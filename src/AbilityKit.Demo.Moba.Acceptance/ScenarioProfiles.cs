using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.Acceptance;

public sealed record WorldProfile(
    string Id,
    string MapId,
    string CollisionProfileId,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record CollisionProfile(
    string Id,
    string Shape,
    string Layer,
    string Mask,
    IReadOnlyDictionary<string, string> Parameters);

public enum BehaviorProfileKind
{
    BehaviorTree,
    Hfsm,
    Scripted,
}

public sealed record BehaviorProfile(
    string Id,
    BehaviorProfileKind Kind,
    string DefinitionId,
    int DecisionIntervalMs,
    IReadOnlyDictionary<string, string> Blackboard,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>Carrier-neutral request used to bind a declared behavior profile to a runtime actor.</summary>
public sealed record BehaviorBindingRequest(
    TestScenario Scenario,
    TestActor Actor,
    int ActorId,
    BehaviorProfile Profile,
    int Seed);

/// <summary>Minimal runtime state exported by a BT/HFSM carrier for acceptance assertions.</summary>
public sealed record BehaviorRuntimeSnapshot(
    string Alias,
    string? ActorId,
    string State,
    IReadOnlyDictionary<string, string> Blackboard);

/// <summary>
/// Adapter boundary between scenario profiles and a concrete behavior runtime. Implementations
/// may construct a behavior tree, HFSM, or deterministic scripted brain; the scenario layer does
/// not depend on any of those libraries.
/// </summary>
public interface IBehaviorProfileBinder
{
    void Bind(in BehaviorBindingRequest request);
    void Start();
    void Stop();
    IReadOnlyList<BehaviorRuntimeSnapshot> CaptureSnapshots();
}

public sealed class NoopBehaviorProfileBinder : IBehaviorProfileBinder
{
    public static NoopBehaviorProfileBinder Instance { get; } = new();
    private NoopBehaviorProfileBinder() { }
    public void Bind(in BehaviorBindingRequest request) { }
    public void Start() { }
    public void Stop() { }
    public IReadOnlyList<BehaviorRuntimeSnapshot> CaptureSnapshots() => Array.Empty<BehaviorRuntimeSnapshot>();
}

public interface IScenarioProfileResolver<TProfile>
{
    bool TryResolve(string id, out TProfile profile);
}

/// <summary>
/// Carrier-neutral profile catalog. Carriers translate resolved profiles into their own
/// world builders and BT/HFSM runtimes; scenario code never constructs those runtimes directly.
/// </summary>
public sealed class ScenarioProfileCatalog :
    IScenarioProfileResolver<WorldProfile>,
    IScenarioProfileResolver<CollisionProfile>,
    IScenarioProfileResolver<BehaviorProfile>
{
    private readonly Dictionary<string, WorldProfile> _worlds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CollisionProfile> _collisions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BehaviorProfile> _behaviors = new(StringComparer.OrdinalIgnoreCase);

    public ScenarioProfileCatalog Add(WorldProfile profile)
    {
        ValidateId(profile.Id, nameof(profile));
        _worlds[profile.Id] = profile;
        return this;
    }

    public ScenarioProfileCatalog Add(CollisionProfile profile)
    {
        ValidateId(profile.Id, nameof(profile));
        _collisions[profile.Id] = profile;
        return this;
    }

    public ScenarioProfileCatalog Add(BehaviorProfile profile)
    {
        ValidateId(profile.Id, nameof(profile));
        if (string.IsNullOrWhiteSpace(profile.DefinitionId))
            throw new ArgumentException("Behavior profile definitionId is required.", nameof(profile));
        if (profile.DecisionIntervalMs <= 0)
            throw new ArgumentException("Behavior profile decisionIntervalMs must be positive.", nameof(profile));
        _behaviors[profile.Id] = profile;
        return this;
    }

    public bool TryResolve(string id, out WorldProfile profile) => _worlds.TryGetValue(id, out profile!);
    bool IScenarioProfileResolver<CollisionProfile>.TryResolve(string id, out CollisionProfile profile) =>
        _collisions.TryGetValue(id, out profile!);
    bool IScenarioProfileResolver<BehaviorProfile>.TryResolve(string id, out BehaviorProfile profile) =>
        _behaviors.TryGetValue(id, out profile!);

    public IReadOnlyList<string> ValidateReferences(TestScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var errors = new List<string>();
        if (!_worlds.TryGetValue(scenario.WorldProfileId, out var world))
            errors.Add($"world profile '{scenario.WorldProfileId}' was not found");
        else if (!string.IsNullOrWhiteSpace(world.CollisionProfileId) && !_collisions.ContainsKey(world.CollisionProfileId))
            errors.Add($"world collision profile '{world.CollisionProfileId}' was not found");

        foreach (var actor in scenario.Actors)
        {
            if (!string.IsNullOrWhiteSpace(actor.CollisionProfileId)
                && !string.Equals(actor.CollisionProfileId, "default", StringComparison.OrdinalIgnoreCase)
                && !_collisions.ContainsKey(actor.CollisionProfileId))
                errors.Add($"actor '{actor.Alias}' collision profile '{actor.CollisionProfileId}' was not found");
            if (!string.IsNullOrWhiteSpace(actor.BehaviorProfileId) && !_behaviors.ContainsKey(actor.BehaviorProfileId))
                errors.Add($"actor '{actor.Alias}' behavior profile '{actor.BehaviorProfileId}' was not found");
        }
        return errors;
    }

    public void ThrowIfInvalid(TestScenario scenario)
    {
        var errors = ValidateReferences(scenario);
        if (errors.Count > 0) throw new InvalidOperationException("Invalid scenario profile references: " + string.Join("; ", errors));
    }

    private static void ValidateId(string id, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Profile id is required.", parameterName);
    }
}
