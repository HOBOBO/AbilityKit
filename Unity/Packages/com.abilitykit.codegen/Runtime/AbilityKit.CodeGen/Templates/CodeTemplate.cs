/// <summary>
/// 文件名称: CodeTemplate.cs
/// 
/// 功能描述: 定义代码模板系统，包括模板接口、基础实现和源码构建器。
/// 
/// 创建日期: 2026-04-06
/// 修改日期: 2026-04-06
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;

namespace AbilityKit.CodeGen.Templates
{
/// <summary>
/// 代码模板接口。
/// </summary>
public interface ITemplate
{
    /// <summary>渲染模板</summary>
    string Render(IDictionary<string, object> variables);

    /// <summary>模板源码</summary>
    string Source { get; }
}

/// <summary>
/// 代码模板基类。
/// </summary>
public abstract class CodeTemplate : ITemplate
{
    protected abstract string TemplateSource { get; }

    public string Source => TemplateSource;

    public string Render(IDictionary<string, object> variables)
    {
        if (variables == null || variables.Count == 0)
            return TemplateSource;

        var result = TemplateSource;

        foreach (var variable in variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            var value = variable.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value);
        }

        return result;
    }

    /// <summary>使用强类型参数渲染模板</summary>
    public abstract string Render<T>(T parameters) where T : class;
}

/// <summary>
/// 字符串模板实现。
/// </summary>
public sealed class StringTemplate : ITemplate
{
    private readonly string _source;

    public string Source => _source;

    public StringTemplate(string source)
    {
        _source = source ?? string.Empty;
    }

    public string Render(IDictionary<string, object> variables)
    {
        if (variables == null || variables.Count == 0)
            return _source;

        var result = _source;

        foreach (var variable in variables)
        {
            var placeholder = $"{{{{{variable.Key}}}}}";
            var value = variable.Value?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value);
        }

        return result;
    }

    /// <summary>使用匿名类型渲染模板</summary>
    public string RenderFromObject(object parameters)
    {
        if (parameters == null) return _source;

        var properties = parameters.GetType().GetProperties();
        var variables = properties.ToDictionary(
            p => p.Name,
            p => p.GetValue(parameters));

        return Render(variables);
    }
}

/// <summary>
/// 源码文本构建器。
/// </summary>
public sealed class SourceTextBuilder
{
    private readonly System.Text.StringBuilder _sb = new();
    private int _indentLevel;
    private string _indentString = "    ";

    /// <summary>设置缩进字符串</summary>
    public SourceTextBuilder WithIndent(string indent)
    {
        _indentString = indent ?? "    ";
        return this;
    }

    /// <summary>增加缩进级别</summary>
    public SourceTextBuilder Indent()
    {
        _indentLevel++;
        return this;
    }

    /// <summary>减少缩进级别</summary>
    public SourceTextBuilder Unindent()
    {
        if (_indentLevel > 0) _indentLevel--;
        return this;
    }

    /// <summary>在当前缩进级别添加一行</summary>
    public SourceTextBuilder Line(string content = "")
    {
        if (_indentLevel > 0 && !string.IsNullOrEmpty(content))
        {
            _sb.Append(string.Concat(Enumerable.Repeat(_indentString, _indentLevel)));
        }
        _sb.AppendLine(content);
        return this;
    }

    /// <summary>添加多行</summary>
    public SourceTextBuilder Lines(params string[] lines)
    {
        foreach (var line in lines)
        {
            Line(line);
        }
        return this;
    }

    /// <summary>添加不带换行的行</summary>
    public SourceTextBuilder LineNoNewline(string content = "")
    {
        if (_indentLevel > 0 && !string.IsNullOrEmpty(content))
        {
            _sb.Append(string.Concat(Enumerable.Repeat(_indentString, _indentLevel)));
        }
        _sb.Append(content);
        return this;
    }

    /// <summary>添加带大括号的花括号块</summary>
    public SourceTextBuilder Block(string statement, Action<SourceTextBuilder> body)
    {
        Line(statement);
        Line("{");
        Indent();
        body(this);
        Unindent();
        Line("}");
        return this;
    }

    /// <summary>添加空行</summary>
    public SourceTextBuilder EmptyLine()
    {
        _sb.AppendLine();
        return this;
    }

    public override string ToString()
    {
        return _sb.ToString();
    }

    /// <summary>清空构建器</summary>
    public void Clear()
    {
        _sb.Clear();
        _indentLevel = 0;
    }
}

/// <summary>
/// 模板变量集合。
/// </summary>
public sealed class TemplateVariables
{
    private readonly Dictionary<string, object> _variables = new();

    public void Set(string name, object value)
    {
        _variables[name] = value;
    }

    public T Get<T>(string name)
    {
        return _variables.TryGetValue(name, out var value) ? (T)value : default;
    }

    public T Get<T>(string name, T defaultValue)
    {
        return _variables.TryGetValue(name, out var value) ? (T)value : defaultValue;
    }

    public bool TryGet<T>(string name, out T value)
    {
        if (_variables.TryGetValue(name, out var v))
        {
            value = (T)v;
            return true;
        }
        value = default;
        return false;
    }

    public bool Contains(string name) => _variables.ContainsKey(name);

    public IReadOnlyDictionary<string, object> ToDictionary() => new Dictionary<string, object>(_variables);
}
}