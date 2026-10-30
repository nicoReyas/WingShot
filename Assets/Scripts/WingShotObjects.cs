using UnityEngine;

public static class WingShotObjects
{
    // Find scene objects using the API available in this Unity version.
    public static T[] All<T>(bool includeInactive = false) where T : Object
    {
        // Newer Unity versions removed the need to choose an object sorting order.
#if UNITY_6000_6_OR_NEWER
        return Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
        return Object.FindObjectsByType<T>(
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif
    }
}
