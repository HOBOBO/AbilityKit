using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace AbilityKit.Analyzer.Config
{
    internal static class ConstraintJson
    {
        private static readonly DataContractJsonSerializer ConfigSerializer =
            CreateSerializer(typeof(PackageConstraintsConfig));

        private static readonly DataContractJsonSerializer AssemblyDefinitionSerializer =
            CreateSerializer(typeof(AssemblyDefinition));

        public static PackageConstraintsConfig DeserializeConfig(string json)
        {
            var config = Deserialize<PackageConstraintsConfig>(ConfigSerializer, json);
            config.Normalize();
            return config;
        }

        public static string DeserializeAssemblyName(string json)
        {
            return Deserialize<AssemblyDefinition>(AssemblyDefinitionSerializer, json).Name;
        }

        private static DataContractJsonSerializer CreateSerializer(System.Type type)
        {
            return new DataContractJsonSerializer(
                type,
                new DataContractJsonSerializerSettings
                {
                    UseSimpleDictionaryFormat = true
                });
        }

        private static T Deserialize<T>(DataContractJsonSerializer serializer, string json)
        {
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (T)serializer.ReadObject(stream);
            }
        }

        [DataContract]
        private sealed class AssemblyDefinition
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }
        }
    }
}
