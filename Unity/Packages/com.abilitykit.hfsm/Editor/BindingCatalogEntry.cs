using System;
using System.Collections.Generic;
using AbilityKit.HFSM.Definition;
using AbilityKit.HFSM.Runtime;
using UnityEngine;

namespace AbilityKit.HFSM.Editor
{

    [Serializable]
    public sealed class BindingCatalogEntry
    {
        [SerializeField] private BindingKind kind;
        [SerializeField] private string key = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string category = string.Empty;
        [SerializeField] private string description = string.Empty;

        public BindingKind Kind { get => kind; set => kind = value; }
        public string Key { get => key; set => key = value ?? string.Empty; }
        public string DisplayName { get => displayName; set => displayName = value ?? string.Empty; }
        public string Category { get => category; set => category = value ?? string.Empty; }
        public string Description { get => description; set => description = value ?? string.Empty; }
    }
}
