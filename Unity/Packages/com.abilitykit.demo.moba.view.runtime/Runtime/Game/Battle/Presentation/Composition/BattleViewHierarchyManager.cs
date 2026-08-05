using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Battle.Hierarchy
{
    /// <summary>
    /// Pure C# manager that owns the categorized sub-root <see cref="Transform"/>
    /// instances under a <see cref="BattleViewHierarchyRoot"/>. Pools, spawners and
    /// factories ask the manager for the transform to parent their instances to,
    /// so the Unity Hierarchy view remains organized and reusable across runs.
    ///
    /// Threading: all methods must be called from the Unity main thread.
    /// </summary>
    public sealed class BattleViewHierarchyManager
    {
        private readonly BattleViewHierarchyRoot _root;
        private readonly Dictionary<BattleViewCategory, Transform> _categoryRoots = new Dictionary<BattleViewCategory, Transform>(32);
        private readonly Dictionary<(BattleViewCategory, int), Transform> _bucketRoots =
            new Dictionary<(BattleViewCategory, int), Transform>(64);
        private readonly Dictionary<(BattleViewCategory, string), Transform> _namedRoots =
            new Dictionary<(BattleViewCategory, string), Transform>(16);

        private static readonly BattleViewCategory[] StandardCategories =
        {
            BattleViewCategory.PoolShell,
            BattleViewCategory.PoolVfx,
            BattleViewCategory.PoolArea,
            BattleViewCategory.PoolProjectile,
            BattleViewCategory.PoolHud,
            BattleViewCategory.PoolFloatingText,
            BattleViewCategory.ActiveCharacter,
            BattleViewCategory.ActiveProjectile,
            BattleViewCategory.ActiveSummon,
            BattleViewCategory.ActiveClone,
            BattleViewCategory.ActiveTurret,
            BattleViewCategory.ActiveMonster,
            BattleViewCategory.ActiveBuilding,
            BattleViewCategory.ActiveArea,
            BattleViewCategory.ActiveVfx,
            BattleViewCategory.ActiveFloatingText,
            BattleViewCategory.Debug,
        };

        /// <summary>
        /// Create a manager that owns children of <paramref name="root"/>.
        /// </summary>
        public BattleViewHierarchyManager(BattleViewHierarchyRoot root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>The hierarchy root that all category transforms are parented under.</summary>
        public BattleViewHierarchyRoot Root => _root;

        /// <summary>
        /// Creates the stable category directory layout up front so every supported
        /// presentation type remains visible even before its first object is spawned.
        /// </summary>
        public void EnsureStandardLayout()
        {
            for (var i = 0; i < StandardCategories.Length; i++)
            {
                GetCategoryRoot(StandardCategories[i]);
            }
        }

        /// <summary>
        /// Get-or-lazily-create the top-level category transform
        /// (e.g. <c>[Battle]/_Pool/_Shell</c>).
        /// </summary>
        public Transform GetCategoryRoot(BattleViewCategory category)
        {
            if (_categoryRoots.TryGetValue(category, out var cached) && cached != null)
            {
                return cached;
            }

            var segments = BattleViewCategoryPaths.GetPathSegments(category);
            var tr = _root.EnsurePath(segments);
            _categoryRoots[category] = tr;
            return tr;
        }

        /// <summary>
        /// Get-or-lazily-create a per-bucket transform under the category root.
        /// Used by pools to group inactive instances by template id
        /// (e.g. <c>[Battle]/_Pool/_Shell/model_42</c>).
        /// </summary>
        public Transform GetBucketRoot(BattleViewCategory category, int bucketKey)
        {
            var key = (category, bucketKey);
            if (_bucketRoots.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var parent = GetCategoryRoot(category);
            var name = FormatBucketName(category, bucketKey);
            var tr = _root.EnsureChild(parent, name);
            _bucketRoots[key] = tr;
            return tr;
        }

        /// <summary>
        /// Get-or-lazily-create a named sub-transform under the category root.
        /// Used for grouping by enum kind (e.g. Model / Range / Vfx for area effects).
        /// </summary>
        public Transform GetNamedRoot(BattleViewCategory category, string subName)
        {
            if (string.IsNullOrEmpty(subName))
            {
                return GetCategoryRoot(category);
            }

            var key = (category, subName);
            if (_namedRoots.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var parent = GetCategoryRoot(category);
            var tr = _root.EnsureChild(parent, subName);
            _namedRoots[key] = tr;
            return tr;
        }

        /// <summary>
        /// Parents a GameObject directly under a category root without implying an
        /// active or pooled lifecycle state.
        /// </summary>
        public void ParentToCategory(BattleViewCategory category, GameObject instance)
        {
            if (instance == null) return;
            var parent = GetCategoryRoot(category);
            if (instance.transform.parent != parent)
            {
                instance.transform.SetParent(parent, worldPositionStays: false);
            }
        }

        /// <summary>
        /// Parent an active GameObject under the appropriate active-view category root.
        /// Convenience overload that parents directly under the category root (not a bucket).
        /// </summary>
        public void ParentActive(BattleViewCategory category, GameObject instance)
        {
            ParentToCategory(category, instance);
        }

        /// <summary>
        /// Parent an active GameObject under a per-bucket active-view category root
        /// (e.g. <c>[Battle]/_Active/_Projectile/tpl_42</c>). Useful when you want to
        /// see one GameObject per active bucket in the hierarchy.
        /// </summary>
        public void ParentActive(BattleViewCategory category, int bucketKey, GameObject instance)
        {
            if (instance == null) return;
            var parent = GetBucketRoot(category, bucketKey);
            if (instance.transform.parent != parent)
            {
                instance.transform.SetParent(parent, worldPositionStays: false);
            }
        }

        /// <summary>
        /// Parent a pooled GameObject under the appropriate pool-bucket root
        /// (e.g. <c>[Battle]/_Pool/_Shell/model_42</c>).
        /// </summary>
        public void ParentPooled(BattleViewCategory category, int bucketKey, GameObject instance)
        {
            if (instance == null) return;
            var parent = GetBucketRoot(category, bucketKey);
            if (instance.transform.parent != parent)
            {
                instance.transform.SetParent(parent, worldPositionStays: false);
            }
        }

        /// <summary>
        /// Detach a GameObject from any hierarchy-managed parent and zero its local
        /// transform. Used by pool <c>ResetInstance</c> to keep pooled objects tidy
        /// when they are not actively held by the category root.
        /// </summary>
        public void DetachToCategory(BattleViewCategory category, int bucketKey, GameObject instance)
        {
            if (instance == null) return;
            var parent = GetBucketRoot(category, bucketKey);
            var tr = instance.transform;
            if (tr.parent != parent)
            {
                tr.SetParent(parent, worldPositionStays: false);
            }
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
            tr.localScale = Vector3.one;
        }

        /// <summary>
        /// Destroy every GameObject under all category transforms and clear
        /// cached transform references. Called from the host's OnDetach to
        /// guarantee no orphans are left when the battle tears down.
        /// </summary>
        public void DestroyAll()
        {
            foreach (var kvp in _categoryRoots)
            {
                var tr = kvp.Value;
                if (tr == null) continue;
                DestroyChildren(tr);
            }

            _categoryRoots.Clear();
            _bucketRoots.Clear();
            _namedRoots.Clear();
        }

        private static void DestroyChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child == null) continue;
                var go = child.gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static string FormatBucketName(BattleViewCategory category, int bucketKey)
        {
            if (category == BattleViewCategory.ActiveProjectile || category == BattleViewCategory.PoolProjectile)
            {
                return $"tpl_{bucketKey}";
            }
            if (category == BattleViewCategory.ActiveVfx || category == BattleViewCategory.PoolVfx)
            {
                return $"vfx_{bucketKey}";
            }
            if (category == BattleViewCategory.PoolArea || category == BattleViewCategory.ActiveArea)
            {
                return $"area_{bucketKey}";
            }
            return $"id_{bucketKey}";
        }

        /// <summary>Number of category roots currently tracked by the manager.</summary>
        public int CategoryCount => _categoryRoots.Count;

        /// <summary>Number of bucket sub-roots currently tracked by the manager.</summary>
        public int BucketCount => _bucketRoots.Count;
    }
}