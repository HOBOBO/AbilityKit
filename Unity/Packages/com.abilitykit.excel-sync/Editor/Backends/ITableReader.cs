using System;
using System.Collections.Generic;

namespace AbilityKit.ExcelSync.Editor
{
    public interface ITableReader : IDisposable
    {
        IReadOnlyList<string> GetHeaders();
        IEnumerable<IReadOnlyList<object>> ReadRows(int startRowIndex);
    }
}
