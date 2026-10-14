using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Sets the Game view to a fixed portrait resolution (reflection: Unity has no public API for it), so
// screen captures show the UI exactly as on a phone. Uses an existing Game view; never opens or focuses one.
public static class GameViewUtil
{
    public static string SetPortrait(int width = 1080, int height = 1920)
    {
        Assembly editor = typeof(Editor).Assembly;
        Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
        Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = singleton.GetProperty("instance").GetValue(null);
        object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        Type groupClass = group.GetType();

        string label = $"Portrait {width}x{height}";
        int index = -1;
        var texts = (string[])groupClass.GetMethod("GetDisplayTexts").Invoke(group, null);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].StartsWith(label))
                index = i;
        }

        if (index < 0)
        {
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            Type sizeKind = editor.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, Enum.Parse(sizeKind, "FixedResolution"), width, height, label);
            groupClass.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = (int)groupClass.GetMethod("GetTotalCount").Invoke(group, null) - 1;
        }

        Type gameViewType = editor.GetType("UnityEditor.GameView");
        UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameViewType);
        if (views.Length == 0)
            return "no Game view open";
        foreach (UnityEngine.Object view in views)
        {
            PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            selected.SetValue(view, index);
            gameViewType.GetMethod("Repaint", BindingFlags.Instance | BindingFlags.Public).Invoke(view, null);
        }
        return $"game view set to {label} (index {index})";
    }
}
