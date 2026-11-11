using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>
    /// Version-bridging wrappers for editor APIs that were deprecated in
    /// Unity 2022.2+ (CS0618 on Unity 6) while keeping 2019.4 compatibility.
    /// </summary>
    public static class NataneEditorCompat
    {
        public static T[] FindObjectsOfTypeCompat<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        public static T[] FindObjectsOfTypeCompat<T>(bool includeInactive) where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>(includeInactive);
#endif
        }

        public static T FindObjectOfTypeCompat<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindFirstObjectByType<T>();
#else
            return Object.FindObjectOfType<T>();
#endif
        }
    }
}
