using AbilityKit.Ability.Explain;

namespace AbilityKit.Ability.Explain.Editor
{
    public sealed class ExplainDetailsContext
    {
        public ExplainDetailsContext()
        {
        }

        public ExplainDetailsContext(ExplainResolveRequest request, ExplainResolveResult result)
        {
            Request = request;
            Result = result;
        }

        public ExplainResolveRequest Request;
        public ExplainResolveResult Result;
    }
}
