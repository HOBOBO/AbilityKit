using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(fileName = "TriggerAuthoringTemplates", menuName = "AbilityKit/Trigger Authoring/Template Catalog")]
    public sealed class TriggerAuthoringTemplateCatalogAsset : ScriptableObject
    {
        public List<TriggerAuthoringTemplateAsset> Templates = new List<TriggerAuthoringTemplateAsset>();
    }
}
