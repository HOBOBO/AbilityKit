namespace AbilityKit.Core.Common.Record.Core
{
    public static class RecordEventTypes
    {
        public static readonly RecordEventType InputCommand = RecordEventType.FromName(RecordEventNames.InputCommand);
        public static readonly RecordEventType StateHashSample = RecordEventType.FromName(RecordEventNames.StateHashSample);
        public static readonly RecordEventType WorldSnapshot = RecordEventType.FromName(RecordEventNames.WorldSnapshot);
        public static readonly RecordEventType WorldDelta = RecordEventType.FromName(RecordEventNames.WorldDelta);
    }
}
