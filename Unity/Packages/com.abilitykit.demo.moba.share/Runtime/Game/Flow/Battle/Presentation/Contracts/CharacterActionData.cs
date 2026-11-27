namespace AbilityKit.Demo.Moba.Share
{
    /// <summary>
    /// 单次施法已冻结的前摇映射，表现不再读取角色当前攻速。
    /// </summary>
    public readonly struct CastWindupTiming
    {
        public readonly int StartFrame, AuthoredMs, ActualMs;

        public CastWindupTiming(int startFrame, int authoredMs, int actualMs)
        {
            StartFrame = startFrame;
            AuthoredMs = authoredMs;
            ActualMs = actualMs;
        }

        public bool HasValue => ActualMs > 0;

        /// <summary>
        /// 将累计整毫秒映射到作者时间；发射标记之后保持原速。
        /// </summary>
        public long ToAuthoredMilliseconds(long elapsedMs) => !HasValue ? elapsedMs : elapsedMs < ActualMs
            ? elapsedMs * AuthoredMs / ActualMs
            : elapsedMs - ActualMs + AuthoredMs;

        /// <summary>
        /// 按模拟的 Q32.32 步长和整毫秒取整规则，从快照帧投影作者时间。
        /// </summary>
        public long ProjectAuthoredMilliseconds(int frame, float secondsPerFrame)
        {
            const double fixedUnit = 4294967296d;
            var tickRaw = System.Math.Floor(secondsPerFrame * fixedUnit);
            var elapsedFrames = System.Math.Max(0L, (long)frame - StartFrame);
            var elapsedMs = (long)System.Math.Floor(elapsedFrames * tickRaw * 1000d / fixedUnit);
            return ToAuthoredMilliseconds(elapsedMs);
        }
    }

    public readonly struct CharacterActionData
    {
        public readonly int ActorId;
        public readonly int EntityVersion;
        public readonly int SnapshotFrame;
        public readonly string Path;
        public readonly long InstanceId;
        public readonly int StartFrame;
        public readonly int LocalFrame;
        public readonly long CastInstanceId;
        public readonly int SkillId;
        public readonly string ModeId;
        public readonly long ModeInstanceId;
        public readonly int ModeStartFrame;
        public readonly int ModeLocalFrame;
        public readonly CastPresentationFrame CastFrame;
        public readonly int CastStartFrame;
        public readonly int CastDurationMs;
        public readonly CastWindupTiming WindupTiming;

        public CharacterActionData(int actorId, int entityVersion, int snapshotFrame,
            string path, long instanceId, int startFrame, int localFrame,
            long castInstanceId, int skillId)
            : this(actorId, entityVersion, snapshotFrame, path, instanceId, startFrame, localFrame,
                castInstanceId, skillId, "normal", 0L, 0, snapshotFrame) { }

        public CharacterActionData(int actorId, int entityVersion, int snapshotFrame,
            string path, long instanceId, int startFrame, int localFrame,
            long castInstanceId, int skillId, string modeId, long modeInstanceId,
            int modeStartFrame, int modeLocalFrame, CastPresentationFrame castFrame = default,
            int castStartFrame = -1, int castDurationMs = 0, CastWindupTiming windupTiming = default)
        {
            ActorId = actorId;
            EntityVersion = entityVersion;
            SnapshotFrame = snapshotFrame;
            Path = path ?? string.Empty;
            InstanceId = instanceId;
            StartFrame = startFrame;
            LocalFrame = localFrame;
            CastInstanceId = castInstanceId;
            SkillId = skillId;
            ModeId = modeId ?? string.Empty;
            ModeInstanceId = modeInstanceId;
            ModeStartFrame = modeStartFrame;
            ModeLocalFrame = modeLocalFrame;
            CastFrame = castFrame;
            CastStartFrame = castStartFrame;
            CastDurationMs = castDurationMs;
            WindupTiming = windupTiming;
        }
    }
}
