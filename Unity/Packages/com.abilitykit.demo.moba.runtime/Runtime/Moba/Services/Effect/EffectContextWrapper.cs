using System.Collections.Generic;
using AbilityKit.Core.Generic;
using AbilityKit.Pipeline;

namespace AbilityKit.Demo.Moba.Services
{
    using AbilityKit.Ability;
    public sealed class EffectContextWrapper : IEffectContext
    {
        private readonly IAbilityPipelineContext _inner;
        private readonly EffectContextKind _kind;

        public static IEffectContext Wrap(IAbilityPipelineContext ctx)
        {
            if (ctx == null) return null;
            if (ctx is IEffectContext ec) return ec;

            EffectContextKind kind;
            if (ctx.TryGetData(AbilityContextKeys.ContextKind.ToKeyString(), out int kindInt))
            {
                kind = (EffectContextKind)kindInt;
            }
            else if (ctx is SkillPipelineContext)
            {
                kind = EffectContextKind.Skill;
            }
            else
            {
                kind = EffectContextKind.Unknown;
            }

            return new EffectContextWrapper(ctx, kind);
        }

        private EffectContextWrapper(IAbilityPipelineContext inner, EffectContextKind kind)
        {
            _inner = inner;
            _kind = kind;
        }

        public EffectContextKind Kind => _kind;
        public int SourceActorId => _inner.GetSourceActorId();
        public int TargetActorId => _inner.GetTargetActorId();

        public bool TryGetSkill(out SkillContextView skill)
        {
            if (_inner is SkillPipelineContext s)
            {
                skill = new SkillContextView(s.SkillId, s.SkillSlot, s.AimPos, s.AimDir, s.CasterUnit);
                return true;
            }

            skill = default;
            return false;
        }

        public object AbilityInstance => _inner.AbilityInstance;
        public AbilityPipelinePhaseId CurrentPhaseId { get => _inner.CurrentPhaseId; set => _inner.CurrentPhaseId = value; }
        public EAbilityPipelineState PipelineState { get => _inner.PipelineState; set => _inner.PipelineState = value; }
        public bool IsAborted { get => _inner.IsAborted; set => _inner.IsAborted = value; }
        public bool IsPaused { get => _inner.IsPaused; set => _inner.IsPaused = value; }
        public float StartTime { get => _inner.StartTime; set => _inner.StartTime = value; }
        public float ElapsedTime => _inner.ElapsedTime;
        public long SourceContextId { get => _inner.GetSourceContextId(); set => _inner.SetSourceContextId(value); }
        public Dictionary<string, object> SharedData => _inner.SharedData;

        public T GetData<T>(string key, T defaultValue = default)
        {
            return _inner.GetData(key, defaultValue);
        }

        public void SetData<T>(string key, T value)
        {
            _inner.SetData(key, value);
        }

        public bool TryGetData<T>(string key, out T value)
        {
            return _inner.TryGetData(key, out value);
        }

        public bool RemoveData(string key)
        {
            return _inner.RemoveData(key);
        }

        public void ClearData()
        {
            _inner.ClearData();
        }

        public void Reset()
        {
            _inner.Reset();
        }
    }
}
