#if UNITY_EDITOR
namespace Emilia.Kit
{
    public interface ISelectedOwner
    {
        bool Validate();
    }
}
#endif