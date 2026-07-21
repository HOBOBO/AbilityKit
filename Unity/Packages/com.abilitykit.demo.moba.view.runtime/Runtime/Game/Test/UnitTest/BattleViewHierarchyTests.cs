using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Hierarchy;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.View;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class BattleViewHierarchyTests
    {
        [SetUp]
        public void SetUp()
        {
            DestroyExistingRoot();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyExistingRoot();
        }

        [Test]
        public void Acquire_PrecreatesStableCategoryLayout_AndUsesSharedLease()
        {
            var first = BattleViewHierarchyRoot.Acquire();
            var second = BattleViewHierarchyRoot.Acquire();

            Assert.AreSame(first, second);
            Assert.AreEqual(2, first.LeaseCount);
            Assert.IsNotNull(first.transform.Find("_Active/_Character"));
            Assert.IsNotNull(first.transform.Find("_Active/_Area"));
            Assert.IsNotNull(first.transform.Find("_Active/_FloatingText"));
            Assert.IsNotNull(first.transform.Find("_Pool/_Shell"));
            Assert.IsNotNull(first.transform.Find("_Pool/_FloatingText"));
            Assert.IsNotNull(first.transform.Find("_Debug/_Inspector"));

            first.Release();

            Assert.IsNotNull(second);
            Assert.AreEqual(1, second.LeaseCount);

            second.Release();

            if (Application.isPlaying)
            {
                Assert.AreEqual(0, second.LeaseCount);
            }
            else
            {
                Assert.IsTrue(second == null);
            }
        }

        [Test]
        public void FloatingText_MovesBetweenActiveAndPoolCategories()
        {
            var root = BattleViewHierarchyRoot.CreateOrFind();
            var factory = new BattleWorldFloatingTextFactory(root.Manager);
            var text = factory.Create("100", Vector3.one, Color.red);

            Assert.AreSame(
                root.Manager.GetCategoryRoot(BattleViewCategory.ActiveFloatingText),
                text.GameObject.transform.parent);

            factory.Release(text);

            Assert.AreSame(
                root.Manager.GetCategoryRoot(BattleViewCategory.PoolFloatingText),
                text.GameObject.transform.parent);
            Assert.IsFalse(text.GameObject.activeSelf);

            factory.ClearPool();
        }

        [Test]
        public void PooledShellLoader_MovesRentedShellToEntityCategory()
        {
            var root = BattleViewHierarchyRoot.CreateOrFind();
            var pool = new BattleViewShellPool(
                _ => new GameObject("Shell"),
                defaultCapacity: 0,
                maxSize: 2,
                hierarchy: root.Manager);
            var loader = new PooledBattleViewShellLoader(pool, root.Manager);

            var shell = loader.CreateShellGameObject(10, 42, BattleEntityKind.Monster);

            Assert.AreSame(
                root.Manager.GetCategoryRoot(BattleViewCategory.ActiveMonster),
                shell.transform.parent);

            pool.Return(42, shell);

            Assert.AreSame(
                root.Manager.GetBucketRoot(BattleViewCategory.PoolShell, 42),
                shell.transform.parent);
            Assert.IsFalse(shell.activeSelf);

            pool.Clear();
        }

        [Test]
        public void AreaPlacer_GroupsWorldAreaButPreservesEntityAttachment()
        {
            var root = BattleViewHierarchyRoot.CreateOrFind();
            var placer = new BattleAreaViewObjectPlacer(root.Manager);
            var area = new GameObject("Area");
            var attach = new GameObject("Attach");

            placer.Place(area, 77, null, new Vector3(1f, 2f, 3f));

            Assert.AreSame(
                root.Manager.GetBucketRoot(BattleViewCategory.ActiveArea, 77),
                area.transform.parent);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), area.transform.position);

            placer.Place(area, 77, attach.transform, Vector3.zero);

            Assert.AreSame(attach.transform, area.transform.parent);
            Assert.AreEqual(Vector3.zero, area.transform.localPosition);

            Object.DestroyImmediate(attach);
        }

        private static void DestroyExistingRoot()
        {
            var root = BattleViewHierarchyRoot.FindAny();
            if (root != null)
            {
                root.DestroyHierarchy();
            }
        }
    }
}
