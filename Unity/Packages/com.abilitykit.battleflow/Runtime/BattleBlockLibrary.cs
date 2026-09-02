using System;
using System.Collections.Generic;

namespace AbilityKit.BattleFlow
{
    /// <summary>
    /// 积木库：项目的积木注册表（框架内置原子积木 + 项目定义的复合/自定义积木）。
    /// 项目通过 <see cref="Add"/> 注册自己的积木（数据声明、不动框架代码），编辑器调色板从这里取。
    /// </summary>
    public sealed class BattleBlockLibrary
    {
        private readonly Dictionary<string, BattleBlock> _blocks = new Dictionary<string, BattleBlock>(StringComparer.OrdinalIgnoreCase);

        /// <summary>注册一个积木。重复 id 会被拒绝。</summary>
        public BattleBlockLibrary Add(BattleBlock block)
        {
            if (block is null) throw new ArgumentNullException(nameof(block));
            if (string.IsNullOrWhiteSpace(block.Id))
                throw new ArgumentException("Block id is required.", nameof(block));
            if (_blocks.ContainsKey(block.Id))
                throw new ArgumentException($"Block '{block.Id}' is already registered.", nameof(block));
            _blocks[block.Id] = block;
            return this;
        }

        /// <summary>按 id 取积木。</summary>
        public bool TryGet(string id, out BattleBlock block) => _blocks.TryGetValue(id, out block!);

        /// <summary>已注册的积木（供编辑器调色板枚举）。</summary>
        public IReadOnlyCollection<BattleBlock> Blocks => _blocks.Values;
    }
}
