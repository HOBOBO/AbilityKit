using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Markers;

/// <summary>
/// 文件名称: MobaInputCommandHandlerRegistry.cs
/// 
/// 功能描述: 扫描并管理基于 Attribute 注册的输入命令处理器。
/// 
/// 创建日期: 2026-05-27
/// 修改日期: 2026-05-27
/// </summary>
namespace AbilityKit.Demo.Moba.Services
{
    public readonly struct MobaInputCommandHandlerDescriptor
    {
        public MobaInputCommandHandlerDescriptor(int opCode, Type handlerType)
        {
            OpCode = opCode;
            HandlerType = handlerType;
        }

        public int OpCode { get; }
        public Type HandlerType { get; }
    }

    /// <summary>
    /// 输入命令处理器注册表。
    /// </summary>
    public sealed class MobaInputCommandHandlerRegistry : MobaMarkerRegistryBase<IMobaInputCommandHandler>
    {
        private readonly Dictionary<int, IMobaInputCommandHandler> _handlers = new Dictionary<int, IMobaInputCommandHandler>();
        private readonly Dictionary<int, MobaInputCommandHandlerDescriptor> _descriptors = new Dictionary<int, MobaInputCommandHandlerDescriptor>();
        private IWorldResolver _services;

        public int HandlerCount => _handlers.Count;
        public int DescriptorCount => _descriptors.Count;

        public MobaInputCommandHandlerRegistry() : base(4)
        {
        }

        /// <summary>
        /// 创建空注册表，由输入契约注册表作为唯一真源写入处理器。
        /// </summary>
        public static MobaInputCommandHandlerRegistry CreateEmpty()
        {
            return new MobaInputCommandHandlerRegistry();
        }

        /// <summary>
        /// 创建自动扫描注册表，供扩展工具和兼容路径使用。
        /// </summary>
        public static MobaInputCommandHandlerRegistry CreateScanned()
        {
            MobaInputCommandHandlerRegistry registry = new MobaInputCommandHandlerRegistry();
            if (RegisterGeneratedAndExternalHandlers(registry) == 0)
            {
                if (AppContext.TryGetSwitch(
                        "AbilityKit.Moba.DisableInputCommandHandlerReflectionFallback",
                        out var reflectionFallbackDisabled) && reflectionFallbackDisabled)
                {
                    throw new InvalidOperationException(
                        "The generated MOBA input command handler manifest is empty and reflection fallback is disabled.");
                }

                MarkerScanner<MobaInputCommandHandlerAttribute>.Scan(
                    new[] { typeof(MobaInputCommandHandlerRegistry).Assembly },
                    registry);
            }

            return registry;
        }

        private static int RegisterGeneratedAndExternalHandlers(MobaInputCommandHandlerRegistry registry)
        {
            var runtimeAssembly = typeof(MobaInputCommandHandlerRegistry).Assembly;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var generatedCount = 0;
            for (int i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly == runtimeAssembly)
                {
                    generatedCount = MobaGeneratedInputCommandHandlerManifest.Register(registry);
                    continue;
                }

                MarkerScanner<MobaInputCommandHandlerAttribute>.Scan(new[] { assembly }, registry);
            }

            return generatedCount;
        }

        /// <summary>
        /// 注册指定 OpCode 的处理器类型。
        /// </summary>
        public void Register(int opCode, Type implType)
        {
            if (!TryRegister(key: opCode, implType)) return;

            _descriptors[opCode] = new MobaInputCommandHandlerDescriptor(opCode, implType);
            if (_services != null)
            {
                TryBindHandler(opCode, implType, allowFallback: true);
            }
        }

        public void BindHandlers(IWorldResolver services)
        {
            _services = services;
            if (_services == null) return;

            foreach (KeyValuePair<int, MobaInputCommandHandlerDescriptor> pair in _descriptors)
            {
                TryBindHandler(pair.Key, pair.Value.HandlerType, allowFallback: true);
            }
        }

        public bool TryGetHandlerDescriptor(int opCode, out MobaInputCommandHandlerDescriptor descriptor)
        {
            return _descriptors.TryGetValue(opCode, out descriptor);
        }

        /// <summary>
        /// 尝试处理输入命令。
        /// </summary>
        public bool TryHandle(MobaInputCommandContext context, FrameIndex frame, PlayerInputCommand command, out MobaInputCommandResult result)
        {
            if (!_handlers.TryGetValue(command.OpCode, out IMobaInputCommandHandler handler))
            {
                if (!_descriptors.TryGetValue(command.OpCode, out MobaInputCommandHandlerDescriptor descriptor) || !TryBindHandler(command.OpCode, descriptor.HandlerType, allowFallback: true))
                {
                    MobaRuntimeLog.Warning(
                        MobaRuntimeLogModule.Input,
                        MobaRuntimeLogPurpose.Rejection,
                        nameof(MobaInputCommandHandlerRegistry),
                        $"Input handler missing. frame={frame.Value} op={command.OpCode} player={command.Player.Value} descriptors={_descriptors.Count} handlers={_handlers.Count}");
                    result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.MissingHandler);
                    return false;
                }

                handler = _handlers[command.OpCode];
            }

            bool handled = handler.Handle(context, frame, command, out result);
            if (!handled && string.IsNullOrEmpty(result.Message))
            {
                result = MobaInputCommandResult.Rejected(command, MobaInputCommandFailureCode.HandlerRejected);
            }

            if (!handled)
            {
                MobaRuntimeLog.Warning(
                    MobaRuntimeLogModule.Input,
                    MobaRuntimeLogPurpose.Rejection,
                    nameof(MobaInputCommandHandlerRegistry),
                    $"Input handler returned false. frame={frame.Value} op={command.OpCode} player={command.Player.Value} handler={handler?.GetType().Name ?? "<null>"} result={result}");
            }

            return handled;
        }

        private bool TryBindHandler(int opCode, Type implType, bool allowFallback)
        {
            if (implType == null) return false;
            if (_handlers.ContainsKey(opCode)) return true;

            if (TryResolveHandler(implType, out IMobaInputCommandHandler resolvedHandler))
            {
                _handlers[opCode] = resolvedHandler;
                return true;
            }

            if (!allowFallback) return false;

            IMobaInputCommandHandler fallbackHandler = Activator.CreateInstance(implType) as IMobaInputCommandHandler;
            if (fallbackHandler == null) return false;

            _handlers[opCode] = fallbackHandler;
            return true;
        }

        private bool TryResolveHandler(Type implType, out IMobaInputCommandHandler handler)
        {
            handler = null;
            if (_services == null) return false;

            try
            {
                if (_services.TryResolve(implType, out object instance) && instance is IMobaInputCommandHandler typedHandler)
                {
                    handler = typedHandler;
                    return true;
                }
            }
            catch (Exception ex)
            {
                MobaRuntimeLog.Exception(
                    ex,
                    MobaRuntimeLogModule.Input,
                    MobaRuntimeLogPurpose.Exception,
                    nameof(MobaInputCommandHandlerRegistry),
                    $"Failed to resolve input command handler from world services. type={implType.FullName}");
            }

            return false;
        }
    }
}
