using System;
using AbilityKit.Ability.EC;
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

            if (!Root.TryGetComponent(out GameFlowDomain flow) || flow == null)
            {
                flow = new GameFlowDomain(this);
                Root.AddComponent(flow);
            }
        }

        private void Start()
        {
            if (!Root.IsValid) return;
            if (Root.TryGetComponent(out GameFlowDomain flow) && flow != null)
            {
                flow.Start();
            }
        }

        private void Update()
        {
            if (!Root.IsValid) return;
            if (Root.TryGetComponent(out GameFlowDomain flow) && flow != null)
            {
                flow.Tick(Time.deltaTime);
            }
        }

        private void OnGUI()
        {
            if (!Root.IsValid) return;
            if (Root.TryGetComponent(out GameFlowDomain flow) && flow != null)
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
