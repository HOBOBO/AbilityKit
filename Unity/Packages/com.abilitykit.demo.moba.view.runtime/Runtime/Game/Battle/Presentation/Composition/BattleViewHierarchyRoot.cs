using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Battle.Hierarchy
{
    /// <summary>
    /// Scene-level MonoBehaviour root that hosts the categorized battle view
    /// hierarchy. Owns one <see cref="BattleViewHierarchyManager"/> instance and
    /// exposes convenience methods for creating / destroying child paths.
    ///
    /// Spawned at runtime by <c>BattleViewFeature.OnAttach</c> (and the
    /// <c>ConfirmedBattleViewFeature</c> equivalent). Torn down in
    /// <c>OnDetach</c> by destroying the root GameObject.
    ///
    /// Hierarchy produced (managed by the inner manager):
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
        /// The manager instance associated with this root. Lazily created on first access.
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
        /// Acquires the shared battle hierarchy root for a view feature. The final
        /// matching <see cref="Release"/> destroys the hierarchy and all managed objects.
        /// </summary>
        public static BattleViewHierarchyRoot Acquire(string displayName = null)
        {
            var root = CreateOrFind(displayName);
            root._leaseCount++;
            _ = root.Manager;
            return root;
        }

        /// <summary>
        /// Creates a new <see cref="BattleViewHierarchyRoot"/> GameObject at scene root.
        /// Always returns a usable instance (the underlying GameObject may already exist).
        /// This method does not acquire a lifecycle lease.
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
        /// Find any existing <see cref="BattleViewHierarchyRoot"/> in the active scene,
        /// or return null if none exists.
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
        /// Ensure a child path under the root exists and return the leaf transform.
        /// Intermediate transforms are created if missing and cached for reuse.
        /// </summary>
        /// <param name="segments">Path segments ordered from shallowest to deepest.</param>
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
        /// Ensure a single child GameObject with the given name exists under
        /// <paramref name="parent"/> and return its transform. Returns the existing
        /// child if one with the same name is already present.
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
        /// Releases one feature lease. The hierarchy is destroyed only after every
        /// feature that acquired it has released its lease.
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
        /// Unconditionally destroys this root and all children. Prefer
        /// <see cref="Release"/> for feature lifecycle teardown.
        /// </summary>
        public void DestroyHierarchy()
        {
            if (_manager != null)
            {
                _manager.DestroyAll();
            }
            // Clear the path cache so stale transform references do not linger after
            // a new root is acquired on a subsequent run.
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
        /// Edit-time flag used by the hierarchy inspector. Read by external
        /// editor windows; the value is not used at runtime.
        /// </summary>
        public bool HideInPlayerBuild => _hideInPlayerBuild;

        /// <summary>Number of active feature lifecycle leases.</summary>
        public int LeaseCount => _leaseCount;

        /// <summary>
        /// Returns a snapshot of the current path cache for diagnostic purposes.
        /// Key is the "/" delimited path relative to this root, value is the transform.
        /// </summary>
        public IReadOnlyDictionary<string, Transform> PathCache => _pathCache;

        /// <summary>
        /// Get-or-create the stats overlay component attached to this root.
        /// Used by view features to register pool providers and display reuse
        /// statistics in the inspector.
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
