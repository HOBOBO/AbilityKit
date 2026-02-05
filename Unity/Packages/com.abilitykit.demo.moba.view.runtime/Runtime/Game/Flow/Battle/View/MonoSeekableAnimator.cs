using UnityEngine;

namespace AbilityKit.Game.Flow
{
    public sealed class MonoSeekableAnimator : MonoBehaviour, IFrameSeekableView
    {
        public Animator Animator;

        public int LayerIndex = 0;

        public int StateHash;

        public float NormalizedTime;

        public void SeekToFrame(int frameIndex, float secondsPerFrame)
        {
            if (Animator == null) return;

            Animator.Play(StateHash, LayerIndex, NormalizedTime);
            Animator.Update(0f);
        }

        private void Reset()
        {
            Animator = GetComponentInChildren<Animator>();
        }
    }
}
