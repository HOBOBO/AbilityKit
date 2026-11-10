using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace NBC.ActionEditor
{
    public enum EditorPlaybackState
    {
        Stoped,
        PlayingForwards,
        PlayingBackwards
    }
    
    [InitializeOnLoad]
    public class AssetPlayer
    {
        static AssetPlayer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ResetCurrentPreview;
            EditorApplication.quitting += ResetCurrentPreview;
            EditorApplication.playModeStateChanged += _ => ResetCurrentPreview();
        }

        private static void ResetCurrentPreview()
        {
            if (_inst != null) App.Stop();
        }

        private static AssetPlayer _inst;

        public static AssetPlayer Inst
        {
            get
            {
                if (_inst == null)
                {
                    _inst = new AssetPlayer();
                }

                return _inst;
            }
        }

        private List<IDirectableTimePointer> timePointers;

        /// <summary>
        /// 预览器
        /// </summary>
        private List<IDirectableTimePointer> unsortedStartTimePointers;

        private float playTimeMin;
        private float playTimeMax;
        private float currentTime;

        public float previousTime { get; private set; }

        private bool preInitialized;
        private readonly List<PreviewBase> previews = new List<PreviewBase>();

        public void ResetPreview()
        {
            // Preserve the existing rewind behavior for movement/animation previews
            // before disposing their owned resources. Do not enter unvisited clips.
            if (timePointers != null)
                for (var i = timePointers.Count - 1; i >= 0; i--)
                {
                    try { timePointers[i].TriggerBackward(0, previousTime); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            foreach (var preview in previews)
            {
                try { preview.Dispose(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            previews.Clear();
            timePointers = null;
            unsortedStartTimePointers = null;
            preInitialized = false;
            currentTime = previousTime = 0;
        }

        public Asset Asset => App.AssetData;

        /// <summary>
        /// 当前时间
        /// </summary>
        public float CurrentTime
        {
            get => currentTime;
            set => currentTime = Mathf.Clamp(value, 0, Length);
        }
        
        public float Length
        {
            get
            {
                if (Asset != null)
                {
                    return Asset.Length;
                }

                return 0;
            }
        }

        public void Sample()
        {
            Sample(currentTime);
        }

        public void Sample(float time)
        {
            CurrentTime = time;
            if ((currentTime == 0 || currentTime == Length) && previousTime == currentTime)
            {
                return;
            }
            
            if (!preInitialized && currentTime > 0 && previousTime == 0)
            {
                InitializePreviewPointers();
            }


            if (timePointers != null)
            {
                InternalSamplePointers(currentTime, previousTime);
            }

            previousTime = currentTime;
        }

        void InternalSamplePointers(float currentTime, float previousTime)
        {
            if (!Application.isPlaying || currentTime > previousTime)
            {
                foreach (var t in timePointers)
                {
                    try
                    {
                        t.TriggerForward(currentTime, previousTime);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }


            if (!Application.isPlaying || currentTime < previousTime)
            {
                for (var i = timePointers.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        timePointers[i].TriggerBackward(currentTime, previousTime);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            if (unsortedStartTimePointers != null)
            {
                foreach (var t in unsortedStartTimePointers)
                {
                    try
                    {
                        t.Update(currentTime, previousTime);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        /// <summary>
        /// 初始化时间指针预览器
        /// </summary>
        public void InitializePreviewPointers()
        {
            // Rebuilding pointers owns the old previews, even when the asset is cleared.
            var sampleTime = currentTime;
            ResetPreview();
            currentTime = sampleTime;
            if (Asset == null) return;
            timePointers = new List<IDirectableTimePointer>();
            unsortedStartTimePointers = new List<IDirectableTimePointer>();

            Dictionary<Type, Type> typeDic = new Dictionary<Type, Type>();
            var childs = EditorTools.GetTypeMetaDerivedFrom(typeof(PreviewBase));
            foreach (var t in childs)
            {
                // Only explicit registrations participate; private test/helper subclasses
                // must not replace an application's registered preview through inheritance.
                var arrs = t.type.GetCustomAttributes(typeof(CustomPreviewAttribute), false);
                foreach (var arr in arrs)
                {
                    if (arr is CustomPreviewAttribute c)
                    {
                        var bindT = c.PreviewType;
                        var iT = t.type;
                        if (!typeDic.ContainsKey(bindT))
                        {
                            if (!iT.IsAbstract) typeDic[bindT] = iT;
                        }
                        else
                        {
                            var old = typeDic[bindT];
                            //如果不是抽象类，且是子类就更新
                            if (!iT.IsAbstract && iT.IsSubclassOf(old))
                            {
                                typeDic[bindT] = iT;
                            }
                        }
                    }
                }
            }

            foreach (var group in Asset.groups.AsEnumerable().Reverse())
            {
                if (!group.IsActive) continue;
                foreach (var track in group.Tracks.AsEnumerable().Reverse())
                {
                    if (!track.IsActive) continue;
                    var tType = track.GetType();
                    if (typeDic.TryGetValue(tType, out var t1))
                    {
                        if (Activator.CreateInstance(t1) is PreviewBase preview)
                        {
                            preview.SetTarget(track);
                            previews.Add(preview);
                            var p3 = new StartTimePointer(preview);
                            timePointers.Add(p3);
                
                            unsortedStartTimePointers.Add(p3);
                            timePointers.Add(new EndTimePointer(preview));
                        }
                    }
                
                    foreach (var clip in track.Clips)
                    {
                        var cType = clip.GetType();
                        if (typeDic.TryGetValue(cType, out var t))
                        {
                            if (Activator.CreateInstance(t) is PreviewBase preview)
                            {
                                preview.SetTarget(clip);
                                previews.Add(preview);
                                var p3 = new StartTimePointer(preview);
                                timePointers.Add(p3);
                
                                unsortedStartTimePointers.Add(p3);
                                timePointers.Add(new EndTimePointer(preview));
                            }
                        }
                    }
                }
            }

            preInitialized = true;
        }
    }
}
