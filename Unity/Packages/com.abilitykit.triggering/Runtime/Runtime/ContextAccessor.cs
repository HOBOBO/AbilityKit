using System;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 上下文服务访问器
    /// 允许从上下文中安全获取服务，无需知道具体上下文类型
    /// </summary>
    /// <typeparam name="TCtx">上下文类型</typeparam>
    public readonly struct ContextAccessor<TCtx>
    {
        private readonly TCtx _context;

        internal ContextAccessor(TCtx context)
        {
            _context = context;
        }

        /// <summary>
        /// 上下文实例
        /// </summary>
        public TCtx Context => _context;

        /// <summary>
        /// 上下文是否有效
        /// </summary>
        public bool HasValue => _context != null;

        /// <summary>
        /// 尝试获取服务（适用于具有 Services 属性的上下文）
        /// </summary>
        public bool TryGetService<TService>(out TService service) where TService : class
        {
            service = default;

            if (_context == null) return false;

            if (_context is IServiceProvider provider)
            {
                service = provider.GetService(typeof(TService)) as TService;
                return service != null;
            }

            var type = typeof(TCtx);

            var servicesProperty = type.GetProperty("Services");
            if (servicesProperty != null)
            {
                var services = servicesProperty.GetValue(_context) as IServiceProvider;
                if (services != null)
                {
                    service = services.GetService(typeof(TService)) as TService;
                    return service != null;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试获取属性值
        /// </summary>
        public bool TryGetProperty<T>(string propertyName, out T value)
        {
            value = default;

            if (_context == null || string.IsNullOrEmpty(propertyName)) return false;

            var type = typeof(TCtx);
            var property = type.GetProperty(propertyName);

            if (property != null)
            {
                var propValue = property.GetValue(_context);
                if (propValue is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取属性值（如果不存在则返回默认值）
        /// </summary>
        public T GetPropertyOrDefault<T>(string propertyName, T defaultValue = default)
        {
            TryGetProperty(propertyName, out T value);
            return value ?? defaultValue;
        }
    }

    /// <summary>
    /// ExecCtx 的 ContextAccessor 扩展
    /// </summary>
    public static class ExecCtxContextAccessorExtensions
    {
        /// <summary>
        /// 获取上下文访问器
        /// </summary>
        public static ContextAccessor<TCtx> GetAccessor<TCtx>(this in ExecCtx<TCtx> ctx)
        {
            return new ContextAccessor<TCtx>(ctx.Context);
        }

        /// <summary>
        /// 安全地尝试获取服务
        /// </summary>
        public static bool TryGetService<TCtx, TService>(this in ExecCtx<TCtx> ctx, out TService service) where TService : class
        {
            var accessor = ctx.GetAccessor();
            return accessor.TryGetService(out service);
        }

        /// <summary>
        /// 尝试获取属性值
        /// </summary>
        public static bool TryGetContextProperty<TCtx, T>(this in ExecCtx<TCtx> ctx, string propertyName, out T value)
        {
            var accessor = ctx.GetAccessor();
            return accessor.TryGetProperty(propertyName, out value);
        }
    }
}
