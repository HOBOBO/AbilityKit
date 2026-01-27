#if UNITY_EDITOR
namespace Emilia.Kit
{
    public interface IObjectDescriptionGetter
    {
        string GetDescription(object obj, object owner, object userData);
    }
}
#endif