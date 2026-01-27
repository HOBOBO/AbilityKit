namespace Emilia.Node.Editor
{
    /// <summary>
    /// 端口展开编辑InputPort
    /// </summary>
    public struct EditorNodeInputPortEditInfo
    {
        /// <summary>
        /// 端口名称
        /// </summary>
        public string portId;

        /// <summary>
        /// 字段路径
        /// </summary>
        public string fieldPath;

        /// <summary>
        /// 强制使用IMGUI绘制到GraphView中
        /// </summary>
        public bool forceImGUIDraw;

        public EditorNodeInputPortEditInfo(string portId, string fieldPath, bool forceImGUIDraw = false)
        {
            this.portId = portId;
            this.fieldPath = fieldPath;
            this.forceImGUIDraw = forceImGUIDraw;
        }
    }
}