using System.Collections.Generic;
using UnityEngine;

// The only owner of the player profile in PlayerPrefs: coins, wins, cues, energy, pending match.
public class ProfileService : SingletonPersistent<ProfileService>
{
    [SerializeField] private CueCatalog _catalog;

    private bool _abandonedNoticePending;

    public CueCatalog Catalog => _catalog;
    public int Coins => PlayerPrefs.GetInt(SaveKeys.Coins, _catalog.StartingCoins);
    public int Wins => PlayerPrefs.GetInt(SaveKeys.Wins, 0);
    public int Losses => PlayerPrefs.GetInt(SaveKeys.Losses, 0);
    // Shots the player has taken, ever (only counted up to what the first-shots hints need).
    public int ShotsTaken => PlayerPrefs.GetInt(SaveKeys.ShotsTaken, 0);
    private const int ShotsTakenCap = 100;
    public CueDefinition SelectedCue => _catalog.Get(SelectedCueId);

    public string SelectedCueId
    {
        get
        {
            string id = PlayerPrefs.GetString(SaveKeys.SelectedCue, _catalog.Starter.Id);
            CueDefinition cue = _catalog.Get(id);
            return cue != null && IsOwned(cue) ? id : _catalog.Starter.Id;
        }
    }

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        ApplyPendingMatchPenalty();
    }

    private void OnEnable()
    {
        GameEvents.OnCueSelectRequested += HandleCueSelectRequested;
        GameEvents.OnCueBuyRequested += HandleCueBuyRequested;
        GameEvents.OnCueRefillRequested += HandleCueRefillRequested;
        GameEvents.OnShotFired += HandleShotFired;
    }

    private void OnDisable()
    {
        GameEvents.OnCueSelectRequested -= HandleCueSelectRequested;
        GameEvents.OnCueBuyRequested -= HandleCueBuyRequested;
        GameEvents.OnCueRefillRequested -= HandleCueRefillRequested;
        GameEvents.OnShotFired -= HandleShotFired;
    }

    private void HandleShotFired(ShotFeedback feedback)
    {
        if (!feedback.IsPlayer || ShotsTaken >= ShotsTakenCap)
            return;
        PlayerPrefs.SetInt(SaveKeys.ShotsTaken, ShotsTaken + 1);
        PlayerPrefs.Save();
    }

    #region Queries
    public bool IsOwned(CueDefinition cue) =>
        cue.IsStarter || PlayerPrefs.GetInt(SaveKeys.CueOwned(cue.Id), 0) == 1;

    public int GetEnergy(CueDefinition cue) =>
        cue.InfiniteEnergy ? int.MaxValue : PlayerPrefs.GetInt(SaveKeys.CueEnergy(cue.Id), cue.MaxEnergy);

    public CueState GetState(CueDefinition cue)
    {
        if (IsOwned(cue))
            return CueState.Owned;
        return Wins >= cue.WinsToUnlock ? CueState.Unlocked : CueState.Locked;
    }

    public ProfileSnapshot GetSnapshot(string justPurchasedId = null)
    {
        int coins = Coins;
        int wins = Wins;
        string selectedId = SelectedCueId;
        float maxGuide = 0f;
        float maxWindow = 0f;
        foreach (CueDefinition cue in _catalog.Cues)
        {
            maxGuide = Mathf.Max(maxGuide, cue.ObjectGuideDiameters);
            maxWindow = Mathf.Max(maxWindow, ReleaseWindowScore(cue));
        }

        var snapshot = new ProfileSnapshot
        {
            Coins = coins,
            Wins = wins,
            Losses = Losses,
            SelectedCueId = selectedId,
            JustPurchasedId = justPurchasedId
        };

        foreach (CueDefinition cue in _catalog.Cues)
        {
            CueState state = GetState(cue);
            int energy = cue.InfiniteEnergy ? 0 : GetEnergy(cue);
            snapshot.Cues.Add(new CueViewData
            {
                Id = cue.Id,
                Name = cue.Name,
                Sprite = cue.Sprite,
                State = state,
                IsSelected = cue.Id == selectedId,
                Price = cue.Price,
                CoinsShort = Mathf.Max(0, cue.Price - coins),
                WinsToUnlock = cue.WinsToUnlock,
                Wins = wins,
                InfiniteEnergy = cue.InfiniteEnergy,
                Energy = energy,
                MaxEnergy = cue.MaxEnergy,
                RefillPrice = cue.RefillPrice,
                CanRefill = state == CueState.Owned && !cue.InfiniteEnergy && energy < cue.MaxEnergy && coins >= cue.RefillPrice,
                GuideDiameters = cue.ObjectGuideDiameters,
                AimLine01 = maxGuide > 0f ? cue.ObjectGuideDiameters / maxGuide : 0f,
                ReleaseWindow01 = maxWindow > 0f ? ReleaseWindowScore(cue) / maxWindow : 0f
            });
        }

        return snapshot;
    }

    // Wider green/yellow and a slower bar both make the release easier.
    private static float ReleaseWindowScore(CueDefinition cue) =>
        (cue.GreenHalf + cue.YellowWidth * 0.5f) * cue.OscillationPeriod;

    public bool CanStartMatch(out string reason)
    {
        CueDefinition cue = SelectedCue;
        if (cue.InfiniteEnergy || GetEnergy(cue) > 0)
        {
            reason = null;
            return true;
        }

        reason = $"Your {cue.Name} is out of energy.";
        return false;
    }
    #endregion

    #region Match
    public void ConsumeEnergyForMatch()
    {
        CueDefinition cue = SelectedCue;
        if (!cue.InfiniteEnergy)
            PlayerPrefs.SetInt(SaveKeys.CueEnergy(cue.Id), Mathf.Max(0, GetEnergy(cue) - 1));
        PlayerPrefs.Save();
        RaiseChanged();
    }

    // Set while a match is running: killing the app mid-match counts as a loss on the next boot.
    public void SetPendingMatch(bool pending)
    {
        PlayerPrefs.SetInt(SaveKeys.PendingMatch, pending ? 1 : 0);
        PlayerPrefs.Save();
    }

    public MatchReward ApplyMatchResult(bool won)
    {
        int oldWins = Wins;
        int oldCoins = Coins;
        int newCoins = won ? oldCoins + _catalog.WinReward : Mathf.Max(0, oldCoins - _catalog.LossPenalty);
        int newWins = won ? oldWins + 1 : oldWins;

        PlayerPrefs.SetInt(SaveKeys.Coins, newCoins);
        if (won)
            PlayerPrefs.SetInt(SaveKeys.Wins, newWins);
        else
            PlayerPrefs.SetInt(SaveKeys.Losses, Losses + 1);
        PlayerPrefs.SetInt(SaveKeys.PendingMatch, 0);
        PlayerPrefs.Save();

        var reward = new MatchReward { Won = won, CoinDelta = newCoins - oldCoins, NewBalance = newCoins };
        foreach (CueDefinition cue in _catalog.Cues)
        {
            if (!cue.IsStarter && cue.WinsToUnlock > oldWins && cue.WinsToUnlock <= newWins)
                reward.NewUnlocks.Add(cue.Name);
        }

        RaiseChanged();
        return reward;
    }

    public bool ApplyPendingMatchPenalty()
    {
        if (PlayerPrefs.GetInt(SaveKeys.PendingMatch, 0) != 1)
            return false;

        ApplyMatchResult(false);
        _abandonedNoticePending = true;
        Debug.Log("ProfileService: last match was abandoned, applied a loss");
        return true;
    }

    // MenuController shows the "abandoned" toast once.
    public bool ConsumeAbandonedNotice()
    {
        bool pending = _abandonedNoticePending;
        _abandonedNoticePending = false;
        return pending;
    }
    #endregion

    #region Inventory actions
    public bool TrySelect(string cueId, out string reason)
    {
        CueDefinition cue = _catalog.Get(cueId);
        if (cue == null || !IsOwned(cue))
        {
            reason = "You don't own this cue.";
            return false;
        }

        PlayerPrefs.SetString(SaveKeys.SelectedCue, cue.Id);
        PlayerPrefs.Save();
        reason = null;
        return true;
    }

    public bool TryBuy(string cueId, out string reason)
    {
        CueDefinition cue = _catalog.Get(cueId);
        reason = null;
        if (cue == null)
            reason = "Unknown cue.";
        else if (IsOwned(cue))
            reason = "You already own this cue.";
        else if (GetState(cue) == CueState.Locked)
            reason = $"Win {cue.WinsToUnlock} matches to unlock.";
        else if (Coins < cue.Price)
            reason = $"Need {cue.Price - Coins} more coins.";

        if (reason != null)
            return false;

        PlayerPrefs.SetInt(SaveKeys.Coins, Coins - cue.Price);
        PlayerPrefs.SetInt(SaveKeys.CueOwned(cue.Id), 1);
        PlayerPrefs.SetInt(SaveKeys.CueEnergy(cue.Id), cue.MaxEnergy);
        PlayerPrefs.Save();
        return true;
    }

    public bool TryRefill(string cueId, out string reason)
    {
        CueDefinition cue = _catalog.Get(cueId);
        reason = null;
        if (cue == null || !IsOwned(cue))
            reason = "You don't own this cue.";
        else if (cue.InfiniteEnergy)
            reason = "This cue never runs out of energy.";
        else if (GetEnergy(cue) >= cue.MaxEnergy)
            reason = "Energy is already full.";
        else if (Coins < cue.RefillPrice)
            reason = $"Need {cue.RefillPrice - Coins} more coins.";

        if (reason != null)
            return false;

        PlayerPrefs.SetInt(SaveKeys.Coins, Coins - cue.RefillPrice);
        PlayerPrefs.SetInt(SaveKeys.CueEnergy(cue.Id), cue.MaxEnergy);
        PlayerPrefs.Save();
        return true;
    }

    private void HandleCueSelectRequested(string cueId)
    {
        if (TrySelect(cueId, out string reason))
            RaiseChanged();
        else
            GameEvents.RaisePurchaseFailed(reason);
    }

    private void HandleCueBuyRequested(string cueId)
    {
        if (!TryBuy(cueId, out string reason))
        {
            GameEvents.RaisePurchaseFailed(reason);
            return;
        }

        GameEvents.RaiseProfileChanged(GetSnapshot(cueId));
        GameEvents.RaiseToastRequested(new ToastMessage($"{_catalog.Get(cueId).Name} purchased!", ToastStyle.Good));
    }

    private void HandleCueRefillRequested(string cueId)
    {
        if (!TryRefill(cueId, out string reason))
        {
            GameEvents.RaisePurchaseFailed(reason);
            return;
        }

        RaiseChanged();
        GameEvents.RaiseToastRequested(new ToastMessage($"{_catalog.Get(cueId).Name} refilled!", ToastStyle.Good));
    }
    #endregion

    private void RaiseChanged() => GameEvents.RaiseProfileChanged(GetSnapshot());
}
