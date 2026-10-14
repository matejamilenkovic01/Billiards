using System;
using System.Collections.Generic;
using UnityEngine;

public class PopupManager : SingletonPersistent<PopupManager>
{
    [SerializeField] private RectTransform _container;
    [SerializeField] private PopupBase[] _popupPrefabs;

    private readonly Dictionary<Type, PopupBase> _instances = new();
    // Most recently shown last; entries that were hidden are pruned on access.
    private readonly List<PopupBase> _stack = new();

    public bool IsAnyOpen => Top != null;

    private PopupBase Top
    {
        get
        {
            _stack.RemoveAll(p => p == null || !p.IsShown);
            return _stack.Count > 0 ? _stack[^1] : null;
        }
    }

    public T Show<T>(object payload = null) where T : PopupBase
    {
        T popup = GetOrCreate<T>();
        if (popup == null)
            return null;

        _stack.Remove(popup);
        _stack.Add(popup);
        popup.Show(payload);
        return popup;
    }

    public void Hide<T>() where T : PopupBase
    {
        if (_instances.TryGetValue(typeof(T), out PopupBase popup))
            popup.Hide();
    }

    // Returns true when a popup consumed the request. From Back, a popup that ignores Back still consumes it.
    public bool HideTop(bool fromBack = false)
    {
        PopupBase top = Top;
        if (top == null)
            return false;

        if (!fromBack)
            top.Hide();
        else if (top.CloseOnBack)
            top.HandleBack();

        return true;
    }

    public void HideAll()
    {
        foreach (PopupBase popup in _instances.Values)
            popup.Hide();
        _stack.Clear();
    }

    public void HideAllImmediate()
    {
        foreach (PopupBase popup in _instances.Values)
            popup.HideImmediate();
        _stack.Clear();
    }

    private T GetOrCreate<T>() where T : PopupBase
    {
        if (_instances.TryGetValue(typeof(T), out PopupBase existing))
            return (T)existing;

        T prefab = Array.Find(_popupPrefabs, p => p is T) as T;
        if (prefab == null)
        {
            Debug.LogError($"PopupManager: no prefab registered for {typeof(T).Name}");
            return null;
        }

        T instance = Instantiate(prefab, _container);
        instance.gameObject.SetActive(false);
        _instances[typeof(T)] = instance;
        return instance;
    }
}
