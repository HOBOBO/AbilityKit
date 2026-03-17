using System;

namespace AbilityKit.ExcelSync.Editor.Codecs
{
    public interface IExcelValueCodec
    {
        bool TryDecode(object cellValue, Type targetType, ExcelCodecContext context, out object value);

        bool TryEncode(object value, ExcelCodecContext context, out object cellValue);
    }
}
