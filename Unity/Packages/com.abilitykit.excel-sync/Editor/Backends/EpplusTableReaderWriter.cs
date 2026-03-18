using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;

namespace AbilityKit.ExcelSync.Editor
{
    internal sealed class EpplusTableReader : ITableReader
    {
        private readonly ExcelPackage package;
        private readonly ExcelWorksheet sheet;
        private readonly ExcelTableOptions options;

        public EpplusTableReader(string filePath, ExcelTableOptions options)
        {
            this.options = options;
            #if EPPLUS_4_5_OR_NEWER
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            #endif
            package = new ExcelPackage(new FileInfo(filePath));
            sheet = string.IsNullOrEmpty(options.SheetName)
                ? package.Workbook.Worksheets[0]
                : (package.Workbook.Worksheets[options.SheetName] ?? package.Workbook.Worksheets[0]);
        }

        public IReadOnlyList<string> GetHeaders()
        {
            var headers = new List<string>();
            if (sheet.Dimension == null)
            {
                return headers;
            }

            var maxCols = sheet.Dimension.End.Column;
            for (var c = 1; c <= maxCols; c++)
            {
                var v = GetCellValueWithMerge(options.HeaderRowIndex, c);
                if (v == null)
                {
                    headers.Add(string.Empty);
                    continue;
                }

                headers.Add(v.ToString().Trim());
            }

            return headers;
        }

        public IEnumerable<IReadOnlyList<object>> ReadRows(int startRowIndex)
        {
            if (sheet.Dimension == null)
            {
                yield break;
            }

            var maxRows = sheet.Dimension.End.Row;
            var maxCols = sheet.Dimension.End.Column;
            for (var r = startRowIndex; r <= maxRows; r++)
            {
                var row = new List<object>(maxCols);
                for (var c = 1; c <= maxCols; c++)
                {
                    var v = GetCellValueWithMerge(r, c);
                    row.Add(v);
                }

                yield return row;
            }
        }

        private object GetCellValueWithMerge(int row, int col)
        {
            var cell = sheet.Cells[row, col];
            var v = cell.Value;
            if (v != null)
            {
                return v;
            }

            if (!cell.Merge)
            {
                return null;
            }

            var mergedAddress = sheet.MergedCells[row, col];
            if (string.IsNullOrEmpty(mergedAddress))
            {
                return null;
            }

            var addr = new ExcelAddress(mergedAddress);
            return sheet.Cells[addr.Start.Row, addr.Start.Column].Value;
        }

        public void Dispose()
        {
            package.Dispose();
        }
    }

    internal sealed class EpplusTableWriter : ITableWriter
    {
        private readonly ExcelPackage package;
        private readonly ExcelWorksheet sheet;
        private readonly FileInfo fileInfo;

        public EpplusTableWriter(string filePath, ExcelTableOptions options)
        {
            #if EPPLUS_4_5_OR_NEWER
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            #endif
            fileInfo = new FileInfo(filePath);
            package = new ExcelPackage(fileInfo);
            if (string.IsNullOrEmpty(options.SheetName))
            {
                sheet = package.Workbook.Worksheets.Count > 0
                    ? package.Workbook.Worksheets[0]
                    : package.Workbook.Worksheets.Add("Sheet1");
            }
            else
            {
                sheet = package.Workbook.Worksheets[options.SheetName] ?? package.Workbook.Worksheets.Add(options.SheetName);
            }
        }

        public void WriteHeaders(IReadOnlyList<string> headers, int headerRowIndex)
        {
            for (var i = 0; i < headers.Count; i++)
            {
                sheet.Cells[headerRowIndex, i + 1].Value = headers[i];
            }
        }

        public void WriteRow(int rowIndex, IReadOnlyList<object> values)
        {
            for (var i = 0; i < values.Count; i++)
            {
                sheet.Cells[rowIndex, i + 1].Value = values[i];
            }
        }

        public void Save()
        {
            package.Save();
        }

        public void Dispose()
        {
            package.Dispose();
        }
    }

    public sealed class EpplusTableReaderWriterFactory : ITableReaderWriterFactory
    {
        public ITableReader CreateReader(string filePath, ExcelTableOptions options)
        {
            return new EpplusTableReader(filePath, options);
        }

        public ITableWriter CreateWriter(string filePath, ExcelTableOptions options)
        {
            return new EpplusTableWriter(filePath, options);
        }
    }
}
