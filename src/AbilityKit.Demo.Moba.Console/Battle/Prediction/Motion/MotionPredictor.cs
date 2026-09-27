using System;
using System.Collections.Generic;
using Prediction = AbilityKit.Ability.StateSync.Prediction;
using Frame = AbilityKit.Ability.StateSync.Frame;
using Vector3 = AbilityKit.Ability.StateSync.Vector3;
using IInputCommand = AbilityKit.Ability.StateSync.IInputCommand;
using IPredictionHandler = AbilityKit.Ability.StateSync.Prediction.IPredictionHandler;
using StateSlots = AbilityKit.Ability.StateSync.Prediction.StateSlots;
using PredictionStrategy = AbilityKit.Ability.StateSync.PredictionStrategy;
using PredictionResult = AbilityKit.Ability.StateSync.PredictionResult;
using MoveInput = AbilityKit.Demo.Moba.Share.Prediction.MobaMovePredictionInput;
using SlotNames = AbilityKit.Demo.Moba.Share.Prediction.MobaPredictionSlotNames;

namespace AbilityKit.Demo.Moba.Console.Battle.Prediction.Motion;

/// <summary>
/// 表现层运动预测器
/// 根据服务器下发的 MotionDescriptor 在本地重建运动计算
/// </summary>
public sealed class MotionPredictor
{
    /// <summary>
    /// 帧时间
    /// </summary>
    public float FrameTime { get; set; } = 1.0f / 30.0f;

    /// <summary>
    /// 当前活跃的运动描述
    /// </summary>
    private readonly List<MotionDescriptor> _activeMotions = new();

    /// <summary>
    /// 起始位置（快照）
    /// </summary>
    private Vector3 _basePosition;

    /// <summary>
    /// 当前预测位置
    /// </summary>
    public Vector3 CurrentPosition { get; private set; }

    /// <summary>
    /// 当前朝向
    /// </summary>
    public Vector3 CurrentForward { get; private set; } = new(0, 0, 1);

    /// <summary>
    /// 是否有活跃运动
    /// </summary>
    public bool HasActiveMotion => _activeMotions.Count > 0;

    /// <summary>
    /// 初始化
    /// </summary>
    public void Initialize(Vector3 position, Vector3 forward)
    {
        _basePosition = position;
        CurrentPosition = position;
        CurrentForward = forward;
        _activeMotions.Clear();
    }

    /// <summary>
    /// 添加运动描述
    /// </summary>
    public void AddMotion(MotionDescriptor descriptor)
    {
        descriptor.StartFrame = descriptor.StartFrame; // 确保已设置
        _activeMotions.Add(descriptor);

        // 如果是平移运动，更新朝向
        if (descriptor is LocomotionDescriptor loco)
        {
            CurrentForward = new Vector3(loco.ForwardX, 0, loco.ForwardZ);
        }
    }

    /// <summary>
    /// 预测指定帧的位置
    /// </summary>
    public Vector3 PredictPosition(Frame frame)
    {
        var pos = _basePosition;
        var forward = CurrentForward;

        foreach (var motion in _activeMotions)
        {
            if (motion.IsFinished(frame))
                continue;

            switch (motion)
            {
                case LocomotionDescriptor loco:
                    pos = loco.ComputePosition(pos, frame, FrameTime);
                    forward = new Vector3(loco.ForwardX, 0, loco.ForwardZ);
                    break;

                case PathFollowerDescriptor path:
                    pos = path.ComputePosition(pos, frame, FrameTime);
                    break;

                case DashDescriptor dash:
                    pos = dash.ComputePosition(pos, frame, FrameTime);
                    break;
            }
        }

        return pos;
    }

    /// <summary>
    /// 推进一帧
    /// </summary>
    public void Tick(Frame frame)
    {
        // 更新当前帧位置
        CurrentPosition = PredictPosition(frame);

        // 清理已结束的运动
        _activeMotions.RemoveAll(m => m.IsFinished(frame));
    }

    /// <summary>
    /// 从服务器快照更新基准位置
    /// </summary>
    public void SyncFromServer(Vector3 serverPosition, Frame serverFrame)
    {
        // 计算预测位置和实际位置的差异
        var predictedPos = PredictPosition(serverFrame);

        // 如果预测偏差很大，切换到服务器位置
        var dx = predictedPos.X - serverPosition.X;
        var dz = predictedPos.Z - serverPosition.Z;
        var distError = MathF.Sqrt(dx * dx + dz * dz);

        if (distError > 0.5f)
        {
            // 预测失败，使用服务器位置作为基准
            _basePosition = serverPosition;
            CurrentPosition = serverPosition;

            // 清理所有运动（需要重新同步）
            _activeMotions.Clear();
        }
    }

    /// <summary>
    /// 清除所有运动
    /// </summary>
    public void Clear()
    {
        _activeMotions.Clear();
    }
}

/// <summary>
/// 运动预测处理器
/// 集成到预测系统中
/// </summary>
public sealed class MotionPredictionHandler : IPredictionHandler
{
    public string Name => "Motion";
    public PredictionStrategy Strategy => PredictionStrategy.OptimisticWithRollback;
    public System.Collections.Generic.IReadOnlyList<string> RequiredSlots => new[] { SlotNames.Position, SlotNames.Velocity };

    private readonly MotionPredictor _predictor = new();
    private readonly float _frameTime;

    public MotionPredictionHandler(float frameTime = 1.0f / 30.0f)
    {
        _frameTime = frameTime;
        _predictor.FrameTime = frameTime;
    }

    public void Predict(IInputCommand input, StateSlots slots, Frame frame)
    {
        // 从槽位获取当前位置
        var currentPos = slots.GetPosition(SlotNames.Position);
        var currentFwd = slots.GetQuaternion(SlotNames.Rotation);

        // 初始化预测器（如果需要）
        if (!_predictor.HasActiveMotion)
        {
            _predictor.Initialize(currentPos, new Vector3(0, 0, 1));
        }

        // 根据输入类型添加/更新运动
        if (input is MoveInput move)
        {
            var loco = new LocomotionDescriptor
            {
                StartFrame = frame.Value,
                InputX = move.VelocityX,
                InputZ = move.VelocityZ,
                Speed = 5.0f, // 应该从配置获取
                ForwardX = MathF.Sin(move.Rotation),
                ForwardZ = MathF.Cos(move.Rotation),
                DurationFrames = 1 // 每帧重置
            };

            // 更新现有运动或添加新的
            var existingIndex = _predictor.HasActiveMotion ? 0 : -1;
            _predictor.AddMotion(loco);
        }

        // 推进预测
        _predictor.Tick(frame);

        // 更新槽位
        var predictedPos = _predictor.CurrentPosition;
        slots.Set(SlotNames.Position, predictedPos);

        // 导出速度
        var dx = predictedPos.X - currentPos.X;
        var dz = predictedPos.Z - currentPos.Z;
        slots.Set(SlotNames.Velocity, new Vector3(dx / _frameTime, 0, dz / _frameTime));
    }

    public PredictionResult Validate(StateSlots predicted, StateSlots server)
    {
        var predPos = predicted.GetPosition(SlotNames.Position);
        var servPos = server.GetPosition(SlotNames.Position);

        var dx = predPos.X - servPos.X;
        var dz = predPos.Z - servPos.Z;
        var distError = MathF.Sqrt(dx * dx + dz * dz);

        if (distError < 0.1f) return PredictionResult.Ok();
        if (distError < 0.5f) return PredictionResult.Minor($"dist={distError:F2}");
        if (distError < 2.0f) return PredictionResult.Major($"dist={distError:F2}");
        return PredictionResult.Critical($"dist={distError:F2}");
    }

    public void ApplyServerState(StateSlots server, StateSlots current)
    {
        var servPos = server.GetPosition(SlotNames.Position);
        var servVel = server.GetPosition(SlotNames.Velocity);

        current.Set(SlotNames.Position, servPos);
        current.Set(SlotNames.Velocity, servVel);

        // 同步预测器
        _predictor.SyncFromServer(servPos, new Frame(0));
    }

    /// <summary>
    /// 获取运动预测器
    /// </summary>
    public MotionPredictor GetPredictor() => _predictor;
}
