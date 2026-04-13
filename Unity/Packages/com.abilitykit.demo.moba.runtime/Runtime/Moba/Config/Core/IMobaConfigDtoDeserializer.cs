using System;
using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// MOBA DTO 鍙嶅簭鍒楀寲鍣ㄦ帴鍙ｏ紙鎵╁睍閫氱敤 IConfigDeserializer锛?
    /// </summary>
    public interface IMobaConfigDtoDeserializer : IConfigDeserializer
    {
        /// <summary>
        /// 鍙嶅簭鍒楀寲 DTO 鏁扮粍锛圡OBA 涓撶敤鏂规硶锛?
        /// </summary>
        Array DeserializeDtoArray(string text, Type dtoType);
    }
}
