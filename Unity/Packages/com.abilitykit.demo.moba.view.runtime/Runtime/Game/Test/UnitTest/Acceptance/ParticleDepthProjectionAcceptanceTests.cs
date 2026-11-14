using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class ParticleDepthProjectionAcceptanceTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Soft_particles_remain_visible_and_fade_at_opaque_intersections(bool orthographic)
        {
            var root = new GameObject("Particle depth projection review");
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform);
            camera.transform.position = new Vector3(-1000, 0, 0);
            camera.orthographic = orthographic;
            camera.orthographicSize = 2;
            camera.nearClipPlane = .3f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.allowHDR = false;
            // Load the referenced asset: Shader.Find alone can omit shaders
            // unloaded between EditMode fixtures in a batch runner.
            var source = Resources.Load<GameObject>("moba/presentation/karthus/Q_Warning");
            Shader particleShader = null;
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                if (renderer.sharedMaterial != null && renderer.sharedMaterial.name == "Area_01")
                    particleShader = renderer.sharedMaterial.shader;
            Assert.IsNotNull(particleShader);
            var material = new Material(particleShader);
            material.SetTexture("_MainTexture", Texture2D.whiteTexture);
            material.SetColor("_Color", Color.green);
            material.SetFloat("_Glow", 1);
            material.SetFloat("_Depth", .5f);
            var background = new Material(Shader.Find("Standard"));
            background.color = Color.black;
            var mesh = new Mesh
            {
                vertices = new[] {new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0)},
                uv = new[] {Vector2.zero, Vector2.up, Vector2.one, Vector2.right},
                colors = new[] {Color.white, Color.white, Color.white, Color.white},
                triangles = new[] {0,1,2,0,2,3}
            };
            var quad = new GameObject("Particle quad");
            quad.transform.SetParent(root.transform);
            quad.transform.position = new Vector3(-1000, 0, 5);
            quad.layer = 30;
            quad.AddComponent<MeshFilter>().sharedMesh = mesh;
            quad.AddComponent<MeshRenderer>().sharedMaterial = material;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            wall.transform.localScale = new Vector3(10, 10, .1f);
            wall.layer = 30;
            wall.GetComponent<Renderer>().sharedMaterial = background;
            var rt = new RenderTexture(64, 64, 24);
            var texture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                var clear = Sample(6.05f);
                var intersection = Sample(5.15f);
                var occluded = Sample(4.05f);
                quad.SetActive(false);
                var opaqueOnly = Sample(4.05f);
                Assert.Greater(clear, .7f, "Visible particles must survive depth fading in both projections.");
                Assert.Greater(intersection, .05f);
                Assert.Less(intersection, clear - .1f, "The intersection must retain its soft fade.");
                // Standard's black albedo can still reflect the scene probe.
                // Occlusion means the particle contributes no extra light.
                Assert.AreEqual(opaqueOnly, occluded, .01f, "Opaque geometry must still occlude the particle.");
                Debug.Log($"Particle depth projection ortho={orthographic}: visible={clear}, intersection={intersection}, occluded={occluded}, opaque={opaqueOnly}");
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                rt.Release();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(background);
            }

            float Sample(float wallZ)
            {
                wall.transform.position = new Vector3(-1000, 0, wallZ);
                camera.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                texture.Apply();
                return texture.GetPixel(32, 32).g;
            }
        }
    }
}
