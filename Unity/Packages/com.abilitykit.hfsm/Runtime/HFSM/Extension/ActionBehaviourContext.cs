namespace UnityHFSM.Extension
{
    public readonly struct ActionBehaviourContext
    {
        public readonly float DeltaTime;
        public readonly float UnscaledDeltaTime;
        public readonly float TimeScale;
        public readonly float Speed;

        public ActionBehaviourContext(float deltaTime, float unscaledDeltaTime, float timeScale, float speed)
        {
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            TimeScale = timeScale;
            Speed = speed;
        }

        public float GetScaledDelta(bool useUnscaled)
        {
            var dt = useUnscaled ? UnscaledDeltaTime : DeltaTime;
            return dt * TimeScale * Speed;
        }
    }
}
