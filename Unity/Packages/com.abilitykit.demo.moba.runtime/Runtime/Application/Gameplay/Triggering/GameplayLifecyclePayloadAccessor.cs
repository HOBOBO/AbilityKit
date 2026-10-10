using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Payload;

namespace AbilityKit.Demo.Moba.Gameplay.Triggering
{
    public sealed class GameplayLifecyclePayloadAccessor :
        IPayloadIntAccessor<GameplayLifecycleEventArgs>,
        IPayloadDoubleAccessor<GameplayLifecycleEventArgs>
    {
        private static readonly int FrameIndexId = StableStringId.Get("payload:" + GameplayTriggerEvents.FrameIndexField);
        private static readonly int ElapsedSecondsId = StableStringId.Get("payload:" + GameplayTriggerEvents.ElapsedSecondsField);
        private static readonly int DeltaSecondsId = StableStringId.Get("payload:" + GameplayTriggerEvents.DeltaSecondsField);
        private static readonly int WinTeamIdId = StableStringId.Get("payload:" + GameplayTriggerEvents.WinTeamIdField);
        private static readonly int TeamIdId = StableStringId.Get("payload:" + GameplayTriggerEvents.TeamIdField);
        private static readonly int ScoreId = StableStringId.Get("payload:" + GameplayTriggerEvents.ScoreField);

        public static bool SupportsField(int fieldId)
        {
            return fieldId == FrameIndexId
                || fieldId == ElapsedSecondsId
                || fieldId == DeltaSecondsId
                || fieldId == WinTeamIdId
                || fieldId == TeamIdId
                || fieldId == ScoreId;
        }

        public bool TryGet(in GameplayLifecycleEventArgs args, int fieldId, out int value)
        {
            if (fieldId == FrameIndexId)
            {
                value = args.FrameIndex;
                return true;
            }

            if (fieldId == WinTeamIdId)
            {
                value = args.WinTeamId;
                return true;
            }

            if (fieldId == TeamIdId)
            {
                value = args.TeamId;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGet(in GameplayLifecycleEventArgs args, int fieldId, out double value)
        {
            if (fieldId == ElapsedSecondsId)
            {
                value = args.ElapsedSeconds;
                return true;
            }

            if (fieldId == DeltaSecondsId)
            {
                value = args.DeltaSeconds;
                return true;
            }

            if (fieldId == ScoreId)
            {
                value = args.Score;
                return true;
            }

            if (TryGet(in args, fieldId, out int intValue))
            {
                value = intValue;
                return true;
            }

            value = default;
            return false;
        }
    }
}
