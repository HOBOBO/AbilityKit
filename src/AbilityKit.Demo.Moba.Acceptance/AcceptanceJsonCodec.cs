using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using AbilityKit.Game.Test.UnitTest;

namespace AbilityKit.Demo.Moba.Acceptance;

/// <summary>
/// 验收 JSON 编解码 —— 用 System.Text.Json 替换 <c>UnityEngine.JsonUtility</c>（dotnet 侧）。
/// 与 MobaAcceptanceRunner.LoadExpectation / LoadTraceRecords、MobaAcceptanceTraceExporter.Export 的 IO 语义对齐，
/// 但不依赖 UnityEngine、不依赖 NUnit。
/// </summary>
/// <remarks>
/// 关键：MobaAcceptanceModels 的 DTO 用的是 public <b>字段</b>（field），System.Text.Json 默认只序列化属性，
/// 故必须 <see cref="JsonSerializerOptions.IncludeFields"/> = true。命名沿用 camelCase（与现有 .expected.json 一致）。
/// </remarks>
public static class AcceptanceJsonCodec
{
    public static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static MobaAcceptanceExpectation LoadExpectation(string path)
    {
        var json = File.ReadAllText(path);
        return ParseExpectation(json);
    }

    public static MobaAcceptanceExpectation ParseExpectation(string json)
        => JsonSerializer.Deserialize<MobaAcceptanceExpectation>(json, Options)
           ?? throw new InvalidDataException("failed to deserialize MobaAcceptanceExpectation");

    public static MobaAcceptanceTraceRecord[] LoadTraceRecords(string jsonlPath)
    {
        using var reader = File.OpenText(jsonlPath);
        return ReadTraceRecords(reader, allowPartial: false, out _);
    }

    internal static MobaAcceptanceTraceRecord[] ReadTraceRecords(TextReader reader, bool allowPartial, out bool malformed)
    {
        malformed = false;
        var records = new List<MobaAcceptanceTraceRecord>();
        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                records.Add(JsonSerializer.Deserialize<MobaAcceptanceTraceRecord>(line, Options)
                            ?? throw new InvalidDataException("failed to deserialize trace record"));
            }
            catch (Exception exception) when (allowPartial && exception is (JsonException or InvalidDataException))
            {
                malformed = true;
            }
        }
        return records.ToArray();
    }

    public static string SerializeSummary(MobaAcceptanceSummary summary)
        => JsonSerializer.Serialize(summary, Options);

    public static MobaAcceptanceSummary ParseSummary(string json)
        => JsonSerializer.Deserialize<MobaAcceptanceSummary>(json, Options)
           ?? throw new InvalidDataException("failed to deserialize MobaAcceptanceSummary");

    public static string Serialize(object value)
        => JsonSerializer.Serialize(value, Options);

    public static void WriteSummary(string path, MobaAcceptanceSummary summary)
        => File.WriteAllText(path, SerializeSummary(summary));
}
