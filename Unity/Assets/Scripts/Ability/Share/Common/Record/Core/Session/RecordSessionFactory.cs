namespace AbilityKit.Ability.Share.Common.Record.Core
{
    public static class RecordSessionFactory
    {
        public static RecordSession CreateDefault(RecordProfile profile = null)
        {
            var p = profile ?? new RecordProfile();
            var container = new RecordContainerBuilder(p).Build();
            var serializer = new JsonRecordContainerSerializer();
            var factory = new DefaultRecordTrackFactory();
            return new RecordSession(p, container, serializer, factory, factory);
        }

        public static RecordSession FromBytes(byte[] data, RecordProfile profile = null)
        {
            var s = CreateDefault(profile);
            s.TryLoad(data);
            return s;
        }

        public static RecordSession LoadFromFile(string path, RecordProfile profile = null)
        {
            var data = RecordFileStore.Load(path);
            return FromBytes(data, profile);
        }

        public static void SaveToFile(IRecordSession session, string path)
        {
            if (session == null) return;
            var data = session.Serialize();
            RecordFileStore.Save(path, data);
        }
    }
}
