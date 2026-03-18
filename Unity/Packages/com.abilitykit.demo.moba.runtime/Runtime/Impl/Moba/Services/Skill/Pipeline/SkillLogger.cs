using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Log;
using AbilityKit.Ability.Share.Math;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    #region Core Interfaces

    public interface ISkillLogger
    {
        ISkillLogScope Scope(int casterActorId, int skillId, long instanceId);

        void Log(SkillLogEntry entry);
        void SetLevel(SkillLogLevel level);
        void SetFilter(ISkillLogFilter filter);

        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
    }

    public interface ISkillLogScope : IDisposable
    {
        ISkillLogScope WithTarget(int targetActorId);
        ISkillLogScope WithSlot(int slot);
        ISkillLogScope WithLevel(int level);
        ISkillLogScope WithPhase(string phaseId);
        ISkillLogScope WithExtra(string key, object value);

        void Log(SkillLogEntry entry);
        void Log(SkillLogLevel level, string type, string message);
    }

    public interface ISkillLogFilter
    {
        bool ShouldLog(SkillLogEntry entry);
    }

    public enum SkillLogLevel
    {
        Off = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Debug = 4,
    }

    #endregion

    #region Log Entry

    public class SkillLogEntry
    {
        public SkillLogLevel Level { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }

        public int CasterActorId { get; set; }
        public int SkillId { get; set; }
        public long InstanceId { get; set; }
        public int TargetActorId { get; set; }
        public int SkillSlot { get; set; }
        public int SkillLevel { get; set; }
        public string PhaseId { get; set; }
        public float ElapsedMs { get; set; }

        public Dictionary<string, object> Extras { get; } = new Dictionary<string, object>();

        public string FormattedMessage { get; private set; }

        public void BuildMessage()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"[{Type}]");
            sb.Append($" Caster={CasterActorId}");
            sb.Append($" SkillId={SkillId}");
            sb.Append($" InstanceId={InstanceId}");

            if (TargetActorId > 0) sb.Append($" Target={TargetActorId}");
            if (SkillSlot > 0) sb.Append($" Slot={SkillSlot}");
            if (SkillLevel > 0) sb.Append($" Level={SkillLevel}");
            if (!string.IsNullOrEmpty(PhaseId)) sb.Append($" Phase={PhaseId}");
            if (ElapsedMs > 0) sb.Append($" Elapsed={ElapsedMs:F1}ms");

            foreach (var kvp in Extras)
            {
                sb.Append($" {kvp.Key}={kvp.Value}");
            }

            sb.Append($" | {Message}");
            FormattedMessage = sb.ToString();
        }
    }

    #endregion

    #region Default Implementation

    public sealed class SkillLogger : ISkillLogger
    {
        private static SkillLogger _instance;
        private static ISkillLogFilter _defaultFilter = new DefaultSkillLogFilter();

        private SkillLogLevel _level = SkillLogLevel.Debug;
        private ISkillLogFilter _filter = _defaultFilter;
        private readonly SkillLogSink _sink;

        public static SkillLogger Instance => _instance ??= new SkillLogger();

        private SkillLogger()
        {
            _sink = new SkillLogSink();
        }

        public static void SetInstance(SkillLogger logger)
        {
            _instance = logger;
        }

        public ISkillLogScope Scope(int casterActorId, int skillId, long instanceId)
        {
            return new SkillLogScope(this, casterActorId, skillId, instanceId);
        }

        public void Log(SkillLogEntry entry)
        {
            if (entry.Level > _level) return;
            if (_filter != null && !_filter.ShouldLog(entry)) return;

            entry.BuildMessage();
            _sink.Write(entry);
        }

        public void SetLevel(SkillLogLevel level) => _level = level;
        public void SetFilter(ISkillLogFilter filter) => _filter = filter ?? _defaultFilter;

        public SkillLogLevel GetLevel() => _level;

        public void LogInfo(string message)
        {
            var entry = new SkillLogEntry { Level = SkillLogLevel.Info, Type = "Skill", Message = message };
            entry.BuildMessage();
            _sink.Write(entry);
        }

        public void LogWarning(string message)
        {
            var entry = new SkillLogEntry { Level = SkillLogLevel.Warning, Type = "Skill", Message = message };
            entry.BuildMessage();
            _sink.Write(entry);
        }

        public void LogError(string message)
        {
            var entry = new SkillLogEntry { Level = SkillLogLevel.Error, Type = "Skill", Message = message };
            entry.BuildMessage();
            _sink.Write(entry);
        }
    }

    public sealed class SkillLogScope : ISkillLogScope
    {
        private readonly SkillLogger _logger;
        private readonly SkillLogEntry _entry;
        private bool _disposed;

        internal SkillLogScope(SkillLogger logger, int casterActorId, int skillId, long instanceId)
        {
            _logger = logger;
            _entry = new SkillLogEntry
            {
                CasterActorId = casterActorId,
                SkillId = skillId,
                InstanceId = instanceId,
            };
        }

        public ISkillLogScope WithTarget(int targetActorId)
        {
            _entry.TargetActorId = targetActorId;
            return this;
        }

        public ISkillLogScope WithSlot(int slot)
        {
            _entry.SkillSlot = slot;
            return this;
        }

        public ISkillLogScope WithLevel(int level)
        {
            _entry.SkillLevel = level;
            return this;
        }

        public ISkillLogScope WithPhase(string phaseId)
        {
            _entry.PhaseId = phaseId;
            return this;
        }

        public ISkillLogScope WithExtra(string key, object value)
        {
            _entry.Extras[key] = value;
            return this;
        }

        public void Log(SkillLogEntry entry)
        {
            _logger.Log(entry);
        }

        public void Log(SkillLogLevel level, string type, string message)
        {
            _entry.Level = level;
            _entry.Type = type;
            _entry.Message = message;
            _logger.Log(_entry);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
        }
    }

    #endregion

    #region Sink

    public class SkillLogSink
    {
        private readonly List<ISkillLogSink> _sinks = new List<ISkillLogSink>();

        public SkillLogSink()
        {
            _sinks.Add(new DefaultSkillLogSink());
        }

        public void AddSink(ISkillLogSink sink)
        {
            if (sink != null) _sinks.Add(sink);
        }

        public void Write(SkillLogEntry entry)
        {
            foreach (var sink in _sinks)
            {
                try
                {
                    sink.Write(entry);
                }
                catch { }
            }
        }
    }

    public interface ISkillLogSink
    {
        void Write(SkillLogEntry entry);
    }

    public sealed class DefaultSkillLogSink : ISkillLogSink
    {
        public void Write(SkillLogEntry entry)
        {
            switch (entry.Level)
            {
                case SkillLogLevel.Error:
                    Log.Error(entry.FormattedMessage);
                    break;
                case SkillLogLevel.Warning:
                    Log.Warning(entry.FormattedMessage);
                    break;
                case SkillLogLevel.Info:
                case SkillLogLevel.Debug:
                default:
                    Log.Info(entry.FormattedMessage);
                    break;
            }
        }
    }

    #endregion

    #region Filter

    public class DefaultSkillLogFilter : ISkillLogFilter
    {
        public virtual bool ShouldLog(SkillLogEntry entry)
        {
            return true;
        }
    }

    public sealed class CompositeSkillLogFilter : ISkillLogFilter
    {
        private readonly List<ISkillLogFilter> _filters = new List<ISkillLogFilter>();

        public CompositeSkillLogFilter Add(ISkillLogFilter filter)
        {
            _filters.Add(filter);
            return this;
        }

        public bool ShouldLog(SkillLogEntry entry)
        {
            foreach (var filter in _filters)
            {
                if (!filter.ShouldLog(entry)) return false;
            }
            return true;
        }
    }

    public class SkillTypeLogFilter : ISkillLogFilter
    {
        private readonly HashSet<string> _enabledTypes = new HashSet<string>();

        public SkillTypeLogFilter Include(params string[] types)
        {
            foreach (var t in types) _enabledTypes.Add(t);
            return this;
        }

        public bool ShouldLog(SkillLogEntry entry)
        {
            return _enabledTypes.Count == 0 || _enabledTypes.Contains(entry.Type);
        }
    }

    #endregion

    #region Convenience Extensions

    public static class SkillLoggerExtensions
    {
        public static void LogSkillStart(this ISkillLogger logger, int casterActorId, int skillId, int skillSlot, int skillLevel, int targetActorId, Vec3 aimPos, Vec3 aimDir, long instanceId)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithSlot(skillSlot).WithLevel(skillLevel).WithTarget(targetActorId)
                    .WithExtra("AimPos", $"({aimPos.X:F1},{aimPos.Y:F1},{aimPos.Z:F1})")
                    .WithExtra("AimDir", $"({aimDir.X:F1},{aimDir.Y:F1},{aimDir.Z:F1})")
                    .Log(SkillLogLevel.Info, "SkillStart", $"Started skill casting");
            }
        }

        public static void LogSkillStage(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string stage, string state)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithPhase(stage).Log(SkillLogLevel.Info, "SkillStage", state);
            }
        }

        public static void LogSkillComplete(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, float elapsedMs)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("ElapsedMs", elapsedMs).Log(SkillLogLevel.Info, "SkillComplete", "Skill completed");
            }
        }

        public static void LogSkillFail(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string reason)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("Reason", reason).Log(SkillLogLevel.Warning, "SkillFail", "Skill failed");
            }
        }

        public static void LogSkillCancel(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string cancelType)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("CancelType", cancelType).Log(SkillLogLevel.Info, "SkillCancel", "Skill cancelled");
            }
        }

        public static void LogSkillInterrupt(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string stage)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithPhase(stage).Log(SkillLogLevel.Warning, "SkillInterrupt", "Skill interrupted");
            }
        }

        public static void LogTriggerEvaluate(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string eventKey, bool passed, string extraInfo = null)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("EventKey", eventKey).WithExtra("Result", passed ? "Pass" : "Fail")
                    .Log(SkillLogLevel.Debug, "TriggerEval", extraInfo ?? (passed ? "Condition passed" : "Condition failed"));
            }
        }

        public static void LogTriggerExecute(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string eventKey, string actionType)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("EventKey", eventKey).WithExtra("Action", actionType)
                    .Log(SkillLogLevel.Info, "TriggerExec", "Trigger executed");
            }
        }

        public static void LogBuffApply(this ISkillLogger logger, int targetActorId, int sourceActorId, int buffId, int stackCount, long contextId)
        {
            using (var scope = logger.Scope(sourceActorId, buffId, contextId))
            {
                scope.WithTarget(targetActorId).WithExtra("Stacks", stackCount)
                    .Log(SkillLogLevel.Info, "BuffApply", $"Applied buff {buffId}");
            }
        }

        public static void LogBuffRemove(this ISkillLogger logger, int targetActorId, int buffId, int stackCount, long contextId, string reason)
        {
            using (var scope = logger.Scope(0, buffId, contextId))
            {
                scope.WithTarget(targetActorId).WithExtra("Stacks", stackCount).WithExtra("Reason", reason)
                    .Log(SkillLogLevel.Info, "BuffRemove", $"Removed buff {buffId}");
            }
        }

        public static void LogPassiveRegister(this ISkillLogger logger, int ownerActorId, int passiveSkillId, long contextId)
        {
            using (var scope = logger.Scope(ownerActorId, passiveSkillId, contextId))
            {
                scope.Log(SkillLogLevel.Info, "PassiveRegister", $"Registered passive skill {passiveSkillId}");
            }
        }

        public static void LogPassiveUnregister(this ISkillLogger logger, int ownerActorId, int passiveSkillId, long contextId, string reason)
        {
            using (var scope = logger.Scope(ownerActorId, passiveSkillId, contextId))
            {
                scope.WithExtra("Reason", reason)
                    .Log(SkillLogLevel.Info, "PassiveUnregister", $"Unregistered passive skill {passiveSkillId}");
            }
        }

        public static void LogPassiveTrigger(this ISkillLogger logger, int ownerActorId, int passiveSkillId, long contextId, string triggerEvent, bool triggered)
        {
            using (var scope = logger.Scope(ownerActorId, passiveSkillId, contextId))
            {
                scope.WithExtra("TriggerEvent", triggerEvent).WithExtra("Result", triggered ? "Triggered" : "Skipped")
                    .Log(SkillLogLevel.Info, "PassiveTrigger", triggered ? "Passive skill triggered" : "Passive skill condition not met");
            }
        }

        public static void LogTriggerEvent(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string eventId)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("EventId", eventId)
                    .Log(SkillLogLevel.Info, "TriggerEvent", "Event published");
            }
        }

        public static void LogSkillTick(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, float deltaTime, float elapsedTime, string state)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("DeltaTime", deltaTime.ToString("F3"))
                     .WithExtra("ElapsedTime", elapsedTime.ToString("F3"))
                     .Log(SkillLogLevel.Debug, "SkillTick", state);
            }
        }

        public static void LogSkillPhase(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string phaseId, string action)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithPhase(phaseId).Log(SkillLogLevel.Debug, "SkillPhase", action);
            }
        }

        public static void LogSkillEffect(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, int effectId, string effectType)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("EffectId", effectId).WithExtra("Type", effectType)
                    .Log(SkillLogLevel.Info, "SkillEffect", "Effect triggered");
            }
        }

        public static void LogSkillPreCheck(this ISkillLogger logger, int casterActorId, int skillId, long instanceId, string checkName, bool result)
        {
            using (var scope = logger.Scope(casterActorId, skillId, instanceId))
            {
                scope.WithExtra("Check", checkName).WithExtra("Result", result ? "Pass" : "Fail")
                    .Log(SkillLogLevel.Info, "SkillPreCheck", result ? "Check passed" : "Check failed");
            }
        }

        public static void LogSkillTarget(this ISkillLogger logger, int casterActorId, int skillId, int targetActorId, Vec3 aimPos)
        {
            using (var scope = logger.Scope(casterActorId, skillId, 0))
            {
                scope.WithTarget(targetActorId)
                    .WithExtra("AimPos", $"({aimPos.X:F1},{aimPos.Y:F1},{aimPos.Z:F1})")
                    .Log(SkillLogLevel.Debug, "SkillTarget", "Target info");
            }
        }

        #endregion
    }
}
