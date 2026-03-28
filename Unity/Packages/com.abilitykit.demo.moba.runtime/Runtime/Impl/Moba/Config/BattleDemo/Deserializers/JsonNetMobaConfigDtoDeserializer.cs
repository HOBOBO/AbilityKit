using System;
using AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.BattleDemo
{
    public sealed class JsonNetMobaConfigDtoDeserializer : IMobaConfigDtoDeserializer
    {
        public static readonly JsonNetMobaConfigDtoDeserializer Instance = new JsonNetMobaConfigDtoDeserializer();

        private JsonNetMobaConfigDtoDeserializer() { }

        public Array DeserializeDtoArray(string text, Type dtoType)
        {
            if (dtoType == null) throw new ArgumentNullException(nameof(dtoType));
            if (string.IsNullOrEmpty(text)) return Array.CreateInstance(dtoType, 0);

            var token = JToken.Parse(text);
            if (token is not JArray array) return Array.CreateInstance(dtoType, 0);

            var list = new System.Collections.Generic.List<object>();
            foreach (var item in array)
            {
                // 使用 Newtonsoft.Json 直接反序列化为普通 DTO
                var obj = item.ToObject(dtoType);
                if (obj != null)
                {
                    list.Add(obj);
                }
            }

            var result = Array.CreateInstance(dtoType, list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                result.SetValue(list[i], i);
            }
            return result;
        }
    }
}
