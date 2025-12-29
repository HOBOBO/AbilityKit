using System;
using UnityEngine;

namespace NBC
{
    public class MonoManager : MonoBehaviour
    {
        public event Action OnUpdate;
        public event Action OnLateUpdate;
        public event Action OnFixedUpdate;
        public event Action OnApplicationQuitAction;
        public event Action<bool> OnApplicationPauseAction;

        private static bool IsQuiting { get; set; }

        private static MonoManager _inst;

        public static MonoManager Inst
        {
            get
            {
                if (_inst != null || IsQuiting) return _inst;
                _inst = FindObjectOfType<MonoManager>();
                if (_inst == null)
                {
                    _inst = new GameObject("_MonoTimer").AddComponent<MonoManager>();
                }

                return _inst;
            }
        }


        ///Creates the MonoManager singleton
        public static void Create()
        {
            _inst = Inst;
        }

        protected void OnApplicationQuit()
        {
            IsQuiting = true;
            OnApplicationQuitAction?.Invoke();
        }

        protected void OnApplicationPause(bool isPause)
        {
            OnApplicationPauseAction?.Invoke(isPause);
        }

        protected void Awake()
        {
            if (_inst != null && _inst != this)
            {
                DestroyImmediate(this.gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            _inst = this;
        }

        protected void Update()
        {
            OnUpdate?.Invoke();
        }

        protected void LateUpdate()
        {
            OnLateUpdate?.Invoke();
        }

        protected void FixedUpdate()
        {
            OnFixedUpdate?.Invoke();
        }
    }
}