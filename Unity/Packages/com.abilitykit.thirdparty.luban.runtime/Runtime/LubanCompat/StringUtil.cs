using System.Collections.Generic;

namespace Luban
{
    public static class StringUtil
    {
        public static string CollectionToString<T>(IEnumerable<T> collection)
        {
            return global::Luban.Utils.StringUtil.CollectionToString(collection);
        }
    }
}
