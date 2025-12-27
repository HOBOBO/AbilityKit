using System;
using AbilityKit.Ability.EC;
using UnityEngine;

namespace AbilityKit.Game
{
    public sealed class GameEntry : MonoBehaviour
    {
        private static GameEntry _instance;

        [SerializeField] private bool _debugEnabled;

        public static GameEntry Instance
        {
            get
            {
                if (_instance == null) throw new InvalidOperationException("GameEntry is not initialized");
                return _instance;
            }
        }

        public static bool IsInitialized => _instance != null;

        public bool DebugEnabled
        {
            get => _debugEnabled;
            set => _debugEnabled = value;
        }

        public EntityWorld World { get; private set; }
        public Entity Root { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            World = new EntityWorld();
            Root = EntityGenerator.CreateRoot(World, "GameRoot");
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public T Get<T>() where T : class
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            return Root.GetComponent<T>();
        }

        public bool TryGet<T>(out T component) where T : class
        {
            if (!Root.IsValid)
            {
                component = null;
                return false;
            }

            return Root.TryGetComponent(out component);
        }

        public void Set<T>(T component) where T : class
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            Root.AddComponent(component);
        }

        public Entity CreateNode(int childId)
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            return Root.AddChild(childId);
        }

        public Entity GetNode(int childId)
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            return Root.GetChildById(childId);
        }

        public bool TryGetNode(int childId, out Entity node)
        {
            if (!Root.IsValid)
            {
                node = default;
                return false;
            }

            return Root.TryGetChildById(childId, out node);
        }
    }
}
