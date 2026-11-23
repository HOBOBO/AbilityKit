using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Identifiers
{
    /// <summary>
    /// 维护按序号比较的字符串与确定性 V1 哈希标识之间的可逆映射。
    /// 注册表非线程安全，不同字符串出现哈希冲突时拒绝注册，不自行分配替代标识。
    /// </summary>
    /// <remarks>
    /// 名称区分大小写，不做 Unicode 规范化；标识保留完整有符号 32 位结果，零与负数均可有效。
    /// 反向映射与冲突检查仅覆盖本实例已成功注册的名称，不构成全局唯一性保证。
    /// </remarks>
    public sealed class StableStringIdRegistry
    {
        private readonly Dictionary<string, int> _nameToId = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<int, string> _idToName = new Dictionary<int, string>();

        /// <summary>
        /// 获取名称已有的标识；尚未登记时注册其确定性 V1 标识。
        /// </summary>
        /// <remarks>
        /// 重复登记相同名称返回已有标识；发生哈希冲突时保留原条目，不登记冲突名称。
        /// 调用方负责并发访问同步及名称集合的生命周期，注册顺序不参与标识计算。
        /// </remarks>
        /// <param name="name">待解析的非空名称，允许空字符串。</param>
        /// <returns>与名称关联的稳定 V1 标识。</returns>
        /// <exception cref="ArgumentNullException">名称为空引用。</exception>
        /// <exception cref="InvalidOperationException">不同名称产生了同一哈希标识。</exception>
        public int GetOrRegister(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (_nameToId.TryGetValue(name, out var id))
            {
                return id;
            }

            id = StableHashV1.Fnv1a32Utf16(name);

            /*
             * 先核查反向映射再写入两张表，保证被拒绝的哈希冲突不会留下单向注册条目。
             */
            if (_idToName.TryGetValue(id, out var existingName) && existingName != name)
            {
                throw new InvalidOperationException($"StableStringIdRegistry hash collision: '{existingName}' and '{name}' => {id}");
            }

            _nameToId[name] = id;
            _idToName[id] = name;
            return id;
        }

        /// <summary>
        /// 尝试按名称解析已经注册的标识，不为未注册的名称创建条目。
        /// </summary>
        /// <param name="name">待查询的名称；空引用按未找到处理。</param>
        /// <param name="id">成功时接收标识，失败时接收零；是否成功以返回值为准。</param>
        /// <returns>名称已注册时为 true，否则为 false。</returns>
        public bool TryGetId(string name, out int id)
        {
            if (name == null)
            {
                id = default;
                return false;
            }

            return _nameToId.TryGetValue(name, out id);
        }

        /// <summary>
        /// 尝试按标识解析已经注册的名称。
        /// </summary>
        /// <remarks>
        /// 空字符串本身也可以成功注册，必须以返回值区分该名称与查询失败。
        /// </remarks>
        /// <param name="id">待查询的标识。</param>
        /// <param name="name">成功时接收注册名称，失败时接收空字符串。</param>
        /// <returns>标识已注册时为 true，否则为 false。</returns>
        public bool TryGetName(int id, out string name)
        {
            if (_idToName.TryGetValue(id, out var registeredName))
            {
                name = registeredName;
                return true;
            }

            name = string.Empty;
            return false;
        }

    }
}
