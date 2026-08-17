#nullable enable

using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Demo.Common.Composition;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Common.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Demo.Common.Tests
{
    public sealed class DemoGameplayCompositionTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            DemoLaunchIntent.Clear();
            DemoMultiplayerLaunchIntent.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DemoLaunchIntent.Clear();
            DemoMultiplayerLaunchIntent.Clear();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void LaunchIntentConsumesRequestExactlyOnce()
        {
            var expected = new DemoLaunchRequest(
                DemoGameplayId.Shooter,
                DemoLaunchMode.Local,
                "shooter.local");

            DemoLaunchIntent.Request(in expected);

            Assert.That(DemoLaunchIntent.TryConsume(out var actual), Is.True);
            Assert.That(actual.Gameplay, Is.EqualTo(DemoGameplayId.Shooter));
            Assert.That(actual.Mode, Is.EqualTo(DemoLaunchMode.Local));
            Assert.That(actual.ProfileId, Is.EqualTo("shooter.local"));
            Assert.That(DemoLaunchIntent.TryConsume(out _), Is.False);
        }

        [Test]
        public void CatalogResolvesProfileByGameplayAndModeWhenIdIsOmitted()
        {
            var rootPrefab = Track(new GameObject("MobaRootPrefab"));
            var profile = CreateProfile(
                "moba.local",
                DemoGameplayId.Moba,
                DemoLaunchMode.Local,
                rootPrefab);
            var catalog = CreateCatalog(profile);
            var request = new DemoLaunchRequest(DemoGameplayId.Moba, DemoLaunchMode.Local);

            var found = catalog.TryFind(in request, out var actual, out var error);

            Assert.That(found, Is.True, error);
            Assert.That(actual, Is.SameAs(profile));
        }

        [Test]
        public void BootstrapInstantiatesAndReleasesSelectedRoot()
        {
            var rootPrefab = Track(new GameObject("ShooterRootPrefab"));
            var profile = CreateProfile(
                "shooter.local",
                DemoGameplayId.Shooter,
                DemoLaunchMode.Local,
                rootPrefab);
            var catalog = CreateCatalog(profile);
            var bootstrapObject = Track(new GameObject("DemoGameplayBootstrap"));
            var bootstrap = bootstrapObject.AddComponent<DemoGameplayBootstrap>();
            SetField(bootstrap, "catalog", catalog);
            var request = new DemoLaunchRequest(
                DemoGameplayId.Shooter,
                DemoLaunchMode.Local,
                profile.ProfileId);
            DemoLaunchIntent.Request(in request);

            var launched = bootstrap.TryLaunch(out var error);

            Assert.That(launched, Is.True, error);
            Assert.That(bootstrap.ActiveProfile, Is.SameAs(profile));
            Assert.That(bootstrap.ActiveRoot, Is.Not.Null);
            Assert.That(bootstrap.ActiveRoot, Is.Not.SameAs(rootPrefab));
            Assert.That(bootstrap.ActiveRoot!.scene, Is.EqualTo(bootstrapObject.scene));

            bootstrap.Shutdown();

            Assert.That(bootstrap.ActiveRoot, Is.Null);
            Assert.That(bootstrap.ActiveProfile, Is.Null);
        }

        [Test]
        public void BootstrapRejectsMismatchedMultiplayerIntentAndClearsIt()
        {
            var rootPrefab = Track(new GameObject("MobaRootPrefab"));
            var profile = CreateProfile(
                "moba.multiplayer",
                DemoGameplayId.Moba,
                DemoLaunchMode.Multiplayer,
                rootPrefab);
            var catalog = CreateCatalog(profile);
            var bootstrapObject = Track(new GameObject("DemoGameplayBootstrap"));
            var bootstrap = bootstrapObject.AddComponent<DemoGameplayBootstrap>();
            SetField(bootstrap, "catalog", catalog);
            var request = new DemoLaunchRequest(
                DemoGameplayId.Moba,
                DemoLaunchMode.Multiplayer,
                profile.ProfileId);
            DemoLaunchIntent.Request(in request);
            DemoMultiplayerLaunchIntent.Request(
                DemoMultiplayerGameplay.Shooter,
                new DemoMultiplayerLaunchRequest(
                    "127.0.0.1",
                    4000,
                    "dev",
                    "local",
                    "account",
                    "token",
                    System.TimeSpan.FromSeconds(5)));
            LogAssert.Expect(
                LogType.Error,
                "[DemoGameplayBootstrap] Gameplay mismatch: composition requested Moba, "
                + "but multiplayer intent requested Shooter.");

            var launched = bootstrap.TryLaunch(out var error);

            Assert.That(launched, Is.False);
            Assert.That(error, Does.Contain("Gameplay mismatch"));
            Assert.That(DemoMultiplayerLaunchIntent.TryPeek(out _, out _), Is.False);
            Assert.That(bootstrap.ActiveRoot, Is.Null);
        }

        private DemoGameplayProfileSO CreateProfile(
            string profileId,
            DemoGameplayId gameplay,
            DemoLaunchMode mode,
            GameObject rootPrefab)
        {
            var profile = Track(ScriptableObject.CreateInstance<DemoGameplayProfileSO>());
            SetField(profile, "profileId", profileId);
            SetField(profile, "gameplay", gameplay);
            SetField(profile, "mode", mode);
            SetField(profile, "rootPrefab", rootPrefab);
            return profile;
        }

        private DemoGameplayCatalogSO CreateCatalog(params DemoGameplayProfileSO[] profiles)
        {
            var catalog = Track(ScriptableObject.CreateInstance<DemoGameplayCatalogSO>());
            SetField(catalog, "profiles", new List<DemoGameplayProfileSO>(profiles));
            return catalog;
        }

        private T Track<T>(T instance) where T : Object
        {
            _objects.Add(instance);
            return instance;
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field!.SetValue(target, value);
        }
    }
}
