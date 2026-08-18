using MemoryPack;
using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Console.Battle.Context;
using AbilityKit.Demo.Moba.Console.Battle.Features;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba;
using PlayerId = AbilityKit.Ability.Host.PlayerId;
using Platform = AbilityKit.Demo.Moba.Console.Platform;
using ECSComponents = AbilityKit.Demo.Moba.Console.Battle.ECS.Components;
using AbilityKit.Demo.Moba.Input;

namespace AbilityKit.Demo.Moba.Console.Battle.Input
{
    /// <summary>
    /// 输入特征模块（表现层）
    ///
    /// 职责边界：
    /// - 采集玩家输入
    /// - 通过 IWorldInputSink 转发输入到逻辑层
    /// - 不执行技能逻辑
    /// - 不计算伤害
    /// - 不直接调用 Session
    ///
    /// 架构说明：
    /// - 表现层持有 IWorldInputSink（通过 DI 或手动注入）
    /// - Sink 实现决定输入传输方式（DirectCall/FrameSync）
    /// - 逻辑层处理后发布事件，表现层通过 ConsoleViewEventSink 订阅
    ///
    /// 与 Unity 对齐：
    /// - Unity InputFeature 通过 IInputSink 与逻辑层解耦
    /// - Console InputFeature 通过 IWorldInputSink 与模拟层解耦
    /// </summary>
    public sealed class ConsoleInputFeature : ConsoleSubFeatureBase, IInputFeature, IPlatformInputSource
    {
        protected override string GetSubFeatureId() => "console_input_feature";
        protected override string[] GetSubFeatureDependencies() => new[] { "console_sync_feature" };

        private IWorldInputSink _inputSink;
        private BattleLocalInputQueue _inputQueue;

        private float _lastMoveDx;
        private float _lastMoveDz;
        private bool _wasMoving;

        public int LocalActorId => Context?.LocalActorId ?? 0;

        /// <summary>
        /// 设置输入转发表层（表现层持有）
        /// </summary>
        public void SetInputSink(IWorldInputSink sink)
        {
            _inputSink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public override void OnAttach(ConsoleBattleContext ctx)
        {
            base.OnAttach(ctx);
            _inputQueue = new BattleLocalInputQueue();
            Platform.Log.Input($"[Input] Attached - PlayerId: {ctx.LocalActorId}");
        }

        public override void OnDetach(ConsoleBattleContext ctx)
        {
            _inputQueue = null;
        }

        public override void Tick(ConsoleBattleContext ctx, float deltaTime)
        {
            if (Context == null || ctx.State != BattleState.InMatch)
            {
                return;
            }
            ProcessInput();
        }

        public void ProcessInput()
        {
            if (Context == null || _inputQueue == null)
            {
                return;
            }

            ProcessMoveInput();
            ProcessSkillInput();

            _inputQueue.Flush();
        }

        private void ProcessMoveInput()
        {
            var dx = Context.HudMoveDx;
            var dz = Context.HudMoveDz;
            var isMoving = Math.Abs(dx) > 0.01f || Math.Abs(dz) > 0.01f;

            if (isMoving || _wasMoving)
            {
                var payload = MobaMoveCodec.Serialize(dx, dz);
                var cmd = new PlayerInputCommand(
                    new FrameIndex(Context.LastFrame),
                    GetLocalPlayerId(),
                    MobaOpCodes.Input.Move,
                    payload);

                SubmitToSink(new FrameIndex(Context.LastFrame), cmd);
                _inputQueue.Enqueue(new LocalPlayerInputEvent(Context.LocalActorId, MobaOpCodes.Input.Move, payload));

                Platform.Log.Input($"[Input] Move: dx={dx:F2}, dz={dz:F2}");
            }

            _wasMoving = isMoving;
            _lastMoveDx = dx;
            _lastMoveDz = dz;
        }

        private void ProcessSkillInput()
        {
            var slot = Context.HudSkillClickSlot;
            if (slot > 0)
            {
                var payload = ConsoleSkillInputCodec.Serialize(slot, SkillInputPhase.Press);
                var cmd = new PlayerInputCommand(
                    new FrameIndex(Context.LastFrame),
                    GetLocalPlayerId(),
                    MobaOpCodes.Input.SkillInput,
                    payload);

                SubmitToSink(new FrameIndex(Context.LastFrame), cmd);
                Platform.Log.Input($"[Input] Skill{slot} pressed");
                Context.HudSkillClickSlot = 0;
            }

            if (Context.HudSkillAimSubmit && Context.HudSkillAimSubmitSlot > 0)
            {
                var aimX = Context.HudSkillAimSubmitDx;
                var aimZ = Context.HudSkillAimSubmitDz;
                var aimPos = new Vec3(aimX, 0, aimZ);
                var payload = ConsoleSkillInputCodec.Serialize(Context.HudSkillAimSubmitSlot, SkillInputPhase.Release, aimPos);

                var cmd = new PlayerInputCommand(
                    new FrameIndex(Context.LastFrame),
                    GetLocalPlayerId(),
                    MobaOpCodes.Input.SkillInput,
                    payload);

                SubmitToSink(new FrameIndex(Context.LastFrame), cmd);
                Platform.Log.Input($"[Input] Skill{Context.HudSkillAimSubmitSlot} aim released at ({aimX:F1}, {aimZ:F1})");
                Context.HudSkillAimSubmit = false;
            }
        }

        private PlayerId GetLocalPlayerId()
        {
            var playerId = Context?.Plan.PlayerId;
            if (!string.IsNullOrEmpty(playerId))
            {
                return new PlayerId(playerId);
            }

            return new PlayerId(Context?.LocalActorId.ToString() ?? string.Empty);
        }

        private void SubmitToSink(FrameIndex frame, PlayerInputCommand command)
        {
            if (_inputSink == null)
            {
                Platform.Log.Input($"[Input] Submit skipped: input sink is not set. OpCode={command.OpCode}");
                return;
            }

            _inputSink.Submit(frame, new[] { command });
        }

        public void SetMoveInput(float dx, float dz)
        {
            if (Context == null) return;
            Context.HudMoveDx = dx;
            Context.HudMoveDz = dz;
            Context.HudHasMove = Math.Abs(dx) > 0.01f || Math.Abs(dz) > 0.01f;
        }

        public void ApplyIntent(in MobaActorIntent intent)
        {
            if (!intent.HasFiniteMovement())
            {
                SetMoveInput(0f, 0f);
                return;
            }

            if (intent.MovementKind == MobaActorMovementIntentKind.Direction)
                SetMoveInput(intent.MoveX, intent.MoveZ);
            else if (intent.MovementKind == MobaActorMovementIntentKind.Hold)
                SetMoveInput(0f, 0f);

            if (intent.HasCast && intent.SkillSlot > 0) ClickSkill(intent.SkillSlot);
        }

        public void ClickSkill(int slot)
        {
            if (Context == null) return;
            Context.HudSkillClickSlot = slot;
        }

        public void AimSkill(int slot, float dx, float dz)
        {
            if (Context == null) return;
            Context.HudSkillAiming = true;
            Context.HudSkillAimSlot = slot;
            Context.HudSkillAimDx = dx;
            Context.HudSkillAimDz = dz;
        }

        public void ReleaseSkillAim(int slot, float dx, float dz)
        {
            if (Context == null) return;
            Context.HudSkillAimSubmit = true;
            Context.HudSkillAimSubmitSlot = slot;
            Context.HudSkillAimSubmitDx = dx;
            Context.HudSkillAimSubmitDz = dz;
        }

        #region IPlatformInputSource 实现

        bool IPlatformInputSource.IsEnabled
        {
            get => Context != null;
            set { }
        }

        void IPlatformInputSource.Update()
        {
            ProcessInput();
        }

        (float x, float z) IPlatformInputSource.GetMoveInput()
        {
            if (Context == null) return (0, 0);
            return (Context.HudMoveDx, Context.HudMoveDz);
        }

        bool IPlatformInputSource.IsAttackPressed() => false;

        int IPlatformInputSource.GetSkillInput()
        {
            if (Context == null) return -1;
            var slot = Context.HudSkillClickSlot;
            Context.HudSkillClickSlot = 0;
            return slot > 0 ? slot - 1 : -1;
        }

        bool IPlatformInputSource.IsStopSkillPressed() => false;
        bool IPlatformInputSource.IsStopPressed() => false;
        (float x, float y) IPlatformInputSource.GetClickPosition() => (-1, -1);
        float IPlatformInputSource.GetCameraRotationInput() => 0f;

        void IPlatformInputSource.Reset()
        {
            if (Context == null) return;
            Context.HudMoveDx = 0;
            Context.HudMoveDz = 0;
            Context.HudHasMove = false;
            Context.HudSkillClickSlot = 0;
            Context.HudSkillAiming = false;
            Context.HudSkillAimSubmit = false;
        }

        #endregion
    }

    /// <summary>
    /// 本地玩家输入事件
    /// </summary>
    public readonly struct LocalPlayerInputEvent
    {
        public int PlayerId { get; init; }
        public int OpCode { get; init; }
        public byte[] Payload { get; init; }

        public LocalPlayerInputEvent(int playerId, int opCode, byte[] payload)
        {
            PlayerId = playerId;
            OpCode = opCode;
            Payload = payload;
        }
    }

    /// <summary>
    /// 本地输入队列
    /// </summary>
    public sealed class BattleLocalInputQueue
    {
        private readonly Queue<LocalPlayerInputEvent> _queue = new();

        public void Enqueue(LocalPlayerInputEvent evt)
        {
            _queue.Enqueue(evt);
        }

        public void Flush()
        {
            _queue.Clear();
        }

        public int Count => _queue.Count;
    }

    /// <summary>
    /// 移动编码器
    /// </summary>
    public static class MobaMoveCodec
    {
        public static byte[] Serialize(float dx, float dz)
        {
            return System.Text.Encoding.UTF8.GetBytes($"{{\"dx\":{dx:F4},\"dz\":{dz:F4}}}");
        }
    }

    /// <summary>
    /// 技能输入编码器（Console 版本）
    /// </summary>
    public static class ConsoleSkillInputCodec
    {
        public static byte[] Serialize(int slot, SkillInputPhase phase, Vec3 aimPos = default)
        {
            var evt = new SkillInputEvent(slot, phase, aimPos: in aimPos);
            return MemoryPackSerializer.Serialize(evt);
        }
    }
}
