using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OfficeOpenXml;

namespace AbilityKit.Tools.ExcelTool
{
    /// <summary>
    /// Luban 配置表 Excel 读写 CLI。所有命令输出 JSON 到 stdout，失败时 stdout 为 {"ok":false,"error":...} 且退出码 1。
    /// </summary>
    internal static class Program
    {
        private const string DefaultDir = "LubanConfig/Moba/MiniTemplate/Datas";
        private const string MarkerHeader = "##var";
        private const string MarkerType = "##type";
        private const string MarkerComment = "##";

        private static int Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);   // EPPlus 4.x 需要（见 csproj 注释）

            try
            {
                if (args.Length == 0 || IsHelp(args[0]))
                {
                    Console.WriteLine(HelpText);
                    return 0;
                }

                var cmd = args[0].ToLowerInvariant();
                var positional = args.Skip(1).Where(a => !a.StartsWith("--")).ToList();
                var options = ParseOptions(args);

                return cmd switch
                {
                    "list" => CmdList(options),
                    "show" => CmdShow(positional, options),
                    "get" => CmdGet(positional, options),
                    "set" => CmdSet(positional, options),
                    "add" => CmdAdd(positional, options),
                    "del" or "delete" => CmdDel(positional, options),
                    "lint" => CmdLint(positional, options),
                    _ => Fail($"unknown command '{cmd}'. See --help.")
                };
            }
            catch (Exception e)
            {
                return Fail($"{e.GetType().Name}: {e.Message}");
            }
        }

        // ---------------- 命令 ----------------

        private static int CmdList(Dictionary<string, string> options)
        {
            var dir = ResolveDir(options);
            var tables = new List<object>();
            foreach (var file in Directory.GetFiles(dir, "*.xlsx").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.StartsWith("__", StringComparison.Ordinal)) continue;   // schema 表不是数据表

                using var pkg = Open(file);
                var ws = FirstSheet(pkg);
                var layout = ReadLayout(ws);
                tables.Add(new
                {
                    table = name,
                    file = Path.GetFileName(file),
                    sheet = ws.Name,
                    rows = Math.Max(0, (ws.Dimension?.End.Row ?? 0) - layout.DataStartRow + 1),
                    fields = layout.Fields.Count,
                    key = layout.KeyField
                });
            }

            return Ok(new { ok = true, dir, count = tables.Count, tables });
        }

        private static int CmdShow(List<string> positional, Dictionary<string, string> options)
        {
            var (path, pkg, ws, layout) = OpenTable(positional, options, requireTable: true);
            using (pkg)
            {
                var rows = ReadRows(ws, layout);

                if (options.TryGetValue("id", out var idFilter))
                {
                    rows = rows.Where(r => string.Equals(Str(r, layout.KeyField), idFilter, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (rows.Count == 0) return Fail($"row {layout.KeyField}={idFilter} not found in {Path.GetFileName(path)}");
                }

                if (options.TryGetValue("limit", out var limitStr) && int.TryParse(limitStr, out var limit) && limit > 0)
                {
                    rows = rows.Take(limit).ToList();
                }

                var fields = layout.Fields.Select((f, i) => new
                {
                    field = f,
                    type = i < layout.Types.Count ? layout.Types[i] : null,
                    comment = i < layout.Comments.Count ? layout.Comments[i] : null
                }).ToList();

                return Ok(new
                {
                    ok = true,
                    table = Path.GetFileNameWithoutExtension(path),
                    sheet = ws.Name,
                    key = layout.KeyField,
                    dataStartRow = layout.DataStartRow,
                    fields,
                    rowCount = rows.Count,
                    rows
                });
            }
        }

        private static int CmdGet(List<string> positional, Dictionary<string, string> options)
        {
            if (positional.Count < 3) return Fail("usage: get <table> <id> <field>");
            var (path, pkg, ws, layout) = OpenTable(positional, options, requireTable: true);
            using (pkg)
            {
                var field = positional[2];
                if (!layout.Fields.Contains(field, StringComparer.OrdinalIgnoreCase))
                    return Fail($"field '{field}' not found. Fields: {string.Join(", ", layout.Fields)}");

                var row = ReadRows(ws, layout).FirstOrDefault(r => string.Equals(Str(r, layout.KeyField), positional[1], StringComparison.OrdinalIgnoreCase));
                if (row == null) return Fail($"row {layout.KeyField}={positional[1]} not found");

                return Ok(new { ok = true, table = Path.GetFileNameWithoutExtension(path), id = positional[1], field, value = Str(row, field) });
            }
        }

        private static int CmdSet(List<string> positional, Dictionary<string, string> options)
        {
            if (positional.Count < 2) return Fail("usage: set <table> <id> <field>=<value> [<field>=<value> ...]");
            var (path, pkg, ws, layout) = OpenTable(positional, options, requireTable: true);
            using (pkg)
            {
                var assignments = ParseAssignments(positional.Skip(2));
                if (assignments.Count == 0) return Fail("no assignments given (expect <field>=<value>)");

                var row = FindRow(ws, layout, positional[1]);
                if (row < 0) return Fail($"row {layout.KeyField}={positional[1]} not found in {Path.GetFileName(path)}");

                var changed = new List<object>();
                foreach (var kv in assignments)
                {
                    var col = FieldColumn(layout, kv.Key);
                    if (col < 0) return Fail($"field '{kv.Key}' not found. Fields: {string.Join(", ", layout.Fields)}");
                    WriteCell(ws, row, col, kv.Value);
                    changed.Add(new { field = kv.Key, value = string.IsNullOrEmpty(kv.Value) ? "(cleared)" : kv.Value });
                }

                pkg.Save();
                return Ok(new { ok = true, table = Path.GetFileNameWithoutExtension(path), id = positional[1], rowIndex = row, changed });
            }
        }

        private static int CmdAdd(List<string> positional, Dictionary<string, string> options)
        {
            if (positional.Count < 2) return Fail("usage: add <table> <Id=N> <field>=<value> [...]");
            var (path, pkg, ws, layout) = OpenTable(positional, options, requireTable: true);
            using (pkg)
            {
                var assignments = ParseAssignments(positional.Skip(1));
                var values = assignments.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
                if (!values.TryGetValue(layout.KeyField, out var key) || string.IsNullOrWhiteSpace(key))
                    return Fail($"missing primary key: pass {layout.KeyField}=<value>");

                if (FindRow(ws, layout, key) >= 0) return Fail($"{layout.KeyField}={key} already exists");

                var lastRow = ws.Dimension?.End.Row ?? layout.DataStartRow - 1;
                var newRow = Math.Max(lastRow + 1, layout.DataStartRow);
                for (var i = 0; i < layout.Fields.Count; i++)
                {
                    values.TryGetValue(layout.Fields[i], out var v);
                    WriteCell(ws, newRow, layout.FieldColumns[i], v);
                }

                pkg.Save();
                var added = ReadRowsWithIndex(ws, layout).FirstOrDefault(x => x.Index == newRow).Row;
                return Ok(new { ok = true, table = Path.GetFileNameWithoutExtension(path), rowIndex = newRow, row = added });
            }
        }

        private static int CmdDel(List<string> positional, Dictionary<string, string> options)
        {
            if (positional.Count < 2) return Fail("usage: del <table> <Id>");
            var (path, pkg, ws, layout) = OpenTable(positional, options, requireTable: true);
            using (pkg)
            {
                var row = FindRow(ws, layout, positional[1]);
                if (row < 0) return Fail($"row {layout.KeyField}={positional[1]} not found in {Path.GetFileName(path)}");

                // 先取出该行内容用于回执，再整行删除（不留空行，避免 Luban 顺序读取时遇到空行）
                var removed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < layout.Fields.Count; i++)
                {
                    removed[layout.Fields[i]] = CellText(ws, row, layout.FieldColumns[i]);
                }

                ws.DeleteRow(row, 1);
                pkg.Save();
                return Ok(new { ok = true, table = Path.GetFileNameWithoutExtension(path), deletedId = positional[1], rowIndex = row, removed });
            }
        }

        private static int CmdLint(List<string> positional, Dictionary<string, string> options)
        {
            var (path, pkg, ws, layout) = OpenTable(positional, options, requireTable: true);
            using (pkg)
            {
                var problems = new List<object>();
                var notes = new List<object>();
                var rows = ReadRowsWithIndex(ws, layout);

                foreach (var (rowIdx, row) in rows)
                {
                    // Luban 用首列识别数据行，因此首位字段不允许为空（实测：这是唯一会拒收的空值位置）
                    if (layout.Fields.Count > 0 && string.IsNullOrEmpty(Str(row, layout.Fields[0])))
                    {
                        problems.Add(new { row = rowIdx, field = layout.Fields[0], issue = "首列为空：Luban 用首列识别数据行，首位字段不允许为空" });
                    }

                    // 非首列的空字符串 Luban 接受（实测），但与本仓库写出口径不一致（我们写缺失单元格）。
                    // 仅作提示，不计入失败。
                    for (var i = 1; i < layout.Fields.Count; i++)
                    {
                        var value = Str(row, layout.Fields[i]);
                        if (value != null && value.Length == 0)
                        {
                            notes.Add(new { row = rowIdx, field = layout.Fields[i], note = "空字符串单元格（Luban 接受；本仓库写出口径为缺失单元格）" });
                        }
                    }
                }

                // 重复主键
                var dupes = rows.Select(r => Str(r.Item2, layout.KeyField))
                                .Where(v => !string.IsNullOrEmpty(v))
                                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                                .Where(g => g.Count() > 1)
                                .Select(g => g.Key)
                                .ToList();
                foreach (var d in dupes) problems.Add(new { row = 0, field = layout.KeyField, issue = $"重复主键 {d}" });

                return Ok(new { ok = problems.Count == 0, table = Path.GetFileNameWithoutExtension(path), problems, notes });
            }
        }

        // ---------------- Luban 布局 ----------------

        private sealed class Layout
        {
            public int HeaderRow = 1;
            public int TypeRow = -1;
            public int CommentRow = -1;
            public int DataStartRow = 2;
            public List<string> Fields = new();
            public List<string> Types = new();
            public List<string> Comments = new();
            public string KeyField = "Id";
            public List<int> FieldColumns = new();   // 与 Fields 一一对应的工作表列号
        }

        private static Layout ReadLayout(ExcelWorksheet ws)
        {
            var layout = new Layout();
            var maxRow = ws.Dimension?.End.Row ?? 0;
            var maxCol = ws.Dimension?.End.Column ?? 0;

            // 标记列在 A 列；若 A 列不是 ##var（非 Luban 布局），退化为"第 1 行是表头、A 列就是首字段"。
            var markerCol = string.Equals(CellText(ws, 1, 1), MarkerHeader, StringComparison.Ordinal) ? 1 : 0;
            layout.HeaderRow = 1;

            for (var r = 1; r <= Math.Min(maxRow, 6); r++)
            {
                var marker = markerCol > 0 ? CellText(ws, r, 1) : null;
                if (marker == MarkerType) layout.TypeRow = r;
                else if (marker == MarkerComment) layout.CommentRow = r;
            }

            var firstFieldCol = markerCol > 0 ? 2 : 1;
            for (var c = firstFieldCol; c <= maxCol; c++)
            {
                var name = CellText(ws, layout.HeaderRow, c);
                if (string.IsNullOrWhiteSpace(name)) continue;
                layout.Fields.Add(name.Trim());
                layout.FieldColumns.Add(c);
                layout.Types.Add(layout.TypeRow > 0 ? CellText(ws, layout.TypeRow, c) : null);
                layout.Comments.Add(layout.CommentRow > 0 ? CellText(ws, layout.CommentRow, c) : null);
            }

            layout.DataStartRow = new[] { layout.HeaderRow, layout.TypeRow, layout.CommentRow }.Max() + 1;
            layout.KeyField = layout.Fields.FirstOrDefault(f => string.Equals(f, "Id", StringComparison.OrdinalIgnoreCase))
                              ?? (layout.Fields.Count > 0 ? layout.Fields[0] : "Id");
            return layout;
        }

        private static List<Dictionary<string, string>> ReadRows(ExcelWorksheet ws, Layout layout)
            => ReadRowsWithIndex(ws, layout).Select(x => x.Row).ToList();

        private static List<(int Index, Dictionary<string, string> Row)> ReadRowsWithIndex(ExcelWorksheet ws, Layout layout)
        {
            var result = new List<(int, Dictionary<string, string>)>();
            var maxRow = ws.Dimension?.End.Row ?? 0;
            for (var r = layout.DataStartRow; r <= maxRow; r++)
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var any = false;
                for (var i = 0; i < layout.Fields.Count; i++)
                {
                    var v = CellText(ws, r, layout.FieldColumns[i]);
                    row[layout.Fields[i]] = v;
                    if (!string.IsNullOrEmpty(v)) any = true;
                }
                if (any) result.Add((r, row));
            }
            return result;
        }

        private static string CellText(ExcelWorksheet ws, int row, int col)
        {
            var cell = ws.Cells[row, col];
            if (cell.Value == null) return null;
            return cell.Value as string ?? Convert.ToString(cell.Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static void WriteCell(ExcelWorksheet ws, int row, int col, string value)
        {
            var cell = ws.Cells[row, col];
            if (string.IsNullOrEmpty(value) || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
            {
                // Luban 以"单元格缺失"判定留空取默认值；空字符串会报"字段不允许为空"
                cell.Value = null;
            }
            else
            {
                cell.Value = value;
            }
        }

        private static string Str(Dictionary<string, string> row, string field)
            => row.TryGetValue(field, out var v) ? v : null;

        private static int FieldColumn(Layout layout, string field)
        {
            for (var i = 0; i < layout.Fields.Count; i++)
                if (string.Equals(layout.Fields[i], field, StringComparison.OrdinalIgnoreCase)) return layout.FieldColumns[i];
            return -1;
        }

        private static int FindRow(ExcelWorksheet ws, Layout layout, string id)
            => ReadRowsWithIndex(ws, layout)
                .Where(x => string.Equals(Str(x.Row, layout.KeyField), id, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Index).DefaultIfEmpty(-1).First();

        // ---------------- 基础设施 ----------------

        private static (string Path, ExcelPackage Pkg, ExcelWorksheet Sheet, Layout Layout) OpenTable(List<string> positional, Dictionary<string, string> options, bool requireTable)
        {
            if (requireTable && positional.Count == 0) throw new ArgumentException("missing <table> argument");
            var dir = ResolveDir(options);
            var table = positional[0];
            var path = Path.IsPathRooted(table) || table.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) && File.Exists(table)
                ? table
                : Path.Combine(dir, table + ".xlsx");
            if (!File.Exists(path)) throw new FileNotFoundException($"table file not found: {path}");

            var pkg = Open(path);
            var ws = FirstSheet(pkg);
            return (path, pkg, ws, ReadLayout(ws));
        }

        private static ExcelPackage Open(string path)
            => new ExcelPackage(new FileInfo(path));

        private static ExcelWorksheet FirstSheet(ExcelPackage pkg)
        {
            // EPPlus 4.x 的 Worksheets 索引是 1-based；Worksheets[0] 会抛 "Worksheet position out of range"
            foreach (var ws in pkg.Workbook.Worksheets) return ws;
            throw new InvalidOperationException("workbook has no worksheet");
        }

        private static string ResolveDir(Dictionary<string, string> options)
        {
            var dir = options.TryGetValue("dir", out var d) ? d : DefaultDir;
            return Path.GetFullPath(dir);
        }

        private static Dictionary<string, string> ParseOptions(string[] args)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
                var key = args[i].Substring(2);
                var val = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
                result[key] = val;
            }
            return result;
        }

        private static List<KeyValuePair<string, string>> ParseAssignments(IEnumerable<string> args)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var a in args)
            {
                var idx = a.IndexOf('=');
                if (idx <= 0) continue;
                list.Add(new KeyValuePair<string, string>(a.Substring(0, idx), a.Substring(idx + 1)));
            }
            return list;
        }

        private static bool IsHelp(string s) => s is "-h" or "--help" or "help";

        private static int Ok(object payload)
        {
            Console.WriteLine(JsonSerializer.Serialize(payload, JsonOpts));
            return 0;
        }

        private static int Fail(string message)
        {
            Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = message }, JsonOpts));
            return 1;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private const string HelpText = @"excel-tool — Luban 配置表 Excel 读写 CLI（输出 JSON）

用法：
  excel-tool list    [--dir <Datas目录>]
      列出所有配置表（行数/字段数/主键）。

  excel-tool show <table> [--id <Id>] [--limit N] [--dir <目录>]
      查看表布局（字段/类型/注释）与数据行。

  excel-tool get <table> <Id> <field>
      读取单个值。

  excel-tool set <table> <Id> <field>=<value> [<field>=<value> ...]
      按 Id 定位数据行并改写字段；空值/"" 表示清除单元格（Luban 语义）。

  excel-tool add <table> <Id=N> <field>=<value> [...]
      追加一行（必须给出主键字段）。

  excel-tool del <table> <Id>
      删除该 Id 所在的数据行。

  excel-tool lint <table>
      按 Luban 规则体检：首列非空、无空字符串单元格、无重复主键。

默认目录：" + DefaultDir + @"
改完 Excel 后用 tools/run-moba-config-sync.ps1 -Mode pull-excel 下发到项目内（SO → JSON）。";
    }
}
