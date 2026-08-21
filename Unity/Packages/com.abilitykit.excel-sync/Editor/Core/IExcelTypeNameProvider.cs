using System;

namespace AbilityKit.ExcelSync.Editor
{
    /// <summary>
    /// 把 C# 成员类型映射为表格"类型行"里的类型字符串（如 "int"、"string"、"list,int"、"json"）。
    /// 供带类型标注的表头生成使用，使下游管线（Luban 或自研配置管线）能按列解析类型。
    /// 自研管线可实现本接口，注入自己的类型命名约定；缺省实现见 <see cref="DefaultExcelTypeNameProvider"/>。
    /// </summary>
    public interface IExcelTypeNameProvider
    {
        string GetTypeName(Type memberType);
    }
}
