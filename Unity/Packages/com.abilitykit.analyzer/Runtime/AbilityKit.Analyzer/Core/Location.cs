/// <summary>
/// 文件名称: Location.cs
/// 
/// 功能描述: 定义代码位置信息的数据结构，用于表示源代码中的具体位置。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;

namespace AbilityKit.Analyzer
{
/// <summary>
/// 表示源代码中一个具体位置的不可变结构体。
/// </summary>
public readonly struct Location : IEquatable<Location>
{
    /// <summary>源文件的完整路径</summary>
    public string FilePath { get; }

    /// <summary>行号（1-based）</summary>
    public int Line { get; }

    /// <summary>列号（1-based）</summary>
    public int Column { get; }

    /// <summary>文本跨度长度，Length > 0 时表示连续范围</summary>
    public int Length { get; }

    public Location(string filePath, int line, int column, int length = 0)
    {
        FilePath = filePath ?? string.Empty;
        Line = line;
        Column = column;
        Length = length;
    }

    /// <summary>获取表示"无位置"的 Location 实例</summary>
    public static Location None => default;

    /// <summary>创建位置实例的便捷工厂方法</summary>
    public static Location Create(string filePath, int line, int column, int length = 1)
    {
        return new Location(filePath, line, column, length);
    }

    public bool Equals(Location other)
    {
        return FilePath == other.FilePath 
            && Line == other.Line 
            && Column == other.Column 
            && Length == other.Length;
    }

    public override bool Equals(object obj)
    {
        return obj is Location other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FilePath, Line, Column, Length);
    }

    /// <summary>格式: "FilePath(Line,Column)" 或 "FilePath(Line,Column,Length)"</summary>
    public override string ToString()
    {
        return Length > 0
            ? $"{FilePath}({Line},{Column},{Length})"
            : $"{FilePath}({Line},{Column})";
    }

    public static bool operator ==(Location left, Location right) => left.Equals(right);
    public static bool operator !=(Location left, Location right) => !left.Equals(right);
}
}