using System;
using UnityEngine;

namespace AbilityKit.Game.UI
{
    public sealed class UIDemoInstaller : MonoBehaviour
    {
        [Serializable]
        private sealed class Entry
        {
            public string Key;
            public UILayer Layer = UILayer.Main;
            public GameObject Prefab;
            public bool OpenOnStart;
        }

        [SerializeField] private UIRoot _root;
        [SerializeField] private Entry[] _entries;

        private void Start()
        {
            if (_root == null) _root = FindFirstObjectByType<UIRoot>();
            if (_root == null) return;

            var ui = _root.UI;
            if (ui == null) return;

            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    var e = _entries[i];
                    if (e == null) continue;
                    if (string.IsNullOrEmpty(e.Key)) continue;
                    if (e.Prefab == null) continue;

                    var uiComp = e.Prefab.GetComponent<UIBase>();
                    if (uiComp == null) continue;

                    var method = typeof(UIManager).GetMethod("RegisterPrefab");
                    if (method == null) continue;

                    var generic = method.MakeGenericMethod(uiComp.GetType());
                    generic.Invoke(ui, new object[] { e.Key, e.Prefab, e.Layer });

                    if (e.OpenOnStart)
                    {
                        ui.Open(e.Key);
                    }
                }
            }
        }
    }
}
