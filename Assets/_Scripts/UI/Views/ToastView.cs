using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Short messages, one at a time, the rest queued: release quality, fouls, hints, purchases.
public class ToastView : MonoBehaviour
{
    private const int MaxQueue = 4;

    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private Image _background;
    [SerializeField] private TMP_Text _text;
    [SerializeField] private float _holdSeconds = 1.3f;

    private readonly Queue<ToastMessage> _queue = new();
    private bool _showing;

    private static readonly Color InfoColor = new(0.1f, 0.12f, 0.14f, 0.85f);
    private static readonly Color GoodColor = new(0.13f, 0.55f, 0.27f, 0.92f);
    private static readonly Color WarningColor = new(0.85f, 0.6f, 0.08f, 0.92f);
    private static readonly Color BadColor = new(0.75f, 0.17f, 0.15f, 0.92f);
    private static readonly Color PerfectColor = new(0.2f, 0.75f, 0.32f, 0.95f);

    private void Awake()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }

    private void OnEnable()
    {
        GameEvents.OnToastRequested += Enqueue;
        GameEvents.OnShotFired += HandleShotFired;
        GameEvents.OnPurchaseFailed += HandlePurchaseFailed;
        GameEvents.OnFoul += HandleFoul;
    }

    private void OnDisable()
    {
        GameEvents.OnToastRequested -= Enqueue;
        GameEvents.OnShotFired -= HandleShotFired;
        GameEvents.OnPurchaseFailed -= HandlePurchaseFailed;
        GameEvents.OnFoul -= HandleFoul;
        _queue.Clear();
        _showing = false;
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        // The bot's release quality is not shown, only heard.
        if (!feedback.IsPlayer)
            return;
        Enqueue(feedback.Zone switch
        {
            ReleaseZone.Green => new ToastMessage("PERFECT!", ToastStyle.Perfect),
            ReleaseZone.Yellow => new ToastMessage("GOOD", ToastStyle.Warning),
            _ => new ToastMessage("MISS", ToastStyle.Bad)
        });
    }

    private void HandlePurchaseFailed(string reason) => Enqueue(new ToastMessage(reason, ToastStyle.Bad));

    private void HandleFoul(FoulType foul)
    {
        string text = foul switch
        {
            FoulType.CueBallPocketed => "FOUL! Cue ball scratched",
            FoulType.WrongBallFirst => "FOUL! Wrong ball first",
            FoulType.NoBallHit => "FOUL! No ball hit",
            FoulType.NoRailAfterContact => "FOUL! No rail after contact",
            _ => null
        };
        if (text != null)
            Enqueue(new ToastMessage(text, ToastStyle.Bad));
    }

    private void Enqueue(ToastMessage message)
    {
        if (_queue.Count >= MaxQueue)
            _queue.Dequeue();
        _queue.Enqueue(message);
        if (!_showing)
            ShowNext();
    }

    private void ShowNext()
    {
        if (_queue.Count == 0)
        {
            _showing = false;
            return;
        }

        _showing = true;
        ToastMessage message = _queue.Dequeue();
        _text.text = message.Text;
        _background.color = message.Style switch
        {
            ToastStyle.Good => GoodColor,
            ToastStyle.Warning => WarningColor,
            ToastStyle.Bad => BadColor,
            ToastStyle.Perfect => PerfectColor,
            _ => InfoColor
        };

        _panel.DOKill();
        _group.DOKill();
        _panel.localScale = Vector3.one * 0.7f;
        bool big = message.Style == ToastStyle.Perfect;
        DOTween.Sequence()
            .Append(_group.DOFade(1f, 0.15f))
            .Join(_panel.DOScale(big ? 1.15f : 1f, 0.25f).SetEase(Ease.OutBack))
            .AppendInterval(_queue.Count > 0 ? _holdSeconds * 0.6f : _holdSeconds)
            .Append(_group.DOFade(0f, 0.2f))
            .OnComplete(ShowNext)
            .SetUpdate(true)
            .SetLink(gameObject);
    }
}
