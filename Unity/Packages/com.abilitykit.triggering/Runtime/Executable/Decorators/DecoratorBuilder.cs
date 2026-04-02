using System.Collections.Generic;
using AbilityKit.Modifiers;

namespace AbilityKit.Triggering.Runtime.Executable
{
    // ========================================================================
    // 修饰器构建器
    // ========================================================================

    /// <summary>
    /// 修饰器构建器 - 用于链式组合多个修饰器
    /// </summary>
    public sealed class DecoratorBuilder
    {
        private ISimpleExecutable _inner;

        public DecoratorBuilder(ISimpleExecutable inner)
        {
            _inner = inner;
        }

        /// <summary>添加持续时间修饰器</summary>
        public DecoratorBuilder WithDuration(float durationMs, bool autoStart = true)
        {
            var deco = DecoratorRegistry.CreateDuration(durationMs);
            deco.AutoStart = autoStart;
            deco.Inner = _inner;
            _inner = deco;
            return this;
        }

        /// <summary>添加标签修饰器</summary>
        public DecoratorBuilder WithTags(params string[] tagNames)
        {
            var tagDeco = DecoratorRegistry.CreateTag(tagNames);
            tagDeco.Inner = _inner;
            _inner = tagDeco;
            return this;
        }

        /// <summary>添加标签修饰器 (使用查询条件)</summary>
        public DecoratorBuilder WithTags(TagQuery required, TagQuery ignore = default)
        {
            var tagDeco = DecoratorRegistry.CreateTag();
            tagDeco.RequiredTags = required;
            tagDeco.IgnoreTags = ignore;
            tagDeco.Inner = _inner;
            _inner = tagDeco;
            return this;
        }

        /// <summary>添加修改器修饰器</summary>
        public DecoratorBuilder WithModifiers(params ModifierData[] modifiers)
        {
            var modDeco = DecoratorRegistry.CreateModifier(modifiers);
            modDeco.Inner = _inner;
            _inner = modDeco;
            return this;
        }

        /// <summary>添加层数修饰器</summary>
        public DecoratorBuilder WithStack(int initialStack = 1, float stackMultiplier = 1f)
        {
            var stackDeco = DecoratorRegistry.CreateStack(initialStack, stackMultiplier);
            stackDeco.Inner = _inner;
            _inner = stackDeco;
            return this;
        }

        /// <summary>添加层级修饰器</summary>
        public DecoratorBuilder WithHierarchy(int? parentId = null)
        {
            var hierDeco = DecoratorRegistry.CreateHierarchy(parentId);
            hierDeco.Inner = _inner;
            _inner = hierDeco;
            return this;
        }

        /// <summary>构建最终行为</summary>
        public ISimpleExecutable Build() => _inner;
    }
}
