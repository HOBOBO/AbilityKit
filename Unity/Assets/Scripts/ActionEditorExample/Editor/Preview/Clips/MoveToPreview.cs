using NBC.ActionEditor;
using NBC.ActionEditorExample;
using UnityEngine;

namespace ActionEditorExample
{
    /// <summary>
    /// 移动至预览
    /// </summary>
    [CustomPreview(typeof(MoveTo))]
    public class MoveToPreview : PreviewBase<MoveTo>
    {
        private Vector3 originalPos;

        public override void Update(float time, float previousTime)
        {
            var endPos = TargetPosition();

            var interpolateAmount = time / clip.Length;

            ModelSampler.EditModel.transform.position =
                Easing.Ease(clip.interpolation, originalPos, endPos, interpolateAmount);
        }


        public override void Enter()
        {
            if (ModelSampler.EditModel != null)
            {
                originalPos = ModelSampler.EditModel.transform.position;
            }
        }

        private Vector3 TargetPosition()
        {
            Vector3 endPos = ModelSampler.DefPosition;
            switch (clip.moveType)
            {
                case MoveToType.Target:
                    //这里直接到目标点位。实际业务请根据自己业务情况计算碰撞盒子和半径等内容
                    endPos = ModelSampler.TargetModel.transform.position;
                    break;
                case MoveToType.OriginalPosition:
                    endPos = ModelSampler.DefPosition;
                    break;
            }

            return endPos;
        }
    }
}