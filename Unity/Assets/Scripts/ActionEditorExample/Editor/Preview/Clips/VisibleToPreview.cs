using NBC.ActionEditor;
using NBC.ActionEditorExample;

namespace ActionEditorExample
{
    /// <summary>
    /// VisibleTo预览
    /// </summary>
    [CustomPreview(typeof(VisibleTo))]
    public class VisibleToPreview : PreviewBase<VisibleTo>
    {
        public override void Update(float time, float previousTime)
        {
            if (ModelSampler.EditModel != null)
            {
                ModelSampler.EditModel.SetActive(clip.visible);
            }
        }
    }
}