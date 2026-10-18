using System;
using UnityEngine;

namespace AbilityKit.Game.UI
{
    public abstract class UIBase : MonoBehaviour
    {
        public bool IsInitialized { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsVisible { get; private set; }

        public UILayer Layer { get; private set; }
        public string Key { get; private set; }

        protected UIContext Context { get; private set; }

        internal void InternalInit(UIContext context, string key, UILayer layer)
        {
            if (IsInitialized) return;
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Key = key;
            Layer = layer;
            IsInitialized = true;
            OnInit();
        }

        internal void InternalOpen(object args)
        {
            if (!IsInitialized) throw new InvalidOperationException($"UI is not initialized: {name}");
            if (IsOpen) return;
            IsOpen = true;
            IsVisible = true;
            gameObject.SetActive(true);
            OnOpen(args);
        }

        internal void InternalShow(object args)
        {
            if (!IsInitialized) throw new InvalidOperationException($"UI is not initialized: {name}");
            if (!IsOpen) throw new InvalidOperationException($"UI is not open: {name}");
            if (IsVisible) return;

            IsVisible = true;
            gameObject.SetActive(true);
            OnShow(args);
        }

        internal void InternalHide(object args)
        {
            if (!IsInitialized) return;
            if (!IsOpen) return;
            if (!IsVisible) return;

            OnHide(args);
            IsVisible = false;
            gameObject.SetActive(false);
        }

        internal void InternalClose(object args)
        {
            if (!IsInitialized) return;
            if (!IsOpen) return;
            OnClose(args);
            IsOpen = false;
            IsVisible = false;
            gameObject.SetActive(false);
        }

        internal void InternalTick(float deltaTime)
        {
            if (!IsOpen) return;
            OnTick(deltaTime);
        }

        internal void InternalDispose(bool destroyGameObject)
        {
            if (!IsInitialized) return;
            if (IsOpen) InternalClose(null);
            OnDispose();
            IsInitialized = false;
            IsVisible = false;
            Context = null;

            if (destroyGameObject)
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnInit() { }
        protected virtual void OnOpen(object args) { }
        protected virtual void OnShow(object args) { }
        protected virtual void OnHide(object args) { }
        protected virtual void OnClose(object args) { }
        protected virtual void OnTick(float deltaTime) { }
        protected virtual void OnDispose() { }
    }
}
