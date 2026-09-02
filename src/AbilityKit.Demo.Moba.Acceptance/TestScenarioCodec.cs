using System.Text.Json;
using AbilityKit.Scenario;

namespace AbilityKit.Demo.Moba.Acceptance;

/// <summary>玩法中立场景 IR 的 STJ 编解码（Expectations 是 opaque，往返测试不含断言插件）。</summary>
public static class TestScenarioCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static TestScenario Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Scenario JSON is empty.", nameof(json));
        var scenario = JsonSerializer.Deserialize<TestScenario>(json, Options)
                       ?? throw new InvalidDataException("Scenario JSON did not contain an object.");
        TestScenarioValidator.ThrowIfInvalid(scenario);
        return scenario;
    }

    public static TestScenario Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllText(path));
    }

    public static string Serialize(TestScenario scenario)
    {
        TestScenarioValidator.ThrowIfInvalid(scenario);
        return JsonSerializer.Serialize(scenario, Options);
    }
}
