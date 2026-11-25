using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Battle.Hierarchy
{
    /// <summary>
    ///
    /// 承载分类战斗视图层级的场景级 MonoBehaviour 根节点。拥有一个 <see cref="BattleViewHierarchyManager"/>，提供创建及销毁子路径的便捷方法。
    ///
    /// 运行时由 <c>BattleViewFeature.OnAttach</c> 及 <c>ConfirmedBattleViewFeature</c> 对应入口生成，在 <c>OnDetach</c> 中通过销毁根 GameObject 拆卸。
    ///
    /// 生成的层级如下，由内部管理器维护：
    /// <code>
    /// [Battle]
    ///   ├── _Pool/
    ///   │   ├── _Shell/
    ///   │   ├── _Vfx/
    ///   │   ├── _Area/
    ///   │   └── _Projectile/
    ///   ├── _Active/
    ///   │   ├── _Character/
    ///   │   ├── _Projectile/
    ///   │   ├── ...
    ///   └── _Debug/
    /// </code>
    ///
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AbilityKit/Battle View Hierarchy Root")]
    public sealed class BattleViewHierarchyRoot : MonoBehaviour
    {
        [Tooltip("Display name prefix used when this root is auto-created. " +
                 "Defaults to [Battle].")]
        [SerializeField] private string _rootName = "[Battle]";

        [Tooltip("If true, the root and its children will be hidden from the Hierarchy " +
                 "when the game is running in a built player. Useful for production builds.")]
        [SerializeField] private bool _hideInPlayerBuild = false;

        private BattleViewHierarchyManager _manager;
        private readonly Dictionary<string, Transform> _pathCache = new Dictionary<string, Transform>(32);
        private int _leaseCount;

        /// <summary>
        /// 此根节点关联的管理器实例，首次访问时按需创建。
        /// </summary>
        public BattleViewHierarchyManager Manager
        {
            get
            {
                if (_manager == null)
                {
                    _manager = new BattleViewHierarchyManager(this);
                    _manager.EnsureStandardLayout();
                }
                return _manager;
            }
        }

        /// <summary>
        /// 为视图功能获取共享战斗层级根节点。最后一次配对的 <see cref="Release"/> 销毁层级及全部受管对象。
        /// </summary>
        public static BattleViewHierarchyRoot Acquire(string displayName = null)
        {
            var root = CreateOrFind(displayName);
            root._leaseCount++;
            _ = root.Manager;
            return root;
        }

        /// <summary>
        /// 在场景根节点创建 <see cref="BattleViewHierarchyRoot"/> GameObject。始终返回可用实例，底层 GameObject 可能已存在。本方法不获取生命周期租约。
        /// </summary>
        public static BattleViewHierarchyRoot CreateOrFind(string displayName = null)
        {
            var resolvedName = string.IsNullOrEmpty(displayName) ? "[Battle]" : displayName;
            var existing = FindByName(resolvedName);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(resolvedName);
            var root = go.AddComponent<BattleViewHierarchyRoot>();
            if (!string.IsNullOrEmpty(displayName))
            {
                root._rootName = displayName;
            }
            return root;
        }

        private static BattleViewHierarchyRoot FindByName(string displayName)
        {
            var roots = Object.FindObjectsOfType<BattleViewHierarchyRoot>(true);
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root != null && string.Equals(root.name, displayName, System.StringComparison.Ordinal))
                {
                    return root;
                }
            }

            return null;
        }

        /// <summary>
        /// 查找活动场景中已有的 <see cref="BattleViewHierarchyRoot"/>，不存在时返回 null。
        /// </summary>
        public static BattleViewHierarchyRoot FindAny()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<BattleViewHierarchyRoot>(FindObjectsInactive.Include);
#else
            return Object.FindObjectOfType<BattleViewHierarchyRoot>(true);
#endif
        }

        /// <summary>
        /// 确保根节点下的子路径存在，返回叶节点变换。缺失的中间变换会被创建并缓存复用。
        /// </summary>
        /// <param name="segments">从浅到深排列的路径段。</param>
        public Transform EnsurePath(string[] segments)
        {
            if (segments == null || segments.Length == 0)
            {
                return transform;
            }

            var current = transform;
            var pathKey = string.Empty;
            for (var i = 0; i < segments.Length; i++)
            {
                var seg = segments[i];
                if (string.IsNullOrEmpty(seg)) continue;

                pathKey = string.IsNullOrEmpty(pathKey) ? seg : pathKey + "/" + seg;
                if (_pathCache.TryGetValue(pathKey, out var cached) && cached != null)
                {
                    current = cached;
                    continue;
                }

                current = EnsureChild(current, seg);
                _pathCache[pathKey] = current;
            }
            return current;
        }

        /// <summary>
        /// 确保 <paramref name="parent"/> 下存在指定名称的单个子 GameObject，并返回其变换。同名子对象已存在时直接返回。
        /// </summary>
        public Transform EnsureChild(Transform parent, string name)
        {
            if (parent == null) parent = transform;
            if (string.IsNullOrEmpty(name)) return parent;

            var existing = parent.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            var tr = go.transform;
            tr.SetParent(parent, worldPositionStays: false);
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
            tr.localScale = Vector3.one;
            return tr;
        }

        /// <summary>
        /// 释放一个功能租约。所有获取该层级的功能均释放租约后，才销毁层级。
        /// </summary>
        public void Release()
        {
#if UNITY_EDITOR
            if (_leaseCount <= 0)
            {
                Debug.Assert(_leaseCount > 0,
                    "[BattleViewHierarchyRoot] Release() called with _leaseCount == 0. " +
                    "Ensure every Acquire() has exactly one matching Release() call.");
                return;
            }
#else
            if (_leaseCount <= 0) return;
#endif

            _leaseCount--;
            if (_leaseCount == 0)
            {
                DestroyHierarchy();
            }
        }

        /// <summary>
        /// 无条件销毁此根节点及全部子节点。功能生命周期拆卸优先使用 <see cref="Release"/>。
        /// </summary>
        public void DestroyHierarchy()
        {
            if (_manager != null)
            {
                _manager.DestroyAll();
            }
            /*
             * 清除路径缓存，避免后续运行获取新根节点后仍保留过期变换引用。
             */
            _pathCache.Clear();
            if (this != null && gameObject != null)
            {
                if (Application.isPlaying) Object.Destroy(gameObject);
                else Object.DestroyImmediate(gameObject);
            }
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(gameObject.name) || gameObject.name == "GameObject")
            {
                gameObject.name = string.IsNullOrEmpty(_rootName) ? "[Battle]" : _rootName;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_rootName))
            {
                _rootName = "[Battle]";
            }
        }
#endif

        /// <summary>
        /// 层级检查器使用的编辑期标志，供外部编辑器窗口读取，运行时不使用。
        /// </summary>
        public bool HideInPlayerBuild => _hideInPlayerBuild;

        /// <summary>
        /// 活动功能生命周期租约数。
        /// </summary>
        public int LeaseCount => _leaseCount;

        /// <summary>
        /// 返回当前路径缓存快照供诊断使用。键为相对此根节点、以“/”分隔的路径，值为变换。
        /// </summary>
        public IReadOnlyDictionary<string, Transform> PathCache => _pathCache;

        /// <summary>
        /// 获取或创建附加在此根节点的统计叠加组件，供视图功能注册对象池提供者，并在检查器显示复用统计。
        /// </summary>
        public BattleViewPoolStatsOverlay GetOrAddStatsOverlay()
        {
            var overlay = GetComponent<BattleViewPoolStatsOverlay>();
            if (overlay == null)
            {
                overlay = gameObject.AddComponent<BattleViewPoolStatsOverlay>();
            }
            return overlay;
        }
    }
}
