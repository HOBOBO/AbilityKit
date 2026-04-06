using System;
using AbilityKit.World.ECS;
using AbilityKit.Game.EntityCreation;
using AbilityKit.Game.Flow;
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
        public IEntity Root { get; private set; }

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

            if (!Root.TryGetRef<GameFlowDomain>(out var existingFlow))
            {
                var flow = new GameFlowDomain(this);
                Root.WithRef(flow);
            }
        }

        private void Start()
        {
            if (!Root.IsValid) return;
            if (Root.TryGetRef<GameFlowDomain>(out var flow))
            {
                flow.Start();
            }
        }

        private void Update()
        {
            if (!Root.IsValid) return;
            if (Root.TryGetRef<GameFlowDomain>(out var flow))
            {
                flow.Tick(Time.deltaTime);
            }
        }

        private void OnGUI()
        {
            if (!Root.IsValid) return;
            if (Root.TryGetRef<GameFlowDomain>(out var flow))
            {
                flow.OnGUI();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public T Get<T>() where T : class
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            return Root.GetRef<T>();
        }

        public bool TryGet<T>(out T component) where T : class
        {
            if (!Root.IsValid)
            {
                component = default(T);
                return false;
            }

            return Root.TryGetRef(out component);
        }

        public void Set<T>(T component) where T : class
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            Root.WithRef(component);
        }

        public IEntity CreateNode(int childId)
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            return Root.World.CreateChild(Root, childId);
        }

        public IEntity GetNode(int childId)
        {
            if (!Root.IsValid) throw new InvalidOperationException("Root entity is not valid");
            Root.TryGetChildById(childId, out var node);
            return node;
        }

        public bool TryGetNode(int childId, out IEntity node)
        {
            if (!Root.IsValid)
            {
                node = default(IEntity);
                return false;
            }

            return Root.TryGetChildById(childId, out node);
        }
    }
}
