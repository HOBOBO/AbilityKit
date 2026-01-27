using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;

namespace EditorAgent
{
    [HideMonoScript]
    public class EditorMono : SerializedMonoBehaviour
    {
        [HideLabel, HideReferenceObjectPicker, NonSerialized, OdinSerialize]
        public object target;
    }
}