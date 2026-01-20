using AbilityKit.Ability.HotReload;
using UnityEngine;

namespace AbilityKit.Game.Editor.HotReload
{
    public sealed class UnityHotfixLogger : IHotfixLogger
    {
        public void Log(string message)
        {
            Debug.Log(message);
        }
    }
}
