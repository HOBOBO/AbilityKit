#if UNITY_EDITOR
using System;
using System.Text;
using Sirenix.Utilities;
using Sirenix.Utilities.Editor;
using Sirenix.OdinInspector.Editor;
#endif

namespace Emilia.Kit
{
    public static class OdinEnumUtility
    {
#if UNITY_EDITOR
        private static readonly StringBuilder SB = new();
#endif

        public static string GetDescription<T>(T value)
        {
#if UNITY_EDITOR
            Func<T, T, bool> equalityComparer = PropertyValueEntry<T>.EqualityComparer;

            var enumVals = EnumTypeUtilities<T>.AllEnumMemberInfos;

            for (int i = 0; i < enumVals.Length; i++)
            {
                var val = enumVals[i];

                if (equalityComparer(val.Value, value))
                {
                    return val.NiceName;
                }
            }

            if (EnumTypeUtilities<T>.IsFlagEnum)
            {
                var val64 = Convert.ToInt64(value);

                if (val64 == 0)
                {
                    return GetNoneValueString<T>();
                }

                SB.Length = 0;

                for (int i = 0; i < enumVals.Length; i++)
                {
                    var val = enumVals[i];
                    var flags = Convert.ToInt64(val.Value);
                    if (flags == 0) continue;
                    if ((val64 & flags) == flags)
                    {
                        if (SB.Length > 0) SB.Append(", ");
                        SB.Append(val.NiceName);
                    }
                }

                return SB.ToString();
            }

            return value.ToString().SplitPascalCase();
#else
            return string.Empty;
#endif

        }

#if UNITY_EDITOR
        private static string GetNoneValueString<T>()
        {
            var name = Enum.GetName(typeof(T), GetZeroValue<T>());
            if (name != null) return name.SplitPascalCase();
            return "None";
        }

        private static T GetZeroValue<T>()
        {
            var backingType = Enum.GetUnderlyingType(typeof(T));
            object backingZero = Convert.ChangeType(0, backingType);
            return (T) backingZero;
        }
#endif
    }
}