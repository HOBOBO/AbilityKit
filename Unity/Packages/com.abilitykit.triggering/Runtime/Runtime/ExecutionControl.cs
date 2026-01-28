namespace AbilityKit.Triggering.Runtime
{
    public sealed class ExecutionControl
    {
        public bool StopPropagation;
        public bool Cancel;

        public void Reset()
        {
            StopPropagation = false;
            Cancel = false;
        }
    }
}
