using UnityEditor;
using UnityEngine;

namespace NataneToon.Editor
{
    /// <summary>Unity のバージョン差を吸収するエディター API のラッパー (2019.4 〜 Unity 6.5)。</summary>
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
        /// Object.GetInstanceID() の代わり (Unity 6.5 ではコンパイルできない)。辞書や SessionState のキー用。
        /// 本物の instance id を取る API には渡せない。
        /// </summary>
        public static int GetObjectId(Object obj)
        {
            return obj != null ? obj.GetHashCode() : 0;
        }

        /// <summary>参照が保存されていれば true (参照先が Missing でも)。Unity 6.5 では objectReferenceInstanceIDValue が使えない。</summary>
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
