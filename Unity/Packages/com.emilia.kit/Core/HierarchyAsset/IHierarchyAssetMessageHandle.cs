namespace Emilia.Kit
{
    public interface IHierarchyAssetMessageHandle
    {
        void MessageHandle(IHierarchyAsset thisAsset, IHierarchyAsset sender, object arg);
    }
}