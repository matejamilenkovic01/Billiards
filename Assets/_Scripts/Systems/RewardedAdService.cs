using UnityEngine;

// Rewarded ads (v3 V12). In this phase a simulated overlay ("Ad (test) · 3", a 3 s countdown, CLOSE): closing early
// gives nothing. It answers OnRewardedAdRequested with OnRewardedAdFinished(placement, rewarded); ProfileService
// pays the reward. Not for a store build until the overlay is replaced.
public class RewardedAdService : SingletonPersistent<RewardedAdService>
{
    [SerializeField] private float _simulatedSeconds = 3f;

    private void OnEnable() => GameEvents.OnRewardedAdRequested += Show;

    private void OnDisable() => GameEvents.OnRewardedAdRequested -= Show;

    public void Show(RewardedPlacement placement)
    {
        Debug.Log($"RewardedAdService: show {placement} (simulated)");
        // PHASE 2: replace with AdMob rewarded (load, show, reward callback -> RaiseRewardedAdFinished).
        PopupManager.Instance.Show<SimulatedAdOverlay>(new SimulatedAdData(placement, _simulatedSeconds,
            completed => GameEvents.RaiseRewardedAdFinished(placement, completed)));
    }
}
