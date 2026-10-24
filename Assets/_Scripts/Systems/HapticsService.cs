using UnityEngine;

public class HapticsService : SingletonPersistent<HapticsService>
{
    private const long LightDurationMs = 20;
    private const int LightAmplitude = 60;
    private const long MediumDurationMs = 30;
    private const int MediumAmplitude = 120;
    private const long StrongDurationMs = 45;
    private const int StrongAmplitude = 180;
    private const float StrongImpactSpeed = 1.5f;
    private const float ImpactThrottleSeconds = 0.08f;
    // Fine aim ticks: the faintest buzz, at most one per throttle window.
    private const long TickDurationMs = 8;
    private const int TickAmplitude = 30;
    private const float TickThrottleSeconds = 0.04f;

    [SerializeField] private bool _verbose;

    private float _lastImpactTime = -1f;
    private float _lastTickTime = -1f;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject _vibrator;
    private AndroidJavaClass _vibrationEffectClass;
    private bool _hasVibrator;
#endif

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
            _hasVibrator = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"HapticsService: vibrator unavailable ({e.Message})");
            _hasVibrator = false;
        }
#endif
    }

    private void OnEnable()
    {
        GameEvents.OnButtonClicked += HandleButtonClicked;
        GameEvents.OnAimSnapped += HandleAimSnapped;
        GameEvents.OnShotFired += HandleShotFired;
        GameEvents.OnBallCollision += HandleBallCollision;
        GameEvents.OnBallPocketedFx += HandleBallPocketedFx;
        GameEvents.OnFoul += HandleFoul;
        GameEvents.OnMatchEnded += HandleMatchEnded;
        // Touching the power strip: you've got hold of the cue.
        GameEvents.OnPullStarted += Light;
        GameEvents.OnFineAimTick += HandleFineAimTick;
    }

    private void OnDisable()
    {
        GameEvents.OnButtonClicked -= HandleButtonClicked;
        GameEvents.OnAimSnapped -= HandleAimSnapped;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnBallCollision -= HandleBallCollision;
        GameEvents.OnBallPocketedFx -= HandleBallPocketedFx;
        GameEvents.OnFoul -= HandleFoul;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
        GameEvents.OnPullStarted -= Light;
        GameEvents.OnFineAimTick -= HandleFineAimTick;
    }

    private void HandleFineAimTick()
    {
        if (Time.unscaledTime - _lastTickTime < TickThrottleSeconds)
            return;
        _lastTickTime = Time.unscaledTime;
        Vibrate(TickDurationMs, TickAmplitude);
    }

    public void Light() => Vibrate(LightDurationMs, LightAmplitude);

    public void Medium() => Vibrate(MediumDurationMs, MediumAmplitude);

    public void Strong() => Vibrate(StrongDurationMs, StrongAmplitude);

    private void HandleButtonClicked() => Light();

    private void HandleAimSnapped() => Light();

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (!feedback.IsPlayer)
            return;

        // A PERFECT release is a short strong kick (v3 V7); otherwise the buzz follows the power.
        if (feedback.Zone == ReleaseZone.Green)
            Strong();
        else if (feedback.Power01 < 0.4f)
            Light();
        else if (feedback.Power01 < 0.8f)
            Medium();
        else
            Strong();
    }

    // Only hard hits buzz; a break would otherwise be one long vibration.
    private void HandleBallCollision(float impactSpeed, CollisionKind kind)
    {
        if (impactSpeed < StrongImpactSpeed || Time.unscaledTime - _lastImpactTime < ImpactThrottleSeconds)
            return;

        _lastImpactTime = Time.unscaledTime;
        Light();
    }

    // v3 V7: a firm buzz for a ball that counts for you, a light one for the rest.
    private void HandleBallPocketedFx(int number, Vector2 screenPosition, PocketFxKind kind, float speed)
    {
        if (kind == PocketFxKind.Legal)
            Medium();
        else
            Light();
    }

    private void HandleFoul(FoulInfo foul) => Medium();

    private void HandleMatchEnded(MatchResult result) => Strong();

    private void Vibrate(long durationMs, int amplitude)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_hasVibrator)
            return;

        using var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", durationMs, amplitude);
        _vibrator.Call("vibrate", effect);
#else
        if (_verbose)
            Debug.Log($"HapticsService: vibrate {durationMs}ms @ {amplitude}");
#endif
    }

    // Never called. Unity only adds android.permission.VIBRATE to the manifest when Handheld.Vibrate
    // is referenced somewhere in code, and the VibrationEffect path above needs that permission.
    private static void EnsureVibratePermission() => Handheld.Vibrate();
}
