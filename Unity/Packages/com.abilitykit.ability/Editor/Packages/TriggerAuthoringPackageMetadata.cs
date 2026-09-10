using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    /// <summary>编辑期内容包信息，不参与 Source JSON 和运行时执行语义。</summary>
    [Serializable]
    public sealed class TriggerAuthoringPackageMetadata
    {
        [SerializeField] private string _domainId;
        [SerializeField] private string _contentKey;
        [SerializeField] private string _owner;
        [SerializeField] private List<string> _tags = new List<string>();

        public string DomainId => _domainId ?? string.Empty;
        public string ContentKey => _contentKey ?? string.Empty;
        public string Owner => _owner ?? string.Empty;
        public IReadOnlyList<string> Tags => _tags ?? (_tags = new List<string>());

        internal void SetIdentity(string domainId, string contentKey)
        {
            _domainId = domainId ?? string.Empty;
            _contentKey = contentKey ?? string.Empty;
        }

        internal void SetOwner(string owner)
        {
            _owner = owner?.Trim() ?? string.Empty;
        }

        internal void SetTags(IEnumerable<string> tags)
        {
            _tags = new List<string>();
            if (tags == null) return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var value in tags)
            {
                var tag = value?.Trim();
                if (!string.IsNullOrEmpty(tag) && seen.Add(tag)) _tags.Add(tag);
            }
        }
    }
}
