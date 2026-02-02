namespace AbilityKit.Ability.Share.Common.TagSystem
{
    public interface ITagTemplateRegistry
    {
        bool TryGet(int templateId, out TagTemplateRuntime template);
    }
}
