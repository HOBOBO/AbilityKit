using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Emilia.Node.Universal.Editor
{
    /// <summary>
    /// 节点消息元素
    /// </summary>
    public class NodeMessageElement : VisualElement
    {
        protected string _message;
        protected NodeMessageLevel _level;

        /// <summary>
        /// 消息内容
        /// </summary>
        public string message => this._message;

        /// <summary>
        /// 消息级别
        /// </summary>
        public NodeMessageLevel level => this._level;

        /// <summary>
        /// 移除事件
        /// </summary>
        public Action onRemove;

        public NodeMessageElement()
        {
            name = "node-message";
        }

        public void Init(string message, NodeMessageLevel level)
        {
            this._message = message;
            this._level = level;

            Texture icon = NodeMessageLevelUtility.GetIcon(level);
            Color color = NodeMessageLevelUtility.GetColor(level);

            Image image = new();
            image.name = "icon";
            image.image = icon;
            image.style.width = 16;
            image.style.height = 16;

            Add(image);

            Label messageLabel = new();
            messageLabel.name = "message";
            messageLabel.text = message;
            messageLabel.style.color = color;

            Add(messageLabel);

            style.color = color;
        }

        /// <summary>
        /// 等待移除，直到条件满足
        /// </summary>
        public void WaitUntilRemove(Func<bool> condition)
        {
            var item = schedule.Execute(() => {
                if (condition()) Remove();
            });

            item.Every(100).Until(condition);
        }

        public void Remove()
        {
            this.onRemove?.Invoke();
        }
    }
}