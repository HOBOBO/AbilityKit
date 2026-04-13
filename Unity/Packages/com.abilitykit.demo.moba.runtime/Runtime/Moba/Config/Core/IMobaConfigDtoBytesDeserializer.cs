using System;
using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// MOBA 浜岃繘鍒?DTO 鍙嶅簭鍒楀寲鍣ㄦ帴鍙?
    /// </summary>
    public interface IMobaConfigDtoBytesDeserializer : IConfigDeserializer
    {
        /// <summary>
        /// 鍙嶅簭鍒楀寲 DTO 鏁扮粍锛圡OBA 涓撶敤鏂规硶锛?
        /// </summary>
        Array DeserializeDtoArray(byte[] bytes, Type dtoType);
    }
}
