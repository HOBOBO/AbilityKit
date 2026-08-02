using System;
using System.Collections.Generic;
using AbilityKit.Ability.World.Services;
using AbilityKit.Core.Logging;
using AbilityKit.Core.Mathematics;

namespace AbilityKit.Demo.Moba.Services
{
    public sealed class SkillRunnerRegistry
    {
        private readonly Dictionary<int, SkillPipelineRunner> _runners = new();
        private readonly IWorldClock _clock;
        private readonly IMobaBattleDiagnosticsService _diagnostics;
        private readonly IMobaBattleExceptionPolicy _exceptions;
        private readonly ISkillLogger _skillLogger;

        public SkillRunnerRegistry(
            IWorldClock clock,
            IMobaBattleDiagnosticsService diagnostics,
            IMobaBattleExceptionPolicy exceptions,
            ISkillLogger skillLogger)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _diagnostics = diagnostics;
            _exceptions = exceptions;
            _skillLogger = skillLogger;
        }

        public SkillPipelineRunner GetOrCreate(int actorId)
        {
            if (!_runners.TryGetValue(actorId, out var runner) || runner == null)
            {
                runner = new SkillPipelineRunner(actorId, _diagnostics, _exceptions, _skillLogger);
                _runners[actorId] = runner;
            }

            return runner;
        }

        public bool TryGetLatestRunningBySlot(int actorId, int slot, out SkillPipelineRunner.RunningSnapshot snapshot)
        {
            snapshot = default;
            if (actorId <= 0 || slot <= 0)
            {
                return false;
            }

            return _runners.TryGetValue(actorId, out var runner) && runner != null && runner.TryGetLatestRunningBySlot(slot, out snapshot);
        }

        public bool TryGetRunningByInstanceId(int actorId, long instanceId, out SkillPipelineRunner.RunningSnapshot snapshot)
        {
            snapshot = default;
            if (actorId <= 0 || instanceId == 0L)
            {
                return false;
            }

            return _runners.TryGetValue(actorId, out var runner) && runner != null && runner.TryGetRunningByInstanceId(instanceId, out snapshot);
        }

        public bool TryUpdateRunningInput(int actorId, int slot, in Vec3 aimPos, in Vec3 aimDir, long targetActorId)
        {
            if (actorId <= 0 || slot <= 0)
            {
                return false;
            }

            return _runners.TryGetValue(actorId, out var runner) && runner != null && runner.UpdateInputBySlot(slot, in aimPos, in aimDir, ToActorId(targetActorId));
        }

        public bool TryUpdateRunningInputAndRelease(int actorId, int slot, in Vec3 aimPos, in Vec3 aimDir, long targetActorId)
        {
            if (!TryUpdateRunningInput(actorId, slot, in aimPos, in aimDir, targetActorId))
            {
                return false;
            }

            return _runners.TryGetValue(actorId, out var runner) && runner != null && runner.MarkReleaseBySlot(slot);
        }

        public bool TryCancelBySlot(int actorId, int slot)
        {
            if (actorId <= 0 || slot <= 0)
            {
                return false;
            }

            return _runners.TryGetValue(actorId, out var runner) && runner != null && runner.CancelBySlot(slot);
        }

        public void CancelBySkillId(int actorId, int skillId)
        {
            if (actorId <= 0 || skillId <= 0)
            {
                return;
            }

            if (_runners.TryGetValue(actorId, out var runner) && runner != null)
            {
                runner.CancelBySkillId(skillId);
            }
        }

        public void CancelAll(int actorId)
        {
            if (actorId <= 0) return;
            if (_runners.TryGetValue(actorId, out var runner) && runner != null)
            {
                runner.CancelAll();
            }
        }

        public bool CancelAndRemove(int actorId, MobaSkillRuntimeEndReason reason)
        {
            if (actorId <= 0) return false;
            if (!_runners.TryGetValue(actorId, out var runner)) return false;

            runner?.CancelAll(reason);
            return _runners.Remove(actorId);
        }

        public void Step(int actorId)
        {
            if (actorId <= 0)
            {
                return;
            }

            if (!_runners.TryGetValue(actorId, out var runner) || runner == null)
            {
                return;
            }

            var dt = _clock.DeltaTime;
            if (dt <= 0f)
            {
                if (_diagnostics != null)
                {
                    _diagnostics.Warning(
                        "skill.runner.invalidDeltaTime",
                        () => $"[SkillRunnerRegistry] Step skipped: deltaTime={dt:0.####}, actor={actorId}, hasRunning={runner.HasRunning}");
                }
                else
                {
                    Log.Warning($"[SkillRunnerRegistry] Step skipped: deltaTime={dt:0.####}, actor={actorId}, hasRunning={runner.HasRunning}");
                }

                return;
            }

            runner.Step(dt);
        }

        public void FillRunningSnapshots(int actorId, List<SkillPipelineRunner.RunningSnapshot> buffer)
        {
            if (actorId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actorId));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!_runners.TryGetValue(actorId, out var runner) || runner == null)
            {
                buffer.Clear();
                return;
            }

            runner.FillRunningSnapshots(buffer);
        }

        public void FillEndedSnapshots(int actorId, List<SkillPipelineRunner.RunningSnapshot> buffer)
        {
            if (actorId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(actorId));
            }

            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (!_runners.TryGetValue(actorId, out var runner) || runner == null)
            {
                buffer.Clear();
                return;
            }

            runner.FillEndedSnapshots(buffer);
        }

        private static int ToActorId(long actorId)
        {
            return actorId > 0 && actorId <= int.MaxValue ? (int)actorId : 0;
        }

        public void Clear()
        {
            _runners.Clear();
        }

        public void Dispose()
        {
            foreach (var kv in _runners)
            {
                kv.Value?.CancelAll();
            }

            _runners.Clear();
        }
    }
}
