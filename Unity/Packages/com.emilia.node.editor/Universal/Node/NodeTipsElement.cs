using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 节点Tips元素
    /// </summary>
    public class NodeTipsElement : Label
    {
        protected double lastTime;
        protected float speed;

        public NodeTipsElement()
        {
            name = "node-tips";
            pickingMode = PickingMode.Ignore;
        }

        public void Init(float speed, Vector3 position)
        {
            this.speed = speed;

            transform.position = position;
            lastTime = EditorApplication.timeSinceStartup;

            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;

            UnregisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        protected void OnUpdate()
        {
            float deltaTime = (float) (EditorApplication.timeSinceStartup - lastTime);

            transform.position -= Vector3.up * deltaTime * speed;

            this.lastTime = EditorApplication.timeSinceStartup;
        }

        public void OnDetachFromPanel(DetachFromPanelEvent e)
        {
            EditorApplication.update -= OnUpdate;
        }
    }
}