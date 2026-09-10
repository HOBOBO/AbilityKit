using System;
using AbilityKit.Ability.Config.Authoring;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(
        fileName = "TriggerAuthoringModule",
        menuName = "AbilityKit/触发器编辑/模块")]
    public sealed class TriggerAuthoringModuleAsset : SerializedScriptableObject
    {
        [SerializeField]
        private TriggerAuthoringProjectAsset _project;

        [OdinSerialize, NonSerialized]
        public TriggerAuthoringSourceMetadata Metadata = new TriggerAuthoringSourceMetadata();

        [OdinSerialize, NonSerialized]
        public TriggerAuthoringModuleData Module = new TriggerAuthoringModuleData();

        [SerializeField, HideInInspector]
        private TriggerAuthoringPackageMetadata _packageMetadata = new TriggerAuthoringPackageMetadata();

        [SerializeField, HideInInspector]
        private string _sourceJsonPath;

        [SerializeField, HideInInspector]
        private string _lastSynchronizedHash;

        public string SourceJsonPath => _sourceJsonPath;
        public string LastSynchronizedHash => _lastSynchronizedHash;
        public TriggerAuthoringProjectAsset Project => _project;
        public TriggerAuthoringPackageMetadata PackageMetadata =>
            _packageMetadata ?? (_packageMetadata = new TriggerAuthoringPackageMetadata());

        internal void SetProject(TriggerAuthoringProjectAsset project)
        {
            _project = project;
        }

        internal void SetPackageMetadata(TriggerAuthoringPackageMetadata value)
        {
            _packageMetadata = value ?? new TriggerAuthoringPackageMetadata();
        }

        internal void MarkSynchronized(string sourceJsonPath, string contentHash)
        {
            _sourceJsonPath = sourceJsonPath ?? string.Empty;
            _lastSynchronizedHash = contentHash ?? string.Empty;
        }
    }
}
