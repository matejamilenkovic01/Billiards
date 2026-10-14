# Implementation Definition — Billiards (8-Ball Duel, Unity 6.3, Android)

Ovaj dokument je **jedini ulaz** za agenta koji pravi igru od početka do kraja.
Čitaj ga ceo pre prvog koraka. Kad nešto nije pokriveno ovde — **stani i pitaj korisnika**, ne pretpostavljaj.

Referentni projekat za arhitekturu: `~/Desktop/UnityProjects/FlappyBird` (gotov, 5 commitova). Njegov `Implementation_definition.md`, `README.md` i `Assets/_Scripts` su **izvor istine za obrasce** (eventi, slojevi, popupi, dugmad, builderi, CLI rad). Ovde se ponavlja samo ono što je bitno; za detalje generičkih sistema čitaj FlappyBird kod.

---

## 0. Pravila rada za agenta

1. Radiš **inkrementalno, kroz tačno 7 commitova** (sekcija 9). Svaki commit je kompajlabilna, pokretljiva celina. Posle svakog: `git commit` + `git push` direktno na `main`.
2. Pre svakog commita prolaziš **verifikaciju** iz tog koraka. Nikad ne tvrdiš da nešto radi ako nisi proverio; ako nešto ne možeš da proveriš (haptic, osećaj kontrola na telefonu), napiši to eksplicitno.
3. Posle svakog commita napiši korisniku kratak izveštaj: šta je urađeno, kako je provereno, šta nije provereno, commit hash.
4. Generičke sisteme **kopiraj iz FlappyBird-a i prilagodi** (lista u 4.1), ne piši ih ispočetka. Gameplay je nov.
5. Stani i pitaj ako: Editor nije dostupan CLI-ju, DOTween/TMP nije importovan, GitHub remote ne postoji, download ne uspe, build pada iz neočiglednog razloga, ili bi rešenje kršilo pravila iz 4.3.
6. Ne diraj ništa van `~/Desktop/UnityProjects/Billiards` osim `~/Desktop/UnityProjects/_kenney_downloads/` (download folder) i **čitanja** FlappyBird projekta (FlappyBird se ne menja).

---

## 1. Cilj i obim

3D bilijar **8-ball duel protiv bota** za **Android (portrait)**. Igra se:
- **u Editoru mišem** (drag/klik = isti gestovi kao dodir) + Esc = pauza/back,
- **na telefonu dodirom** + sistemsko Back dugme.

Art je **jednostavan** (primitive + proceduralne teksture + Kenney UI). **UX mora biti odličan**: jasno ko je na potezu, jasna putanja, udarac koji je zabavan i fer, glatke animacije, zvuk + haptic na svaku akciju.

### 1.1 Funkcionalni zahtevi (korisnikovi, obavezni)
| # | Zahtev |
|---|---|
| R1 | Portrait orijentacija, sve na **engleskom**. |
| R2 | Početni ekran: naslov + **3 dugmeta vertikalno: Play, Settings, Cues** (inventar štapova). |
| R3 | Settings = isto kao FlappyBird: popup sa dva slajdera **Sound Effects** i **Music**, čuvaju se. |
| R4 | Muzika prikladna za bilijar duel (lounge/jazz), loop, ne prekida se između scena. |
| R5 | Inventar: **5 štapova**, svaki u jednom od stanja: **Owned**, **Unlocked (not owned)**, **Locked**. Prvi je posedovan od starta, ostali se kupuju novčićima. **Tačno jedan je selektovan**; meč se uvek igra selektovanim štapom. |
| R6 | Štap ima **energiju** koja se troši igranjem; posedovani štap se dopunjava novčićima, opcija stoji **pored štapa**. |
| R7 | Bolji štap = **lakši release** (šira zelena zona, sporija oscilacija) i **duža linija putanje kugle u koju se udara**. |
| R8 | Play vodi direktno na početak meča. |
| R9 | Meč: 3D sto, **first-person pogled** kao igrač koji stoji za stolom. Korisnik protiv bota, **pravila 8-ball**, potezi se smenjuju. |
| R10 | Na potezu korisnik može da **kruži pogledom oko stola**. |
| R11 | Pozicioniranje bele kugle (ball in hand) — vidi odluke 1.2. |
| R12 | Nišanjenje sa **prikazanom putanjom**: bela → do prve kugle (ghost ball), pa **kratka** linija smera pogođene kugle (osnovni štap ≈ 2 prečnika kugle). |
| R13 | Udarac: korisnik **povuče štap nazad** (snaga); dok drži, **release bar osciluje gore-dole**: **zeleno u sredini, žuto oko njega, crveno na krajevima**. Pušta prst = udarac. Zeleno = kugla ide tačno kuda je nanišanio; što dalje od zelenog, to veće odstupanje. |
| R14 | Bot igra na **prosečnom** nivou: ko dobro release-uje tokom meča pobeđuje, ko loše gubi. Potez bota traje **~10 s**, jasno označeno da je protivnik na potezu. |
| R15 | Kraj meča: popup **WIN / LOSE**. |
| R16 | Start: **10 novčića**. |
| R17 | **Realna fizika kugli**: odbijanje kugla–kugla (prenos impulsa po liniji centara), odbijanje od cushion-a (gubitak energije), kotrljanje i postepeno zaustavljanje, vidljiva rotacija kugli, **upadanje u rupu** (kugla koja dođe do rupe upadne sa animacijom i nestane sa stola; kugla koja samo okrzne ivicu rupe se odbije od jaws-a). Detalji 4.6.1 i 4.6.6. |
| R18 | **Prikaz preostalih kugli** za oba igrača tokom celog meča: koje su kugle čije, koliko je ostalo, koje su ubačene; prikaz 8-ice kad je na redu. Detalji 4.6.9 (PlayerPanelView) i 4.6.11. |

### 1.2 Odluke korisnika (iz razgovora)
| Tema | Odluka |
|---|---|
| Nagrada | **Win +10**, **Loss −1**; balans nikad ispod 0. |
| Bela kugla | **Ball in hand samo po pravilima**: break iz kuhinje (iza head string-a); posle **faula protivnika** postavljanje bilo gde na stolu. Važi i za bota. Inače se igra odakle je bela stala. |
| Energija | **−1 po meču** sa selektovanog štapa (skida se na početku meča). Na 0 se taj štap ne može koristiti dok se ne dopuni. **Početni štap ima beskonačnu energiju** (nema softlocka sa 0 novčića). |
| Otključavanje | **Po broju pobeda** (ukupno, perzistentno). Locked → Unlocked kad broj pobeda dostigne prag; kupovina tek tada. |
| Snaga/preciznost | Jedan gest: drag nazad = snaga (štap se vidno povlači), za vreme držanja bar osciluje, release = udarac (vidi 4.7.5). |
| Muzika | **CC0 download** (OpenGameArt, samo eksplicitno CC0) uz **fallback**: generisani lounge loop u C# kao u FlappyBird-u. |
| Pauza u meču | Popup: **Resume**, **Settings**, **Forfeit** (sa potvrdom; = poraz, −1 novčić, energija već potrošena). Odlazak aplikacije u pozadinu = auto pauza. |
| Git / platforma | Korisnik pravi prazan GitHub repo `matejamilenkovic01/Billiards`; agent dodaje SSH remote i pushuje na `main`. Android APK kao FlappyBird. |

### 1.3 Odluke koje je doneo planer (korisnik ih može promeniti pre starta)
- **8-ball bez prijave rupe** (no call pocket): 8 u bilo koju rupu, legalno = pobeda. Pojednostavljuje UX.
- Ko razbija: **nasumično** (coin flip animacija "YOU BREAK" / "OPPONENT BREAKS").
- Nema spina (english), skoka ni masé-a. Udarac je uvek u centar bele.
- Napuštanje meča ubijanjem aplikacije se računa kao poraz (vidi 4.8, `PendingMatch`).
- Protivnik se zove **"Opponent"** (ikonica robota/čoveka iz Kenney Game Icons).
- Sto: 8ft, zelena čoja, tamno drvo, tamna soba sa toplim svetlom iznad stola.

### 1.4 Van obima
Multiplayer, spin/masé/jump, reklame, IAP, leaderboard, lokalizacija, iOS, Play Store potpisivanje, nivoi težine bota, replay.

---

## 2. Okruženje i preduslovi

### 2.1 Provereno stanje (2026-10-14)
| Stvar | Vrednost |
|---|---|
| Projekat | `~/Desktop/UnityProjects/Billiards` |
| Editor | Unity **6000.3.9f1**, otvoren na ovom projektu, `unity status` → `ready` (port 7800) |
| Template | **Universal 3D** (URP 17.3), `Assets/Settings/{Mobile,PC}_RPAsset`, `SampleScene`, `TutorialInfo`, `Readme.asset` (obrisati poslednja tri) |
| Paketi | isti kao FlappyBird bez 2D paketa; `com.unity.inputsystem` 1.18.0, `com.unity.pipeline` 0.8.0-exp.1 **već instaliran**, `com.unity.test-framework` 1.6.0 |
| Input | `activeInputHandler: 1` (Input System) — proveri |
| Player Settings | orijentacija **AutoRotation** (mora Portrait), min API 25 (mora 26), package name nije postavljen |
| Git | `git init` urađen, grana `main`, **0 commitova**, **nema remote-a**, **nema `.gitignore`**. GitHub repo `git@github.com:matejamilenkovic01/Billiards.git` **postoji i prazan je** (SSH `ls-remote` radi) |
| DOTween / TMP | **Importovani (2026-10-14)**: DOTween **1.3.045** u `Assets/Plugins/Demigiant/DOTween/`, `Assets/Resources/DOTweenSettings.asset` (moduli UI, Audio, Sprite, Physics, Physics2D uključeni; TMP modul nije — free verzija), `Assets/TextMesh Pro/` sa Essentials; `scriptCompilationFailed = false` |
| `gh` CLI | nije instaliran (ne koristi ga) |
| Kenney download folder | `~/Desktop/UnityProjects/_kenney_downloads/` već sadrži: animal-pack, digital-audio, game-icons, impact-sounds, interface-sounds, tappy-plane, ui-audio, ui-pack (iskoristi ih, ne skidaj ponovo) |
| Android | moduli instalirani sa Editorom (dele se sa FlappyBird-om); putanje `aapt`/`adb` kao u FlappyBird dokumentu 6.3/6.4 |
| Python | bez Pillow-a → sve teksture i audio generiši iz Unity Editor C# koda |

### 2.2 Preduslovi koje radi KORISNIK (agent ih **proverava** na početku)
1. **DOTween (free)** importovan (Package Manager → My Assets → DOTween → Import), pa **Tools → Demigiant → DOTween Utility Panel → Setup DOTween** sa modulima **UI**, **Audio** (Sprite i Physics nisu potrebni). Provera: `Assets/Plugins/Demigiant/DOTween/` i `Assets/Resources/DOTweenSettings.asset`. Free DOTween nema TMP modul — vidi FlappyBird 2.2 napomenu (koristi `Graphic` ekstenzije, `DOTween.To`).
2. **TMP Essentials** importovani (Window → TextMeshPro → Import TMP Essential Resources).
3. **Prazan GitHub repo** `git@github.com:matejamilenkovic01/Billiards.git` (bez README/.gitignore).
4. Editor otvoren na Billiards, bez modalnog dijaloga.

**Stanje: 1–3 su urađeni i provereni (2026-10-14).** Agent ih ipak brzo proveri na startu (Editor može biti zatvoren/restartovan).

Pošto je 1–3 urađeno pre prvog commita agenta, prvi commit je `chore: import DOTween and TMP essentials` + početni projekat (kao u FlappyBird-u: `Initial Unity project` pa `chore`), tek onda commit 1 iz sekcije 9.

---

## 3. Rad sa Unity CLI

**Identično FlappyBird dokumentu, sekcija 3** (Builder Editor skripte u `Assets/_Scripts/Editor/Builders/`, pokretanje kroz `unity command eval '...'`, `--detach` + `unity job wait` za duge operacije, idempotentni builderi, nikad ručno editovanje `.unity/.prefab/.asset` YAML-a, verifikacija kroz `unity recompile`, logove, `editor_play`/`editor_stop`, screenshot). Pročitaj `unity skill show` (posebno `playmode-verification-loop`) pre prvog Play testa.

Dodatno za ovaj projekat:
- **Headless simulacija**: fizika, pravila i bot su čist C# bez MonoBehaviour-a (4.6), pa se testiraju kroz `eval` **bez Play mode-a** (brzo i determinističko). Za duže simulacije (stotine mečeva) koristi `--detach`.
- Menu item-i idu pod **Tools → Billiards → …**.

---

## 4. Arhitektura

### 4.1 Šta se kopira iz FlappyBird-a (pa prilagođava)
| Fajl(ovi) | Prilagođavanje |
|---|---|
| `Core/Singleton.cs`, `Core/Bootstrap.cs`, `Core/IdleMotion.cs` | bez izmena (osim targetFrameRate 60) |
| `Core/GameEvents.cs` | **obrazac** isti (event + `RaiseX` + `ResetAll`), sadržaj nov (4.4) |
| `Core/SaveKeys.cs` | novi ključevi (4.8) |
| `Systems/AudioManager.cs` | novi `SoundId`-jevi (5.4), muzika 50% tokom pauze ostaje, volumen kugli po jačini udara |
| `Systems/HapticsService.cs` | dodaj `Medium`; slušaoci iz 4.4 |
| `Systems/SceneLoader.cs` (+ transition canvas) | scene `Menu`/`Match` |
| `Systems/InputRouter.cs` | Back/Esc logika za Match (pauza) i Menu (zatvori popup) |
| `UI/Buttons/UIButton.cs` + prefab + varijante | dodaj **disabled** stanje (siv sprite, nema scale animacije, ne diže `OnButtonClicked`; tap na disabled dugme diže `OnDisabledButtonClicked` → AudioManager pušta tihi `Error` zvuk) |
| `UI/Popups/PopupBase.cs`, `PopupManager.cs`, `SettingsPopup.cs`, `PopupData.cs` | Settings bez izmena; novi popupi u 4.6.9 |
| `UI/Tooltips/*` | bez izmena; koristi se za objašnjenje statistika štapa |
| `UI/Views/SafeArea.cs` | bez izmena |
| `Editor/Builders/TempObjectScope.cs`, `ProjectSettingsBuilder`, `ArtImportBuilder`, `SystemsPrefabBuilder`, `UIPrefabBuilder`, `AndroidBuilder` | putanje, imena, menu `Tools → Billiards`; `GeneratedArtBuilder` piše **nove** generatore (5.3), muzički generator se kopira kao osnova fallback-a |

Ne kopiraj: `Bird*`, `Pipe*`, `LevelScroller`, `ScrollingLoop`, `GetReadyView`, `TapCatcher`, `ScoreView`, `ComponentPool`/`WeightedRandom` (ne trebaju; dodaj ih samo ako zaista zatrebaju).

### 4.2 Struktura foldera
```
Assets/
  _Scenes/            Menu.unity (0), Match.unity (1)
  _Scripts/
    Core/             Singleton, GameEvents, Bootstrap, SaveKeys, IdleMotion
    Systems/          AudioManager, HapticsService, SceneLoader, InputRouter, ProfileService
    Data/             CueCatalog, CueDefinition, MatchConfig, BotProfile, Snapshots (CueViewData, ProfileSnapshot…)
    Pool/             (čist C#, bez Unity scene zavisnosti)
                      PoolPhysics, BallState, TableGeometry, ShotParams, ShotResult, ShotPredictor,
                      EightBallRules, RulesState, BallGroup, ShotAccuracy, RackBuilder
    Gameplay/         MatchController, TurnState, PlayerShotController, BotController, BotBrain,
                      TableView, BallView, CueView, AimGuideView, CameraRig, ShotFxView
    Menu/             MenuController, MenuTableOrbit
    UI/
      Buttons/        UIButton
      Popups/         PopupBase, PopupManager, SettingsPopup, CuesPopup, CueCardView, ConfirmPopup,
                      PausePopup, MatchResultPopup, NoEnergyPopup, PopupData
      Tooltips/       TooltipManager, TooltipView, TooltipTrigger
      Views/          MainMenuView, CoinCounterView, SelectedCueView, MatchHudView, PlayerPanelView,
                      TurnBannerView, ShotControlsView, PowerStripView, ReleaseBarView, FineAimWheelView,
                      TableTouchSurface, ToastView, SafeArea
    Editor/Builders/  ProjectSettingsBuilder, ArtImportBuilder, GeneratedArtBuilder, DataAssetBuilder,
                      TablePrefabBuilder, SystemsPrefabBuilder, UIPrefabBuilder, MenuSceneBuilder,
                      MatchSceneBuilder, AndroidBuilder, TempObjectScope
    Editor/Checks/    PhysicsChecks, RulesChecks, PredictorChecks, BotBalanceCheck, EconomyChecks (statičke metode za eval)
  _Prefabs/           Table/ (Table, Ball, Cue), UI/, Popups/
  _ScriptableObjects/ CueCatalog.asset, MatchConfig.asset, BotProfile.asset
  _Art/               Kenney/<pack>/ (+License.txt), Generated/ (teksture kugli, čoja, drvo, ikonice koje Kenney nema)
  _Materials/         Felt, Wood, Rail, PocketBlack, Ball_0..Ball_15, Cue_1..Cue_5, GuideLine, GhostBall
  _Audio/             Kenney/<pack>/, Music/ (CC0 track ili generisani), Generated/
  Resources/          Systems.prefab, DOTweenSettings.asset
```
Bez namespace-a i bez asmdef-ova (kao FlappyBird). `Editor/Checks` su Editor-only verifikacioni skriptovi (ostaju u repou — korisni za kasniji tuning).

### 4.3 Slojevi i pravila zavisnosti (kao FlappyBird R12 — ključno)
| Sloj | Šta je | Sme | Ne sme |
|---|---|---|---|
| **UI** (`_Scripts/UI/**`) | Canvas komponente, uključujući kontrole udarca i touch površinu | sluša/diže `GameEvents`, zove `PopupManager.Instance` i `TooltipManager.Instance`, dobija podatke kroz payload / event snapshot | referencira bilo koji Manager/Service/Controller, `Pool/*` logiku, čita `PlayerPrefs` |
| **Controllers** (`MatchController`, `MenuController`, `PlayerShotController`, `BotController`) | Orkestracija | sluša UI request evente, zove servise, `PopupManager`, pravi payload | referencira konkretne UI view-ove |
| **Systems** (`AudioManager`, `HapticsService`, `SceneLoader`, `InputRouter`, `ProfileService`, `PopupManager`, `TooltipManager`) | Persistent servisi | evente, `PlayerPrefs` | — |
| **Pool** (`_Scripts/Pool/**`) | Čista simulacija i pravila | samo `UnityEngine` math tipove (`Vector2`, `Mathf`) | MonoBehaviour, evente, scenu, `Time`, `Random` (koristi prosleđeni `System.Random`) |
| **Gameplay views** (`TableView`, `BallView`, `CueView`, `AimGuideView`, `CameraRig`) | Prikaz sveta | čitaju stanje koje im controller da, slušaju evente | da znaju za UI |

Automatske provere pre svakog commita (moraju vratiti prazno):
```bash
grep -rnE "\b(AudioManager|HapticsService|SceneLoader|InputRouter|ProfileService|MatchController|MenuController|PlayerShotController|BotController|PoolPhysics|EightBallRules)\b|PlayerPrefs" Assets/_Scripts/UI
grep -rnE "MonoBehaviour|GameEvents|UnityEngine\.Random|Time\.|PlayerPrefs" Assets/_Scripts/Pool
```

### 4.4 `GameEvents`
Obrazac iz FlappyBird-a. Spisak (agent sme da doda event ako zatreba, ali ga upiše u ovu tabelu u README-u):

| Event | Payload | Ko diže | Ko sluša |
|---|---|---|---|
| **Menu / profil** ||||
| `OnPlayRequested` | — | MainMenuView, MatchResultPopup (Play Again), NoEnergyPopup | MenuController / MatchController |
| `OnSettingsRequested` | — | MainMenuView, PausePopup | MenuController / MatchController |
| `OnCuesRequested` | — | MainMenuView, SelectedCueView, NoEnergyPopup ("Change Cue") | MenuController |
| `OnCueSelectRequested` | `string cueId` | CueCardView | ProfileService |
| `OnCueBuyRequested` | `string cueId` | CueCardView (posle ConfirmPopup) | ProfileService |
| `OnCueRefillRequested` | `string cueId` | CueCardView, NoEnergyPopup (posle ConfirmPopup) | ProfileService |
| `OnProfileChanged` | `ProfileSnapshot` | ProfileService (i MenuController na Start za početno stanje) | CoinCounterView, SelectedCueView, CuesPopup |
| `OnPurchaseFailed` | `string reason` | ProfileService | ToastView |
| **Settings / UI** (isto kao FlappyBird) | `OnSfxVolumeChanged`, `OnMusicVolumeChanged`, `OnButtonClicked`, `OnPopupShown` |||
| `OnDisabledButtonClicked` | — | UIButton (disabled) | AudioManager (Error) |
| **Tok meča** ||||
| `OnPauseRequested` / `OnResumeRequested` | — | MatchHudView, InputRouter / PausePopup | MatchController |
| `OnForfeitRequested` | — | PausePopup (posle ConfirmPopup) | MatchController |
| `OnMenuRequested` | — | MatchResultPopup | MatchController |
| `OnMatchStarted` | `MatchStartInfo` (ko razbija, cue podešavanja za UI) | MatchController | MatchHudView, TurnBannerView, AudioManager |
| `OnTurnStarted` | `TurnInfo` (Player/Bot, ballInHand, kitchenOnly, legalTargets, groups) | MatchController | MatchHudView, PlayerPanelView, TurnBannerView, ShotControlsView, CameraRig |
| `OnTurnPhaseChanged` | `TurnPhase` | PlayerShotController, BotController | ShotControlsView, CameraRig, AimGuideView |
| `OnGroupsAssigned` | `BallGroup player, BallGroup bot` | MatchController | PlayerPanelView, ToastView |
| `OnBallPocketed` | `int number, int pocketIndex` | MatchController (iz simulacije) | PlayerPanelView, AudioManager, HapticsService, ShotFxView |
| `OnFoul` | `FoulType` | MatchController | ToastView, AudioManager |
| `OnMatchEnded` | `MatchResult` (won, reason, coinDelta, newBalance, newUnlocks) | MatchController | AudioManager, HapticsService (popup se otvara kroz PopupManager) |
| **Kontrole udarca (UI → gameplay)** ||||
| `OnLookDrag` | `Vector2 deltaPx` | TableTouchSurface | PlayerShotController (orbit / aim / tilt zavisno od faze) |
| `OnLookModeToggled` | `bool on` | ShotControlsView | PlayerShotController |
| `OnFineAimDrag` | `float deltaPx` | FineAimWheelView | PlayerShotController |
| `OnCueBallDragged` | `Vector2 screenPos` | TableTouchSurface (u PlaceCueBall fazi) | PlayerShotController |
| `OnPlaceCueBallConfirmed` | — | ShotControlsView ("Done") | PlayerShotController |
| `OnPullChanged` | `float pull01` | PowerStripView | PlayerShotController |
| `OnPullReleased` | — | PowerStripView | PlayerShotController |
| **Udarac (gameplay → UI)** ||||
| `OnReleaseMeterSetup` | `ReleaseMeterSettings` (green/yellow polu-širine, period) | PlayerShotController | ReleaseBarView |
| `OnReleaseMeterChanged` | `float position` (−1..1), `float power01` | PlayerShotController (svaki frejm dok se drži) | ReleaseBarView, PowerStripView |
| `OnShotFired` | `ShotFeedback` (zone: Perfect/Good/Miss, power01, isPlayer) | PlayerShotController, BotController | ToastView, AudioManager, HapticsService, CueView |
| `OnBallCollision` | `float impactSpeed`, `CollisionKind` (Ball/Cushion) | MatchController (iz simulacije, throttle) | AudioManager, HapticsService (samo jaki) |

### 4.5 Bootstrap i persistent sistemi
Kao FlappyBird 4.5. `Systems.prefab`: `AudioManager`, `HapticsService`, `SceneLoader` (+canvas), `InputRouter`, **`ProfileService`**, `PopupManager` (+canvas), `TooltipManager` (+canvas), `EventSystem` (`InputSystemUIInputModule`). Scene nemaju sopstveni EventSystem. Play radi iz bilo koje scene (Match direktno: koristi selektovani štap; ako nema energije u Editoru, dozvoli meč i loguj upozorenje — samo `UNITY_EDITOR`).

### 4.6 Specifikacija sistema

#### 4.6.1 Fizika — `PoolPhysics` (čist C#)
**Zašto ne PhysX:** PhysX nije deterministički između poziva, slabo radi kotrljanje i odbijanje malih brzih kugli, i ne može da se "pusti unapred" za predikciju i bota. Zato: sopstvena **2D simulacija na ravni stola** (x,z), 3D je samo prikaz. Alternativa (PhysX + Rigidbody) je razmotrena i odbačena iz tih razloga.

- Jedinice: metri, sekunde. Sto **8ft**: play area **2.24 × 1.12 m** (duža osa = Z, da se u portrait-u i top view-u prirodno uklapa). Kugla: prečnik **0.05715 m** (R = 0.028575).
- `BallState`: `int Number` (0 = bela, 1–7 solids, 8, 9–15 stripes), `Vector2 Pos`, `Vector2 Vel`, `bool Pocketed`, `float RollAngle` + `Vector3 RollAxis` (samo za prikaz rotacije).
- `TableGeometry`: cushion segmenti (pomereni za R ka unutra, tj. kolizija centra kugle sa linijom), **jaws** (kratki ukošeni segmenti na ulazu svake rupe), 6 rupa (`Vector2 Center`, `float CaptureRadius`: corner 0.058, side 0.062 — početne vrednosti), head string (linija na 1/4 dužine od head kraja, tj. Z = −0.56 m od centra), foot spot (Z = +0.56 m).
- Korak: fiksni **dt = 1/1000 s**, `Step()` u petlji; `Simulate(ShotParams)` vraća `ShotResult` (vidi niže). U meču `MatchController` napreduje simulaciju realnim vremenom (akumulator, max 40 koraka po frejmu) da bi animacija bila 1:1 sa simulacijom.
- Kretanje: usporavanje `a = MatchConfig.RollDecel (0.18 m/s²) + MatchConfig.LinearDamping (0.12/s) · |v|`; stop ispod 0.005 m/s.
- Kugla–kugla: jednake mase, elastičan sudar sa restitucijom 0.95 po normali, razdvajanje preklapanja. Brzi slučaj (break): korak je dovoljno mali (7 m/s · 1 ms = 7 mm < R) — bez CCD-a.
- Kugla–cushion: refleksija normalne komponente sa restitucijom 0.78, tangencijalno trenje 0.1.
- Rupa: centar kugle u `CaptureRadius` → `Pocketed = true`, brzina 0, zabeleži `pocketIndex` (prikaz: kugla upadne i nestane — 4.6.6).
- `ShotResult` (beleži se tokom simulacije, potreban pravilima): `firstContactBall`, `anyCushionAfterContact`, lista `(ballNumber, pocketIndex)` po redosledu, `cueBallPocketed`, konačne pozicije, lista kolizija (za zvuk).
- **Determinizam**: isti ulaz → isti izlaz (proverava se u commitu 2).
- Sve konstante u `MatchConfig` SO. Tuning ciljevi (verifikacija): break pri 100% snage — kugle se umire za **5–9 s**; udarac 30% snage — bela pređe ~1–1.5 dužine stola; nema prolaska kroz cushion ni kroz drugu kuglu u 1000 nasumičnih udaraca.

#### 4.6.2 Pravila — `EightBallRules` (čist C#)
`RulesState`: ko je na potezu, grupe (Open / Solids / Stripes po igraču), `isBreakShot`, `ballInHand`, `kitchenOnly`, kraj meča + razlog.
`Evaluate(RulesState, ShotResult) → RulesOutcome` (sledeće stanje, faul tip, poruke za UI):
1. **Break**: bela iz kuhinje. Ubačene kugle ostaju dole, sto ostaje **Open**, igrač nastavlja ako je nešto ubacio. Scratch na break-u → faul (protivnik ball in hand **bilo gde**). **8 ubačena na break-u → vraća se na foot spot** (ili najbliže slobodno mesto po liniji), bez pobede/poraza; ako je uz to scratch → faul.
2. **Open table** posle break-a: prva legalno ubačena kugla (bez faula) dodeljuje grupu igraču (ako su ubačene obe vrste — grupa prve ubačene). Udaranje 8 kao prve kugle na open table = faul.
3. **Faulovi** (→ protivnik dobija **ball in hand bilo gde**): bela u rupi; bela ne dotakne nijednu kuglu; prva dodirnuta kugla nije iz igračeve grupe (ili 8 pre nego što je grupa očišćena); posle prvog kontakta nijedna kugla nije udarila u cushion **i** ništa nije ubačeno.
4. Igrač **nastavlja** potez ako je legalno ubacio bar jednu kuglu svoje grupe (na open table: bilo koju osim 8) bez faula. Inače potez prelazi na protivnika.
5. **8-ball**: ako je grupa očišćena i 8 legalno ubačena bez faula → **pobeda**. 8 ubačena pre čišćenja grupe → **poraz**. 8 ubačena uz faul (npr. scratch) → **poraz**.
6. Kugle protivnika ubačene tokom tvog udarca ostaju dole (računaju se protivniku).
7. Razlozi kraja (engleski, za popup): "You pocketed the 8-ball!", "Opponent pocketed the 8-ball.", "You pocketed the 8-ball too early.", "Opponent scratched on the 8-ball.", "You forfeited." itd.

`RackBuilder.Build(System.Random)`: trokut sa vrhom na foot spot-u, 8 u centru, u zadnjim uglovima jedna solid i jedna stripe, ostalo nasumično; kugle razmaknute 0.1 mm da break bude stabilan.

#### 4.6.3 Predikcija putanje — `ShotPredictor` (čist C#)
Analitički (ne pokreće celu simulaciju, da bi bio brz svaki frejm):
- Swept circle bele duž pravca nišana: prvi presek sa drugom kuglom (krug radijusa 2R) ili cushion-om.
- Ako je prva kugla: `ghostPos` (pozicija bele u trenutku kontakta), `objectDir = normalize(objCenter − ghostPos)`. Vraća: tačku kontakta, pogođenu kuglu, da li je **legalna meta** za trenutnog igrača.
- Ako je prvo cushion: tačka udara + odbijeni pravac (jedan odraz), pa ponovi swept test za jedan segment (max 1 odraz u prikazu).
- `AimGuideView` crta:
  - **Bela linija** od bele do ghost-a (puna, blago providna, sa tačkicama koje sporo teku ka meti — UX "živa" linija).
  - **Ghost ball** prsten na mestu kontakta.
  - **Linija pogođene kugle** dužine `cue.ObjectGuideLength` od centra pogođene kugle u `objectDir` (osnovni štap = **2 prečnika**).
  - Ako je meta **ilegalna**: prsten i linija crveni + mali "✕" i toast-hint jednom po potezu "Hit your own group first" / "Hit the 8-ball only after clearing your group".
- Linije su `LineRenderer` sa unlit materijalom koji se crta iznad čoje (depth offset), širina ~6 mm.

#### 4.6.4 Preciznost udarca — `ShotAccuracy` (čist C#)
- Bar pozicija `p ∈ [−1, 1]` (0 = centar). Kretanje **linearno ping-pong** (konstantna brzina → fer i predvidljivo): pun ciklus (−1 → 1 → −1) traje `cue.OscillationPeriod` sekundi. Počinje od **donjeg kraja** (−1) kad pull pređe 5%, tako da prvi prolaz kroz zeleno dođe posle ~¼ perioda (igrač ima vremena da namesti snagu).
- Zone po `|p|`: **Green** `≤ cue.GreenHalf`; **Yellow** `≤ cue.GreenHalf + cue.YellowWidth`; ostalo **Red**.
- Ugaona greška (iz `MatchConfig`):
  - Green → **0°** (kugla ide tačno po nišanu). Toast **"PERFECT!"** (zeleno).
  - Yellow → linearno **0.4° → 1.6°** po dubini u zoni. Toast **"GOOD"** (žuto).
  - Red → linearno **2.0° → 5.0°**. Toast **"MISS"** (crveno).
  - **Znak greške** = znak `p` (gore = levo, dole = desno), da igrač nauči čitati bar; ugao se primenjuje na pravac bele.
  - Red zona dodatno menja snagu ±12% (nasumično, `System.Random`) — loš release je osetan.
- Snaga: `speed = Lerp(MinSpeed 0.35, MaxSpeed 6.5, pull01^1.6)` m/s (kriva daje fine kontrolisane meke udarce). Break koristi isti gest; `MaxSpeed` je isti.
- Ista funkcija se koristi za bota (4.6.8) — bot nema "skrivenu" preciznost.

#### 4.6.5 Ekonomija i inventar — `CueCatalog` + `ProfileService`
`CueDefinition` (u `CueCatalog.asset`, `DataAssetBuilder` pravi asset):

| # | Id / Name | Unlock (wins) | Price | Max energy | Refill (full) | GreenHalf | YellowWidth | Period (s) | Object guide (prečnika) | Boje |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `rookie` / **Rookie Cue** | 0 | 0 (owned) | **∞** | — | 0.10 | 0.30 | 1.30 | **2** | svetlo drvo |
| 2 | `club` / **Club Cue** | 1 | 30 | 8 | 5 | 0.13 | 0.30 | 1.45 | 4 | tamno drvo + crvena traka |
| 3 | `pro` / **Pro Cue** | 3 | 70 | 10 | 8 | 0.16 | 0.30 | 1.60 | 6 | crno + zlatna traka |
| 4 | `master` / **Master Cue** | 6 | 140 | 12 | 12 | 0.19 | 0.28 | 1.80 | 9 | plava + srebrna |
| 5 | `legend` / **Legend Cue** | 10 | 260 | 15 | 18 | 0.22 | 0.26 | 2.00 | 13 | zlatna, blagi emisivni sjaj |

Obrazloženje: Win +10 / Loss −1 → Club posle ~2–3 pobede, Legend je dugoročan cilj (~25 pobeda ukupno). Refill = trošak ~½ pobede, pa je energija primetna ali ne frustrira. Vrednosti su **početne**; balans proverava `EconomyChecks`/`BotBalanceCheck`.

Stanja štapa (izračunava `ProfileService`, šalje se kao `CueViewData`):
- **Locked**: `wins < WinsToUnlock`. Kartica zatamnjena, lock ikonica, tekst **"Win 3 matches to unlock (1/3)"** + progress traka.
- **Unlocked (not owned)**: dugme **"BUY · 🪙 70"**; ako nema dovoljno novčića, dugme je disabled, cena crvena, mali tekst "Need 12 more coins".
- **Owned**: dugme **"SELECT"**; selektovani ima zeleni okvir, bedž **"SELECTED"**, bez dugmeta.
- Energija (samo owned, ne-∞): ikonica munje + traka **"6/10"** + dugme **"REFILL · 🪙 8"** pored (disabled ako je puna ili nema novca). Rookie prikazuje **"∞"**.

`ProfileService` (`SingletonPersistent`, jedini vlasnik PlayerPrefs profila):
- API za controllere: `ProfileSnapshot GetSnapshot()`, `bool CanStartMatch(out string reason)`, `void ConsumeEnergyForMatch()`, `MatchReward ApplyMatchResult(bool won)` (vraća coin delta, novi balans i listu **novo otključanih** štapova).
- Sluša `OnCueSelectRequested/BuyRequested/RefillRequested`, validira (dovoljno novca, stanje), menja stanje, `PlayerPrefs.Save()`, diže `OnProfileChanged(snapshot)` ili `OnPurchaseFailed`.
- Balans nikad < 0. Selekcija samo owned štapa. Kupovina ne selektuje automatski — **pita**: posle kupovine kartica pokaže "SELECT" sa kratkim highlight-om (pulse) + toast "Club Cue purchased!".

#### 4.6.6 Prikaz stola — `TableView`, `BallView`, `CueView`
- `TablePrefabBuilder` pravi `Table.prefab` iz primitiva: čoja (plane, materijal Felt sa generisanom suptilnom noise teksturom), cushion-i (izduženi box-ovi tačno po `TableGeometry`, malo viši od kugle), drveni okvir, 6 rupa (crni cilindri / kratki tamni "levak" ispod nivoa čoje, rupe u okviru se postižu tako što se okvir sastavi iz segmenata oko rupa), **dijamanti** na okviru (mali beli quad-ovi — pomažu orijentaciju), noge stola, pod (tamni), zid nije potreban (tamna pozadina kamere).
- Osvetljenje: jedno **toplo Spot/Area-like svetlo iznad stola** (URP Spot sa mekom senkom) + slab ambient; post-processing: blaga vinjeta i bloom (Volume, Mobile-friendly). Cilj je "bilijar sala" atmosfera uz 60 FPS na srednjem telefonu.
- `BallView`: sfera (Unity sphere mesh, prečnik 0.05715), materijal po broju (5.3), rotacija iz `RollAxis/RollAngle` (kotrljanje bez klizanja: ugao += |v|·dt / R oko ose ⟂ na v). Upad u rupu: DOTween — pomeri ka centru rupe + spusti 0.08 m + skaliraj na 0.8 (0.25 s), pa sakrij.
- `CueView`: proceduralni mesh štapa (lathe: vrh 12 mm → kraj 30 mm, dužina 1.45 m, segmenti boje po `CueDefinition`). Postavljen iza bele duž nišana, blago podignut (5°). Pull: pomera se nazad do **0.28 m** pri pull01 = 1. Udar: brz potez napred (0.06 s, `InQuad`), pa nestane (fade/skriven) dok se kugle kreću, vraća se na početku sledećeg poteza.

#### 4.6.7 Kamera — `CameraRig`
Perspektivna kamera, vertikalni FOV **55°** (portrait), smooth prelazi DOTween/SmoothDamp 0.45 s između režima:
| Režim | Kada | Opis |
|---|---|---|
| **Aim (first person)** | korisnikov potez, nišanjenje | Iza bele u pravcu nišana: rastojanje 0.55 m, visina 0.22 m iznad kugle, pitch ~18°. Vertikalni drag menja **tilt** između "nisko" (0.12 m, 10°, precizno) i "visoko" (0.55 m, 35°, pregled). Pogled se rotira oko bele zajedno sa nišanom. |
| **Look around** | dugme "Look" (ikonica oka) | Kruži oko centra stola na visini stojećeg čoveka (1.55 m iznad poda, sto 0.8 m) na radijusu ~1.9 m, gleda u centar stola. Horizontalni drag = kruženje (360°), vertikalni = pitch (25°–80°, do skoro top-down). Dugme "Back to shot" vraća u Aim. |
| **Place cue ball** | ball in hand | Top-down (malo iskošeno 75°), ceo sto u kadru (sto vertikalno u portrait-u). |
| **Shot follow** | posle udarca (oba igrača) | Glatko se podiže na visoki "broadcast" pogled (pitch ~60°) iznad stola, blago prati belu dok se kreće, ceo sto vidljiv. |
| **Opponent** | bot na potezu | Pogled sa strane stola sa koje bot gađa (stojeći ugao iza bele bota, visina ~1.0 m iznad stola, pitch ~40°) — korisnik vidi štap bota kako nišani. |
Kamera nikad ne ulazi u sto/okvir (clamp radijusa i visine).

#### 4.6.8 Bot — `BotBrain` (čist C#) + `BotController` (MonoBehaviour)
`BotBrain.ChooseShot(RulesState, BallState[], BotProfile, System.Random) → BotDecision` (pravac, snaga, opciono pozicija bele za ball in hand):
1. **Kandidati**: za svaku legalnu metu × 6 rupa: ghost ball tačka; odbaci ako je putanja bela→ghost ili meta→rupa blokirana (provera koridora širine 2R) ili je cut angle > 75°.
2. **Ocena**: `score = w1·cos(cut) − w2·dist(bela,meta) − w3·dist(meta,rupa) + w4·ugao_ulaska_u_rupu`; snaga iz potrebne brzine da meta stigne do rupe + margina (formula iz modela usporavanja).
3. **Provera simulacijom**: top 5 kandidata se simuliraju **bez greške** u `PoolPhysics` (kopija stanja); bira se najbolji koji ubacuje metu i nema scratch/faul. Ako nijedan → **safety**: najmekši udarac koji legalno dodirne svoju kuglu i neku kuglu pošalje u cushion (pa tek onda nasumičan legalan kontakt).
4. **Ball in hand**: uzorkuj ~60 pozicija bele (grid + random, u kuhinji ako `kitchenOnly`), uzmi poziciju sa najboljim kandidatom.
5. **Break**: pravac ka vrhu trokuta ± mala varijacija, snaga 95–100%.
6. **Greška (ključ balansa)**: bot "release-uje" kroz istu `ShotAccuracy` funkciju: uzorkuj zonu po `BotProfile` (**Green 45% / Yellow 40% / Red 15%**), poziciju unutar zone uniformno, i primeni grešku kao igrač sa parametrima **Club Cue** (bot ne zavisi od štapa korisnika). Zbog toga igrač koji često pogađa zeleno dobija, a onaj koji često promašuje gubi.

`BotController` (tajming poteza, **~10 s** ukupno pre udarca, sve u `BotProfile`):
- 0 s: banner **"OPPONENT'S TURN"**, kamera → Opponent režim, PlayerPanel bota svetli + kružni tajmer/pulsiranje.
- 0–3.5 s (±0.7): "thinking" — štap bota polako "traži" (rotira se između 2–3 kandidata), mali tekst "Opponent is thinking…" sa animiranim tačkama. Ako ima ball in hand: bela se vidno pomeri na izabranu poziciju (0.6 s).
- 3.5–6.5 s: nišani — štap se smiri na izabrani pravac (mikro korekcije).
- 6.5–8.5 s: povlači štap nazad srazmerno snazi, kratka pauza.
- ~8.5–9.5 s: udarac (`OnShotFired`, isPlayer=false, toast kvaliteta bota se **ne** prikazuje — samo zvuk).
- Bot ne prikazuje guide linije (korisnik ne dobija besplatnu predikciju za tuđi udarac).
- Pauza zaustavlja tajmer bota (koristi skalirano vreme).

#### 4.6.9 Popupi i ekrani (UI)
Svi popupi nasleđuju `PopupBase` (FlappyBird animacija), dugmad su `UIButton` varijante.
- **SettingsPopup** — isti kao FlappyBird (Sound Effects, Music, Close). Koristi se u Menu i iz PausePopup-a.
- **CuesPopup** (inventar) — full-height panel "CUES", gore balans novčića (`CoinCounterView`), vertikalni `ScrollRect` sa **5 `CueCardView`** kartica (sortirane po redu iz kataloga). Kartica (visina ~300 ref px): slika štapa (generisani sprite iz mesh boja — 5.3) horizontalno, ime, dve stat trake sa ikonicama i **TooltipTrigger**-om: "Aim Line" (dužina guide-a) i "Release Window" (green+yellow širina / sporost), energija + REFILL, i akciono dugme po stanju (4.6.5). Payload: `CuesPopupData(ProfileSnapshot)`; dok je otvoren, sluša `OnProfileChanged` i re-renderuje **bez** zatvaranja (animiraj samo promenjene vrednosti — count-up novčića, fill energije).
- **ConfirmPopup** — generički: naslov, tekst, ikona + cena, "Confirm"/"Cancel". Payload: `ConfirmPopupData(title, body, confirmLabel, Action onConfirm)`. Koristi se za Buy, Refill, Forfeit.
- **NoEnergyPopup** — kad Play, a selektovani štap ima 0 energije: "Your Club Cue is out of energy." + dugmad **"REFILL · 🪙 5"** (disabled ako nema novca) i **"CHANGE CUE"** (otvara Cues) + Close.
- **PausePopup** — Resume, Settings, Forfeit (Forfeit → ConfirmPopup "Forfeit the match? This counts as a loss (−1 coin)."). Back = Resume.
- **MatchResultPopup** — veliki naslov **"YOU WIN!"** (zlatno, konfete — jednostavne UI čestice) ili **"YOU LOSE"** (prigušeno crveno), razlog (4.6.2/7), coin red "🪙 +10" sa count-up animacijom balansa, ako je nešto **novo otključano** — kartica "New cue unlocked: Pro Cue!" sa dugmetom koje vodi na Cues; dugmad **"PLAY AGAIN"** (prolazi istu proveru energije) i **"MENU"**. `_closeOnBack = false`.

**MainMenuView (Menu scena)**:
- Pozadina: 3D sto sa složenim trouglom, kamera polako kruži (`MenuTableOrbit`), zamućeno/zatamnjeno da UI bude čitljiv.
- Gore desno: `CoinCounterView` (ikonica novčića + broj; count-up kad se menja).
- Naslov **"BILLIARDS"** + podnaslov "8-Ball Duel", blagi bob (`IdleMotion`).
- `SelectedCueView`: mala kartica sa trenutnim štapom (ime + energija "∞" ili "6/10"); tap otvara Cues.
- Tri velika dugmeta vertikalno: **PLAY** (idle pulse), **SETTINGS**, **CUES**. Verzija u uglu.

**MatchHudView (Match scena)**:
- Gore: dva `PlayerPanelView` — levo **YOU**, desno **OPPONENT**; svaki ima ikonicu, 7 malih kuglica svoje grupe (ubačene postaju sive; dok je sto Open, prikazuje "?" kuglice), i aktivni igrač ima **svetleći okvir + strelicu**. Između njih mala 8-ball ikonica koja zasvetli kad je igraču ostala samo 8.
- Gore desno ispod panela: Pause dugme (ikonica).
- `TurnBannerView`: na početku svakog poteza traka preko sredine ekrana klizi unutra/van (0.8 s): **"YOUR TURN"** (zeleno) / **"OPPONENT'S TURN"** (narandžasto) / **"BALL IN HAND"** (plavo, posle faula protivnika). Faul toast: **"FOUL! Cue ball scratched"**, **"FOUL! Wrong ball first"**, **"FOUL! No ball hit"**, **"FOUL! No rail after contact"**.
- `ToastView`: kratke poruke (Perfect/Good/Miss, "You are SOLIDS", "Table is open" itd.), max 1 istovremeno + red čekanja.
- `ShotControlsView` (vidi 4.7) vidljiv samo na korisnikovom potezu.

#### 4.6.11 Prikaz preostalih kugli (R18)
- **`PlayerPanelView`** (YOU i OPPONENT, gore): 7 slotova sa **pravim mini ikonicama kugli** (isti generisani izgled i brojevi kao 3D kugle, 5.3 — UI sprite po kugli 96×96).
  - **Open table**: slotovi prikazuju sive "?" kuglice + tekst "Open table".
  - **Posle dodele grupe**: slotovi se popune kuglama grupe (solids 1–7 / stripes 9–15) sa kratkom animacijom (kuglice "upadnu" u slotove jedna za drugom, 0.05 s razmak) + tekst "SOLIDS" / "STRIPES".
  - **Ubačena kugla** (`OnBallPocketed`): njen slot postaje siv/providan sa kvačicom i mali pop (`DOPunchScale`); važi i kad protivnik slučajno ubaci tvoju kuglu.
  - **Brojač** "4 left" pored slotova.
  - **Kad je grupa očišćena**: slotovi se smanje, a 8-ball ikonica pored imena igrača zasvetli i pulsira + toast "Pocket the 8-ball to win!".
- **Ladica ubačenih kugli** (`ShotFxView` + mali 3D/UI tray): ubačene kugle se prikazuju u redu sa strane ekrana (ili kao 3D žleb ispod stola koji kamera u Look režimu vidi) — redosled ubacivanja; daje osećaj napretka. Implementacija: UI red ikonica dole iznad kontrola, 15 slotova, kugla doleti iz pozicije rupe (world→screen) do slota (0.4 s, `OutCubic`).
- Podaci dolaze isključivo kroz evente (`OnTurnStarted.TurnInfo` nosi trenutno stanje grupa i ubačenih kugli, `OnGroupsAssigned`, `OnBallPocketed`) — UI ne čita `RulesState`.
- Ako se 8 vrati na foot spot (break), ništa se ne beleži kao ubačeno.

#### 4.6.10 Tok meča — `MatchController`
Stanja: `Intro → Turn(Player|Bot) → Simulating → Resolving → (Turn | Ended)`.
1. **Ulazak u scenu**: `ProfileService.ConsumeEnergyForMatch()`, postavi `PendingMatch = 1`, napravi rack, coin flip animacija (~1.2 s) ko razbija, `OnMatchStarted`.
2. **Turn**: `OnTurnStarted(TurnInfo)`; korisnik → `PlayerShotController` aktivan; bot → `BotController`.
3. **Simulating**: po `OnShotFired` pokreni simulaciju u realnom vremenu, diži `OnBallPocketed`/`OnBallCollision`; input blokiran; kamera Shot follow.
4. **Resolving**: `EightBallRules.Evaluate`, 8 vrati na foot spot ako treba, poruke/faulovi, `OnGroupsAssigned` ako su dodeljene; pauza 0.6 s da korisnik vidi rezultat; sledeći potez ili kraj.
5. **Ended**: `ProfileService.ApplyMatchResult(won)`, `PendingMatch = 0`, posle 1.0 s `PopupManager.Show<MatchResultPopup>(payload)`.
6. Pauza: `OnPauseRequested` (u bilo kom stanju osim Ended) → `timeScale = 0`, simulacija i bot staju, PausePopup. Auto pauza na `OnApplicationPause(true)`.
7. Forfeit → Ended sa porazom. Menu/Play Again kroz `SceneLoader`.

### 4.7 UX udarca (najvažniji deo igre)

#### 4.7.1 Faze korisnikovog poteza (`TurnPhase`)
`PlaceCueBall` (samo ako ball in hand) → `Aim` (⇄ `Look`) → `Pulling` → (udarac) → kraj poteza.

#### 4.7.2 Raspored kontrola (portrait, ref 1080×1920, safe area)
```
┌───────────────────────────────┐
│ [YOU ●●●●○○○]  8  [OPP ●●●●●○○] [II] │  ← PlayerPanels + Pause
│                               │
│        3D pogled (Aim)        │
│   drag ↔ = nišan, drag ↕ = tilt│
│ ┃                         ║   │
│ ┃ fine     [release bar] ║   │  ← levo: Fine Aim wheel; desno: Power strip (štap)
│ ┃ aim      (samo dok se  ║   │
│ ┃ wheel     povlači)     ║   │
│ ┃                         ║   │
│ [👁 Look]                     │  ← dole levo
└───────────────────────────────┘
```
- **`TableTouchSurface`** (providna full-screen Image, iza kontrola; obrazac kao FlappyBird `TapCatcher`): `IDragHandler` → `OnLookDrag(delta)`; u `PlaceCueBall` fazi → `OnCueBallDragged(screenPos)`. Pokriva i miš i dodir.
- **Aim**: horizontalni drag rotira nišan oko bele: **0.12°/px** (ref px), sa "magnetizmom" — kad je ghost-ball tačno na liniji pune lopte (cut 0°) na legalnu metu, blago se uhvati (±0.3°) i kratko vibrira light haptic. Na početku poteza nišan je automatski usmeren na **najlakšu legalnu kuglu** (koristi `BotBrain` ocenu bez greške) — korisnik odmah vidi smislen udarac.
- **Fine Aim wheel** (levo, vertikalna "točkić" traka sa crticama koje se pomeraju): **0.01°/px** za preciznu korekciju. Na Editoru i A/D / ←/→ strelice rade fine aim (0.05° po frejmu).
- **Power strip** (desno, ~170 ref px širok, ~1000 visok): prikaz štapa koji gleda nagore. Korisnik stavi prst **bilo gde na traci i vuče nadole** = povlačenje štapa: `pull01 = clamp01(dragDown / 800 ref px)`. Prikaz: štap u traci se spušta, broj snage "64%" iznad, 3D štap se vidno povlači nazad. 
- **Release bar** se pojavljuje (fade 0.1 s) čim `pull01 > 0.05`: vertikalna traka levo od power strip-a, ~700 ref px visoka, **zeleno u sredini, žuto iznad i ispod, crveno na krajevima** (veličine zona po štapu — bolji štap ima vidljivo širu zelenu i žutu), indikator (bela horizontalna crta sa senkom) osciluje gore-dole. Zelena zona blago pulsira kad je indikator u njoj.
- **Pusti prst** → udarac (pull01 u tom trenutku = snaga, pozicija indikatora = preciznost). **Otkazivanje**: vrati prst gore dok `pull01 < 0.05` i pusti → nema udarca, bar nestaje, toast "Shot cancelled".
- Dok se povlači, nišan je zaključan (drag po stolu ne menja pravac) — sprečava slučajne pomeraje.
- Feedback udarca: toast PERFECT/GOOD/MISS u boji zone, zvuk udarca štapa jačine po snazi, haptic (Light < 40% snage, Medium < 80%, Strong iznad), na Perfect dodatno "ding". Ako je Miss, guide linija namere ostane 0.6 s providno vidljiva pored stvarne putanje — korisnik vidi koliko je otišlo u stranu (uči sistem).

#### 4.7.3 Look around
Dugme **"👁 LOOK"** dole levo → Look režim (guide linije sakrivene, kontrole udarca sakrivene, dugme postaje **"↩ BACK TO SHOT"**). Drag = kruženje/pitch (4.6.7). Tap na kuglu u Look režimu → automatski nišan ka toj kugli i povratak u Aim (brz način da se izabere meta).

#### 4.7.4 Ball in hand
- Kamera u top-down; bela kugla ima **pulsirajući prsten + ikonicu ruke** iznad.
- Drag bilo gde po stolu pomera belu (raycast na ravan čoje → `TableGeometry` clamp unutar cushion-a). Ghost bele je **zelen** kad je pozicija validna, **crven** kad se preklapa sa kuglom ili je van dozvoljene zone; na pustanje nevalidne pozicije bela se vrati na poslednju validnu.
- Na break-u (`kitchenOnly`): kuhinja je osenčena svetlijim slojem + tekst "Place the cue ball behind the line".
- Dugme **"DONE"** (dole sredina) → Aim faza. Ball in hand se može menjati sve do početka povlačenja: u Aim fazi dugme **"✋ MOVE BALL"** vraća u PlaceCueBall.

#### 4.7.5 Brojevi na jednom mestu (`MatchConfig` + `CueDefinition`)
Sve vrednosti iz 4.6.1, 4.6.4, 4.6.7, 4.7.2 i tajming bota žive u SO-ovima, ne u kodu. Inspector je jedino mesto za tuning.

### 4.8 Persistencija (`Core/SaveKeys.cs`)
`Coins` (int, default 10), `Wins` (int), `Losses` (int), `CueOwned_<id>` (int 0/1; rookie implicitno 1), `CueEnergy_<id>` (int; default = MaxEnergy pri kupovini), `SelectedCue` (string, default `rookie`), `PendingMatch` (int 0/1), `SfxVolume`, `MusicVolume`.
- `PendingMatch == 1` pri boot-u (`ProfileService.Awake`) → primeni poraz (−1) i obriši flag; na Menu sceni prikaži toast "Last match was abandoned (−1 coin)".
- PlayerPrefs čitaju/pišu **samo** `ProfileService` i `AudioManager`.

### 4.9 Konvencije koda
Iste kao FlappyBird 4.9 (`[SerializeField] private _camelCase`, `OnX`/`HandleX`, `SetLink`, `SetUpdate(true)` za UI tweenove, bez `Find*` u runtime-u, kešovan `GetComponent`). `Pool/*` klase su `sealed`, bez alokacija u `Step()` (prealocirani nizovi) — predikcija se računa svaki frejm.

---

## 5. Art i audio

### 5.1 Kenney (CC0) — iskoristi postojeće download-e iz `_kenney_downloads/`
| Paket | Za šta |
|---|---|
| UI Pack | dugmad (normal/pressed/disabled), paneli (9-slice), slider, scroll, Kenney Future font |
| Game Icons | pause, gear, home, return, audio/music, lock, eye (look), hand (ako postoji), star, trophy |
| Interface Sounds / UI Audio | click, popup open, whoosh, toast, confirm, error (purchase failed) |
| Impact Sounds | kugla–kugla klik (`impactGlass_light`/`impactPlate_light` — izaberi najsličniji bilijarskom kliku), cushion (`impactSoft`), udar štapa (`impactWood_light`), upad u rupu (`impactWood_medium` niže pitch) |
| Digital Audio / Casino Audio (skini ako treba) | coin count-up, kupovina |
| Music Jingles (skini: https://kenney.nl/assets/music-jingles) | win i lose jingle |
Za nove pakete — download procedura identična FlappyBird 5.2 (zip URL iz HTML-a stranice, kopiraj samo korišćene fajlove + `License.txt`). Ikonice koje Kenney nema (novčić, munja/energija, ruka) → generiši (5.3). `CREDITS.md` u root-u.

### 5.2 Muzika
1. Pretraži OpenGameArt (https://opengameart.org, filter **License: CC0**) za "lounge", "jazz", "smooth" loop, 1–3 min, ogg/mp3. Prihvati **samo** ako stranica eksplicitno kaže **CC0**. Zabeleži naslov, autora, URL i licencu u `CREDITS.md`. Download u `_kenney_downloads/music/` pa kopiraj u `Assets/_Audio/Music/`.
2. Ako u ~15 min ne nađeš verifikovan CC0 track ili download ne radi → **fallback**: `GeneratedArtBuilder` generiše `music_lounge_loop.wav` (prilagodi FlappyBird `MusicGenerator`): ~85 BPM, swing, ii–V–I progresija (npr. Dm7–G7–Cmaj7–A7), walking bass (sinus + blagi attack), meki akordi (električni klavir: sinus + 2. harmonik sa brzim decay-om), četkica na doboš (filtrirani noise), 16 taktova, seamless loop, peak −6 dBFS. Javi korisniku koju varijantu si uzeo.
3. Import: Vorbis, Streaming / Compressed In Memory.

### 5.3 Generisano u Editor C# (`GeneratedArtBuilder`)
- **Kugle**: equirectangular `Texture2D` 512×256 po kugli: solids = puna boja + beli krug sa brojem na "prednjoj" i "zadnjoj" strani; stripes = bela sa obojenom trakom (±30° geografske širine) + beli krug sa brojem; bela = čista bela sa jednom malom crvenom tačkom (da se vidi rotacija); 8 = crna. Brojevi: ugrađeni **bitmap font cifara** (npr. 5×7 matrica skalirana i antialiasovana) — bez spoljnih fontova. Standardne boje: 1 žuta, 2 plava, 3 crvena, 4 ljubičasta, 5 narandžasta, 6 zelena, 7 bordo, 8 crna (stripes 9–15 isti redosled). Materijali: URP Lit, smoothness 0.85.
- **Čoja**: 256×256 zelena sa blagim noise-om (tileable). **Drvo**: 256×256 proceduralna "zrna". 
- **Ikonice**: novčić (zlatni krug sa ivicom i senkom), munja (žuta), ruka (jednostavna silueta ako Kenney nema) — 128×128 PNG.
- **Sprite štapova za UI** (5×, 1024×64): horizontalni štap u bojama iz `CueDefinition`.
- Deterministički (fiksni seed), idempotentno.

### 5.4 `SoundId`
`Click, PopupOpen, Whoosh, Toast, Error, CueHit, BallClick, Cushion, Pocket, Foul, Perfect, CoinTick, Purchase, Win, Lose, TurnStart`.
`BallClick`/`Cushion` volumen = `clamp01(impactSpeed / 3 m/s)`, pitch 0.95–1.08 random, throttle max 1 zvuk po 25 ms (break bi inače bio kakofonija).

---

## 6. Android

### 6.1 Player Settings (`ProjectSettingsBuilder`, commit 1)
| Podešavanje | Vrednost |
|---|---|
| Company / Product | `Mateja Milenkovic` / `Billiards` |
| Package Name | `com.matejamilenkovic.billiards` |
| Version / Version Code | `0.1.0` / `1` |
| Orientation | **Portrait**, bez auto-rotacije |
| Min API | **26** |
| Target API | Automatic |
| Scripting Backend / Arch | IL2CPP / ARM64 |
| Active Input Handling | Input System (proveri) |
| Build target | **Android** (switch u commitu 1 — `EditorUserBuildSettings.SwitchActiveBuildTarget`, traje; `--detach` + `job wait`) |
| Quality / URP | Android koristi `Mobile_RPAsset`; MSAA 2x, soft shadows uključene samo za glavno svetlo, HDR off ako bloom radi i bez njega (proveri FPS) |
| Build scenes | `Menu` (0), `Match` (1) |
| targetFrameRate | 60 (Bootstrap) |

### 6.2–6.4 Build, provera APK-a, test na uređaju
Identično FlappyBird 6.2–6.4 (`AndroidBuilder.Build()` → `Builds/Android/Billiards.apk`, `aapt dump badging/permissions` — mora `minSdk 26` i `android.permission.VIBRATE`, instalacija preko `adb` ako je uređaj povezan, `logcat -s Unity`). Osećaj kontrola, haptic i čitljivost na ekranu potvrđuje **korisnik**.

---

## 7. Git
- Pre prvog commita: `.gitignore` za Unity (Library, Temp, Obj, Logs, UserSettings, Builds, `*.csproj`, `*.sln`, `.idea/`, `.vscode/`, `.DS_Store`, `*.apk`) — uzmi FlappyBird `.gitignore` kao osnovu.
- `git remote add origin git@github.com:matejamilenkovic01/Billiards.git`; ako repo ne postoji ili push ne prođe → **stani i javi**, nikad force push.
- Svaki asset ima `.meta`; nema siročadi `.meta`.
- Obe grep provere iz 4.3 prazne.
- Poruka: `<oblast>: <kratak opis>` + lista stavki + `Co-Authored-By` linija ako harness zahteva.
- Ovaj dokument ide u prvi commit.

---

## 8. Verifikacioni alati (`Editor/Checks/*`, pokreću se kroz `eval`)
| Check | Šta proverava |
|---|---|
| `PhysicsChecks.Determinism()` | isti break 2× → identične krajnje pozicije (bit-exact) |
| `PhysicsChecks.NoTunneling(1000)` | 1000 nasumičnih udaraca: nijedna kugla van play area, nijedno preklapanje > 0.5 mm na kraju |
| `PhysicsChecks.Timings()` | break 100% → umirenje 5–9 s; udarac 30% → pređeni put bele u ciljanom opsegu |
| `PhysicsChecks.Collisions()` | (a) pun udarac u mirnu kuglu → bela skoro stane, meta nastavi ~95% brzine; (b) cut 30° → uglovi posle sudara ≈ 90° razmak; (c) udarac u cushion pod 45° → odbijanje pod ≈ 45° sa manjom brzinom; (d) kugla usmerena u ivicu rupe (jaw) se odbije, kugla u centar rupe upadne |
| `PhysicsChecks.StraightPot()` | bela–meta–rupa u liniji, Green release → meta ulazi u 100% od 50 udaraca na različitim rastojanjima |
| `RulesChecks.All()` | ručno složeni `ShotResult` scenariji za svako pravilo iz 4.6.2 (scratch, wrong first, no rail, 8 early, 8 + scratch, 8 na break-u, open table dodela, nastavak poteza) |
| `PredictorChecks.MatchesSimulation()` | za 200 nasumičnih nišana: ghost tačka i `objectDir` iz `ShotPredictor` odgovaraju prvom kontaktu iz `PoolPhysics` (tolerancija 1 mm / 0.5°) |
| `BotBalanceCheck.Run(200)` | headless mečevi bot vs. "simulirani igrač" (isti `BotBrain`, različita release distribucija): **Dobar igrač** (Green 75 / Yellow 20 / Red 5) pobeđuje **≥ 70%**; **Loš igrač** (Green 20 / Yellow 45 / Red 35) pobeđuje **≤ 30%**; **Isti kao bot** ~50% (±8%). Ako nije u opsegu → tuning `BotProfile`/zona, pa ponovo. Prijavi brojeve korisniku. |
| `EconomyChecks.All()` | start 10; win +10; loss −1 ne ide ispod 0; buy bez novca odbijen; buy locked odbijen; select neposedovanog odbijen; refill pun/∞ odbijen; energija −1 po meču, rookie ostaje ∞; unlock pragovi po pobedama; PendingMatch → poraz pri boot-u |

---

## 9. Plan commitova (7)

### Commit 1 — `setup: project settings, Android target, assets, core systems`
**Obim**: provera preduslova (2.2), `.gitignore`, remote, brisanje SampleScene/TutorialInfo/Readme, struktura foldera, `ProjectSettingsBuilder` (6.1 + switch na Android), kopiranje Core/Systems iz FlappyBird-a (4.1) uz prilagođen `GameEvents` (svi eventi iz 4.4), `SaveKeys`, `ProfileService` (logika + PlayerPrefs, bez UI), Kenney import + `ArtImportBuilder`, `GeneratedArtBuilder` (kugle + UI sprite-ovi kugli 96×96, čoja, drvo, ikonice, cue sprite-ovi), muzika (5.2), `DataAssetBuilder` (`CueCatalog`, `MatchConfig`, `BotProfile` asseti), `SystemsPrefabBuilder` (bez UI sistema), prazne `Menu`/`Match` scene, `CREDITS.md`.
**Verifikacija**: `unity recompile` 0 grešaka; Player Settings kroz `eval`; Play prazne scene → Systems postoji, muzika svira; `EconomyChecks.All()` prolazi; screenshot generisanih tekstura kugli (sve 16 u gridu).

### Commit 2 — `pool core: physics, table, rack, prediction`
**Obim**: `Pool/*` (fizika, geometrija, rack, predictor, accuracy), `TablePrefabBuilder` (sto, kugle, štap, svetlo, volume), `TableView`/`BallView`/`CueView`, privremeni `DebugShot` (samo Editor, `eval` ispaljuje udarac) u `MatchSceneBuilder` verziji bez UI-ja.
**Verifikacija**: `PhysicsChecks.*` (uključujući `Collisions`) i `PredictorChecks` prolaze (prijavi brojeve); Play + `eval` break → kugle se razlete i umire, kugle se vizuelno kotrljaju i upadaju u rupe; screenshot pre i posle break-a; FPS u Editoru stabilan.

### Commit 3 — `rules & bot: 8-ball rules, match flow, bot AI`
**Obim**: `EightBallRules`, `RulesState`, `MatchController` (bez popupa), `BotBrain`, `BotController` sa tajmingom, `BotBalanceCheck`, privremeno: "bot vs bot" mod za Editor (`eval` ga pali) koji igra ceo meč u sceni.
**Verifikacija**: `RulesChecks.All()`; `BotBalanceCheck.Run(200)` u ciljanim opsezima (prijavi brojeve); Play bot vs bot ceo meč do kraja bez izuzetaka, potez bota 8–11 s pre udarca (izmeri iz loga); faul → ball in hand radi za bota.

### Commit 4 — `player shot UX: camera, aim, guide, power pull, release bar, ball in hand`
**Obim**: `CameraRig` (svi režimi), `PlayerShotController`, `AimGuideView`, `TableTouchSurface`, `ShotControlsView`, `PowerStripView`, `ReleaseBarView`, `FineAimWheelView`, ball in hand UX, Look režim, auto-nišan na početku poteza, toast Perfect/Good/Miss (privremeni jednostavan `ToastView`). Igrač vs bot radi end-to-end (kraj meča još samo log).
**Verifikacija**: Play + `eval` simulira gestove (raise `OnLookDrag`/`OnPullChanged`/`OnPullReleased` u tačnim trenucima) → Green release daje grešku 0°, Red > 2°; cancel ne ispaljuje; guide se poklapa sa simulacijom (vizuelno + `PredictorChecks`); ball in hand odbija preklapanje; screenshot-ovi: Aim (nisko i visoko), Look, Place, Pulling sa barom za **Rookie** i **Legend** (vidljivo šira zelena zona i duža linija).

### Commit 5 — `ui framework & menu: buttons, popups, settings, cues inventory, economy`
**Obim**: kopija UIButton/Popup/Tooltip/SceneLoader/SafeArea + TMP font iz FlappyBird-a, `UIPrefabBuilder`, Systems prefab dopuna; `MenuSceneBuilder` (3D sto u pozadini, naslov, Play/Settings/Cues, coin counter, selected cue), `MenuController`, `SettingsPopup`, `CuesPopup` + `CueCardView`, `ConfirmPopup`, `NoEnergyPopup`, `ToastView` (finalni).
**Verifikacija**: grep 4.3 prazan; kroz `eval` postavi profil (novčići/pobede) i proveri sva 3 stanja kartice + disabled dugmad + tooltip na statovima; Buy → Confirm → novčići se smanje, kartica postane Owned; Select menja selektovani; Refill puni energiju; Play sa 0 energije → NoEnergyPopup; Settings slajderi menjaju volumen i čuvaju se; screenshot svakog popupa i menija.

### Commit 6 — `match flow & hud: panels, banners, pause/forfeit, results, rewards`
**Obim**: `MatchHudView`, `PlayerPanelView` + ladica ubačenih kugli (4.6.11), `TurnBannerView`, faul toast-ovi, `PausePopup` (Resume/Settings/Forfeit + Confirm), `MatchResultPopup` (razlog, coin count-up, novo otključano, Play Again/Menu), `MatchController` integracija sa `ProfileService` (energija na startu, nagrada na kraju, PendingMatch), auto pauza, `InputRouter` Back/Esc. Ukloni privremene debug modove iz commita 2–3 (ili ih ostavi samo pod `UNITY_EDITOR` kroz Tools meni — navedi koje).
**Verifikacija (ceo tok)**: Menu → Play → tranzicija → coin flip → break → … → kraj (pobedu forsiraj kroz `eval` postavljanjem stanja stola) → popup sa +10 i balansom → Play Again troši energiju → Pause → Forfeit → −1 → Menu pokazuje tačan balans; PendingMatch scenario (izađi iz Play mode-a usred meča, uđi ponovo) → −1 + toast; Esc zatvara popup / pauzira; screenshot svakog stanja HUD-a (open table, grupe dodeljene, samo 8 ostala, bot na potezu, ball in hand).

### Commit 7 — `polish & android: juice, audio, haptics, balance, APK`
**Obim**: svi zvukovi iz 5.4 povezani, haptic po 4.7.2, konfete/count-up/pulse, glatke kamere, Play idle pulse, provera 60 FPS (Profiler kroz `eval` ili `Time.unscaledDeltaTime` prosek), finalni `BotBalanceCheck`, `AndroidBuilder` + APK + `aapt` provera + instalacija ako je uređaj povezan, `README.md` (kontrole, build, struktura, tabela evenata, gde je tuning: `CueCatalog`, `MatchConfig`, `BotProfile`).
**Verifikacija**: APK builduje bez grešaka (zabeleži veličinu); `aapt` = tačan package, minSdk 26, VIBRATE; (uređaj) app se pokreće, logcat bez izuzetaka; DoD lista (10) prođena.

---

## 10. Definition of Done
- [ ] R1 Portrait, svi stringovi na engleskom.
- [ ] R2 Menu: Play, Settings, Cues vertikalno + coin counter + selected cue.
- [ ] R3 Settings: 2 slajdera, rade i čuvaju se (isti popup kao FlappyBird).
- [ ] R4 Lounge muzika (CC0 ili generisana), loop kroz scene, 50% tokom pauze.
- [ ] R5 5 štapova, 3 stanja, rookie posedovan, kupovina novčićima, otključavanje po pobedama, jedan selektovan, meč koristi selektovani.
- [ ] R6 Energija −1 po meču, Refill pored štapa, rookie ∞, NoEnergyPopup.
- [ ] R7 Bolji štap: šira zelena/žuta zona, sporija oscilacija, duža linija pogođene kugle (vidljivo na screenshot-u).
- [ ] R8 Play → meč.
- [ ] R9 3D sto, first-person Aim kamera, 8-ball pravila (svi `RulesChecks`), smena poteza.
- [ ] R10 Look around režim.
- [ ] R11 Ball in hand: kuhinja na break-u, bilo gde posle faula, za oba igrača.
- [ ] R12 Guide: bela → ghost ball, kratka linija pogođene kugle, crveno za ilegalnu metu.
- [ ] R13 Pull-back snaga + oscilujući release bar (zeleno sredina, žuto, crveno krajevi); Green = tačno, ostalo odstupa srazmerno; cancel radi.
- [ ] R14 `BotBalanceCheck` u opsezima; potez bota ~10 s, jasno označen.
- [ ] R15 WIN/LOSE popup sa razlogom.
- [ ] R16 Start 10 novčića; Win +10 / Loss −1, nikad < 0; Forfeit i napušten meč = poraz.
- [ ] R17 Kugle se realno odbijaju (kugla–kugla, cushion), kotrljaju sa vidljivom rotacijom, usporavaju i staju; upadaju u rupe sa animacijom; okrznuta ivica rupe odbija kuglu (`PhysicsChecks` + screenshot sekvenca break-a).
- [ ] R18 PlayerPanel oba igrača prikazuje preostale/ubačene kugle svoje grupe, "Open table" pre dodele, 8-ball highlight; ladica ubačenih kugli radi.
- [ ] Grep provere iz 4.3 prazne; svi `Editor/Checks` prolaze.
- [ ] APK buildovan i proveren; igra radi mišem u Editoru i dodirom na telefonu (osećaj potvrđuje korisnik).
- [ ] 7 commitova na `main`, svaki pushovan i kompajlabilan.

---

## 11. Poznati rizici
| Rizik | Šta uraditi |
|---|---|
| Sopstvena fizika "ne deluje kao bilijar" (previše/premalo klizanja, cushion-i mrtvi) | Tuning samo kroz `MatchConfig`; `PhysicsChecks.Timings` ciljevi; pokaži korisniku kratak snimak/screenshot-sekvencu break-a pre commita 2 ako nisi siguran |
| Predikcija i simulacija se razilaze | `PredictorChecks` je obavezan gate; isti `TableGeometry` i R u oba |
| Bot prejak ili preslab | `BotBalanceCheck` sa brojevima; menjaj prvo release distribuciju bota, pa težine ocene kandidata |
| Touch konflikt (drag po stolu vs. power strip vs. fine aim) | Odvojene UI površine; `TableTouchSurface` je ispod svih kontrola (obrazac FlappyBird `TapCatcher`); nišan zaključan tokom povlačenja |
| Portrait + 3D: sto ne staje u kadar | Kamera režimi računaju udaljenost iz FOV-a i aspect-a (fit), ne fiksne vrednosti |
| Break proizvodi 16 zvukova istovremeno | Throttle iz 5.4 |
| Performanse na telefonu (senke, post-processing) | Jedno svetlo sa senkom, Mobile_RPAsset, isključi bloom ako FPS < 55 |
| CC0 muzika nije jasno licencirana | Fallback generator (5.2) — ne koristi ništa što nije eksplicitno CC0 |
| Free DOTween bez TMP modula | Kao FlappyBird: `DOColor`/`DOFade` na `Graphic`, `DOTween.To` za brojanje |
| Pipeline CLI / Editor nedostupan, Safe Mode | FlappyBird 3.4 i 10 |
| Domain reload isključen → stari pretplatnici | `GameEvents.ResetAll` + unsubscribe u `OnDisable` |
