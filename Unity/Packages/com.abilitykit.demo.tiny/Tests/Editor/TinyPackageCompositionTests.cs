using System.Linq;
using AbilityKit.Demo.Common.Composition;
using AbilityKit.Demo.Common.Gameplay;
using AbilityKit.Demo.Tiny.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Demo.Tiny.Tests
{
    public sealed class TinyPackageCompositionTests
    {
        private const string Package = "Packages/com.abilitykit.demo.tiny/";

        [Test]
        public void StarterProfileReferencesUsableTinyRootAndScene()
        {
            var profile = AssetDatabase.LoadAssetAtPath<DemoGameplayProfileSO>(
                Package + "Composition/Profiles/TinyMultiplayerProfile.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<DemoGameplayCatalogSO>(
                Package + "Composition/Profiles/TinyGameplayCatalog.asset");
            var bootstrapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Package + "Composition/Prefabs/TinyGameplayBootstrap.prefab");
            var scenePath = Package + "Scenes/TinyDemoGameplayScene.unity";
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

            Assert.That(profile, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(profile.ProfileId, Is.EqualTo("tiny-multiplayer"));
            Assert.That(profile.Gameplay, Is.EqualTo(DemoGameplayId.Tiny));
            Assert.That(profile.Mode, Is.EqualTo(DemoLaunchMode.Multiplayer));
            Assert.That(profile.RootPrefab, Is.Not.Null);
            Assert.That(profile.RootPrefab.GetComponent<TinyGameplayRoot>(), Is.Not.Null);

            var request = new DemoLaunchRequest(DemoGameplayId.Tiny,
                DemoLaunchMode.Multiplayer, profile.ProfileId);
            Assert.That(catalog.TryFind(in request, out var found, out var error), Is.True, error);
            Assert.That(found, Is.SameAs(profile));

            Assert.That(scene, Is.Not.Null);
            Assert.That(bootstrapPrefab, Is.Not.Null);
            var bootstrap = bootstrapPrefab.GetComponent<DemoGameplayBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            var reference = new SerializedObject(bootstrap).FindProperty("catalog").objectReferenceValue;
            Assert.That(reference, Is.SameAs(catalog));
            Assert.That(AssetDatabase.GetDependencies(scenePath), Does.Contain(
                Package + "Composition/Prefabs/TinyGameplayBootstrap.prefab"));
            Assert.That(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == scenePath), Is.True);
        }
    }
}
