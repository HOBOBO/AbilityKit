using System.Collections.Generic;
using AbilityKit.HFSM.Runtime;
using UnityEngine;

namespace AbilityKit.HFSM.Samples.CompleteHfsmShowcase
{
    [CreateAssetMenu(fileName = "ShowcaseActions", menuName = "AbilityKit/HFSM/Showcase Actions")]
    public sealed class HfsmShowcaseActionAsset : ScriptableObject
    {
        [SerializeField] private List<CompositeActionBinding> _states = new List<CompositeActionBinding>();

        public void LoadJson(string json) => _states = CompositeActionCatalog.LoadJson(json).States;

        public string ExportJson() => new CompositeActionCatalog { States = _states }.SaveJson();
    }
}
