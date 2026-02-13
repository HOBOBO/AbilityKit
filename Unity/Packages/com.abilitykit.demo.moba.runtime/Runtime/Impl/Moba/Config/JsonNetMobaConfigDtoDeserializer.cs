using System;
using Newtonsoft.Json;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    public sealed class JsonNetMobaConfigDtoDeserializer : IMobaConfigDtoDeserializer
    {
        public static readonly JsonNetMobaConfigDtoDeserializer Instance = new JsonNetMobaConfigDtoDeserializer();

        private JsonNetMobaConfigDtoDeserializer() { }

        public Array DeserializeDtoArray(string text, Type dtoType)
        {
            if (dtoType == null) throw new ArgumentNullException(nameof(dtoType));
            var dtoArrayType = dtoType.MakeArrayType();
            return (Array)JsonConvert.DeserializeObject(text, dtoArrayType);
        }
    }
}
