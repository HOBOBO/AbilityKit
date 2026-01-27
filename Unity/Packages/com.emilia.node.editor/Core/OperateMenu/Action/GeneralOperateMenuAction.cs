using System;

namespace Emilia.Node.Editor
{
    /// <summary>
    /// 通用菜单行为
    /// </summary>
    public class GeneralOperateMenuAction : OperateMenuAction
    {
        /// <summary>
        /// 选中状态获取回调
        /// </summary>
        public Func<bool> isOnCallback;

        /// <summary>
        /// 验证状态获取回调
        /// </summary>
        public Func<OperateMenuContext, OperateMenuActionValidity> validityCallback;

        /// <summary>
        /// 执行回调
        /// </summary>
        public Action<OperateMenuActionContext> executeCallback;

        public override bool isOn => this.isOnCallback?.Invoke() ?? base.isOn;

        public override OperateMenuActionValidity GetValidity(OperateMenuContext context) => this.validityCallback?.Invoke(context) ?? base.GetValidity(context);

        public override void Execute(OperateMenuActionContext context)
        {
            this.executeCallback?.Invoke(context);
        }

        /// <summary>
        /// 转OperateMenuActionInfo结构
        /// </summary>
        public OperateMenuActionInfo ToActionInfo(string name, string category, int priority)
        {
            OperateMenuActionInfo actionInfo = new();
            actionInfo.name = name;
            actionInfo.category = category;
            actionInfo.action = this;
            actionInfo.priority = priority;
            return actionInfo;
        }
    }
}