#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.International.Converters.PinYinConverter;
using NPinyin;
using UnityEngine;

namespace Emilia.Kit
{
    public static class PinYinConverterUtility
    {
        private static readonly Dictionary<string, string> _pinyinCache = new();
        private static readonly Dictionary<char, bool> _isChineseChar = new();

        public static string ConvertToAllSpell(string strChinese)
        {
            if (string.IsNullOrEmpty(strChinese)) return string.Empty;
            if (_pinyinCache.TryGetValue(strChinese, out string cachedResult)) return cachedResult;

            try
            {
                StringBuilder fullSpell = new();
                for (int i = 0; i < strChinese.Length; i++)
                {
                    var chr = strChinese[i];
                    fullSpell.Append(GetSpell(chr));
                }

                string result = fullSpell.ToString();

                _pinyinCache[strChinese] = result;
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError("全拼转化出错！" + e.Message);
                return string.Empty;
            }
        }

        public static bool ContainsChinese(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            return str.Any(IsChineseCharacter);
        }

        public static bool AllChinese(string str)
        {
            if (string.IsNullOrEmpty(str)) return false;
            return str.All(IsChineseCharacter);

        }

        private static bool IsChineseCharacter(char chr)
        {
            if (_isChineseChar.TryGetValue(chr, out bool isChinese)) return isChinese;

            isChinese = ChineseChar.IsValidChar(chr);
            _isChineseChar[chr] = isChinese;
            return isChinese;
        }

        private static string GetSpell(char chr)
        {
            string converter = Pinyin.GetPinyin(chr);

            bool isChinese = ChineseChar.IsValidChar(converter[0]);
            if (isChinese == false) return converter;

            ChineseChar chineseChar = new(converter[0]);
            for (var i = 0; i < chineseChar.Pinyins.Count; i++)
            {
                string value = chineseChar.Pinyins[i];
                if (string.IsNullOrEmpty(value) == false) return value.Remove(value.Length - 1, 1);
            }

            return converter;
        }
    }
}
#endif