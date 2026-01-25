using System.Collections.Generic;
using AbilityKit.Ability;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class EffectContextWrapper : IEffectContext
    {
        private readonly IAbilityPipelineContext _inner;
        private readonly EffectContextKind _kind;
        private readonly long _sourceContextId;

        public static IEffectContext Wrap(IAbilityPipelineContext ctx)
        {
            if (ctx == null) return null;
            if (ctx is IEffectContext ec) return ec;

            if (ctx is SkillPipelineContext)
            {
                return new EffectContextWrapper(ctx, EffectContextKind.Skill);
            }

            var kind = EffectContextKind.Unknown;
            if (ctx.SharedData != null && ctx.SharedData.TryGetValue(MobaEffectPipelineSharedKeys.ContextKind, out var kindObj))
            {
                if (kindObj is int ki) kind = (EffectContextKind)ki;
                else if (kindObj is long kl) kind = (EffectContextKind)(int)kl;
            }

            return new EffectContextWrapper(ctx, kind);
        }

        private EffectContextWrapper(IAbilityPipelineContext inner, EffectContextKind kind)
        {
            _inner = inner;
            _kind = kind;

            long sourceContextId = 0;
            if (inner.SharedData != null && inner.SharedData.TryGetValue(MobaEffectPipelineSharedKeys.SourceContextId, out var idObj))
            {
                if (idObj is long l) sourceContextId = l;
                else if (idObj is int i) sourceContextId = i;
            }
            _sourceContextId = sourceContextId;
        }

        public EffectContextKind Kind => _kind;
        public int SourceActorId => _inner.GetCasterActorId();
        public int TargetActorId => _inner.GetTargetActorId();
        public long SourceContextId => _sourceContextId;

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
        public Dictionary<string, object> SharedData => _inner.SharedData;

        public T GetData<T>(string key, T defaultValue = default)
        {
            return _inner.GetData(key, defaultValue);
        }

        public void SetData<T>(string key, T value)
        {
            _inner.SetData(key, value);
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
