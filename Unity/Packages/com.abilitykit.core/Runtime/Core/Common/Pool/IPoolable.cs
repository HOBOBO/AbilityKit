namespace AbilityKit.Ability.Share.Common.Pool
{
    public interface IPoolable
    {
        void OnPoolGet();
        void OnPoolRelease();
        void OnPoolDestroy();
    }
}
