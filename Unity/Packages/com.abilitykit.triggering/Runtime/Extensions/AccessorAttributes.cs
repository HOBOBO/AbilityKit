using System;

namespace AbilityKit.Triggering.Runtime.Extensions
{
    /// <summary>
    /// 标记 Payload 字段访问器的 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PayloadFieldAccessorAttribute : Attribute
    {
        /// <summary>
        /// Payload 类型
        /// </summary>
        public Type PayloadType { get; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="payloadType">Payload 类型</param>
        public PayloadFieldAccessorAttribute(Type payloadType)
        {
            PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
        }
    }
    
    /// <summary>
    /// 标记 Blackboard 解析器的 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class BlackboardResolverAttribute : Attribute
    {
        /// <summary>
        /// Blackboard 域
        /// </summary>
        public string Domain { get; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="domain">Blackboard 域</param>
        public BlackboardResolverAttribute(string domain)
        {
            Domain = domain ?? throw new ArgumentNullException(nameof(domain));
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be empty", nameof(domain));
            }
        }
    }
    
    /// <summary>
    /// 标记 变量域 解析器的 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class VarDomainResolverAttribute : Attribute
    {
        /// <summary>
        /// 变量域名称
        /// </summary>
        public string Domain { get; }
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="domain">变量域名称</param>
        public VarDomainResolverAttribute(string domain)
        {
            Domain = domain ?? throw new ArgumentNullException(nameof(domain));
            if (string.IsNullOrWhiteSpace(domain))
            {
                throw new ArgumentException("Domain cannot be empty", nameof(domain));
            }
        }
    }
}