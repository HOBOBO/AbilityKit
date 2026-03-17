using System;
using System.Collections.Generic;

namespace AbilityKit.ExcelSync.Editor
{
    public interface ITableWriter : IDisposable
    {
        void WriteHeaders(IReadOnlyList<string> headers, int headerRowIndex);
        void WriteRow(int rowIndex, IReadOnlyList<object> values);
        void Save();
    }
}
