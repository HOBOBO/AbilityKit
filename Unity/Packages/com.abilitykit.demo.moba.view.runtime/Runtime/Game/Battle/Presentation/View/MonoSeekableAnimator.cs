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

            var elapsedSeconds = Mathf.Max(0, frameIndex) * Mathf.Max(0f, secondsPerFrame);
            Animator.Play(StateHash, LayerIndex, 0f);
            Animator.Update(0f);
            var state = Animator.GetCurrentAnimatorStateInfo(LayerIndex);
            var frameNormalizedTime = state.length > Mathf.Epsilon
                ? elapsedSeconds / state.length
                : 0f;
            Animator.Play(StateHash, LayerIndex, NormalizedTime + frameNormalizedTime);
            Animator.Update(0f);
        }

        private void Reset()
        {
            Animator = GetComponentInChildren<Animator>();
        }
    }
}
