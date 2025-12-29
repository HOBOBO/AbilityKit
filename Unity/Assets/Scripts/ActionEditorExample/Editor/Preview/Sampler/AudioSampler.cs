using System;
using System.Collections.Generic;
using UnityEngine;

namespace ActionEditorExample
{
    /// <summary>
    /// 音频采样器
    /// </summary>
    public static class AudioSampler
    {
        [System.Serializable]
        public struct SampleSettings
        {
            public float volume;
            public float pitch;
            public float pan;
            public float spatialBlend;
            public bool ignoreTimescale;

            public static SampleSettings Default()
            {
                var settings = new SampleSettings();
                settings.volume = 1;
                settings.pitch = 1;
                settings.pan = 0;
                settings.spatialBlend = 0;
                settings.ignoreTimescale = false;
                return settings;
            }
        }

        static AudioSampler()
        {
            InitSource();
        }

        private const string ROOT_NAME = "_AudioSources";

        private static GameObject root;

        private static readonly Queue<AudioSource> idleSources = new Queue<AudioSource>();
        private static readonly List<AudioSource> useSources = new List<AudioSource>();

        private static void InitSource()
        {
            if (root == null)
            {
                root = GameObject.Find(ROOT_NAME);
                if (root == null)
                {
                    root = new GameObject(ROOT_NAME);
                }
            }

            var ss = root.GetComponentsInChildren<AudioSource>();
            foreach (var s in ss)
            {
                idleSources.Enqueue(s);
            }
        }

        /// <summary>
        /// 获得一个闲置中的source
        /// </summary>
        /// <param name="keyID"></param>
        /// <returns></returns>
        public static AudioSource GetSource()
        {
            if (idleSources.Count > 0)
            {
                var s = idleSources.Dequeue();
                useSources.Add(s);
                return s;
            }

            var newSource = new GameObject("_AudioSource").AddComponent<AudioSource>();
            newSource.transform.SetParent(root.transform);
            newSource.playOnAwake = false;
            useSources.Add(newSource);
            return newSource;
        }

        /// <summary>
        /// 返回一个sourece
        /// </summary>
        /// <param name="source"></param>
        public static void RetureSource(AudioSource source)
        {
            idleSources.Enqueue(source);
            useSources.Remove(source);
        }

        public static void Sample(AudioSource source, AudioClip clip, float time, float previousTime, float volume)
        {
            var settings = SampleSettings.Default();
            settings.volume = volume;
            Sample(source, clip, time, previousTime, settings);
        }

        public static void Sample(AudioSource source, AudioClip clip, float time, float previousTime,
            SampleSettings settings)
        {
            if (source == null)
            {
                return;
            }

            if (Math.Abs(previousTime - time) < 0.0001f)
            {
                source.Stop();
                return;
            }

            source.clip = clip;
            source.volume = Mathf.Clamp(settings.volume, 0, 1);
            source.pitch = Mathf.Clamp(settings.ignoreTimescale ? settings.pitch : settings.pitch * Time.timeScale, -3,
                3);
            source.panStereo = Mathf.Clamp(settings.pan, -1, 1);

            time = Mathf.Repeat(time, clip.length - 0.001f);

            if (!source.isPlaying)
            {
                source.time = time;
                source.Play();
            }

            if (!settings.ignoreTimescale)
            {
                if (Mathf.Abs(source.time - time) > 0.1f * Time.timeScale)
                {
                    source.time = time;
                }
            }
        }
    }
}