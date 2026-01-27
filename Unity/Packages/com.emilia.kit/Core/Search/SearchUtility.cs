#if UNITY_EDITOR
using FuzzySharp;

namespace Emilia.Kit
{
    public static class SearchUtility
    {
        public const int MaxSearchScore = 100;
        public const int MinSearchScore = 0;

        /// <summary>
        /// 智能搜索
        /// </summary>
        /// <param name="target">目标文本</param>
        /// <param name="input">输入文本</param>
        /// <param name="inputNullResult">输入为空时的返回结果，默认为true</param>
        /// <param name="ignoreCase">是否忽略大小写，默认为true</param>
        /// <param name="pinYinWeight">拼音搜索权重</param>
        /// <returns>搜索分数0-100</returns>
        public static int SmartSearch(string target, string input, bool inputNullResult = true, bool ignoreCase = true, float pinYinWeight = 0.6f)
        {
            if (string.IsNullOrEmpty(input)) return inputNullResult ? MaxSearchScore : MinSearchScore;
            if (string.IsNullOrEmpty(target)) return MinSearchScore;

            string searchTarget = ignoreCase ? target.ToLower() : target;
            string searchInput = ignoreCase ? input.ToLower() : input;

            int directScore = Fuzz.WeightedRatio(searchInput, searchTarget);

            if (pinYinWeight <= 0) return directScore;

            string pinYin = PinYinConverterUtility.ConvertToAllSpell(searchTarget);
            int pinYinScore = Fuzz.WeightedRatio(searchInput, pinYin);
            pinYinScore = (int) (pinYinScore * pinYinWeight);

            return directScore > pinYinScore ? directScore : pinYinScore;
        }

        /// <summary>
        /// 拼音搜索
        /// </summary>
        /// <param name="target">目标文本</param>
        /// <param name="input">输入文本</param>
        /// <param name="inputNullResult">输入为空时的返回值</param>
        /// <returns>搜索分数0-100</returns>
        public static int SearchPinYin(string target, string input, bool inputNullResult = true)
        {
            if (string.IsNullOrEmpty(input)) return inputNullResult ? MaxSearchScore : MinSearchScore;
            if (string.IsNullOrEmpty(target)) return MinSearchScore;

            string inputPinYin = PinYinConverterUtility.ConvertToAllSpell(input);
            string targetPinYin = PinYinConverterUtility.ConvertToAllSpell(target);
            return Fuzz.WeightedRatio(inputPinYin, targetPinYin);
        }

        /// <summary>
        /// 匹配
        /// </summary>
        /// <param name="target">目标文本</param>
        /// <param name="input">输入文本</param>
        /// <param name="score">分数</param>
        /// <returns>结果</returns>
        public static bool Matching(string target, string input, int score = MinSearchScore) => SmartSearch(target, input) > score;

        /// <summary>
        /// 简单搜索
        /// </summary>
        /// <param name="target">目标文本</param>
        /// <param name="input">输入文本</param>
        /// <param name="nullResult">输入为空时的返回结果，默认为true</param>
        /// <param name="ignoreCase">是否忽略大小写，默认为true</param>
        /// <returns>结果</returns>
        public static bool SimpleSearch(string target, string input, bool nullResult = true, bool ignoreCase = true)
        {
            if (string.IsNullOrEmpty(input)) return nullResult;
            if (string.IsNullOrEmpty(target)) return false;

            string searchTarget = ignoreCase ? target.ToLower() : target;
            string searchInput = ignoreCase ? input.ToLower() : input;

            return SimpleMatching(searchTarget, searchInput);
        }

        /// <summary>
        /// 简单匹配
        /// </summary>
        /// <param name="text">目标文本</param>
        /// <param name="str">输入文本</param>
        /// <returns>结果</returns>
        public static bool SimpleMatching(string text, string str)
        {
            int i = 0;
            bool result = false;

            int strLength = str.Length;
            for (var j = 0; j < strLength; j++)
            {
                var temp = str[j];
                result = false;

                for (; i < text.Length; i++)
                {
                    if (temp != text[i]) continue;

                    result = true;
                    break;
                }
            }
            return result;
        }
    }
}
#endif