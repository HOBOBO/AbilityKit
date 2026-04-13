using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Share.Impl.Moba.Struct;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Core.Math;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.World.ECS;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AbilityKit.Game.Flow
{
    public sealed class BattleInputFeature : IGamePhaseFeature
    {
        private BattleContext _ctx;
        private float _inputDiagCooldown;

        private float _lastMoveDx;
        private float _lastMoveDz;

        private int _moveStopRepeatTicks;

        public void OnAttach(in GamePhaseContext ctx)
        {
            ctx.Root.TryGetRef(out _ctx);
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            _ctx = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime)
        {
            if (_ctx == null || _ctx.Session == null) return;

            if (_ctx.Plan.EnableInputReplay) return;

            var plan = _ctx.Plan;
            var playerId = new PlayerId(string.IsNullOrEmpty(plan.PlayerId) ? "p1" : plan.PlayerId);
            var worldId = new AbilityKit.Ability.World.Abstractions.WorldId(string.IsNullOrEmpty(plan.WorldId) ? "room_1" : plan.WorldId);

            _ctx.LocalInputQueue ??= new BattleLocalInputQueue();

            float dx;
            float dz;
            if (_ctx.HudHasMove)
            {
                dx = _ctx.HudMoveDx;
                dz = _ctx.HudMoveDz;
            }
            else
            {
                GetMoveInput(out dx, out dz);
            }
            var wasMoving = Math.Abs(_lastMoveDx) > 0.0001f || Math.Abs(_lastMoveDz) > 0.0001f;
            var isMoving = Math.Abs(dx) > 0.0001f || Math.Abs(dz) > 0.0001f;

            if (isMoving || (wasMoving && !isMoving))
            {
                var payload = MobaMoveCodec.Serialize(dx, dz);
                var cmd = new PlayerInputCommand(new FrameIndex(_ctx.LastFrame + 1), playerId, (int)MobaOpCode.Move, payload);
                _ctx.InputRecordWriter?.Append(in cmd);
                _ctx.Session.SubmitInput(new SubmitInputRequest(worldId, cmd));
                _ctx.LocalInputQueue.Enqueue(new LocalPlayerInputEvent(playerId, (int)MobaOpCode.Move, payload));

                if (!isMoving && wasMoving)
                {
                    _moveStopRepeatTicks = 2;
                }

                _lastMoveDx = dx;
                _lastMoveDz = dz;
            }
            else
            {
                _lastMoveDx = dx;
                _lastMoveDz = dz;

                if (_moveStopRepeatTicks > 0)
                {
                    _moveStopRepeatTicks--;
                    var payload = MobaMoveCodec.Serialize(0f, 0f);
                    var cmd = new PlayerInputCommand(new FrameIndex(_ctx.LastFrame + 1), playerId, (int)MobaOpCode.Move, payload);
                    _ctx.InputRecordWriter?.Append(in cmd);
                    _ctx.Session.SubmitInput(new SubmitInputRequest(worldId, cmd));
                    _ctx.LocalInputQueue.Enqueue(new LocalPlayerInputEvent(playerId, (int)MobaOpCode.Move, payload));
                }

                _inputDiagCooldown -= deltaTime;
                if (_inputDiagCooldown <= 0f)
                {
                    _inputDiagCooldown = 1f;
                }
            }

            if (GetSkillKeyDown(out var slot))
            {
                var op = slot == 1 ? (int)MobaOpCode.Skill1 : slot == 2 ? (int)MobaOpCode.Skill2 : (int)MobaOpCode.Skill3;
                var cmd = new PlayerInputCommand(new FrameIndex(_ctx.LastFrame + 1), playerId, op, Array.Empty<byte>());
                _ctx.InputRecordWriter?.Append(in cmd);
                _ctx.Session.SubmitInput(new SubmitInputRequest(worldId, cmd));
                _ctx.LocalInputQueue.Enqueue(new LocalPlayerInputEvent(playerId, op, Array.Empty<byte>()));
            }

            var hudSlot = _ctx.HudSkillClickSlot;
            if (hudSlot > 0)
            {
                var op = hudSlot == 1 ? (int)MobaOpCode.Skill1 : hudSlot == 2 ? (int)MobaOpCode.Skill2 : (int)MobaOpCode.Skill3;
                var cmd = new PlayerInputCommand(new FrameIndex(_ctx.LastFrame + 1), playerId, op, Array.Empty<byte>());
                _ctx.InputRecordWriter?.Append(in cmd);
                _ctx.Session.SubmitInput(new SubmitInputRequest(worldId, cmd));
                _ctx.LocalInputQueue.Enqueue(new LocalPlayerInputEvent(playerId, op, Array.Empty<byte>()));
                _ctx.HudSkillClickSlot = 0;
            }

            if (_ctx.HudSkillAimSubmit && _ctx.HudSkillAimSubmitSlot > 0)
            {
                var slot2 = _ctx.HudSkillAimSubmitSlot;
                var aimDx = _ctx.HudSkillAimSubmitDx;
                var aimDz = _ctx.HudSkillAimSubmitDz;

                var aimPos = new Vec3(aimDx, 0f, aimDz);
                var aimDir = new Vec3(aimDx, 0f, aimDz);
                var evt = new SkillInputEvent(slot: slot2, phase: SkillInputPhase.Release, aimPos: in aimPos, aimDir: in aimDir);
                var payload = SkillInputCodec.Serialize(in evt);

                var cmd = new PlayerInputCommand(new FrameIndex(_ctx.LastFrame + 1), playerId, (int)MobaOpCode.SkillInput, payload);
                _ctx.InputRecordWriter?.Append(in cmd);
                _ctx.Session.SubmitInput(new SubmitInputRequest(worldId, cmd));
                _ctx.LocalInputQueue.Enqueue(new LocalPlayerInputEvent(playerId, (int)MobaOpCode.SkillInput, payload));

                _ctx.HudSkillAimSubmit = false;
                _ctx.HudSkillAimSubmitSlot = 0;
                _ctx.HudSkillAimSubmitDx = 0f;
                _ctx.HudSkillAimSubmitDz = 0f;
            }

            _ctx.LocalInputQueue.Flush();
        }

        private static bool GetSkillKeyDown(out int slot)
        {
            slot = 0;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.jKey.wasPressedThisFrame) { slot = 1; return true; }
                if (kb.kKey.wasPressedThisFrame) { slot = 2; return true; }
                if (kb.lKey.wasPressedThisFrame) { slot = 3; return true; }
                return false;
            }
#endif

            if (Input.GetKeyDown(KeyCode.J)) { slot = 1; return true; }
            if (Input.GetKeyDown(KeyCode.K)) { slot = 2; return true; }
            if (Input.GetKeyDown(KeyCode.L)) { slot = 3; return true; }
            return false;
        }

        private static void GetMoveInput(out float dx, out float dz)
        {
            dx = 0f;
            dz = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) dx -= 1f;
                if (kb.dKey.isPressed) dx += 1f;
                if (kb.wKey.isPressed) dz += 1f;
                if (kb.sKey.isPressed) dz -= 1f;
                return;
            }
#endif

            if (Input.GetKey(KeyCode.A)) dx -= 1f;
            if (Input.GetKey(KeyCode.D)) dx += 1f;
            if (Input.GetKey(KeyCode.W)) dz += 1f;
            if (Input.GetKey(KeyCode.S)) dz -= 1f;
        }
    }
}
