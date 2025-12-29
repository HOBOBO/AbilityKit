using System;
using UnityEngine;

namespace NBC.Extensions
{
    public static partial class TaskChainExtension
    {
        public static SequenceTaskCollection Sequence<T>(this T selfbehaviour) where T : MonoBehaviour
        {
            var retNodeChain = new SequenceTaskCollection();
            return retNodeChain;
        }

        public static ParallelTaskCollection Parallel<T>(this T selfbehaviour) where T : MonoBehaviour
        {
            var retNodeChain = new ParallelTaskCollection();
            return retNodeChain;
        }

        // public static TimelineList Timeline<T>(this T selfbehaviour) where T : MonoBehaviour
        // {
        //     var retNodeChain = new TimelineList();
        //     return retNodeChain;
        // }

        // public static ITaskCollection OnComplete(this ITaskCollection selfChain, Action<bool> callback)
        // {
        //     selfChain.OnComplete(callback);
        //     return selfChain;
        // }
    }
}