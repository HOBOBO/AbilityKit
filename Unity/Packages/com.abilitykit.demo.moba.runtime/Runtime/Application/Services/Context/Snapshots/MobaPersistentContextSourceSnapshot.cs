namespace AbilityKit.Demo.Moba.Services
{
    /// <summary>
    /// Cross-frame value snapshot of formal context identity. A skill runtime handle is a
    /// generation-checked capability value, not a live runtime reference; consumers must
    /// resolve it through MobaSkillCastRuntimeService before use.
    /// </summary>
    public readonly struct MobaPersistentContextSourceSnapshot : IMobaContextSourceProvider, IMobaTriggerLineageContextProvider, IMobaTriggerExecutionSnapshotProvider, IMobaCombatContextSource
    {
        private readonly MobaContextSourceView _source;

        public MobaPersistentContextSourceSnapshot(in MobaContextSourceView source)
        {
            _source = source.IsValid
                ? new MobaContextSourceView(
                    source.ResolveKind,
                    MobaContextSourceBoundary.Snapshot,
                    source.ContextKind,
                    source.TraceKind,
                    source.SourceActorId,
                    source.TargetActorId,
                    source.SourceContextId,
                    source.ParentContextId != 0 ? source.ParentContextId : source.SourceContextId,
                    source.RootContextId != 0 ? source.RootContextId : source.SourceContextId,
                    source.OwnerContextId != 0 ? source.OwnerContextId : source.SourceContextId,
                    source.ConfigId,
                    source.TriggerId,
                    source.Frame,
                    source.RuntimeKind,
                    source.RuntimeConfigId,
                    false,
                    source.SkillRuntimeHandle)
                : default;
        }

        public bool IsValid => _source.IsValid;
        public bool HasExecutionSource => _source.HasExecutionSource;
        public MobaContextSourceView Source => _source;

        public static bool TryCapture(object payload, out MobaPersistentContextSourceSnapshot snapshot)
        {
            return MobaPersistentContextSourceSnapshotFactory.TryCapture(payload, out snapshot);
        }

        public static MobaPersistentContextSourceSnapshot FromContextSource(in MobaContextSourceView source)
        {
            return MobaPersistentContextSourceSnapshotFactory.FromContextSource(in source);
        }

        public bool TryGetContextSource(out MobaContextSourceView source)
        {
            source = _source;
            return source.IsValid;
        }

        public bool TryGetLineageContext(out MobaTriggerLineageContext lineageContext)
        {
            if (!_source.HasExecutionSource)
            {
                lineageContext = default;
                return false;
            }

            lineageContext = new MobaTriggerLineageContext(
                _source.ContextKind,
                _source.TraceKind,
                _source.SourceActorId,
                _source.TargetActorId,
                _source.SourceContextId,
                _source.RootContextId != 0 ? _source.RootContextId : _source.SourceContextId,
                _source.OwnerContextId != 0 ? _source.OwnerContextId : _source.SourceContextId,
                _source.ConfigId);
            return lineageContext.SourceActorId > 0 && lineageContext.SourceContextId != 0;
        }

        public bool TryGetExecutionSnapshot(out MobaTriggerExecutionSnapshot snapshot)
        {
            snapshot = new MobaTriggerExecutionSnapshot(
                _source.ContextKind,
                _source.SourceActorId,
                _source.TargetActorId,
                _source.SourceContextId,
                _source.RootContextId != 0 ? _source.RootContextId : _source.SourceContextId,
                _source.OwnerContextId != 0 ? _source.OwnerContextId : _source.SourceContextId,
                _source.TriggerId,
                _source.ConfigId,
                _source.Frame,
                _source.SkillRuntimeHandle);
            return snapshot.IsValid;
        }

        public bool TryGetCombatContextSource(out MobaCombatContextSource source)
        {
            source = new MobaCombatContextSource(
                _source.ContextKind,
                _source.TraceKind,
                _source.SourceActorId,
                _source.TargetActorId,
                _source.SourceContextId,
                _source.RootContextId != 0 ? _source.RootContextId : _source.SourceContextId,
                _source.OwnerContextId != 0 ? _source.OwnerContextId : _source.SourceContextId,
                _source.ConfigId,
                _source.TriggerId,
                _source.Frame,
                _source.SkillRuntimeHandle,
                _source.RuntimeKind,
                _source.RuntimeConfigId,
                false);
            return source.HasExecutionSource;
        }
    }
}
