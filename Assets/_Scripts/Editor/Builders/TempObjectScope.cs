using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Prefab builders assemble objects in the open scene before saving them as assets.
// If a builder throws halfway, this removes the leftovers so they never end up in a scene.
public sealed class TempObjectScope : IDisposable
{
    private readonly Scene _scene;
    private readonly HashSet<GameObject> _existingRoots;

    public TempObjectScope()
    {
        _scene = SceneManager.GetActiveScene();
        _existingRoots = new HashSet<GameObject>(_scene.GetRootGameObjects());
    }

    public void Dispose()
    {
        foreach (GameObject root in _scene.GetRootGameObjects())
        {
            if (!_existingRoots.Contains(root))
                UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
