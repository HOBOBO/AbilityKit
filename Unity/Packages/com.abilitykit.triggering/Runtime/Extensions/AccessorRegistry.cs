using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.Extensions
{
    /// <summary>
    /// 访问器注册表
    /// </summary>
    public class AccessorRegistry
    {
        private static readonly Type GenericPayloadAccessorType = typeof(IPayloadAccessor<>);
        private readonly Dictionary<Type, object> _payloadAccessors = new Dictionary<Type, object>();
        private readonly Dictionary<string, IBlackboard> _blackboardResolvers = new Dictionary<string, IBlackboard>();
        private readonly Dictionary<string, INumericVarDomain> _varDomainResolvers = new Dictionary<string, INumericVarDomain>();
        private bool _scanned;
        
        /// <summary>
        /// 扫描并注册所有带有 Attribute 的访问器
        /// </summary>
        public void ScanAndRegister()
        {
            if (_scanned) return;
            
            try
            {
                // 扫描所有程序集
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var assembly in assemblies)
                {
                    try
                    {
                        // 扫描类型
                        var types = GetLoadableTypes(assembly);
                        foreach (var type in types)
                        {
                            if (type == null || !type.IsClass || type.IsAbstract) continue;

                            // 注册 Payload 访问器
                            RegisterPayloadAccessor(type);
                            
                            // 注册 Blackboard 解析器
                            RegisterBlackboardResolver(type);
                            
                            // 注册 变量域 解析器
                            RegisterVarDomainResolver(type);
                        }
                    }
                    catch (Exception ex)
                    {
                        // 忽略无法加载的程序集
                        System.Diagnostics.Debug.WriteLine($"Error scanning assembly {assembly.FullName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error scanning assemblies: {ex.Message}");
            }
            finally
            {
                _scanned = true;
            }
        }
        
        /// <summary>
        /// 重置扫描状态并重新扫描
        /// </summary>
        public void ResetAndRescan()
        {
            _scanned = false;
            _payloadAccessors.Clear();
            _blackboardResolvers.Clear();
            _varDomainResolvers.Clear();
            ScanAndRegister();
        }
        
        /// <summary>
        /// 注册 Payload 访问器
        /// </summary>
        /// <param name="type">类型</param>
        private void RegisterPayloadAccessor(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(PayloadFieldAccessorAttribute), false)
                .FirstOrDefault() as PayloadFieldAccessorAttribute;
            
            if (attr == null) return;
            
            try
            {
                // 检查是否实现了 IPayloadAccessor<TPayload> 接口
                if (!ImplementsGenericPayloadAccessor(type, attr.PayloadType))
                {
                    System.Diagnostics.Debug.WriteLine($"Type {type.Name} has PayloadFieldAccessorAttribute but does not implement IPayloadAccessor<{attr.PayloadType.Name}>");
                    return;
                }
                
                // 创建实例并注册
                var accessor = Activator.CreateInstance(type);
                if (accessor != null)
                {
                    _payloadAccessors[attr.PayloadType] = accessor;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering payload accessor {type.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 注册 Blackboard 解析器
        /// </summary>
        /// <param name="type">类型</param>
        private void RegisterBlackboardResolver(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(BlackboardResolverAttribute), false)
                .FirstOrDefault() as BlackboardResolverAttribute;
            
            if (attr == null) return;
            
            try
            {
                // 检查是否实现了 IBlackboard 接口
                var resolverType = typeof(IBlackboard);
                if (!resolverType.IsAssignableFrom(type))
                {
                    System.Diagnostics.Debug.WriteLine($"Type {type.Name} has BlackboardResolverAttribute but does not implement IBlackboard");
                    return;
                }
                
                // 创建实例并注册
                var resolver = Activator.CreateInstance(type) as IBlackboard;
                if (resolver != null)
                {
                    _blackboardResolvers[attr.Domain] = resolver;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering blackboard resolver {type.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 注册 变量域 解析器
        /// </summary>
        /// <param name="type">类型</param>
        private void RegisterVarDomainResolver(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(VarDomainResolverAttribute), false)
                .FirstOrDefault() as VarDomainResolverAttribute;
            
            if (attr == null) return;
            
            try
            {
                // 检查是否实现了 INumericVarDomain 接口
                var resolverType = typeof(INumericVarDomain);
                if (!resolverType.IsAssignableFrom(type))
                {
                    System.Diagnostics.Debug.WriteLine($"Type {type.Name} has VarDomainResolverAttribute but does not implement INumericVarDomain");
                    return;
                }
                
                // 创建实例并注册
                var resolver = Activator.CreateInstance(type) as INumericVarDomain;
                if (resolver != null)
                {
                    _varDomainResolvers[attr.Domain] = resolver;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering var domain resolver {type.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取 Blackboard 解析器
        /// </summary>
        /// <param name="domain">Blackboard 域</param>
        /// <returns>Blackboard 解析器</returns>
        public IBlackboard GetBlackboardResolver(string domain)
        {
            _blackboardResolvers.TryGetValue(domain, out var resolver);
            return resolver;
        }
        
        /// <summary>
        /// 获取 变量域 解析器
        /// </summary>
        /// <param name="domain">变量域名称</param>
        /// <returns>变量域 解析器</returns>
        public INumericVarDomain GetVarDomainResolver(string domain)
        {
            _varDomainResolvers.TryGetValue(domain, out var resolver);
            return resolver;
        }

        /// <summary>
        /// 获取指定 Payload 类型的访问器实例
        /// </summary>
        /// <param name="payloadType">Payload 类型</param>
        /// <returns>访问器实例</returns>
        public object GetPayloadAccessor(Type payloadType)
        {
            _payloadAccessors.TryGetValue(payloadType, out var accessor);
            return accessor;
        }

        /// <summary>
        /// 尝试获取指定 Payload 类型的访问器实例
        /// </summary>
        public bool TryGetPayloadAccessor(Type payloadType, out object accessor)
        {
            return _payloadAccessors.TryGetValue(payloadType, out accessor);
        }

        /// <summary>
        /// 检查是否存在指定域的 Blackboard 解析器
        /// </summary>
        public bool HasBlackboardResolver(string domain)
        {
            return _blackboardResolvers.ContainsKey(domain);
        }

        private static bool ImplementsGenericPayloadAccessor(Type candidateType, Type payloadType)
        {
            var expected = GenericPayloadAccessorType.MakeGenericType(payloadType);
            return expected.IsAssignableFrom(candidateType);
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }
    }
}