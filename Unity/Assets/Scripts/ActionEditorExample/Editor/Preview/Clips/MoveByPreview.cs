using NBC.ActionEditor;
using NBC.ActionEditorExample;
using UnityEngine;

namespace ActionEditorExample
{
    /// <summary>
    /// 移动至预览
    /// </summary>
    [CustomPreview(typeof(MoveBy))]
    public class MoveByPreview : PreviewBase<MoveBy>
    {
        private Vector3 originalPos;

        public override void Update(float time, float previousTime)
        {
            var target = originalPos + clip.move;
            ModelSampler.EditModel.transform.position =
                Easing.Ease(clip.interpolation, originalPos, target, time / clip.Length);
            
            
        }

        public override void Enter()
        {
            if (ModelSampler.EditModel != null)
            {
                originalPos = ModelSampler.EditModel.transform.position;
            }
        }
    }
}