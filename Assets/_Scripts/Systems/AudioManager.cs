using System;
using DG.Tweening;
using UnityEngine;

public class AudioManager : SingletonPersistent<AudioManager>
{
    private const float DefaultSfxVolume = 0.8f;
    private const float DefaultMusicVolume = 0.5f;
    private const float PausedMusicFactor = 0.5f;
    private const float JingleMusicFactor = 0.3f;
    private const float JingleDuckSeconds = 3.5f;
    private const float ImpactFullVolumeSpeed = 3f;
    private const float ImpactThrottleSeconds = 0.025f;
    private const float MinImpactVolume = 0.04f;

    [SerializeField] private SoundData[] _sounds;
    [SerializeField] private AudioClip _musicClip;
    [Tooltip("Round-robin one-shot sources, so per-sound pitch changes don't affect sounds still playing.")]
    [SerializeField] private AudioSource[] _sfxSources;
    [SerializeField] private AudioSource _musicSource;

    private float _sfxVolume;
    private float _musicVolume;
    private bool _isPaused;
    private float _duckFactor = 1f;
    private int _nextSource;
    private float _lastBallClickTime = -1f;
    private float _lastCushionTime = -1f;

    public float SfxVolume => _sfxVolume;
    public float MusicVolume => _musicVolume;
    public bool IsMusicPlaying => _musicSource != null && _musicSource.isPlaying;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        _sfxVolume = PlayerPrefs.GetFloat(SaveKeys.SfxVolume, DefaultSfxVolume);
        _musicVolume = PlayerPrefs.GetFloat(SaveKeys.MusicVolume, DefaultMusicVolume);
        ApplyVolumes();

        _musicSource.clip = _musicClip;
        _musicSource.loop = true;
        _musicSource.Play();
    }

    private void OnEnable()
    {
        GameEvents.OnSfxVolumeChanged += HandleSfxVolumeChanged;
        GameEvents.OnMusicVolumeChanged += HandleMusicVolumeChanged;
        GameEvents.OnButtonClicked += HandleButtonClicked;
        GameEvents.OnDisabledButtonClicked += HandleDisabledButtonClicked;
        GameEvents.OnPopupShown += HandlePopupShown;
        GameEvents.OnToastRequested += HandleToastRequested;
        GameEvents.OnCoinTick += HandleCoinTick;
        GameEvents.OnPurchaseFailed += HandlePurchaseFailed;
        GameEvents.OnProfileChanged += HandleProfileChanged;
        GameEvents.OnPauseChanged += HandlePauseChanged;
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnShotFired += HandleShotFired;
        GameEvents.OnBallCollision += HandleBallCollision;
        GameEvents.OnBallPocketed += HandleBallPocketed;
        GameEvents.OnFoul += HandleFoul;
        GameEvents.OnCueBallPlaced += HandleCueBallPlaced;
        GameEvents.OnMatchEnded += HandleMatchEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnSfxVolumeChanged -= HandleSfxVolumeChanged;
        GameEvents.OnMusicVolumeChanged -= HandleMusicVolumeChanged;
        GameEvents.OnButtonClicked -= HandleButtonClicked;
        GameEvents.OnDisabledButtonClicked -= HandleDisabledButtonClicked;
        GameEvents.OnPopupShown -= HandlePopupShown;
        GameEvents.OnToastRequested -= HandleToastRequested;
        GameEvents.OnCoinTick -= HandleCoinTick;
        GameEvents.OnPurchaseFailed -= HandlePurchaseFailed;
        GameEvents.OnProfileChanged -= HandleProfileChanged;
        GameEvents.OnPauseChanged -= HandlePauseChanged;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnBallCollision -= HandleBallCollision;
        GameEvents.OnBallPocketed -= HandleBallPocketed;
        GameEvents.OnFoul -= HandleFoul;
        GameEvents.OnCueBallPlaced -= HandleCueBallPlaced;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveVolumes();
    }

    private void OnApplicationQuit() => SaveVolumes();

    public void Play(SoundId id, float volumeScale = 1f, float pitch = 1f)
    {
        SoundData sound = Array.Find(_sounds, s => s.Id == id);
        if (sound == null || sound.Clip == null)
        {
            Debug.LogWarning($"AudioManager: no clip for {id}");
            return;
        }

        AudioSource source = _sfxSources[_nextSource];
        _nextSource = (_nextSource + 1) % _sfxSources.Length;
        source.pitch = pitch * sound.Pitch;
        source.PlayOneShot(sound.Clip, sound.Volume * volumeScale);
    }

    public void PlayVaried(SoundId id, float volumeScale = 1f, float minPitch = 0.95f, float maxPitch = 1.05f) =>
        Play(id, volumeScale, UnityEngine.Random.Range(minPitch, maxPitch));

    public void SaveVolumes()
    {
        PlayerPrefs.SetFloat(SaveKeys.SfxVolume, _sfxVolume);
        PlayerPrefs.SetFloat(SaveKeys.MusicVolume, _musicVolume);
        PlayerPrefs.Save();
    }

    private void ApplyVolumes()
    {
        foreach (AudioSource source in _sfxSources)
            source.volume = _sfxVolume;
        _musicSource.volume = _musicVolume * (_isPaused ? PausedMusicFactor : 1f) * _duckFactor;
    }

    // Win/lose jingles sit on top of the music, so the music steps back for a moment.
    private void DuckMusic()
    {
        DOTween.Kill(this);
        DOTween.Sequence()
            .Append(DOTween.To(() => _duckFactor, SetDuck, JingleMusicFactor, 0.25f))
            .AppendInterval(JingleDuckSeconds)
            .Append(DOTween.To(() => _duckFactor, SetDuck, 1f, 1.2f))
            .SetUpdate(true)
            .SetTarget(this)
            .SetLink(gameObject);
    }

    private void SetDuck(float value)
    {
        _duckFactor = value;
        ApplyVolumes();
    }

    private void HandleSfxVolumeChanged(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SaveKeys.SfxVolume, _sfxVolume);
        ApplyVolumes();
    }

    private void HandleMusicVolumeChanged(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SaveKeys.MusicVolume, _musicVolume);
        ApplyVolumes();
    }

    private void HandlePauseChanged(bool paused)
    {
        _isPaused = paused;
        ApplyVolumes();
    }

    private void HandleButtonClicked() => PlayVaried(SoundId.Click);

    private void HandleDisabledButtonClicked() => Play(SoundId.Error, 0.45f);

    private void HandlePopupShown() => Play(SoundId.PopupOpen);

    private void HandleToastRequested(ToastMessage message)
    {
        if (message.Style is ToastStyle.Info or ToastStyle.Good or ToastStyle.Warning)
            Play(SoundId.Toast, 0.6f);
    }

    private float _lastCoinTick = -1f;

    // Count-ups tick fast; keep it to a pleasant rattle.
    private void HandleCoinTick()
    {
        if (Time.unscaledTime - _lastCoinTick < 0.05f)
            return;
        _lastCoinTick = Time.unscaledTime;
        PlayVaried(SoundId.CoinTick, 0.6f, 0.97f, 1.1f);
    }

    private void HandlePurchaseFailed(string reason) => Play(SoundId.Error, 0.7f);

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        if (!string.IsNullOrEmpty(snapshot.JustPurchasedId))
            Play(SoundId.Purchase);
    }

    private void HandleTurnStarted(TurnInfo info) => Play(SoundId.TurnStart, 0.7f);

    private void HandleShotFired(ShotFeedback feedback)
    {
        Play(SoundId.CueHit, Mathf.Lerp(0.35f, 1f, feedback.Power01), UnityEngine.Random.Range(0.97f, 1.03f));
        if (feedback.IsPlayer && feedback.Zone == ReleaseZone.Green)
            Play(SoundId.Perfect, 0.8f);
    }

    // A break produces dozens of contacts in a few frames; the throttle keeps it a rattle, not a roar.
    private void HandleBallCollision(float impactSpeed, CollisionKind kind)
    {
        float volume = Mathf.Clamp01(impactSpeed / ImpactFullVolumeSpeed);
        if (volume < MinImpactVolume)
            return;

        float now = Time.unscaledTime;
        if (kind == CollisionKind.Ball)
        {
            if (now - _lastBallClickTime < ImpactThrottleSeconds)
                return;
            _lastBallClickTime = now;
            PlayVaried(SoundId.BallClick, volume, 0.95f, 1.08f);
        }
        else
        {
            if (now - _lastCushionTime < ImpactThrottleSeconds)
                return;
            _lastCushionTime = now;
            PlayVaried(SoundId.Cushion, volume, 0.95f, 1.08f);
        }
    }

    private void HandleBallPocketed(int number, int pocketIndex) => PlayVaried(SoundId.Pocket, 1f, 0.92f, 1.0f);

    private void HandleFoul(FoulInfo foul) => Play(SoundId.Foul);

    // A quiet tap as the cue ball is set down by hand.
    private void HandleCueBallPlaced() => PlayVaried(SoundId.BallClick, 0.25f, 0.85f, 0.9f);

    private void HandleMatchEnded(MatchResult result)
    {
        DuckMusic();
        Play(result.Won ? SoundId.Win : SoundId.Lose);
    }
}

[Serializable]
public class SoundData
{
    public SoundId Id;
    public AudioClip Clip;
    [Range(0f, 1f)] public float Volume = 1f;
    [Range(0.5f, 1.5f)] public float Pitch = 1f;
}

public enum SoundId
{
    Click = 0,
    PopupOpen = 1,
    Whoosh = 2,
    Toast = 3,
    Error = 4,
    CueHit = 5,
    BallClick = 6,
    Cushion = 7,
    Pocket = 8,
    Foul = 9,
    Perfect = 10,
    CoinTick = 11,
    Purchase = 12,
    Win = 13,
    Lose = 14,
    TurnStart = 15
}
