using System;
using System.Collections.Generic;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Prediction;
using SlotNames = AbilityKit.Demo.Moba.Share.Prediction.MobaPredictionSlotNames;
using IInputCommand = AbilityKit.Ability.StateSync.IInputCommand;
using IPredictionHandler = AbilityKit.Ability.StateSync.Prediction.IPredictionHandler;
using IReadOnlyListSystem = System.Collections.Generic.IReadOnlyList<string>;
using StateSlots = AbilityKit.Ability.StateSync.Prediction.StateSlots;
using Frame = AbilityKit.Ability.StateSync.Frame;
using PredictionStrategy = AbilityKit.Ability.StateSync.PredictionStrategy;
using PredictionResult = AbilityKit.Ability.StateSync.PredictionResult;
using SharedSkillInput = AbilityKit.Demo.Moba.Share.Prediction.MobaSkillPredictionInput;
using SharedSkillHandler = AbilityKit.Demo.Moba.Share.Prediction.MobaSkillPredictionHandler;

namespace AbilityKit.Demo.Moba.Console.Battle.Prediction.Handlers;

/// <summary>
/// 技能释放输入
/// 实现 IInputCommand 接口
/// </summary>
public sealed class SkillInput : SharedSkillInput
{
    public SkillInput(int skillId, int targetId = 0)
        : base(predictionKey: 0, skillId: skillId, targetId: targetId)
    {
    }
}

/// <summary>
/// 冷却预测处理器
/// </summary>
public sealed class CooldownHandler : SharedSkillHandler
{
    public CooldownHandler(Func<int, float>? getSkillCooldown = null)
        : base(skillId => (int)Math.Ceiling(30f * (getSkillCooldown ?? DefaultCooldown)(skillId)))
    {
    }

    private static float DefaultCooldown(int id)
    {
        return id switch
        {
            1 => 5.0f,
            2 => 8.0f,
            3 => 12.0f,
            4 => 30.0f,
            _ => 10.0f
        };
    }
}

/// <summary>
/// 生命值预测处理器
/// </summary>
public sealed class HealthHandler : IPredictionHandler
{
    public string Name => "Health";
    public PredictionStrategy Strategy => PredictionStrategy.OptimisticWithRollback;
    public IReadOnlyListSystem RequiredSlots => new[] { SlotNames.Health };

    public void Predict(IInputCommand input, StateSlots slots, Frame frame)
    {
        // 生命值通常不预测，只被动接收服务器更新
    }

    public PredictionResult Validate(StateSlots predicted, StateSlots server)
    {
        var predHp = predicted.GetFloat(SlotNames.Health);
        var servHp = server.GetFloat(SlotNames.Health);

        if (Math.Abs(predHp - servHp) > 0.1f)
        {
            return PredictionResult.Critical("health mismatch - damage not predicted");
        }
        return PredictionResult.Ok();
    }

    public void ApplyServerState(StateSlots server, StateSlots current)
    {
        if (server.Has(SlotNames.Health))
            current.Set(SlotNames.Health, server.GetFloat(SlotNames.Health));
        if (server.Has(SlotNames.MaxHealth))
            current.Set(SlotNames.MaxHealth, server.GetFloat(SlotNames.MaxHealth));
    }
}
