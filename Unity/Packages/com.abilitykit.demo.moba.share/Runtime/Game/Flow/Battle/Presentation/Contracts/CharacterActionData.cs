namespace AbilityKit.Demo.Moba.Share
{
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

        public CharacterActionData(int actorId, int entityVersion, int snapshotFrame,
            string path, long instanceId, int startFrame, int localFrame,
            long castInstanceId, int skillId)
            : this(actorId, entityVersion, snapshotFrame, path, instanceId, startFrame, localFrame,
                castInstanceId, skillId, "normal", 0L, 0, snapshotFrame) { }

        public CharacterActionData(int actorId, int entityVersion, int snapshotFrame,
            string path, long instanceId, int startFrame, int localFrame,
            long castInstanceId, int skillId, string modeId, long modeInstanceId,
            int modeStartFrame, int modeLocalFrame, CastPresentationFrame castFrame = default)
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
        }
    }
}
