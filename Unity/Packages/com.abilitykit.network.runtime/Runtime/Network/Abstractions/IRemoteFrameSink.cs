namespace AbilityKit.Network.Abstractions
{
    public interface IRemoteFrameSink<TFrame>
    {
        void Add(int frame, TFrame frameData);
    }
}
