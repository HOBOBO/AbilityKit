using NBC.ActionEditor;
using NBC.ActionEditorExample;
using UnityEditor.Animations;
using UnityEngine;

namespace ActionEditorExample
{
    /// <summary>
    /// 动画预览
    /// </summary>
    [CustomPreview(typeof(PlayAnimation))]
    public class PlayAnimationPreview : PreviewBase<PlayAnimation>
    {
        private Animator _animator;
        private AnimationClip _animationClip;

        public override void Update(float time, float previousTime)
        {
            if (_animator != null && _animationClip != null)
            {
                Preview(_animationClip, _animator.gameObject, time);
            }
        }

        public override void Enter()
        {
            var model = ModelSampler.EditModel;
            if (model != null)
            {
                _animator = model.GetComponent<Animator>();
            }

            if (_animator != null)
            {
                var audioClipName = string.Empty;
                if (clip.animationClip != null)
                {
                    audioClipName = clip.animationClip.name;
                }

                if (_animator.runtimeAnimatorController is AnimatorController
                    animatorController)
                {
                    var layer = animatorController.layers[0];
                    var states = layer.stateMachine.states;
                    foreach (var child in states)
                    {
                        if (child.state.name == audioClipName)
                        {
                            if (child.state.motion is AnimationClip c)
                            {
                                _animationClip = c;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 这里直接采样播放，如果项目需要，请自行通过AnimationMixerPlayable来扩展动画融合播放
        /// </summary>
        /// <param name="animationClip"></param>
        /// <param name="gameObject"></param>
        /// <param name="currentTime"></param>
        public void Preview(AnimationClip animationClip, GameObject gameObject, float currentTime)
        {
            animationClip.SampleAnimation(gameObject, currentTime);
        }
    }
}