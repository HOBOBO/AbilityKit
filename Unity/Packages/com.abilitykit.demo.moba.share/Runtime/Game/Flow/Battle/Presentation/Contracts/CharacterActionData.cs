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

        public CharacterActionData(int actorId, int entityVersion, int snapshotFrame,
            string path, long instanceId, int startFrame, int localFrame,
            long castInstanceId, int skillId)
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
        }
    }
}
