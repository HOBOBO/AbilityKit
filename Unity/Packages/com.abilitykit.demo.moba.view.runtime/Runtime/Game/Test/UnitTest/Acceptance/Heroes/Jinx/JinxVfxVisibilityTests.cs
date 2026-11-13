using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class JinxVfxVisibilityTests
    {
        [TestCase("AA_Minigun_Projectile")]
        [TestCase("AA_Rocket_Projectile")]
        [TestCase("E_Trap")]
        [TestCase("W_Projectile")]
        [TestCase("R_Projectile")]
        public void FormalResourceEmitsVisibleParticlesNearItsRuntimeAnchor(string suffix)
        {
            var prefab = Resources.Load<GameObject>("MobaVfx/Jinx/" + suffix);
            Assert.IsNotNull(prefab);
            var instance = Object.Instantiate(prefab);
            try
            {
                var count = 0;
                foreach (var system in instance.GetComponentsInChildren<ParticleSystem>())
                {
                    system.Simulate(0.15f, false, true, true);
                    var renderer = system.GetComponent<ParticleSystemRenderer>();
                    TestContext.WriteLine($"{system.name}: particles={system.particleCount}, pos={system.transform.position}, scale={system.transform.lossyScale}, bounds={renderer.bounds}, material={renderer.sharedMaterial?.name}");
                    if (renderer.enabled && renderer.bounds.SqrDistance(Vector3.up) < 9f)
                        count += system.particleCount;
                }
                if (suffix != "W_Projectile")
                    Assert.Greater(count, 0, "The configured formal prefab must emit particles at its gameplay anchor.");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    CaptureVisibleResource(instance, suffix);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static void CaptureVisibleResource(GameObject instance, string suffix)
        {
            var cameraObject = new GameObject("JinxFormalVfxAcceptanceCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var scene = EditorSceneManager.NewPreviewScene();
            SceneManager.MoveGameObjectToScene(instance, scene);
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            camera.scene = scene;
            var target = RenderTexture.GetTemporary(512, 512, 24);
            var texture = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.transform.position = new Vector3(4f, 5f, -6f);
                camera.transform.LookAt(Vector3.up * 0.5f);
                camera.orthographic = true;
                camera.orthographicSize = 3f;
                // The formal soft-particle shaders sample scene depth. Match the
                // production BattleViewCameraController's render prerequisite.
                camera.depthTextureMode = DepthTextureMode.Depth;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
                camera.targetTexture = target;
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                    "../../local/Logs/jinx-formal-vfx-" + suffix + ".png"));
                instance.SetActive(false);
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                texture.Apply();
                var hidden = texture.GetPixels32();
                instance.SetActive(true);
                var maxContribution = 0;
                foreach (var time in new[] { 0.03f, 0.1f, 0.25f, 0.5f, 1f, 3f })
                {
                    foreach (var system in instance.GetComponentsInChildren<ParticleSystem>())
                        system.Simulate(time, false, true, true);
                    camera.Render();
                    texture.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                    texture.Apply();
                    var visible = texture.GetPixels32();
                    var contribution = 0;
                    for (var i = 0; i < visible.Length; i++) if (!visible[i].Equals(hidden[i])) contribution++;
                    TestContext.WriteLine($"{suffix}: time={time}, rendered pixels={contribution}");
                    if (suffix.EndsWith("_Projectile") && time >= 1f)
                        Assert.Greater(contribution, 20,
                            "A flying visual must remain visible until gameplay impact/expiry, beyond the extracted demo clip's short burst.");
                    if (contribution > maxContribution)
                    {
                        maxContribution = contribution;
                        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                    }
                }
                TestContext.WriteLine($"{suffix}: max rendered pixels={maxContribution}, capture={path}");
                Assert.Greater(maxContribution, 20, "The formal resource must contribute visible pixels, not just instantiate components.");
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject);
                SceneManager.MoveGameObjectToScene(instance, SceneManager.GetActiveScene());
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
