using System;
using System.Collections.Generic;
using NBC.ActionEditor;
using AbilityKit.ActionEditorImpl;
using UnityEditor;
using UnityEngine;

namespace ActionEditorExample
{
    [NBC.ActionEditor.CustomPreview(typeof(PlayParticle))]
    public class PlayParticlePreview : PreviewBase<PlayParticle>
    {
        protected GameObject EffectObject { get; private set; }
        protected Vector3 BaseScale { get; private set; }
        protected virtual float SimulationSpeed => 1f;
        private ParticleSystem[] _roots = Array.Empty<ParticleSystem>();

        public override void Enter()
        {
            if (EffectObject == null) CreateEffect();
            if (EffectObject == null) return;
            PlaceEffect();
            EffectObject.SetActive(true);
            foreach (var root in _roots)
                root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public override void Update(float time, float previousTime)
        {
            if (EffectObject == null) return;
            UpdatePlacement();
            EffectObject.SetActive(true);
            // Restart deterministically for forward and backward scrubbing.
            // Simulate independent roots once; Unity includes their child systems.
            foreach (var root in _roots)
                root.Simulate(Mathf.Max(0f, time) * SimulationSpeed, true, true, true);
        }

        public override void Exit()
        {
            if (EffectObject != null) EffectObject.SetActive(false);
        }

        public override void Reverse() => Exit();
        public override void ReverseEnter()
        {
            if (EffectObject != null) EffectObject.SetActive(true);
        }

        public override void Dispose()
        {
            if (EffectObject != null) UnityEngine.Object.DestroyImmediate(EffectObject);
            EffectObject = null;
            _roots = Array.Empty<ParticleSystem>();
        }

        protected virtual GameObject LoadPrefab()
        {
            // Business subclasses own template resolution; the generic editor has
            // no dependency on demo tables, resource directories or runtime assemblies.
            if (clip.presentationTemplateId > 0) return null;
            if (!string.IsNullOrWhiteSpace(clip.resourceKey))
                return Resources.Load<GameObject>(clip.resourceKey);
            return AssetDatabase.LoadAssetAtPath<GameObject>(clip.resPath);
        }

        protected virtual void PlaceEffect() { }
        protected virtual void UpdatePlacement() { }
        protected virtual void ConfigureParticle(ParticleSystem system) { }

        private void CreateEffect()
        {
            var prefab = LoadPrefab();
            if (prefab == null) return;
            EffectObject = UnityEngine.Object.Instantiate(prefab);
            EffectObject.name = prefab.name + " (ActionEditor Preview)";
            foreach (var child in EffectObject.GetComponentsInChildren<Transform>(true))
                child.gameObject.hideFlags = HideFlags.HideAndDontSave;
            BaseScale = prefab.transform.localScale;
            var roots = new List<ParticleSystem>();
            var systems = EffectObject.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < systems.Length; i++)
            {
                var system = systems[i];
                ConfigureParticle(system);
                if (system.useAutoRandomSeed)
                {
                    system.useAutoRandomSeed = false;
                    system.randomSeed = (uint)(i + 1);
                }
                var parent = system.transform.parent;
                while (parent != null && parent != EffectObject.transform.parent && parent.GetComponent<ParticleSystem>() == null)
                    parent = parent.parent;
                if (parent == null || parent == EffectObject.transform.parent) roots.Add(system);
            }
            _roots = roots.ToArray();
        }
    }
}
