using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [CreateAssetMenu(fileName = "TriggerAuthoringProject", menuName = "AbilityKit/Trigger Authoring/Project")]
    public sealed class TriggerAuthoringProjectAsset : ScriptableObject
    {
        [SerializeField]
        private TriggerEventCatalogAsset _eventCatalog;

        [SerializeField]
        private TriggerGlobalBlackboardCatalogAsset _globalBlackboardCatalog;

        [SerializeField]
        private TriggerAuthoringTemplateCatalogAsset _templateCatalog;

        [SerializeField]
        private List<TriggerAuthoringModuleAsset> _modules = new List<TriggerAuthoringModuleAsset>();

        [SerializeField]
        private string _runtimeOutputRoot;

        public TriggerEventCatalogAsset EventCatalog => _eventCatalog;
        public TriggerGlobalBlackboardCatalogAsset GlobalBlackboardCatalog => _globalBlackboardCatalog;
        public TriggerAuthoringTemplateCatalogAsset TemplateCatalog => _templateCatalog;
        public IReadOnlyList<TriggerAuthoringModuleAsset> Modules => _modules;

        /// <summary>Runtime Plan 一键导出根目录；相对 Unity 工程根，空表示未配置。</summary>
        public string RuntimeOutputRoot => _runtimeOutputRoot;

        internal void SetRuntimeOutputRoot(string value)
        {
            _runtimeOutputRoot = value ?? string.Empty;
        }

        internal void SetCatalogs(
            TriggerEventCatalogAsset eventCatalog,
            TriggerGlobalBlackboardCatalogAsset globalBlackboardCatalog,
            TriggerAuthoringTemplateCatalogAsset templateCatalog = null)
        {
            _eventCatalog = eventCatalog;
            _globalBlackboardCatalog = globalBlackboardCatalog;
            _templateCatalog = templateCatalog;
        }

        internal void SetModules(IEnumerable<TriggerAuthoringModuleAsset> modules)
        {
            _modules = modules != null
                ? new List<TriggerAuthoringModuleAsset>(modules)
                : new List<TriggerAuthoringModuleAsset>();
        }

        internal bool AddModule(TriggerAuthoringModuleAsset module)
        {
            if (module == null) return false;
            if (_modules.Contains(module)) return false;
            _modules.Add(module);
            return true;
        }

        internal bool RemoveModule(TriggerAuthoringModuleAsset module)
        {
            return module != null && _modules.Remove(module);
        }

        internal void RemoveModuleAt(int index)
        {
            if (index >= 0 && index < _modules.Count) _modules.RemoveAt(index);
        }
    }
}
