using System.Collections.Generic;

namespace AbilityKit.Ability.Editor
{
    internal interface IVarKeyProvider
    {
        int Order { get; }
        bool CanProvide(in VarKeyQuery query);
        void CollectKeys(in VarKeyQuery query, List<string> output);
    }
}
