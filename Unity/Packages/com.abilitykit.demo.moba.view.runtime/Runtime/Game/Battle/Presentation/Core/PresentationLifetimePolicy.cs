namespace AbilityKit.Game.Flow
{
    public static class PresentationLifetimePolicy
    {
        public static float ExpireAt(float nowSeconds, int durationMs)
        {
            return durationMs > 0 ? nowSeconds + durationMs / 1000f : 0f;
        }

        public static bool IsExpired(float nowSeconds, float expireAtSeconds)
        {
            return expireAtSeconds > 0f && nowSeconds >= expireAtSeconds;
        }
    }
}
