/// <summary>
/// 文件名称: GeneratorRegistry.cs
/// 
/// 功能描述: 定义代码生成器的注册表和基类。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using AbilityKit.CodeGen.Attributes;
using AbilityKit.CodeGen.Core;

namespace AbilityKit.CodeGen.Registration
{

/// <summary>
/// 代码生成器接口。
/// </summary>
public interface ICodeGenerator
{
    /// <summary>生成器的唯一标识</summary>
    string Id { get; }

    /// <summary>触发此生成器的特性类型</summary>
    Type TriggerAttributeType { get; }

    /// <summary>生成器优先级（越高越先执行）</summary>
    int Priority { get; }

    /// <summary>生成代码</summary>
    GenerationResult Generate(GeneratorContext context);

    /// <summary>是否能够处理指定的特性类型</summary>
    bool CanHandle(Type attributeType);
}

/// <summary>
/// 生成器全局注册表。
/// </summary>
public static class GeneratorRegistry
{
    private static readonly object LockObj = new();
    private static readonly Dictionary<string, ICodeGenerator> _generators = new();
    private static readonly Dictionary<Type, List<ICodeGenerator>> _generatorsByAttribute = new();

    /// <summary>所有已注册生成器</summary>
    public static IReadOnlyDictionary<string, ICodeGenerator> Generators
    {
        get
        {
            lock (LockObj)
            {
                return _generators;
            }
        }
    }

    /// <summary>注册一个生成器</summary>
    public static void Register(ICodeGenerator generator)
    {
        if (generator == null)
            throw new ArgumentNullException(nameof(generator));

        lock (LockObj)
        {
            if (_generators.ContainsKey(generator.Id))
            {
                throw new InvalidOperationException(
                    $"A generator with ID '{generator.Id}' is already registered.");
            }

            _generators[generator.Id] = generator;

            var attrType = generator.TriggerAttributeType;
            if (!_generatorsByAttribute.ContainsKey(attrType))
            {
                _generatorsByAttribute[attrType] = new List<ICodeGenerator>();
            }

            var list = _generatorsByAttribute[attrType];
            list.Add(generator);
            list.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    /// <summary>注销一个生成器</summary>
    public static bool Unregister(string generatorId)
    {
        lock (LockObj)
        {
            if (!_generators.TryGetValue(generatorId, out var generator))
                return false;

            _generators.Remove(generatorId);

            var attrType = generator.TriggerAttributeType;
            if (_generatorsByAttribute.TryGetValue(attrType, out var list))
            {
                list.Remove(generator);
            }

            return true;
        }
    }

    /// <summary>获取指定 ID 的生成器</summary>
    public static ICodeGenerator GetGenerator(string generatorId)
    {
        lock (LockObj)
        {
            return _generators.TryGetValue(generatorId, out var generator)
                ? generator
                : null;
        }
    }

    /// <summary>获取能够处理指定特性类型的所有生成器</summary>
    public static IEnumerable<ICodeGenerator> GetGeneratorsForAttribute(Type attributeType)
    {
        lock (LockObj)
        {
            return _generatorsByAttribute.TryGetValue(attributeType, out var list)
                ? list.ToList()
                : Enumerable.Empty<ICodeGenerator>();
        }
    }

    /// <summary>从类型注册生成器</summary>
    public static ICodeGenerator RegisterFromType(Type generatorType)
    {
        var attr = generatorType.GetCustomAttribute(typeof(RegisterGeneratorAttribute), false)
            as RegisterGeneratorAttribute;

        if (attr == null)
            throw new InvalidOperationException(
                $"Type {generatorType.FullName} does not have a RegisterGeneratorAttribute.");

        return null;
    }

    /// <summary>清空注册表</summary>
    public static void Clear()
    {
        lock (LockObj)
        {
            _generators.Clear();
            _generatorsByAttribute.Clear();
        }
    }
}

/// <summary>
/// SubFeature 代码生成器的基类。
/// </summary>
public abstract class SubFeatureGeneratorBase : ICodeGenerator
{
    public abstract string Id { get; }
    public abstract Type TriggerAttributeType { get; }
    public abstract int Priority { get; }

    public virtual GenerationResult Generate(GeneratorContext context)
    {
        var code = GenerateCore(context);
        var outputFile = OutputFile.Create(
            $"{context.TargetSymbol.Name}.generated.cs",
            code);

        return GenerationResult.SuccessResult(
            new[] { outputFile },
            Id,
            context.GetFullName());
    }

    protected abstract string GenerateCore(GeneratorContext context);

    public virtual bool CanHandle(Type attributeType)
    {
        return TriggerAttributeType.IsAssignableFrom(attributeType);
    }
}

}
