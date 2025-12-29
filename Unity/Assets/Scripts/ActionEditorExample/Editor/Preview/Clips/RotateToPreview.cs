using NBC.ActionEditor;
using NBC.ActionEditorExample;
using UnityEngine;

namespace ActionEditorExample
{
    /// <summary>
    /// 旋转角度预览
    /// </summary>
    [CustomPreview(typeof(RotateTo))]
    public class RotateToPreview : PreviewBase<RotateTo>
    {
        private Vector3 originalRot;

        public override void Update(float time, float previousTime)
        {
            var target = originalRot + clip.targetRotation;
            ModelSampler.EditModel.transform.localEulerAngles =
                Easing.Ease(clip.interpolation, originalRot, target, time / clip.Length);
        }

        public override void Enter()
        {
            if (ModelSampler.EditModel != null)
            {
                originalRot = ModelSampler.EditModel.transform.localEulerAngles;
            }
        }
    }
}