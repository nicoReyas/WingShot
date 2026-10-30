using UnityEngine;

public static class WingShotObjects
{
    public static T[] All<T>(bool includeInactive = false) where T : Object
    {
#if UNITY_6000_6_OR_NEWER
        return Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
        return Object.FindObjectsByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif
    }
}
