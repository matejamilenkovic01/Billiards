using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Renders generated art into PNGs for review without entering Play mode.
public static class ArtPreview
{
    // 4x4 grid of the 16 ball materials. yaw 0 shows the -Z ("back") number circle, 180 the +Z one.
    public static string RenderBallGrid(string outputPath, float yaw = 0f, float pitch = 12f)
    {
        using var scope = new TempObjectScope();
        var root = new GameObject("BallGridPreview");

        const float spacing = 0.07f;
        for (int number = 0; number < 16; number++)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(ball.GetComponent<Collider>());
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * 0.05715f;
            int column = number % 4;
            int row = number / 4;
            ball.transform.localPosition = new Vector3((column - 1.5f) * spacing, (1.5f - row) * spacing, 0f);
            ball.transform.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            ball.GetComponent<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(GeneratedArtBuilder.BallMaterialPath(number));
        }

        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.rotation = Quaternion.Euler(35f, -25f, 0f);
        var light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.3f;

        var cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform, false);
        cameraObject.transform.position = new Vector3(0f, 0f, -0.75f);
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 25f;
        camera.nearClipPlane = 0.05f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.2f, 0.15f);
        cameraObject.AddComponent<UniversalAdditionalCameraData>();

        return Render(camera, outputPath, 768, 768);
    }

    // Renders the open scene from an arbitrary viewpoint with a temporary camera (post-processing on).
    public static string RenderView(Vector3 position, Vector3 lookAt, string outputPath, int width = 540, int height = 960, float fov = 55f)
    {
        var cameraObject = new GameObject("PreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            cameraObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.02f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.03f, 0.028f);
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            return Render(camera, outputPath, width, height);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    public static string Render(Camera camera, string outputPath, int width, int height)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        camera.targetTexture = target;
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;
        camera.targetTexture = null;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        File.WriteAllBytes(outputPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        target.Release();
        Object.DestroyImmediate(target);
        return outputPath;
    }
}
