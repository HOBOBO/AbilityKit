using System;
using AbilityKit.Triggering.Eventing;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.Extensions
{
    /// <summary>
    /// RpnNumericExprParser 扩展方法
    /// </summary>
    public static class RpnNumericExprParserExtensions
    {
        private static readonly AccessorRegistry _registry = new AccessorRegistry();
        
        static RpnNumericExprParserExtensions()
        {
            // 初始化时扫描并注册访问器
            _registry.ScanAndRegister();
        }
        
        /// <summary>
        /// 使用 Attribute 方式解析 RPN 表达式
        /// </summary>
        /// <param name="exprText">表达式文本</param>
        /// <param name="strictBlackboardDomain">是否对 Blackboard 域启用严格校验</param>
        /// <returns>解析后的节点数组</returns>
        public static RpnNumericNode[] ParseWithAttributes(string exprText, bool strictBlackboardDomain = false)
        {
            return RpnNumericExprParser.Parse(
                exprText,
                payloadFieldIdResolver: ResolvePayloadFieldId,
                blackboardDomainIdResolver: domain => ResolveBlackboardDomainId(domain, strictBlackboardDomain),
                blackboardKeyIdResolver: ResolveBlackboardKeyId
            );
        }

        /// <summary>
        /// 使用 Attribute 方式解析 RPN 表达式（严格模式）
        /// </summary>
        /// <param name="exprText">表达式文本</param>
        /// <returns>解析后的节点数组</returns>
        public static RpnNumericNode[] ParseWithAttributesStrict(string exprText)
        {
            return ParseWithAttributes(exprText, strictBlackboardDomain: true);
        }
        
        /// <summary>
        /// 解析 Payload 字段 ID
        /// </summary>
        /// <param name="fieldName">字段名称</param>
        /// <returns>字段 ID</returns>
        private static int ResolvePayloadFieldId(string fieldName)
        {
            // 这里可以根据实际需求实现字段 ID 解析逻辑
            // 示例：使用稳定字符串 ID
            return StableStringId.Get($"payload:{fieldName}");
        }
        
        /// <summary>
        /// 解析 Blackboard 域 ID
        /// </summary>
        /// <param name="domain">域名称</param>
        /// <returns>域 ID</returns>
        private static int ResolveBlackboardDomainId(string domain, bool strictBlackboardDomain)
        {
            // 若 Attribute 注册了该域，优先表示为已声明域；当前仍使用稳定字符串 ID。
            // 这样不会改变现有 ID 编码策略，同时允许通过注册表做域白名单管理。
            if (!_registry.HasBlackboardResolver(domain))
            {
                if (strictBlackboardDomain)
                {
                    throw new InvalidOperationException($"Blackboard domain '{domain}' has no attributed resolver.");
                }
                System.Diagnostics.Debug.WriteLine($"Blackboard domain '{domain}' has no attributed resolver, fallback to stable id.");
            }

            // 这里可以根据实际需求实现域 ID 解析逻辑
            // 示例：使用稳定字符串 ID
            return StableStringId.Get($"bb:{domain}");
        }
        
        /// <summary>
        /// 解析 Blackboard 键 ID
        /// </summary>
        /// <param name="domainAndKey">domain:key 组合键</param>
        /// <returns>键 ID</returns>
        private static int ResolveBlackboardKeyId(string domainAndKey)
        {
            if (string.IsNullOrWhiteSpace(domainAndKey))
            {
                throw new ArgumentException("Blackboard key token cannot be empty.", nameof(domainAndKey));
            }

            // Parse 回调约定传入 "domain:key"，
            // 这里必须保持和 RpnNumericExprParser 默认编码一致：bb:domain:key
            return StableStringId.Get($"bb:{domainAndKey}");
        }
        
        /// <summary>
        /// 获取访问器注册表
        /// </summary>
        /// <returns>访问器注册表</returns>
        public static AccessorRegistry GetAccessorRegistry()
        {
            return _registry;
        }
        
        /// <summary>
        /// 重新扫描并注册访问器
        /// </summary>
        public static void RescanAccessors()
        {
            // 重置扫描状态并重新扫描
            _registry.ResetAndRescan();
        }
    }
}