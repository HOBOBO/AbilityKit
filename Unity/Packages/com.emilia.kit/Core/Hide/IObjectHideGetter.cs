#if UNITY_EDITOR
namespace Emilia.Kit
{
    public interface IObjectHideGetter
    {
        bool IsHide(object obj, object owner, object userData);
    }
}
#endif