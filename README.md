# Billiards — 8-Ball Duel

3D 8-ball against a bot for Android (portrait). Unity 6.3 (6000.3.9f1), URP, Input System, DOTween, TextMeshPro.
First-person: you aim bent over the cue (the camera steps back just enough to keep the hit ball and its pocket in
view), straighten up to watch your shot, and stand beside the opponent on their turn. The table sits in a small club.

## Modes

| | PLAY (vs computer) |
|---|---|
| Opponent | a CPU character of your division (~6 s turns, plays the division's cue; tap the table while it thinks to hurry it ×2) |
| Rank | 5 divisions (Bronze → Diamond) × 3 stages (III, II, I) of stars, then Grand Champion; win +1 ★ (+1 more from the 3rd win in a row); every division start is a checkpoint; a loss costs by division (Bronze nothing, Silver −1 ★, Gold+ −1 ★ and at 0 ★ one stage down) |
| Coins | win 20 / 30 / 45 / 65 / 90 by division (100 as Grand Champion), loss 5 / 8 / 12 / 16 / 20 (25), objectives 40–400, a reward for each new stage and division (paid once), a set bonus; spent only on cues (Club 400, Pro 1000, Master 1700, Champion 5000, Legend 10000) |
| Objectives (v4 V12) | a set of 6 per division (30), opened by entering the division, progress counts for every open objective, 3 shown on the menu (finished ones first), CLAIM; the last claim of a set pays its bonus; at least 3 per set also move in a lost match |
| Quit / forfeit / app killed mid-match | counts as a loss |
| You use | any cue you own (no energy, no entry fee) |

The next match lives in `MatchSetup` (`Mode`, `DivisionId`, `OpponentId`, `VenueId`), set by PLAY and PLAY AGAIN
from the current division. The two-player pass & play mode was removed (gameplay redesign G0).

**First run (v3 V9/V13):** a fresh install goes straight into the tutorial (`MatchMode.Tutorial`, five lessons from
`TutorialConfig.asset`: power, aim, release, power vs control, ball in hand; SKIP any time), then the House Rules card
and the first ranked match in Bronze III against Rookie Ray, then the menu with coach marks on the rank badge and the
objectives. Settings → HOW TO PLAY runs the tutorial again (back to the menu). One-time hints in real matches come
from `HintService` (ball in hand, groups, only the 8 left, FINE AIM after 5 shots, GREEN after 2 red releases, a hard
missed shot, free look, a lost star, a finished objective).

**No probes (v4 V14):** rewarded ads, Star Shield, Double coins, the Starter Pack and ONLINE fake doors and the survey are removed; a store build has nothing simulated in it.

**Screens (v4 V11-V13):** the menu runs badge → objectives (3, finished first, ALL ›) → PLAY across the width → CUES with your cue → the cue gap card ("Gold opponents use the Pro Cue", coins, TRY) or one line about the next cue. The badge opens DIVISIONS (every division with its venue, the bot's cue, the 8-ball rule, stages and rewards; Grand Champion on top; a NEW chip until looked at); ALL › opens the objective sets (a tab per division, the next one locked). The result shows the stars, a stage step (badge flip, reward row of its own), a new division (full-screen celebration with NEW RULE, the new venue and the reward), finished objectives with CLAIM, and NEXT MATCH with the next opponent; MENU is the quiet way out.

**Profile v2** (`ProfileService`, the only owner of the profile in PlayerPrefs): a profile older than v2 (coins / wins /
cue energy) is wiped on first start and begins fresh (there were no real users); sound and the one-time
rules / hint / tutorial flags stay.

## Controls

| | Editor (mouse / keyboard) | Android |
|---|---|---|
| Aim | drag left/right on the table | drag |
| Higher / lower over the cue | drag up/down on the table | drag |
| Fine aim | drag the left wheel, or A/D and ←/→ | drag the left wheel |
| Shoot | take the grip at the top of the right strip, drag down (it follows your finger 1:1, SOFT / MED / HARD at 15 / 50 / 85%), let go (release bar decides accuracy) | same |
| Cancel a shot | drag back up under 5% and let go | same |
| Look around | LOOK, then left drag (orbit and tilt), wheel (zoom), right drag (move the pivot); tap a ball to aim at it | LOOK, then 1 finger (orbit and tilt), pinch (zoom), 2 fingers (move the pivot) |
| Opponent's turn | same gestures take the camera (free look); TABLE VIEW or a double tap gives it back | same |
| Ball in hand | drag the cue ball, DONE | same |
| Pause / close popup | Esc | Back |

Aim guide (v3 V1): a thin line for the cue ball up to the first obstacle (ghost ball, cushion, pocket), then only
the cue's arrow length (`ObjectGuideDiameters` × 2R): the hit ball in gold, the cue ball after the contact (curved
while it slides) or after the cushion in white. An opponent's ball turns the line and ghost a muted red; a scratch
turns them orange with the pocket's rim. Geometry: `AimGuideLayout` (checked by `GuideChecks.LengthRule`).

Release bar: green = straight on the line, yellow = up to 1.6° off, red = 2–5° off and ±12% power (errors × 0.7 for
the softest shot up to × 1.35 for the hardest). Bar above green pulls the shot left, below pulls it right. Better
cues have wider green/yellow zones, a slower bar and a longer object-ball guide. Power changes the window live while
you pull (v3 V8, `MatchConfig` GreenScale/YellowScale/PeriodScale/ErrorScale, p = 0 → 1): green × 1.6 → 0.55,
yellow × 1.2 → 0.85, bar period × 1.35 → 0.72. The bot uses the same bar: it lets go N(0, σ) off the centre of
green (`BotProfile.TimingSigmaMs`).

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
| `Assets/_ScriptableObjects/CueCatalog.asset` | the 6 cues (House … Legend): price, release zones and speed, guide length; AIM / CONTROL / TIMING / OVR are computed from these (`CueRatings`) |
| `Assets/_ScriptableObjects/DivisionCatalog.asset` | divisions: venue, stars per stage, loss rule, win coins, bot cue, bot profile, CPU characters (line, extra thinking, full break); streak bonus, Grand Champion coins |
| `Assets/_ScriptableObjects/Divisions/Bot_<id>.asset` | the bot of each division: timing σ (the balance knob), max cut, candidates, turn timing; its cue window is copied from the division's cue on every build |
| `Assets/_ScriptableObjects/ObjectiveCatalog.asset` | the objective sets per division: text, kind, target, parameter, reward, set bonus |
| `Assets/_ScriptableObjects/EconomyConfig.asset` | start coins (0); wins, losses and stage / division rewards live in the DivisionCatalog |
| `Assets/_ScriptableObjects/Venues/<id>.asset` | five venues (basement, downtown, riverside, lounge, royal): cloth colour, caps, table light, ambient, baked reflections, thumbnail; the rooms (walls, carpet, furniture, neon) come from `RoomPrefabBuilder.Styles` |
| `Assets/_ScriptableObjects/BotProfile.asset` | the base bot (copied into each division's bot when that is first built; shot choice of the simulated player in the balance checks) |

`DataAssetBuilder.Build()` rebuilds the cue catalog from `DataAssetBuilder.CueSpecs` and the names, venues, cues and
characters of the divisions and the objective texts; tuned numbers (stars, coins, σ, targets, rewards) stay.
`Build(true)` resets everything to the defaults in code.

## Structure

```
Assets/
  _Scenes/            Menu (0), Match (1)
  _Scripts/
    Core/             Singleton, GameEvents (all cross-layer events), Bootstrap, SaveKeys, IdleMotion, MatchSetup
    Systems/          AudioManager, HapticsService, SceneLoader, InputRouter, ProfileService
    Data/             CueCatalog/CueDefinition, CueRatings, DivisionCatalog, RankLadder, ObjectiveCatalog, ObjectiveRules, EconomyConfig, MatchConfig, BotProfile, snapshots and event payloads
    Pool/             pure C# (no MonoBehaviour, Time or UnityEngine.Random): PoolPhysics, TableGeometry,
                      RackBuilder, ShotPredictor, ShotAccuracy, EightBallRules/RulesState, BotBrain
    Gameplay/         MatchController, PlayerShotController, BotController, CameraRig, TableView, BallView,
                      CueView, AimGuideView
    Menu/             MenuController, MenuTableOrbit
    UI/               UIButton, popups, tooltips, HUD and shot-control views (events only)
    Editor/Builders/  code that builds scenes, prefabs, generated art, data assets and the APK
    Editor/Checks/    PhysicsChecks, RulesChecks, PredictorChecks, BotBalanceCheck, DivisionBalanceCheck, EconomyChecks, debug menu
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

## Table look (v3 V6)

- Pockets: the cloth rolls over the hole edge, a leather funnel goes down to a rounded bottom, a brass cap sits on the
  rail; cushions and jaws are drawn only outside the holes (physics unchanged).
- Rails are one mesh with rounded top edges, flush with the cushions (`MatchConfig.RailTop` 46 mm).
- Light: one neutral-warm spot 3 m above the cloth (the only shadow caster) and a canopy with an emissive panel; the
  room has no realtime lights (ambient + emissive surfaces). Measured in the Editor (Watch): cloth luma 0.53, edges
  89% of the centre, room 0.16.
- HDR, Neutral tone mapping, bloom only on emission, vignette 0.2, MSAA 4x; world textures mipmapped, trilinear,
  aniso 8 (Poly Haven) / 4 (generated). Balls: smoothness 0.9, one baked reflection probe, soft contact shadows.
- `VenueTheme` (`_ScriptableObjects/Venues`) holds the cloth colour, wood, caps, light and ambient; `VenueView` on the
  table applies the one picked in `MatchSetup.VenueId` (Riverside for now).

## Juice (v3 V7)

- A pocketed ball carries on over the rim, falls into the liner, bounces once and stays visible until the turn ends;
  the rim glows green (counts for the shooter), red (cue ball / 8 too early) or white (opponent's ball). Its icon
  flies from the pocket to its slot in the panel, which greys when it lands.
- Sounds: `SoundData` holds variants (never the same one twice); break, soft/hard cue hit, pocket drop + rattle, a
  semitone higher per extra ball in the same shot (up to +4) with a chime, a roll loop by the balls' total speed, a
  room ambience loop. Placeholder mapping: `SystemsPrefabBuilder.Sounds`.
- Release: a PERFECT / GOOD / MISS stamp above the cue ball, chalk puff on PERFECT, the bar shakes on a miss.

## Events (`GameEvents`)

| Event | Raised by | Listened to by |
|---|---|---|
| OnPlayRequested / OnSettingsRequested / OnCuesRequested | MainMenuView, MatchResultPopup, PausePopup | MenuController, MatchController |
| OnCueSelectRequested / OnCueBuyRequested | CueCardView | ProfileService |
| OnObjectiveClaimRequested(objectiveId) *(v3, id since v4)* | ObjectiveRowView (menu, results) | ProfileService |
| OnTutorialStep(step, result) / OnTutorialCompleted(skipped) *(v3)* | TutorialController | ProfileService (TutorialDone), TutorialView; analytics later |
| OnTutorialStepShown / OnTutorialSkipRequested *(v3)* | TutorialController / TutorialView, MatchController (pause FORFEIT) | TutorialView / TutorialController |
| OnHowToPlayRequested / OnHapticsToggled *(v3)* | SettingsPopup | MenuController / HapticsService |
| OnDivisionsRequested *(v4, replaces OnRankedRoadRequested)* | RankBadgeView (menu) | MenuController (DivisionsPopup) |
| OnObjectivesRequested *(v4)* | ObjectivesPanelView (ALL ›) | MenuController (ObjectivesPopup) |
| OnNextMatchRequested *(v4)* | MatchResultPopup (NEXT MATCH) | MatchController |
| OnObjectiveSetCompleted(CelebrationData) *(v4)* | ProfileService (last CLAIM of a set) | MenuController, MatchResultPopup (CelebrationPopup) |
| OnRankCelebration(kind) *(v3, v4 adds DivisionUp, ObjectiveSetComplete, ChampionMilestone)* | MatchResultPopup, CelebrationPopup | AudioManager, HapticsService |
| OnProfileChanged / OnPurchaseFailed | ProfileService, MenuController | CoinCounterView, SelectedCueView, CuesPopup, ToastView, AudioManager |
| OnSfxVolumeChanged / OnMusicVolumeChanged | SettingsPopup | AudioManager |
| OnButtonClicked / OnDisabledButtonClicked / OnPopupShown | UIButton, PopupBase | AudioManager, HapticsService |
| OnToastRequested *(added)* | controllers, ProfileService, PlayerPanelView | ToastView, AudioManager |
| OnCoinTick *(added)* | CoinCounterView, MatchResultPopup | AudioManager |
| OnPauseRequested / OnResumeRequested / OnForfeitRequested / OnMenuRequested | MatchHudView, InputRouter, PausePopup, MatchResultPopup | MatchController |
| OnPauseChanged *(added)* | MatchController | AudioManager (music at 50%) |
| OnMatchStarted / OnTurnStarted / OnMatchEnded | MatchController | HUD views, VsIntroView (MatchStartInfo: opponent, rank, venue), CameraRig, controllers, AudioManager, HapticsService |
| OnTurnPhaseChanged | PlayerShotController, BotController, MatchController | ShotControlsView, CameraRig, TableTouchSurface, PowerStripView, ReleaseBarView |
| OnGroupsAssigned / OnBallPocketed / OnFoul | MatchController | PlayerPanelView, ToastView, AudioManager, HapticsService |
| OnLookDrag / OnLookModeToggled / OnFineAimDrag / OnCueBallDragged / OnPullChanged / OnPullReleased / OnPlaceCueBallConfirmed | UI shot controls | PlayerShotController |
| OnCueBallDragEnded / OnTableTapped / OnMoveCueBallRequested *(added)* | TableTouchSurface, ShotControlsView | PlayerShotController |
| OnReleaseMeterSetup / OnReleaseMeterChanged | PlayerShotController | ReleaseBarView, PowerStripView |
| OnShotFired / OnBallCollision / OnAimSnapped *(added)* | controllers, MatchController | ToastView, CameraRig, AudioManager, HapticsService |
| OnOrbitDrag / OnOrbitPinch / OnOrbitPan / OnOrbitDragEnded *(v3 V2)* | TableTouchSurface | CameraRig, CoachMarkView |
| OnTableViewRequested *(v3 V2)* | FreeLookChipView, TableTouchSurface (double tap) | CameraRig |
| OnFreeLookChanged *(v3 V2)* | CameraRig | FreeLookChipView |
| OnBallPocketedFx (number, screen pos, PocketFxKind, speed) *(v3 V7)* | TableView | PlayerPanelView (flyer), AudioManager, HapticsService |
| OnRollingSpeed (Σ\|v\|) *(v3 V7)* | MatchController | AudioManager (roll loop) |
| OnCoachMarkRequested / OnCoachMarkCleared *(v3 V2)* | HintService | CoachMarkView |
| OnReleaseMeterChanged (position, power, greenHalf, yellowEnd, period) *(v3 V8)* | PlayerShotController | ReleaseBarView, PowerStripView |

## Balance and pace (v3 commit 12)

Simulated players differ only in release timing (σ 30 / 45 / 65 ms) and choose shots like the base bot; each plays
the division's expected cue. Bots per division: σ Bronze 50, Silver 48, Gold 55, Platinum 50, Diamond 56 ms (max cut
55 / 65 / 75 / 75 / 75°, 2 / 3 / 4 / 5 / 5 candidates).

| Division | good | average (target) | weak |
|---|---|---|---|
| Bronze | 77% | 62% (62) | 52% |
| Silver | 69% | 58% (60) | 48% |
| Gold | 65% | 59% (58) | 46% |
| Platinum | 57% | 51% (56) | 44% |
| Diamond | 50% | 53% (55) | 45% |

`DivisionBalanceCheck.Run(400, strict: true)`. Gate (user decisions 2026-10-24/25): the average player on target
±6 p.p. and the order good ≥ average − 5, average ≥ weak + 5. With wide release windows timing separates players
little in the upper divisions, so the "good ≥ 66–75%" targets are reported, not gated.

Pace (`ProgressionSim.Run(2000)`, median matches; the average player buys the next cue as soon as it can and claims
objectives, no rewarded):

| Milestone | average | target | good | weak |
|---|---|---|---|---|
| Silver checkpoint | 8 | 6–9 | 6 | 11 |
| Club Cue (400) | 6 | 5–9 | 5 | 7 |
| Gold checkpoint | 22 | 20–28 | 17 | 39 |
| Pro Cue (1000) | 19 | 18–28 | 16 | 27 |
| Platinum checkpoint | 45 | 40–50 | 32 | 88 |
| Master Cue (1700) | 50 | 40–55 | 44 | 74 |
| Diamond checkpoint | 73 | 70–90 | 64 | 149 |
| Champion Cue (5000) | 146 | 80+ | 145 | 198 |
| Legend Cue (10000) | >250 | 120+ | >250 | >250 |

Tuning (data only): cue prices Club 300 → 400, Pro 900 → 1000, Master 2200 → 1700; bot σ as above. The weak player
reaches Gold around day 7 (target ~5), accepted: helping them in Silver would push the average player under the
Gold target. A match takes a median of 3.7 min (`MatchLengthCheck`, the player's turn assumed 9 s).

## Analytics (phase 2 mapping)

Nothing is sent in this build. Each analytics event of the release plan (§6) maps to a `GameEvents` event that
already carries what it needs; phase 2 adds one listener (an `AnalyticsService` in Systems) that forwards them.

| Analytics event | From `GameEvents` | Parameters |
|---|---|---|
| `tutorial_step` | `OnTutorialStep(step, result)` | step 1-5, result (started / succeeded / failed / skipped) |
| `tutorial_complete` | `OnTutorialCompleted(skipped)` | skipped, duration_s |
| `match_start` | `OnMatchStarted(MatchStartInfo)` | division, stage, opponent, venue, cue, match_index |
| `shot` | `OnShotFired(ShotFeedback)` | zone (perfect/good/miss), power, is_break, fine_aim |
| `match_end` | `OnMatchEnded(MatchResult)` | result, reason, stars (+/-), streak bonus, coins, rank before / after, objectives done |
| `checkpoint_reached` | `OnMatchEnded` with `NewDivision` | division |
| `objective_claimed` | `OnObjectiveClaimRequested(objectiveId)` | objective id, reward |
| `cue_purchased` | `OnCueBuyRequested(id)` + `OnProfileChanged` | cue, coins_before |
| `forfeit` | `OnForfeitRequested` | division, shots so far |

User properties: `division`, `stage`, `best_cue`, `matches_played`.

## Builders and checks

Scenes, prefabs and generated assets come from `Assets/_Scripts/Editor/Builders` (**Tools → Billiards → Builders**).
Re-running a builder overwrites manual edits to what it builds. Full rebuild order: Generated Art → Art Import →
Materials → Data Assets → Project Settings → Table Prefabs → Room Prefab → UI Prefabs → Systems Prefab → Match Scene →
Menu Scene.

Checks run from `eval` (e.g. `unity command eval 'return PhysicsChecks.All();'`): `PhysicsChecks`, `RulesChecks.All`,
`PredictorChecks.MatchesSimulation`, `BotBalanceCheck.Run(200)`, `EconomyChecks.All` (rank, coins, objectives,
cues and migration on a scratch profile; restores yours), `ContrastChecks.Buttons` (button label vs face, WCAG; v3 V13), in Play mode `TutorialChecks.RunThrough` / `Skip`
(then poll `TutorialChecks.Status`) and `TutorialChecks.Hints` (Menu scene), `ShotDifficultyCheck.All` (release odds and pot rates by
cue × power), `DivisionBalanceCheck.Run(400, strict: true)` (win rates per division for good / average / weak timing with the
division's cue, and one cue better / worse), `ProgressionSim.Run(2000)` (pace: checkpoints and cues, ~13 min),
`MatchLengthCheck.Run(100)` (median match length).

Editor-only debug (Play mode, Match scene): **Tools → Billiards → Debug** — Bot vs Bot, Restart (Player Breaks),
Set Up Winning Shot.
