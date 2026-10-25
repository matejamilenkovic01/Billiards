using UnityEngine;

// The spin picker's rules (v4 V2), apart from the view so the checks can drive them: a press or drag on the big
// cue ball puts the tip there, at most maxOffset ball radii from the centre (the marker stops at that ring);
// a second tap within DoubleTapSeconds near the first one puts it back in the centre.
public sealed class SpinPicker
{
    public const float DoubleTapSeconds = 0.3f;
    public const float DoubleTapPixels = 40f;

    private float _lastTap = -10f;
    private Vector2 _lastTapAt;

    public float MaxOffset = 0.5f;
    public Vector2 Tip { get; private set; }

    // offsetPixels: from the big ball's centre; ballRadiusPixels: its drawn radius (= one ball radius).
    public static Vector2 TipFromOffset(Vector2 offsetPixels, float ballRadiusPixels, float maxOffset) =>
        Vector2.ClampMagnitude(offsetPixels / Mathf.Max(1f, ballRadiusPixels), maxOffset);

    // True when this press was a double tap (back to the centre).
    public bool Press(Vector2 offsetPixels, float ballRadiusPixels, float time)
    {
        if (time - _lastTap <= DoubleTapSeconds && (offsetPixels - _lastTapAt).magnitude <= DoubleTapPixels)
        {
            _lastTap = -10f;
            Tip = Vector2.zero;
            return true;
        }
        _lastTap = time;
        _lastTapAt = offsetPixels;
        Tip = TipFromOffset(offsetPixels, ballRadiusPixels, MaxOffset);
        return false;
    }

    public void Drag(Vector2 offsetPixels, float ballRadiusPixels) => Tip = TipFromOffset(offsetPixels, ballRadiusPixels, MaxOffset);

    public void Reset()
    {
        Tip = Vector2.zero;
        _lastTap = -10f;
    }
}
