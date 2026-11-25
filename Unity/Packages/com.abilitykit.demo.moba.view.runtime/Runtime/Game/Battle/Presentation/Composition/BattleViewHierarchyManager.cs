using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Game.Battle.Hierarchy
{
    /// <summary>
    ///
    ///
    /// 纯 C# 管理器，拥有 <see cref="BattleViewHierarchyRoot"/> 下分类子根节点的 <see cref="Transform"/> 实例。对象池、生成器及工厂向管理器请求实例父变换，让 Unity 层级视图保持有序，并可跨运行复用。
    ///
    /// 线程要求：所有方法必须在 Unity 主线程调用。
    ///
    ///
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
            BattleViewCategory.ActiveMinion,
            BattleViewCategory.ActiveArea,
            BattleViewCategory.ActiveVfx,
            BattleViewCategory.ActiveFloatingText,
            BattleViewCategory.Debug,
        };

        /// <summary>
        /// 创建拥有 <paramref name="root"/> 子节点的管理器。
        /// </summary>
        public BattleViewHierarchyManager(BattleViewHierarchyRoot root)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// 所有分类变换的共同层级根节点。
        /// </summary>
        public BattleViewHierarchyRoot Root => _root;

        /// <summary>
        /// 预先创建稳定分类目录，使每种支持的表现类型在首个对象生成前就可见。
        /// </summary>
        public void EnsureStandardLayout()
        {
            for (var i = 0; i < StandardCategories.Length; i++)
            {
                GetCategoryRoot(StandardCategories[i]);
            }
        }

        /// <summary>
        ///
        /// 获取或按需创建顶层分类变换。
        /// (e.g. <c>[Battle]/_Pool/_Shell</c>).
        ///
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
        ///
        /// 获取或按需创建分类根节点下的分桶变换，供对象池按模板 ID 组织非活动实例。
        /// (e.g. <c>[Battle]/_Pool/_Shell/model_42</c>).
        ///
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
        /// 获取或按需创建分类根节点下的具名子变换，用于按枚举种类分组，如区域效果的 Model / Range / Vfx。
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
        /// 将 GameObject 直接挂到分类根节点，不隐含活动或池化生命周期状态。
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
        /// 将活动 GameObject 直接挂到对应活动视图分类根节点的便捷重载，不使用分桶。
        /// </summary>
        public void ParentActive(BattleViewCategory category, GameObject instance)
        {
            ParentToCategory(category, instance);
        }

        /// <summary>
        /// 将活动 GameObject 挂到活动视图分类的分桶根节点，如 <c>[Battle]/_Active/_Projectile/tpl_42</c>，便于在层级中按活动分桶查看 GameObject。
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
        ///
        /// 将池化 GameObject 挂到对应对象池分桶根节点。
        /// (e.g. <c>[Battle]/_Pool/_Shell/model_42</c>).
        ///
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
        /// 将 GameObject 从层级管理的父节点脱离，并将局部变换归零。供对象池 <c>ResetInstance</c> 在分类根节点未持有对象时保持池对象整洁。
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
        /// 销毁所有分类变换下的 GameObject，并清除缓存变换引用。由宿主 OnDetach 调用，确保战斗拆卸后无孤立对象。
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

        /// <summary>
        /// 管理器当前跟踪的分类根节点数。
        /// </summary>
        public int CategoryCount => _categoryRoots.Count;

        /// <summary>
        /// 管理器当前跟踪的分桶子根节点数。
        /// </summary>
        public int BucketCount => _bucketRoots.Count;
    }
}
