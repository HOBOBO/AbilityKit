using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(fileName = "TriggerAuthoringTemplates", menuName = "AbilityKit/触发器编辑/模板目录")]
    public sealed class TriggerAuthoringTemplateCatalogAsset : ScriptableObject
    {
        [InspectorName("模板")]
        public List<TriggerAuthoringTemplateAsset> Templates = new List<TriggerAuthoringTemplateAsset>();

        internal bool AddTemplate(TriggerAuthoringTemplateAsset template)
        {
            if (template == null) return false;
            Templates = Templates ?? new List<TriggerAuthoringTemplateAsset>();
            if (Templates.Contains(template)) return false;
            Templates.Add(template);
            return true;
        }

        internal bool RemoveTemplate(TriggerAuthoringTemplateAsset template)
        {
            return template != null && Templates != null && Templates.Remove(template);
        }
    }
}
