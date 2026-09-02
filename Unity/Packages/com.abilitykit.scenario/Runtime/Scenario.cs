using System;
using System.Collections.Generic;

namespace AbilityKit.Scenario
{

/// <summary>
/// 玩法中立的战斗场景契约（DSL 的规范 IR）。Profile 用引用，DSL 可以描述一个世界而不内嵌载体专属的构造代码。
/// <see cref="Expectations"/> 是项目侧的断言插件（opaque），框架不解释、不序列化它——由各项目（MOBA/shooter…）定义自己的断言类型并挂载。
/// </summary>
public sealed class TestScenario
{
    public string SchemaVersion { get; init; } = "1.0";
    public string CaseId { get; init; } = string.Empty;
    public string WorldProfileId { get; init; } = "default";
    public string Carrier { get; init; } = "dotnet.console";
    public int TickRate { get; init; } = 30;
    public int Seed { get; init; }
    public int TimeoutMs { get; init; } = 30_000;
    public IReadOnlyDictionary<string, string> WorldParameters { get; init; } = new Dictionary<string, string>();
    public string? NavigationProfileId { get; init; }
    /// <summary>环境 Profile 引用（不透明字符串 id，指向 com.abilitykit.environment 的 EnvironmentProfileCatalog）。</summary>
    public string? EnvironmentProfileId { get; init; }
    public IReadOnlyList<TestObstacle> Obstacles { get; init; } = Array.Empty<TestObstacle>();
    public IReadOnlyList<TestActor> Actors { get; init; } = Array.Empty<TestActor>();
    public IReadOnlyList<TestSetupAction> Setup { get; init; } = Array.Empty<TestSetupAction>();
    public IReadOnlyList<TestTimelineStep> Timeline { get; init; } = Array.Empty<TestTimelineStep>();
    public IReadOnlyList<TestCommand> Commands { get; init; } = Array.Empty<TestCommand>();
    public IReadOnlyList<TestNetworkWatch> NetworkWatches { get; init; } = Array.Empty<TestNetworkWatch>();

    /// <summary>项目侧的断言插件（opaque）。例如 MOBA 的 TestExpectations；框架不解释它。</summary>
    public object? Expectations { get; init; }
}

public sealed class TestObstacle
{
    public string Id { get; init; } = string.Empty;
    public string Shape { get; init; } = "box";
    public TestVector3 Position { get; init; }
    public TestVector3 Size { get; init; } = new(1, 1, 1);
    public string CollisionProfileId { get; init; } = "default";
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

public sealed class TestCommand
{
    public int AtMs { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ActorAlias { get; init; }
    public string? TargetAlias { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
}

public sealed class TestNetworkWatch
{
    public string Name { get; init; } = string.Empty;
    public string Direction { get; init; } = "both";
    public string? ActorAlias { get; init; }
    public string? Property { get; init; }
    public string? Comparator { get; init; }
    public string? ExpectedValue { get; init; }
}

public sealed class TestActor
{
    public string Alias { get; init; } = string.Empty;
    public string? PlayerId { get; init; }
    public string Archetype { get; init; } = "unit";
    public string? BehaviorProfileId { get; init; }
    public string CollisionProfileId { get; init; } = "default";
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public int TeamId { get; init; }
    public int HeroId { get; init; }
    public int AttributeTemplateId { get; init; }
    public int[] SkillIds { get; init; } = Array.Empty<int>();
    public TestVector3? Position { get; init; }
    public TestVector3? Facing { get; init; }
}

public sealed class TestSetupAction
{
    public string Action { get; init; } = string.Empty;
    public string? ActorAlias { get; init; }
    public string? Property { get; init; }
    public double Value { get; init; }
}

public sealed class TestTimelineStep
{
    public int AtMs { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? ActorAlias { get; init; }
    public string? TargetAlias { get; init; }
    public int Slot { get; init; }
    public TestVector3? Position { get; init; }
    public TestVector3? Direction { get; init; }
}

/// <summary>载体中立的 3D 向量（原为 record struct，为 Unity C# 兼容改为普通 readonly struct）。</summary>
public readonly struct TestVector3
{
    public TestVector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; }
    public float Y { get; }
    public float Z { get; }
}

}
