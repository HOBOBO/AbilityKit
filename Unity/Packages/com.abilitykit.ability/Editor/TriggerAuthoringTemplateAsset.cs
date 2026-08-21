using System;
using AbilityKit.Ability.Config.Authoring;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(fileName = "TriggerAuthoringTemplate", menuName = "AbilityKit/Trigger Authoring/Template")]
    public sealed class TriggerAuthoringTemplateAsset : SerializedScriptableObject
    {
        [SerializeField]
        private TriggerAuthoringProjectAsset _project;

        [OdinSerialize, NonSerialized]
        public TriggerAuthoringSourceMetadata Metadata = new TriggerAuthoringSourceMetadata();

        [OdinSerialize, NonSerialized]
        public TriggerAuthoringTemplateData Template = new TriggerAuthoringTemplateData();

        [SerializeField, HideInInspector]
        private string _sourceJsonPath;

        [SerializeField, HideInInspector]
        private string _lastSynchronizedHash;

        public TriggerAuthoringProjectAsset Project => _project;
        public string SourceJsonPath => _sourceJsonPath;
        public string LastSynchronizedHash => _lastSynchronizedHash;

        internal void SetProject(TriggerAuthoringProjectAsset project)
        {
            _project = project;
        }

        internal void MarkSynchronized(string sourceJsonPath, string contentHash)
        {
            _sourceJsonPath = sourceJsonPath ?? string.Empty;
            _lastSynchronizedHash = contentHash ?? string.Empty;
        }
    }
}
