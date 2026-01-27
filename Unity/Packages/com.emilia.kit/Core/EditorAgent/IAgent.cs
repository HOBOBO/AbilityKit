#if UNITY_EDITOR
namespace Emilia.Kit
{
    public interface IAgent
    {
        void Start();
        void Update();
        void OnEnable();
        void OnDisable();
        void OnDestroy();
    }
}
#endif