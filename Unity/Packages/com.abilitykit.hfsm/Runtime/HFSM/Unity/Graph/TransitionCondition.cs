using System;
using System.Collections.Generic;


namespace AbilityKit.HFSM.Graph.Conditions
{

    /// <summary>
    /// 抽象条件基类 - 所有条件都必须继承此类
    /// </summary>
    [Serializable]
    public abstract class TransitionCondition
    {
        /// <summary>
        /// 条件的类型名称，用于序列化和反序列化
        /// </summary>
        public abstract string TypeName { get; }

        /// <summary>
        /// 条件的显示名称，用于UI显示
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// 获取条件的简短描述
        /// </summary>
        public abstract string GetDescription();

        /// <summary>
        /// 评估条件是否满足
        /// </summary>
        /// <param name="context">运行时上下文</param>
        /// <returns>条件是否满足</returns>
        public abstract bool Evaluate(IEvaluationContext context);

        /// <summary>
        /// 创建条件的深拷贝
        /// </summary>
        public abstract TransitionCondition Clone();

        /// <summary>
        /// 获取此条件需要的参数名称列表（用于验证）
        /// </summary>
        public abstract string[] GetRequiredParameters();

        /// <summary>
        /// 从配置字典设置参数
        /// </summary>
        public abstract void SetFromConfig(Dictionary<string, object> config);

        /// <summary>
        /// 序列化为配置字典
        /// </summary>
        public abstract Dictionary<string, object> ToConfig();

        /// <summary>
        /// 获取序列化的数据（用于导出）
        /// </summary>
        public object GetSerializedData()
        {
            var config = ToConfig();
            config["$type"] = TypeName;
            return config;
        }
    }
}
