using AbilityKit.Game.Flow;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Playables;

namespace AbilityKit.Game.Platform.Unity.Presentation
{
    /// <summary>Optional Unity resource completion and particle-tail support, replaceable by the host.</summary>
    public sealed class UnityPresentationLifetimeAdapter : IPresentationLifetimeAdapter<GameObject>
    {
        private sealed class DrainBaseline
        {
            public Animator[] Animators;
            public bool[] Enabled;
        }
        private static readonly ConditionalWeakTable<GameObject, DrainBaseline> Drains =
            new ConditionalWeakTable<GameObject, DrainBaseline>();

        public static void ResetForReuse(GameObject visual)
        {
            if (visual == null || !Drains.TryGetValue(visual, out var baseline)) return;
            for (var i = 0; i < baseline.Animators.Length; i++)
                if (baseline.Animators[i] != null) baseline.Animators[i].enabled = baseline.Enabled[i];
            foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true))
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Drains.Remove(visual);
        }

        public bool TryIsComplete(GameObject visual, out bool complete)
        {
            complete = false;
            if (visual == null) { complete = true; return true; }
            var particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            var directors = visual.GetComponentsInChildren<PlayableDirector>(true);
            if (particles.Length == 0 && directors.Length == 0) return false;
            for (var i = 0; i < particles.Length; i++)
                if (particles[i].IsAlive(true)) return true;
            for (var i = 0; !Drains.TryGetValue(visual, out _) && i < directors.Length; i++)
                if (directors[i].state == PlayState.Playing ||
                    (directors[i].duration > 0 && directors[i].time < directors[i].duration)) return true;
            complete = true;
            return true;
        }

        public bool TryBeginDrain(GameObject visual)
        {
            if (visual == null) return false;
            var particles = visual.GetComponentsInChildren<ParticleSystem>(true);
            if (particles.Length == 0) return false;
            // Stop producers first so Animator/Timeline cannot resume particle emission.
            if (!Drains.TryGetValue(visual, out _))
            {
                var animators = visual.GetComponentsInChildren<Animator>(true);
                var baseline = new DrainBaseline { Animators = animators, Enabled = new bool[animators.Length] };
                for (var i = 0; i < animators.Length; i++)
                {
                    baseline.Enabled[i] = animators[i].enabled;
                    animators[i].enabled = false;
                }
                Drains.Add(visual, baseline);
            }
            foreach (var director in visual.GetComponentsInChildren<PlayableDirector>(true)) director.Stop();
            for (var i = 0; i < particles.Length; i++)
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            return true;
        }
    }
}
