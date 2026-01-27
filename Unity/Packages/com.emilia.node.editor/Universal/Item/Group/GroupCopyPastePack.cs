using Emilia.Kit;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 组拷贝粘贴
    /// </summary>
    public class GroupCopyPastePack : ItemCopyPastePack
    {
        public GroupCopyPastePack(EditorItemAsset asset) : base(asset) { }

        public override bool CanDependency(ICopyPastePack pack)
        {
            INodeCopyPastePack nodeCopyPastePack = pack as INodeCopyPastePack;
            if (nodeCopyPastePack == null) return false;

            EditorGroupAsset groupAsset = _copyAsset as EditorGroupAsset;
            if (groupAsset == null) return false;

            string copyNodeId = nodeCopyPastePack.copyAsset.id;
            if (groupAsset.innerNodes.Contains(copyNodeId)) return true;

            return false;
        }

        protected override void PasteDependency(CopyPasteContext copyPasteContext)
        {
            EditorGroupAsset groupAsset = this._pasteAsset as EditorGroupAsset;
            groupAsset.innerNodes.Clear();

            int amount = copyPasteContext.dependency.Count;
            for (int i = 0; i < amount; i++)
            {
                ICopyPastePack pack = copyPasteContext.dependency[i];
                INodeCopyPastePack nodeCopyPastePack = pack as INodeCopyPastePack;
                if (nodeCopyPastePack == null) continue;
                groupAsset.innerNodes.Add(nodeCopyPastePack.pasteAsset.id);
            }
        }
    }
}