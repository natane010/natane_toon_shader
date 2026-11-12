using UnityEditor;
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

        /// <summary>
        /// Session-stable identity key for a UnityEngine.Object (dictionary / HashSet keys,
        /// SessionState key suffixes). Replaces Object.GetInstanceID(), which no longer
        /// compiles on Unity 6.5 (it was replaced by EntityId). Object.GetHashCode() is the instance id on
        /// every older editor and the EntityId hash on newer ones, so it needs no #if.
        /// Not valid as an argument to APIs that take a real instance id.
        /// </summary>
        public static int GetObjectId(Object obj)
        {
            return obj != null ? obj.GetHashCode() : 0;
        }

        /// <summary>
        /// True when an object-reference property stores a reference (even one whose target
        /// is missing). objectReferenceInstanceIDValue no longer compiles on Unity 6.5.
        /// </summary>
        public static bool HasStoredObjectReference(SerializedProperty property)
        {
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return false;
#if UNITY_6000_5_OR_NEWER
            return !property.objectReferenceEntityIdValue.Equals(EntityId.None);
#else
            return property.objectReferenceInstanceIDValue != 0;
#endif
        }
    }
}
