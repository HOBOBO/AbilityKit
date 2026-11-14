#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Editor
{
    /// <summary>Offline visual measurement of the actual mounted and published resources.</summary>
    public static class LuxPresentationAudit
    {
        public static void Capture()
        {
            if(!Application.isBatchMode) throw new System.InvalidOperationException("Run this offline review in an isolated batch project.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (System.Environment.GetCommandLineArgs().Contains("-luxRebuild")) LuxSkillSync.BuildPresentation();
            var args = System.Environment.GetCommandLineArgs();
            var option = System.Array.IndexOf(args,"-luxReviewOutput");
            var dir = option>=0 ? args[option+1] : Path.GetFullPath("../local/Logs/lux-spatial-review");
            Directory.CreateDirectory(dir);
            var camera = new GameObject("Review camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f,.14f,.18f);
            camera.depthTextureMode=DepthTextureMode.Depth;
            camera.fieldOfView=40;
            camera.nearClipPlane = .3f; camera.farClipPlane = 200;
            var light = new GameObject("Review light").AddComponent<Light>();
            light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45,-30,0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0,-.06f,12);
            floor.transform.localScale = new Vector3(45,.1f,50);
            floor.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Standard")) { color = new Color(.25f,.28f,.32f) };
            var grid = new Material(Shader.Find("Unlit/Color")) {color=new Color(.36f,.39f,.43f)};
            for(var z=-5;z<=35;z++) Mark(new Vector3(0,0,z),new Vector3(12,.006f,.012f),grid);
            for(var x=-6;x<=6;x++) Mark(new Vector3(x,0,15),new Vector3(.012f,.006f,40),grid);
            var outline=new Material(Shader.Find("Unlit/Color")) {color=new Color(.2f,.8f,.8f)};
            Mark(new Vector3(-1,.015f,16.7f),new Vector3(.04f,.01f,33.4f),outline);
            Mark(new Vector3(1,.015f,16.7f),new Vector3(.04f,.01f,33.4f),outline);
            Mark(new Vector3(0,.015f,33.4f),new Vector3(2,.01f,.04f),outline);
            var records = new JArray();
            foreach (var name in new[] { "Model", "Walk", "AA_Projectile", "Q_Projectile", "W_Projectile", "W_ShieldUp", "Q_Cage", "Passive_Mark", "E_Charge", "E_Explosion", "R_Channel" })
            {
                var owned = new[] {"Q_Projectile","W_Projectile","Q_Cage","Passive_Mark","E_Charge","E_Explosion","R_Channel"}.Contains(name);
                var key = owned ? "moba/presentation/lux/"+name : "ChampionVfx/12.Lux_DarkStar/Lux_DarkStar_"+(name=="Walk"?"Model":name);
                var instance = Object.Instantiate(Resources.Load<GameObject>(key));
                if(name=="E_Charge")instance.transform.localScale*=3.1f;
                if(name=="E_Explosion")instance.transform.localScale*=.1f;
                instance.transform.position = Vector3.zero;
                var animator = instance.GetComponentInChildren<Animator>();
                if(animator) { animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Update(0); }
                foreach(var t in name=="R_Channel" ? new[]{.3f,1.15f,1.45f} : new[]{.5f})
                {
                    if(animator) { animator.Play(name=="Walk"?Animator.StringToHash("Walk"):0,0,name=="R_Channel"||name=="Walk"?t:t/1.933333f); animator.Update(0); }
                    var center=name=="R_Channel" ? new Vector3(0,0,16):new Vector3(0,1,0);
                    camera.transform.position=center+new Vector3(8,7,-9).normalized*(name=="R_Channel"?55:9.5f);
                    camera.transform.LookAt(center);
                    foreach(var p in instance.GetComponentsInChildren<ParticleSystem>())
                    { p.useAutoRandomSeed=false; p.randomSeed=42; p.Simulate(t,false,true,true); }
                    var rows = new JArray();
                    foreach(var renderer in instance.GetComponentsInChildren<Renderer>())
                    {
                        var bounds = renderer.bounds;
                        if(renderer is SkinnedMeshRenderer skin)
                        {
                            var mesh = new Mesh(); skin.BakeMesh(mesh,true);
                            var points=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();
                            if(points.Length>0) { bounds=new Bounds(points[0],Vector3.zero); foreach(var v in points) bounds.Encapsulate(v); }
                            Object.DestroyImmediate(mesh);
                        }
                        var system=renderer.GetComponent<ParticleSystem>();
                        var record=new JObject { ["path"]=AnimationUtility.CalculateTransformPath(renderer.transform,instance.transform),
                            ["enabled"]=renderer.enabled,
                            ["materials"]=JArray.FromObject(renderer.sharedMaterials.Select(m=>m ? m.name+":"+m.shader.name : "missing")),
                            ["scale"]=renderer.transform.lossyScale.ToString("F4"),
                            ["rendererBounds"]=renderer.bounds.ToString("F4"),
                            ["min"]=JArray.FromObject(new[]{bounds.min.x,bounds.min.y,bounds.min.z}),
                            ["max"]=JArray.FromObject(new[]{bounds.max.x,bounds.max.y,bounds.max.z}),
                            ["particles"]=system ? system.particleCount : 0 };
                        if(renderer is ParticleSystemRenderer particles && particles.enabled && system.particleCount>0)
                        {
                            var mesh=new Mesh(); particles.BakeMesh(mesh,camera,true); mesh.RecalculateBounds();
                            var origin=particles.transform.position;
                            record["geometryMin"]=JArray.FromObject(new[]{mesh.bounds.min.x+origin.x,mesh.bounds.min.y+origin.y,mesh.bounds.min.z+origin.z});
                            record["geometryMax"]=JArray.FromObject(new[]{mesh.bounds.max.x+origin.x,mesh.bounds.max.y+origin.y,mesh.bounds.max.z+origin.z});
                            if(particles.mesh)record["sourceMeshBounds"]=particles.mesh.bounds.ToString("F4");
                            if(particles.mesh && particles.name=="PS_RangeCircle")
                            {
                                record["sourceMeshVertices"]=JArray.FromObject(particles.mesh.vertices.Select(v=>v.ToString("F5")));
                                var values=new ParticleSystem.Particle[system.particleCount];system.GetParticles(values);
                                record["particleSizes"]=JArray.FromObject(values.Select(v=>v.GetCurrentSize3D(system).ToString("F5")));
                                record["mainScaleMode"]=system.main.scalingMode.ToString();
                                record["geometryVertices"]=JArray.FromObject(mesh.vertices.Select(v=>v.ToString("F5")));
                            }
                            Object.DestroyImmediate(mesh);
                        }
                        rows.Add(record);
                    }
                    records.Add(new JObject { ["name"]=name,["time"]=t,["renderers"]=rows });
                    camera.orthographicSize=name=="R_Channel"?19:3.4f;
                    var rt=new RenderTexture(1280,720,24); var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                    texture.ReadPixels(new Rect(0,0,1280,720),0,0); texture.Apply();
                    File.WriteAllBytes(Path.Combine(dir,name+"-"+t.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),texture.EncodeToPNG());
                    camera.targetTexture=null; RenderTexture.active=null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(texture);
                }
                Object.DestroyImmediate(instance);
            }
            File.WriteAllText(Path.Combine(dir,"bounds.json"),records.ToString());
            Debug.Log("[LuxPresentationAudit] "+dir);
        }

        private static void Mark(Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
#endif
