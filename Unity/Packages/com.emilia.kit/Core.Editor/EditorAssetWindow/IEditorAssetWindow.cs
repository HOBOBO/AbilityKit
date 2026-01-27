namespace Emilia.Kit.Editor
{
    public interface IEditorAssetWindow
    {
        string id { get; }

        void OnReOpen(object arg);

        void OnOpen(object arg);
    }
}