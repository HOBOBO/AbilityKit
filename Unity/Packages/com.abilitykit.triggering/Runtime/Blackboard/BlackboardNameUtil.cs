using System;
using System.Text;

namespace AbilityKit.Triggering.Blackboard
{
    public static class BlackboardNameUtil
    {
        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            name = name.Trim();
            var sb = new StringBuilder(name.Length);

            for (int i = 0; i < name.Length; i++)
            {
                var c = name[i];
                if (c == '\\' || c == '/') c = '.';
                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }
    }
}
