using System;
using AbilityKit.Ability.Config.Authoring;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(
        fileName = "TriggerAuthoringModule",
        menuName = "AbilityKit/Trigger Authoring Module")]
    public sealed class TriggerAuthoringModuleAsset : SerializedScriptableObject
    {
        [OdinSerialize, NonSerialized]
        public TriggerAuthoringSourceMetadata Metadata = new TriggerAuthoringSourceMetadata();

        [OdinSerialize, NonSerialized]
        public TriggerAuthoringModuleData Module = new TriggerAuthoringModuleData();

        [SerializeField, HideInInspector]
        private string _sourceJsonPath;

        [SerializeField, HideInInspector]
        private string _lastSynchronizedHash;

        public string SourceJsonPath => _sourceJsonPath;
        public string LastSynchronizedHash => _lastSynchronizedHash;

        internal void MarkSynchronized(string sourceJsonPath, string contentHash)
        {
            _sourceJsonPath = sourceJsonPath ?? string.Empty;
            _lastSynchronizedHash = contentHash ?? string.Empty;
        }
    }
}
