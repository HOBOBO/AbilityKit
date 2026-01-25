using System;

namespace AbilityKit.Ability.Triggering.Json
{
    public interface ITextLoader
    {
        bool TryLoad(string id, out string text);
    }
}
