using UnityEngine;

// Spawns the persistent Systems prefab before any scene loads, so Play works from any scene in the Editor.
public static class Bootstrap
{
    private const string SystemsResourcePath = "Systems";

    private static GameObject _systems;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        Application.targetFrameRate = 60;

        if (_systems != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(SystemsResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"Bootstrap: Resources/{SystemsResourcePath} prefab not found.");
            return;
        }

        _systems = Object.Instantiate(prefab);
        _systems.name = prefab.name;
        Object.DontDestroyOnLoad(_systems);
    }
}
