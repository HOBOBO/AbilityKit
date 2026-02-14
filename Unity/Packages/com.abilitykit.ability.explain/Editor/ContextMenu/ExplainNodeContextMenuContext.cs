namespace AbilityKit.Ability.Explain.Editor
{
    public readonly struct ExplainNodeContextMenuContext
    {
        public readonly ExplainResolveRequest Request;
        public readonly ExplainResolveResult Result;

        public ExplainNodeContextMenuContext(ExplainResolveRequest request, ExplainResolveResult result)
        {
            Request = request;
            Result = result;
        }
    }
}
