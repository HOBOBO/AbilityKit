namespace AbilityKit.Core.Common.Pool
{
    public interface IPoolable
    {
        void OnPoolGet();
        void OnPoolRelease();
        void OnPoolDestroy();
    }
}
