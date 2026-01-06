using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.UI
{
    public sealed class UIManager
    {
        private sealed class UIReg
        {
            public string Key;
            public string Path;
            public GameObject Prefab;
            public UILayer Layer;
            public Type UiType;
        }

        private readonly Dictionary<string, UIReg> _registry = new Dictionary<string, UIReg>(StringComparer.Ordinal);
        private readonly Dictionary<string, UIBase> _instances = new Dictionary<string, UIBase>(StringComparer.Ordinal);

        private readonly Dictionary<UILayer, Transform> _layerRoots = new Dictionary<UILayer, Transform>();
        private readonly Stack<UIPanel> _popupStack = new Stack<UIPanel>();

        private readonly UIContext _context;
        private readonly IUIAssetProvider _assets;

        public UIManager(IUIAssetProvider assets)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _context = new UIContext(this);
        }

        public void SetLayerRoot(UILayer layer, Transform root)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _layerRoots[layer] = root;
        }

        public void Register<T>(string key, string prefabPath, UILayer layer) where T : UIBase
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("UI key is required", nameof(key));
            if (string.IsNullOrEmpty(prefabPath)) throw new ArgumentException("Prefab path is required", nameof(prefabPath));

            _registry[key] = new UIReg
            {
                Key = key,
                Path = prefabPath,
                Prefab = null,
                Layer = layer,
                UiType = typeof(T)
            };
        }

        public void RegisterPrefab<T>(string key, GameObject prefab, UILayer layer) where T : UIBase
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("UI key is required", nameof(key));
            if (prefab == null) throw new ArgumentNullException(nameof(prefab));

            _registry[key] = new UIReg
            {
                Key = key,
                Path = null,
                Prefab = prefab,
                Layer = layer,
                UiType = typeof(T)
            };
        }

        public bool IsOpen(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return _instances.TryGetValue(key, out var ui) && ui != null && ui.IsOpen;
        }

        public T Open<T>(string key, object args = null) where T : UIBase
        {
            var ui = OpenInternal(key, args);
            if (ui is T t) return t;
            throw new InvalidOperationException($"UI type mismatch: key={key} expected={typeof(T).FullName} actual={ui?.GetType().FullName}");
        }

        public UIBase Open(string key, object args = null)
        {
            return OpenInternal(key, args);
        }

        private UIBase OpenInternal(string key, object args)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("UI key is required", nameof(key));

            if (!_registry.TryGetValue(key, out var reg))
            {
                throw new KeyNotFoundException($"UI not registered: {key}");
            }

            if (!_layerRoots.TryGetValue(reg.Layer, out var layerRoot) || layerRoot == null)
            {
                throw new InvalidOperationException($"UI layer root not set: {reg.Layer}");
            }

            if (!_instances.TryGetValue(key, out var ui) || ui == null)
            {
                ui = CreateInstance(reg, layerRoot);
                _instances[key] = ui;
            }

            if (ui is UIPanel panel && panel.UseStack)
            {
                if (_popupStack.Count > 0)
                {
                    var top = _popupStack.Peek();
                    if (top != null && top != panel) top.InternalHide(null);
                }

                if (_popupStack.Count == 0 || _popupStack.Peek() != panel)
                {
                    _popupStack.Push(panel);
                }
            }

            ui.InternalOpen(args);
            return ui;
        }

        public bool Show(string key, object args = null)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_instances.TryGetValue(key, out var ui) || ui == null) return false;
            if (!ui.IsOpen) return false;
            ui.InternalShow(args);
            return true;
        }

        public bool Hide(string key, object args = null)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_instances.TryGetValue(key, out var ui) || ui == null) return false;
            if (!ui.IsOpen) return false;
            ui.InternalHide(args);
            return true;
        }

        public bool TryGet<T>(string key, out T ui) where T : UIBase
        {
            if (_instances.TryGetValue(key, out var u) && u is T t)
            {
                ui = t;
                return true;
            }

            ui = null;
            return false;
        }

        public bool Close(string key, object args = null)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_instances.TryGetValue(key, out var ui) || ui == null) return false;

            ui.InternalClose(args);

            if (ui is UIPanel panel && panel.UseStack)
            {
                CloseFromStack(panel);
            }

            return true;
        }

        public bool CloseTopPopup(object args = null)
        {
            while (_popupStack.Count > 0)
            {
                var top = _popupStack.Pop();
                if (top == null) continue;

                top.InternalClose(args);

                while (_popupStack.Count > 0 && _popupStack.Peek() == null) _popupStack.Pop();
                if (_popupStack.Count > 0)
                {
                    var next = _popupStack.Peek();
                    if (next != null)
                    {
                        if (!next.IsOpen) next.InternalOpen(null);
                        else next.InternalShow(null);
                    }
                }

                return true;
            }

            return false;
        }

        public bool Destroy(string key, bool destroyGameObject = true)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!_instances.TryGetValue(key, out var ui) || ui == null) return false;

            _instances.Remove(key);

            if (ui is UIPanel panel && panel.UseStack)
            {
                RemoveFromStack(panel);
            }

            ui.InternalDispose(destroyGameObject);
            return true;
        }

        public void Tick(float deltaTime)
        {
            foreach (var kv in _instances)
            {
                if (kv.Value == null) continue;
                kv.Value.InternalTick(deltaTime);
            }
        }

        private UIBase CreateInstance(UIReg reg, Transform layerRoot)
        {
            var prefab = reg.Prefab != null ? reg.Prefab : _assets.Load(reg.Path);
            if (prefab == null) throw new InvalidOperationException($"UI prefab not found: key={reg.Key} path={reg.Path}");

            var go = UnityEngine.Object.Instantiate(prefab, layerRoot);
            go.name = reg.Key;

            UIBase ui;
            if (reg.UiType != null)
            {
                ui = go.GetComponent(reg.UiType) as UIBase;
                if (ui == null) throw new InvalidOperationException($"UI component not found on prefab: key={reg.Key} type={reg.UiType.FullName} path={reg.Path}");
            }
            else
            {
                ui = go.GetComponent<UIBase>();
                if (ui == null) throw new InvalidOperationException($"UIBase component not found on prefab: key={reg.Key} path={reg.Path}");
            }

            ui.InternalInit(_context, reg.Key, reg.Layer);
            ui.gameObject.SetActive(false);
            return ui;
        }

        private void CloseFromStack(UIPanel panel)
        {
            if (_popupStack.Count == 0) return;
            if (_popupStack.Peek() == panel)
            {
                _popupStack.Pop();
                while (_popupStack.Count > 0 && _popupStack.Peek() == null) _popupStack.Pop();
                if (_popupStack.Count > 0)
                {
                    var next = _popupStack.Peek();
                    if (next != null)
                    {
                        if (!next.IsOpen) next.InternalOpen(null);
                        else next.InternalShow(null);
                    }
                }
                return;
            }

            RemoveFromStack(panel);
        }

        private void RemoveFromStack(UIPanel panel)
        {
            if (_popupStack.Count == 0) return;

            var tmp = new Stack<UIPanel>(_popupStack.Count);
            while (_popupStack.Count > 0)
            {
                var p = _popupStack.Pop();
                if (p != null && p != panel) tmp.Push(p);
            }

            while (tmp.Count > 0) _popupStack.Push(tmp.Pop());
        }
    }
}
