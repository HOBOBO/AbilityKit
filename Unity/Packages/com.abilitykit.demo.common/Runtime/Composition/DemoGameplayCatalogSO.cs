#nullable enable

using System;
using System.Collections.Generic;
using AbilityKit.Demo.Common.Gameplay;
using UnityEngine;

namespace AbilityKit.Demo.Common.Composition
{
    [CreateAssetMenu(
        fileName = "DemoGameplayCatalog",
        menuName = "AbilityKit/Demo/Gameplay Catalog")]
    public sealed class DemoGameplayCatalogSO : ScriptableObject
    {
        [SerializeField] private List<DemoGameplayProfileSO> profiles = new List<DemoGameplayProfileSO>();

        public IReadOnlyList<DemoGameplayProfileSO> Profiles => profiles;

        public bool TryFind(
            in DemoLaunchRequest request,
            out DemoGameplayProfileSO? profile,
            out string error)
        {
            profile = null;
            for (var i = 0; i < profiles.Count; i++)
            {
                var candidate = profiles[i];
                if (candidate == null || !Matches(candidate, in request))
                {
                    continue;
                }

                if (profile != null)
                {
                    error = string.IsNullOrWhiteSpace(request.ProfileId)
                        ? $"Multiple gameplay profiles match {request.Gameplay}/{request.Mode}."
                        : $"Multiple gameplay profiles use id '{request.ProfileId}'.";
                    profile = null;
                    return false;
                }

                profile = candidate;
            }

            if (profile == null)
            {
                error = string.IsNullOrWhiteSpace(request.ProfileId)
                    ? $"No gameplay profile matches {request.Gameplay}/{request.Mode}."
                    : $"Gameplay profile '{request.ProfileId}' was not found for {request.Gameplay}/{request.Mode}.";
                return false;
            }

            if (!profile.TryValidate(out error))
            {
                profile = null;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool Matches(
            DemoGameplayProfileSO candidate,
            in DemoLaunchRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.ProfileId)
                && !string.Equals(candidate.ProfileId, request.ProfileId, StringComparison.Ordinal))
            {
                return false;
            }

            return candidate.Gameplay == request.Gameplay && candidate.Mode == request.Mode;
        }
    }
}
