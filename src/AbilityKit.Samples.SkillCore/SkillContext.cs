using System.Collections.Generic;
using AbilityKit.Pipeline;

namespace AbilityKit.Samples.SkillCore;

/// <summary>
/// 技能管线上下文：实现 <see cref="IAbilityPipelineContext"/> 的最小载体，
/// 阶段之间通过 SharedData 传递施法者 / 目标 / 事件发布委托。
/// </summary>
public sealed class SkillContext : IAbilityPipelineContext
{
    private readonly Dictionary<string, object?> _sharedData = new();

    public object? AbilityInstance { get; set; }

    public Dictionary<string, object?> SharedData => _sharedData;

    public AbilityPipelinePhaseId CurrentPhaseId { get; set; }

    public EAbilityPipelineState PipelineState { get; set; }

    public bool IsAborted { get; set; }

    public bool IsPaused { get; set; }

    public float StartTime { get; set; }

    public float ElapsedTime { get; set; }

    public T GetData<T>(string key, T defaultValue = default!)
        => _sharedData.TryGetValue(key, out var value) && value is T typed ? typed : defaultValue;

    public void SetData<T>(string key, T value) => _sharedData[key] = value;

    public bool TryGetData<T>(string key, out T value)
    {
        if (_sharedData.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    public bool RemoveData(string key) => _sharedData.Remove(key);

    public void ClearData() => _sharedData.Clear();

    public void Reset()
    {
        CurrentPhaseId = default;
        PipelineState = EAbilityPipelineState.Ready;
        IsAborted = false;
        IsPaused = false;
        StartTime = 0;
        ElapsedTime = 0;
        _sharedData.Clear();
    }
}

/// <summary>管线配置的最小实现（Samples 里的 DefaultAbilityPipelineConfig 不随 asmdef 分发，接入方自备）。</summary>
public sealed class SkillPipelineConfig : IAbilityPipelineConfig
{
    public int ConfigId => 0;

    public string ConfigName => "SkillCore";

    public IReadOnlyList<IAbilityPhaseConfig> PhaseConfigs => _phaseConfigs;

    public bool AllowInterrupt => true;

    public bool AllowPause => true;

    private readonly List<IAbilityPhaseConfig> _phaseConfigs = new();

    public SkillPipelineConfig()
    {
    }
}
