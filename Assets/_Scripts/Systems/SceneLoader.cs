using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

// Two half-screen panels close over the old scene, the new scene loads behind them, then they open.
public class SceneLoader : SingletonPersistent<SceneLoader>
{
    private const float CloseDuration = 0.35f;
    private const float OpenDuration = 0.35f;

    [SerializeField] private CanvasGroup _blocker;
    [SerializeField] private RectTransform _topPanel;
    [SerializeField] private RectTransform _bottomPanel;

    private bool _isLoading;
    private float _openAmount;

    public bool IsLoading => _isLoading;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        SetPanelsOpen(1f);
        _blocker.blocksRaycasts = false;
    }

    public void Load(string sceneName)
    {
        if (_isLoading)
            return;

        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        _isLoading = true;
        _blocker.blocksRaycasts = true;

        if (TooltipManager.Instance != null)
            TooltipManager.Instance.HideImmediate();
        if (PopupManager.Instance != null)
            PopupManager.Instance.HideAll();

        AudioManager.Instance.Play(SoundId.Whoosh);
        yield return TweenPanels(0f, CloseDuration, Ease.InCubic).WaitForCompletion();

        if (PopupManager.Instance != null)
            PopupManager.Instance.HideAllImmediate();

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (!operation.isDone)
            yield return null;

        // One frame for the new scene's Start methods before revealing it.
        yield return null;
        Time.timeScale = 1f;

        yield return TweenPanels(1f, OpenDuration, Ease.OutCubic).WaitForCompletion();
        _blocker.blocksRaycasts = false;
        _isLoading = false;
    }

    private Tween TweenPanels(float openAmount, float duration, Ease ease) =>
        DOTween.To(() => _openAmount, SetPanelsOpen, openAmount, duration)
            .SetEase(ease)
            .SetUpdate(true)
            .SetLink(gameObject);

    // 0 = closed (panels meet in the middle), 1 = fully off screen. Driven through anchors so it is
    // independent of the screen size and needs no layout pass.
    private void SetPanelsOpen(float openAmount)
    {
        _openAmount = openAmount;
        // Slightly past 0.5 so the seam shadow hanging off each panel also leaves the screen.
        float shift = openAmount * 0.54f;
        _topPanel.anchorMin = new Vector2(0f, 0.5f + shift);
        _topPanel.anchorMax = new Vector2(1f, 1f + shift);
        _bottomPanel.anchorMin = new Vector2(0f, -shift);
        _bottomPanel.anchorMax = new Vector2(1f, 0.5f - shift);
    }
}
