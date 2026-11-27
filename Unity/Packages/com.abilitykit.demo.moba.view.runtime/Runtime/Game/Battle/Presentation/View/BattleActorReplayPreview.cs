#if UNITY_EDITOR
using System;
using AbilityKit.Game.Battle.Moba.Config;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 在隔离的编辑器场景中渲染两份记录姿态，不创建或推进游戏世界。
    /// </summary>
    internal sealed class BattleActorReplayPreview : IDisposable
    {
        private readonly PreviewRenderUtility _preview;
        private GameObject _authorityFlight;
        private GameObject _predictionFlight;
        private Material _predictionFlightMaterial;
        private readonly Material _authorityMaterial;
        private readonly GameObject _authority;
        private readonly GameObject _prediction;
        private readonly float _actorRadius;

        internal GameObject AuthorityModel => _authority;
        internal GameObject PredictionModel => _prediction;

        private BattleActorReplayPreview(BattleViewModelPrefab model)
        {
            _preview = new PreviewRenderUtility();
            try
            {
                _authority = CreateModel(model);
                _prediction = CreateModel(model);
                _authorityMaterial = CreateAuthorityMaterial();
                ApplyAuthorityMaterial(_authority, _authorityMaterial);
                foreach (var renderer in _prediction.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled) continue;
                    _actorRadius = Mathf.Max(_actorRadius, (renderer.bounds.center - _prediction.transform.position).magnitude
                        + renderer.bounds.extents.magnitude);
                }

                _preview.camera.orthographic = true;
                _preview.camera.clearFlags = CameraClearFlags.SolidColor;
                _preview.camera.backgroundColor = new Color(0.22f, 0.24f, 0.27f);
                _preview.camera.nearClipPlane = 0.01f;
                _preview.lights[0].intensity = 1.3f;
                _preview.lights[0].transform.rotation = Quaternion.Euler(40f, -25f, 0f);
                _preview.lights[1].intensity = 0.6f;
                _preview.ambientColor = Color.gray;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// 使用正式模型表与资源加载器；不会用占位模型冒充正式资源。
        /// </summary>
        internal static BattleActorReplayPreview Create(int modelId)
        {
            var model = new BattleViewModelPrefabResolver().Resolve(MobaConfigLoader.LoadDefault(), modelId);
            return model.Prefab == null ? null : new BattleActorReplayPreview(model);
        }

        /// <summary>
        /// 直接应用录制姿态并绘制。缺样本时隐藏对应演员。
        /// </summary>
        internal void Draw(Rect rect, Bounds recordedBounds, Pose? authority, Pose? prediction)
        {
            if (!PrepareFrame(rect, recordedBounds, authority, prediction)) return;
            _preview.BeginPreview(rect, GUIStyle.none);
            try { _preview.Render(true); }
            finally { _preview.EndAndDrawPreview(rect); }
        }

        /// <summary>
        /// 使用窗口相同的镜头与模型渲染独立帧，供本地验收保存证据。
        /// </summary>
        internal Texture2D RenderStatic(Rect rect, Bounds recordedBounds, Pose? authority, Pose? prediction)
        {
            if (!PrepareFrame(rect, recordedBounds, authority, prediction)) return null;
            _preview.BeginStaticPreview(rect);
            Texture2D image = null;
            try { _preview.Render(true); }
            finally { image = _preview.EndStaticPreview(); }
            return image;
        }

        private bool PrepareFrame(Rect rect, Bounds recordedBounds, Pose? authority, Pose? prediction)
        {
            ApplyPoses(authority, prediction);
            if (rect.width <= 1f || rect.height <= 1f) return false;
            var reach = Mathf.Max(1f, recordedBounds.extents.magnitude + _actorRadius);
            var camera = _preview.camera;
            camera.orthographicSize = reach * 1.13f / Mathf.Min(1f, rect.width / rect.height);
            camera.farClipPlane = reach * 8f;
            camera.transform.position = recordedBounds.center + new Vector3(3f, 2f, -1.5f).normalized * (reach * 4f);
            camera.transform.LookAt(recordedBounds.center);
            return true;
        }

        /// <summary>
        /// 输出独立的模型可见性和姿态，不改 Prefab 或共享材质。
        /// </summary>
        internal void ApplyPoses(Pose? authority, Pose? prediction)
        {
            ApplyPose(_authority, authority);
            ApplyPose(_prediction, prediction);
        }

        /// <summary>
        /// 在角色同一镜头显示弹体位置标记；没有录制样本就隐藏，不实例化正式弹体或特效。
        /// </summary>
        internal void SetFlightPositions(Vector3? authority, Vector3? prediction)
        {
            if (authority.HasValue && _authorityFlight == null)
                _authorityFlight = CreateFlightMarker("Authority flight", _authorityMaterial, 0.25f);
            if (prediction.HasValue && _predictionFlight == null)
            {
                _predictionFlightMaterial = new Material(_authorityMaterial)
                {
                    color = new Color(1f, 0.60f, 0.10f),
                    hideFlags = HideFlags.HideAndDontSave
                };
                _predictionFlight = CreateFlightMarker("Prediction flight", _predictionFlightMaterial, 0.32f);
            }
            ApplyFlightPosition(_authorityFlight, authority);
            ApplyFlightPosition(_predictionFlight, prediction);
        }

        private GameObject CreateFlightMarker(string name, Material material, float diameter)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = name;
            _preview.AddSingleGO(marker);
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.localScale = Vector3.one * diameter;
            marker.GetComponent<Renderer>().sharedMaterial = material;
            return marker;
        }

        private static void ApplyFlightPosition(GameObject marker, Vector3? position)
        {
            if (marker == null) return;
            if (position is Vector3 recorded) marker.transform.position = recorded;
            marker.SetActive(position.HasValue);
        }

        /// <summary>
        /// 回收临时场景、角色、预览纹理及独立的权威材质。
        /// </summary>
        public void Dispose()
        {
            try { _preview.Cleanup(); }
            finally
            {
                if (_authorityMaterial != null) UnityEngine.Object.DestroyImmediate(_authorityMaterial);
                if (_predictionFlightMaterial != null) UnityEngine.Object.DestroyImmediate(_predictionFlightMaterial);
            }
        }

        private static Material CreateAuthorityMaterial()
        {
            var shader = Shader.Find("Standard");
            if (shader == null || !shader.isSupported)
                throw new InvalidOperationException("当前编辑器无法渲染回放的权威模型材质。");

            var material = new Material(shader)
            {
                color = new Color(0.28f, 0.30f, 0.33f),
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetFloat("_Glossiness", 0f);
            return material;
        }

        private static void ApplyAuthorityMaterial(GameObject actor, Material material)
        {
            foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;

                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    if (materials[index] != null) materials[index] = material;
                    renderer.SetPropertyBlock(null, index);
                }
                renderer.SetPropertyBlock(null);
                renderer.sharedMaterials = materials;
            }
        }

        private GameObject CreateModel(BattleViewModelPrefab model)
        {
            var actor = _preview.InstantiatePrefabInScene(model.Prefab);
            actor.transform.localScale = Vector3.one * (model.Scale > 0f ? model.Scale : 1f);
            foreach (var behaviour in actor.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            foreach (var particles in actor.GetComponentsInChildren<ParticleSystemRenderer>(true)) particles.enabled = false;
            return actor;
        }

        private static void ApplyPose(GameObject actor, Pose? pose)
        {
            if (pose is Pose observed) actor.transform.SetPositionAndRotation(observed.position, observed.rotation);
            actor.SetActive(pose.HasValue);
        }
    }
}
#endif
