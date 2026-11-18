using System;

namespace AbilityKit.Core.Identifiers
{
    /// <summary>
    /// 为持久化和跨运行时标识提供带版本的稳定哈希算法，不分配中间编码缓冲区。
    /// V1 契约内不得改变既有输出；需要新算法时应建立独立版本及迁移规则。
    /// </summary>
    /// <remarks>
    /// 输入按原始顺序参与计算，不进行大小写折叠或 Unicode 规范化。
    /// 哈希不保证唯一性，也不承担密码学摘要或安全校验职责。
    /// </remarks>
    public static class StableHashV1
    {
        private const uint Fnv32OffsetBasis = 2166136261u;
        private const uint Fnv32Prime = 16777619u;
        private const int ReplacementCodePoint = 0xFFFD;

        /// <summary>
        /// 将每个 UTF-16 代码单元作为一个值混合，计算 FNV-1a 32 位哈希。
        /// 此方法保持原有字符串标识算法，不等同于对 UTF-8 字节序列计算哈希。
        /// </summary>
        /// <remarks>
        /// 增补字符的两个代理代码单元分别混合；未配对代理项也按原始代码单元参与计算。
        /// 乘法按 32 位无符号整数回绕，最终保留全部位并解释为有符号整数。
        /// </remarks>
        /// <param name="value">待计算哈希的非空字符串，允许空字符串。</param>
        /// <returns>哈希值的有符号 32 位表示。</returns>
        /// <exception cref="ArgumentNullException">字符串为空引用。</exception>
        public static int Fnv1a32Utf16(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            unchecked
            {
                uint hash = Fnv32OffsetBasis;
                for (int i = 0; i < value.Length; i++)
                {
                    /*
                     * V1 按代码单元混合，不能改成逐字节编码或合并代理对，否则会改变既有标识。
                     */
                    hash ^= value[i];
                    hash *= Fnv32Prime;
                }

                return (int)hash;
            }
        }

        /// <summary>
        /// 计算 V1 UTF-16 哈希并清除符号位，供使用非负标识的领域调用。
        /// </summary>
        /// <param name="value">待计算哈希的非空字符串，允许空字符串。</param>
        /// <returns>非负的 31 位标识，不保证不同字符串之间没有冲突。</returns>
        /// <exception cref="ArgumentNullException">字符串为空引用。</exception>
        public static int Fnv1a32Utf16NonNegative(string value)
        {
            return Fnv1a32Utf16(value) & 0x7FFFFFFF;
        }

        /// <summary>
        /// 按 UTF-8 编码字节计算 FNV-1a 32 位哈希，不分配中间缓冲区。
        /// 非法 UTF-16 代理项序列按 Unicode 替换字符编码。
        /// </summary>
        /// <remarks>
        /// 合法代理对编码为同一个 Unicode 码点；每个未配对代理代码单元独立替换为 U+FFFD。
        /// 非法输入可能与显式包含替换字符的输入得到相同结果，不能据此反推原字符串。
        /// </remarks>
        /// <param name="value">待计算哈希的非空字符串，允许空字符串。</param>
        /// <returns>哈希值的有符号 32 位表示。</returns>
        /// <exception cref="ArgumentNullException">字符串为空引用。</exception>
        public static int Fnv1a32Utf8(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            unchecked
            {
                uint hash = Fnv32OffsetBasis;
                for (int index = 0; index < value.Length; index++)
                {
                    /*
                     * 只有合法代理对才消耗后一个代码单元，避免替换非法输入时跳过后续有效字符。
                     */
                    int codePoint = value[index];
                    if (char.IsHighSurrogate(value[index]))
                    {
                        if (index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]))
                        {
                            codePoint = char.ConvertToUtf32(value[index], value[++index]);
                        }
                        else
                        {
                            codePoint = ReplacementCodePoint;
                        }
                    }
                    else if (char.IsLowSurrogate(value[index]))
                    {
                        codePoint = ReplacementCodePoint;
                    }

                    hash = AppendUtf8(hash, codePoint);
                }

                return (int)hash;
            }
        }

        private static uint AppendUtf8(uint hash, int codePoint)
        {
            if (codePoint <= 0x7F)
            {
                return Mix(hash, (byte)codePoint);
            }

            if (codePoint <= 0x7FF)
            {
                hash = Mix(hash, (byte)(0xC0 | codePoint >> 6));
                return Mix(hash, (byte)(0x80 | codePoint & 0x3F));
            }

            if (codePoint <= 0xFFFF)
            {
                hash = Mix(hash, (byte)(0xE0 | codePoint >> 12));
                hash = Mix(hash, (byte)(0x80 | codePoint >> 6 & 0x3F));
                return Mix(hash, (byte)(0x80 | codePoint & 0x3F));
            }

            hash = Mix(hash, (byte)(0xF0 | codePoint >> 18));
            hash = Mix(hash, (byte)(0x80 | codePoint >> 12 & 0x3F));
            hash = Mix(hash, (byte)(0x80 | codePoint >> 6 & 0x3F));
            return Mix(hash, (byte)(0x80 | codePoint & 0x3F));
        }

        private static uint Mix(uint hash, byte value)
        {
            unchecked
            {
                return (hash ^ value) * Fnv32Prime;
            }
        }
    }
}
