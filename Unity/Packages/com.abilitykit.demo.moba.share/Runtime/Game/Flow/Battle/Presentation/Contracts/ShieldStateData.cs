using System;

namespace AbilityKit.Demo.Moba.Share
{
    public readonly struct ShieldStateData
    {
        public int ActorId { get; }
        public float CurrentShield { get; }
        public float Capacity { get; }
        public float PersonalShield { get; }
        public float SharedShield { get; }
        public int ActiveLayerCount { get; }
        public bool HasShared { get; }
        public bool HasShield => CurrentShield > 0f;

        public ShieldStateData(int actorId, float currentShield, float capacity,
            float personalShield, float sharedShield, int activeLayerCount, bool hasShared)
        {
            ActorId = actorId;
            CurrentShield = Math.Max(0f, currentShield);
            Capacity = Math.Max(0f, capacity);
            PersonalShield = Math.Max(0f, personalShield);
            SharedShield = Math.Max(0f, sharedShield);
            ActiveLayerCount = Math.Max(0, activeLayerCount);
            HasShared = hasShared;
        }
    }
}
