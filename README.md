# Billiards — 8-Ball Duel

3D 8-ball against a bot for Android (portrait). Unity 6.3 (6000.3.9f1), URP, Input System, DOTween, TextMeshPro.
First-person: you aim bent over the cue (the camera steps back just enough to keep the hit ball and its pocket in
view), straighten up to watch your shot, and stand beside the opponent on their turn. The table sits in a small club.

## Modes

| | PLAY (vs computer) |
|---|---|
| Opponent | bot (~10 s turns, plays the Club Cue) |
| Coins, wins, unlocks | win +10, loss −1 |
| Cue energy | −1 per match |
| Quit / forfeit | counts as a loss |
| You use | your selected cue |

The mode for the next match lives in `MatchSetup.Mode` (set by the menu, kept for PLAY AGAIN). The two-player
pass & play mode was removed (gameplay redesign G0).

## Controls

| | Editor (mouse / keyboard) | Android |
|---|---|---|
| Aim | drag left/right on the table | drag |
| Higher / lower over the cue | drag up/down on the table | drag |
| Fine aim | drag the left wheel, or A/D and ←/→ | drag the left wheel |
| Shoot | press on the right strip, drag down (power), let go (release bar decides accuracy) | same |
| Cancel a shot | drag back up under 5% and let go | same |
| Look around | LOOK, then drag (walk around / crouch-rise), tap a ball to aim at it | same |
| Ball in hand | drag the cue ball, DONE | same |
| Pause / close popup | Esc | Back |

Release bar: green = straight on the line, yellow = up to 1.6° off, red = 2–5° off and ±12% power. Bar above green
pulls the shot left, below pulls it right. Better cues have wider green/yellow zones, a slower bar and a longer
object-ball guide.

## Build the APK

Needs the Android module (SDK, NDK, OpenJDK) installed with the Editor.

- In the Editor: **Tools → Billiards → Build Android APK**
- From the CLI, with the Editor open: `unity command eval --detach -- --code 'return AndroidBuilder.Build();' --timeout 3600000`, then `unity job wait <job-id>`

Output: `Builds/Android/Billiards.apk` (release, IL2CPP ARM64, Unity debug keystore — fine for sideloading).

```bash
adb install -r Builds/Android/Billiards.apk
```

## Where to tune

| Asset | What |
|---|---|
| `Assets/_ScriptableObjects/MatchConfig.asset` | table and pockets, physics (rolling resistance, restitution), shot power curve, release errors, control sensitivity, first-person camera (eye heights, distances, sway), timings |
| `Assets/_ScriptableObjects/CueCatalog.asset` | economy (start 10, win +10, loss −1) and the 5 cues: unlock wins, price, energy, refill, release zones and speed, guide length |
| `Assets/_ScriptableObjects/BotProfile.asset` | bot release odds (45/40/15 — the balance knob), shot scoring weights, turn timing |

`DataAssetBuilder.Build()` rebuilds the cue catalog from `DataAssetBuilder.CueSpecs`; `Build(true)` also resets
MatchConfig and BotProfile to the defaults in code.

## Structure

```
Assets/
  _Scenes/            Menu (0), Match (1)
  _Scripts/
    Core/             Singleton, GameEvents (all cross-layer events), Bootstrap, SaveKeys, IdleMotion, MatchSetup
    Systems/          AudioManager, HapticsService, SceneLoader, InputRouter, ProfileService
    Data/             CueCatalog/CueDefinition, MatchConfig, BotProfile, snapshots and event payloads
    Pool/             pure C# (no MonoBehaviour, Time or UnityEngine.Random): PoolPhysics, TableGeometry,
                      RackBuilder, ShotPredictor, ShotAccuracy, EightBallRules/RulesState, BotBrain
    Gameplay/         MatchController, PlayerShotController, BotController, CameraRig, TableView, BallView,
                      CueView, AimGuideView
    Menu/             MenuController, MenuTableOrbit
    UI/               UIButton, popups, tooltips, HUD and shot-control views (events only)
    Editor/Builders/  code that builds scenes, prefabs, generated art, data assets and the APK
    Editor/Checks/    PhysicsChecks, RulesChecks, PredictorChecks, BotBalanceCheck, EconomyChecks, debug menu
  _Prefabs/           Table (Table, TableDecor, Room, Ball, Cue), UI, Popups
  _Art, _Audio        Kenney CC0 assets and CC0 music (see CREDITS.md), generated art
```

Layer rules (both must print nothing):

```bash
grep -rnE "\b(AudioManager|HapticsService|SceneLoader|InputRouter|ProfileService|MatchController|MenuController|PlayerShotController|BotController|PoolPhysics|EightBallRules)\b|PlayerPrefs" Assets/_Scripts/UI
grep -rnE "MonoBehaviour|GameEvents|UnityEngine\.Random|Time\.|PlayerPrefs" Assets/_Scripts/Pool
```

## Physics

Own deterministic 2D simulation on the table plane (1 ms step), not PhysX: it has to be replayable ahead of time
for the aim guide and the bot. Ball contacts are rewound to the exact moment of impact, so the guide matches
the simulation (checked: < 0.01 mm, 0.000°).

Balls slide, then roll (v3 V4): every ball keeps a roll velocity (`BallState.Roll`, R·ω). Off the cue or off another
ball it slides with no roll; cloth friction (`MatchConfig.SlideFriction`, μ 0.2) turns slip into roll until it rolls
at 5/7 of that speed. Ball-ball contacts keep each ball's roll, so a cue ball that arrives rolling follows through and
one that still slides stops (stun). Cushions remove the roll into them. `BallMotion` is the same model in closed
form, for the guide (the cue ball's curved path after contact) and the bot's speed estimates.

## Events (`GameEvents`)

| Event | Raised by | Listened to by |
|---|---|---|
| OnPlayRequested / OnSettingsRequested / OnCuesRequested | MainMenuView, MatchResultPopup, NoEnergyPopup, PausePopup | MenuController, MatchController |
| OnCueSelectRequested / OnCueBuyRequested / OnCueRefillRequested | CueCardView, NoEnergyPopup | ProfileService |
| OnProfileChanged / OnPurchaseFailed | ProfileService, MenuController | CoinCounterView, SelectedCueView, CuesPopup, NoEnergyPopup, ToastView, AudioManager |
| OnSfxVolumeChanged / OnMusicVolumeChanged | SettingsPopup | AudioManager |
| OnButtonClicked / OnDisabledButtonClicked / OnPopupShown | UIButton, PopupBase | AudioManager, HapticsService |
| OnToastRequested *(added)* | controllers, ProfileService, PlayerPanelView | ToastView, AudioManager |
| OnCoinTick *(added)* | CoinCounterView, MatchResultPopup | AudioManager |
| OnPauseRequested / OnResumeRequested / OnForfeitRequested / OnMenuRequested | MatchHudView, InputRouter, PausePopup, MatchResultPopup | MatchController |
| OnPauseChanged *(added)* | MatchController | AudioManager (music at 50%) |
| OnMatchStarted / OnTurnStarted / OnMatchEnded | MatchController | HUD views, CameraRig, controllers, AudioManager, HapticsService |
| OnTurnPhaseChanged | PlayerShotController, BotController, MatchController | ShotControlsView, CameraRig, TableTouchSurface, PowerStripView, ReleaseBarView |
| OnGroupsAssigned / OnBallPocketed / OnFoul | MatchController | PlayerPanelView, ToastView, AudioManager, HapticsService |
| OnLookDrag / OnLookModeToggled / OnFineAimDrag / OnCueBallDragged / OnPullChanged / OnPullReleased / OnPlaceCueBallConfirmed | UI shot controls | PlayerShotController |
| OnCueBallDragEnded / OnTableTapped / OnMoveCueBallRequested *(added)* | TableTouchSurface, ShotControlsView | PlayerShotController |
| OnReleaseMeterSetup / OnReleaseMeterChanged | PlayerShotController | ReleaseBarView, PowerStripView |
| OnShotFired / OnBallCollision / OnAimSnapped *(added)* | controllers, MatchController | ToastView, CameraRig, AudioManager, HapticsService |

## Builders and checks

Scenes, prefabs and generated assets come from `Assets/_Scripts/Editor/Builders` (**Tools → Billiards → Builders**).
Re-running a builder overwrites manual edits to what it builds. Full rebuild order: Generated Art → Art Import →
Materials → Data Assets → Project Settings → Table Prefabs → Room Prefab → UI Prefabs → Systems Prefab → Match Scene →
Menu Scene.

Checks run from `eval` (e.g. `unity command eval 'return PhysicsChecks.All();'`): `PhysicsChecks`, `RulesChecks.All`,
`PredictorChecks.MatchesSimulation`, `BotBalanceCheck.Run(200)`, `EconomyChecks.All` (restores your profile).

Editor-only debug (Play mode, Match scene): **Tools → Billiards → Debug** — Bot vs Bot, Restart (Player Breaks),
Set Up Winning Shot.
