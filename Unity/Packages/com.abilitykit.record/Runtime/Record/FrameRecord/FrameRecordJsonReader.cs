using System.IO;
using Newtonsoft.Json;

namespace AbilityKit.Core.Recording.FrameRecord
{
    public static class FrameRecordJsonReader
    {
        public static FrameRecordFile Load(string path)
        {
            var resolved = RecordPathResolver.Resolve(path);
            var json = File.ReadAllText(resolved);
            return JsonConvert.DeserializeObject<FrameRecordFile>(json);
        }

    }
}
