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
    [Tooltip("Loops: balls rolling on the cloth, and the room's ambience during a match (v3 V7).")]
    [SerializeField] private AudioSource _rollSource;
    [SerializeField] private AudioSource _ambienceSource;

    private const float BreakClickSilence = 0.15f;
    private const float RollFullSpeed = 4f;
    private const float RollMaxVolume = 0.5f;
    private const float RollFadeSeconds = 0.2f;
    private const float AmbienceVolume = 0.35f;
    private const float RattleDelay = 0.1f;
    private const int MaxComboSemitones = 4;

    private float _sfxVolume;
    private float _musicVolume;
    private bool _isPaused;
    private float _duckFactor = 1f;
    private int _nextSource;
    private float _lastBallClickTime = -1f;
    private float _lastCushionTime = -1f;
    private float _clicksSilentUntil = -1f;
    private int _pocketsThisShot;
    private float _rollTarget;

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
        GameEvents.OnRankCelebration += HandleRankCelebration;
        GameEvents.OnPurchaseFailed += HandlePurchaseFailed;
        GameEvents.OnProfileChanged += HandleProfileChanged;
        GameEvents.OnPauseChanged += HandlePauseChanged;
        GameEvents.OnTurnStarted += HandleTurnStarted;
        GameEvents.OnShotFired += HandleShotFired;
        GameEvents.OnBallCollision += HandleBallCollision;
        GameEvents.OnBallPocketedFx += HandleBallPocketedFx;
        GameEvents.OnRollingSpeed += HandleRollingSpeed;
        GameEvents.OnFoul += HandleFoul;
        GameEvents.OnCueBallPlaced += HandleCueBallPlaced;
        GameEvents.OnMatchStarted += HandleMatchStarted;
        GameEvents.OnMenuRequested += HandleMenuRequested;
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
        GameEvents.OnRankCelebration -= HandleRankCelebration;
        GameEvents.OnPurchaseFailed -= HandlePurchaseFailed;
        GameEvents.OnProfileChanged -= HandleProfileChanged;
        GameEvents.OnPauseChanged -= HandlePauseChanged;
        GameEvents.OnTurnStarted -= HandleTurnStarted;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnBallCollision -= HandleBallCollision;
        GameEvents.OnBallPocketedFx -= HandleBallPocketedFx;
        GameEvents.OnRollingSpeed -= HandleRollingSpeed;
        GameEvents.OnFoul -= HandleFoul;
        GameEvents.OnCueBallPlaced -= HandleCueBallPlaced;
        GameEvents.OnMatchStarted -= HandleMatchStarted;
        GameEvents.OnMenuRequested -= HandleMenuRequested;
        GameEvents.OnMatchEnded -= HandleMatchEnded;
    }

    // The roll loop follows the balls' total speed and fades out when they stop.
    private void Update()
    {
        if (_rollSource == null)
            return;
        float target = _rollTarget * _sfxVolume;
        _rollSource.volume = Mathf.MoveTowards(_rollSource.volume, target, Time.unscaledDeltaTime * RollMaxVolume / RollFadeSeconds);
        if (_rollSource.volume <= 0.001f && target <= 0f && _rollSource.isPlaying)
            _rollSource.Stop();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            SaveVolumes();
    }

    private void OnApplicationQuit() => SaveVolumes();

    public void Play(SoundId id, float volumeScale = 1f, float pitch = 1f)
    {
        SoundData sound = Find(id);
        AudioClip clip = sound?.Next();
        if (clip == null)
        {
            Debug.LogWarning($"AudioManager: no clip for {id}");
            return;
        }

        AudioSource source = _sfxSources[_nextSource];
        _nextSource = (_nextSource + 1) % _sfxSources.Length;
        source.pitch = pitch * sound.Pitch;
        source.PlayOneShot(clip, sound.Volume * volumeScale);
    }

    private SoundData Find(SoundId id) => Array.Find(_sounds, s => s.Id == id);

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
        if (_ambienceSource != null)
            _ambienceSource.volume = AmbienceVolume * _sfxVolume * (Find(SoundId.RoomAmbience)?.Volume ?? 1f);
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

    // Results screen (v3 V12): a chime per star, a thud for a lost one, a fanfare for a stage or a checkpoint.
    private void HandleRankCelebration(RankCelebration kind)
    {
        switch (kind)
        {
            case RankCelebration.StarGained: Play(SoundId.ComboChime, 0.7f, 1.1f); break;
            case RankCelebration.StarLost: Play(SoundId.Error, 0.6f, 0.8f); break;
            case RankCelebration.StageUp: Play(SoundId.Purchase); break;
            case RankCelebration.Checkpoint: Play(SoundId.Win); break;
            case RankCelebration.ObjectiveDone: Play(SoundId.Purchase, 0.7f); break;
        }
    }

    private void HandleProfileChanged(ProfileSnapshot snapshot)
    {
        if (!string.IsNullOrEmpty(snapshot.JustPurchasedId))
            Play(SoundId.Purchase);
    }

    private void HandleTurnStarted(TurnInfo info) => Play(SoundId.TurnStart, 0.7f);

    // The tip on the ball: soft or hard by power, a crash on the break (and the first clicks folded into it).
    private void HandleShotFired(ShotFeedback feedback)
    {
        _pocketsThisShot = 0;
        if (feedback.IsBreak)
        {
            PlayVaried(SoundId.Break, 1f, 0.96f, 1.04f);
            _clicksSilentUntil = Time.unscaledTime + 0.06f + BreakClickSilence;
        }
        else
        {
            PlayVaried(feedback.Power01 < 0.5f ? SoundId.CueHitSoft : SoundId.CueHitHard, Mathf.Lerp(0.35f, 1f, feedback.Power01), 0.97f, 1.03f);
        }
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
            if (now - _lastBallClickTime < ImpactThrottleSeconds || now < _clicksSilentUntil)
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

    // A thunk by entry speed, a short rattle after it; each further ball in the same shot is a semitone higher
    // (up to +4), with a soft chime on top so a run feels like one.
    private void HandleBallPocketedFx(int number, Vector2 screenPosition, PocketFxKind kind, float speed)
    {
        _pocketsThisShot++;
        float pitch = Mathf.Pow(2f, Mathf.Min(_pocketsThisShot - 1, MaxComboSemitones) / 12f);
        float volume = Mathf.Clamp(speed / 2f, 0.35f, 1f);
        Play(SoundId.PocketDrop, volume, pitch * UnityEngine.Random.Range(0.97f, 1.02f));
        DOVirtual.DelayedCall(RattleDelay, () => Play(SoundId.PocketRattle, volume * 0.5f, pitch), true).SetLink(gameObject);
        if (_pocketsThisShot >= 2)
            Play(SoundId.ComboChime, 0.35f, pitch);
    }

    private void HandleRollingSpeed(float totalSpeed)
    {
        float amount = Mathf.Clamp01(totalSpeed / RollFullSpeed);
        _rollTarget = amount * RollMaxVolume * (Find(SoundId.BallRollLoop)?.Volume ?? 1f);
        if (_rollSource == null)
            return;
        _rollSource.pitch = Mathf.Lerp(0.9f, 1.1f, amount);
        if (_rollTarget > 0f && !_rollSource.isPlaying)
            StartLoop(_rollSource, SoundId.BallRollLoop, 0f);
    }

    private void HandleMatchStarted(MatchStartInfo info)
    {
        if (_ambienceSource != null && !_ambienceSource.isPlaying)
            StartLoop(_ambienceSource, SoundId.RoomAmbience, AmbienceVolume * _sfxVolume);
    }

    private void HandleMenuRequested()
    {
        if (_ambienceSource != null)
            _ambienceSource.Stop();
    }

    private void StartLoop(AudioSource source, SoundId id, float volume)
    {
        AudioClip clip = Find(id)?.Next();
        if (clip == null)
            return;
        source.clip = clip;
        source.loop = true;
        source.volume = volume;
        source.Play();
    }

    private void HandleFoul(FoulInfo foul) => Play(SoundId.Foul);

    // A quiet tap as the cue ball is set down by hand.
    private void HandleCueBallPlaced() => PlayVaried(SoundId.BallClick, 0.25f, 0.85f, 0.9f);

    private void HandleMatchEnded(MatchResult result)
    {
        DuckMusic();
        Play(result.Won ? SoundId.Win : SoundId.Lose);
    }
}

// One sound and its variants (v3 V7): each play picks one at random, never the same one twice in a row.
[Serializable]
public class SoundData
{
    public SoundId Id;
    public AudioClip[] Clips;
    [Range(0f, 1f)] public float Volume = 1f;
    [Range(0.5f, 1.5f)] public float Pitch = 1f;

    [NonSerialized] private int _last = -1;

    public AudioClip Next()
    {
        if (Clips == null || Clips.Length == 0)
            return null;
        int index = UnityEngine.Random.Range(0, Clips.Length);
        if (Clips.Length > 1 && index == _last)
            index = (index + 1 + UnityEngine.Random.Range(0, Clips.Length - 1)) % Clips.Length;
        _last = index;
        return Clips[index];
    }
}

public enum SoundId
{
    Click = 0,
    PopupOpen = 1,
    Whoosh = 2,
    Toast = 3,
    Error = 4,
    CueHitSoft = 5,
    BallClick = 6,
    Cushion = 7,
    PocketDrop = 8,
    Foul = 9,
    Perfect = 10,
    CoinTick = 11,
    Purchase = 12,
    Win = 13,
    Lose = 14,
    TurnStart = 15,
    CueHitHard = 16,
    Break = 17,
    PocketRattle = 18,
    BallRollLoop = 19,
    RoomAmbience = 20,
    ComboChime = 21
}
