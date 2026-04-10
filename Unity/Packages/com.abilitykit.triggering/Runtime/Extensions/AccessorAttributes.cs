using System;
using AbilityKit.Core.Common.Marker;

namespace AbilityKit.Triggering.Runtime.Extensions
{
    /// <summary>
    /// 标记 Payload 字段访问器的 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class PayloadFieldAccessorAttribute : MarkerAttribute
    {
        public Type PayloadType { get; }

        public PayloadFieldAccessorAttribute(Type payloadType)
        {
            PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
        }
    }

    /// <summary>
    /// 标记 Blackboard 解析器的 Attribute
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class BlackboardResolverAttribute : MarkerAttribute
    {
        public string Domain { get; }

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
    public sealed class VarDomainResolverAttribute : MarkerAttribute
    {
        public string Domain { get; }

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