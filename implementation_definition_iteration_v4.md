# Implementation Definition v4 — Billiards (spin, realnija fizika, pravila po diviziji, progresija bez probnih vrata)

Ovaj dokument je **jedini ulaz** za agenta koji radi iteraciju v4. Čitaj ga ceo pre prvog koraka.
Kad nešto nije pokriveno — **stani i pitaj korisnika**, ne pretpostavljaj.

**Nadograđuje** `implementation_definition_v3.md` (završen: 12 commitova, poslednji `bfc9a7b`, 3 checkpointa potvrđena 2026-10-25).
Gde se ovaj dokument i v3 razlikuju, **važi ovaj**. v3 ostaje u repou kao istorija.

Prateći dokumenti (čitaj, ne menjaj):
- `README.md` — arhitektura, eventi, builderi, checks, balans i tempo (stanje posle v3).
- `implementation_definition_v3.md` — §0.1 (Unity CLI na ovom Mac-u) i debug alati **važe i ovde**.
- `implementation_definition.md` — slojevi (4.3), konvencije (4.9), CLI rad (3) i verifikacioni alati (8).
- `release_plan.md` — kontekst proizvoda. **Izmena:** redovi „Namera plaćanja“ (fake door, rewarded opt-in) i anketa iz §0.1, kao i §2.4–2.6 (rewarded, fake door, anketa), **ne važe za ovu fazu** (odluka korisnika 2026-10-25, §1.2). MVP se validira metrikama iz §5.2 ovog dokumenta.

Svi tekstovi u igri su na **engleskom**.

---

## 0. Pravila rada za agenta

1. Radiš kroz **12 commitova** (§9). Svaki commit je kompajlabilna, pokretljiva celina. Posle svakog commita uradi `git commit` + `git push` na `main` (remote `origin`). Nikad force push.
2. **Pre svakog commita:**
   - `unity recompile` mora dati 0 grešaka;
   - grep provere slojeva (README „Layer rules“) moraju biti prazne — i komentari se računaju, pa ne pominji imena controllera u `UI/`;
   - svi `Editor/Checks` moraju prolaziti, stari i novi (ili se svesno rekalibrišu tamo gde ovaj dokument to kaže — prijavi pre/posle);
   - uradi verifikaciju iz tog commita.

   Nikad ne tvrdiš da nešto radi ako nisi proverio. Ono što ne možeš da proveriš (osećaj na telefonu, haptic, zvuk, FPS bez uređaja) napiši eksplicitno.
3. **Posle svakog commita** pošalji kratak izveštaj korisniku **na srpskom**: šta je urađeno, kako je provereno, šta nije provereno, putanje screenshotova i commit hash.
4. **Checkpointi sa korisnikom** su posle commita **4, 9 i 12**:
   - napravi APK (`AndroidBuilder.Build()`) i proveri ga sa `aapt` (package, minSdk 26, VIBRATE);
   - ako je telefon povezan (`adb devices`), instaliraj APK;
   - javi korisniku šta tačno da testira i **čekaj potvrdu** pre nastavka.
5. **Stani i pitaj** ako:
   - Editor nije dostupan CLI-ju;
   - build pada iz neočiglednog razloga;
   - rešenje bi kršilo slojeve;
   - fizika ne može da uđe u ciljeve iz V1 tabele bez menjanja modela (ne samo brojeva);
   - balans ili tempo ne mogu da uđu u ciljeve ni posle tuninga podataka;
   - asset/licenca nije dostupna (font, ruka, teksture);
   - ili nešto nije pokriveno ovim dokumentom.
6. Scene, prefabe i generisane assete menjaš **samo kroz buildere** (`Editor/Builders`), nikad ručno YAML. Svi brojevi idu u ScriptableObject-e (`MatchConfig`, `CueCatalog`, `DivisionCatalog`, `ObjectiveCatalog`, `EconomyConfig`, `BotProfile`, `TutorialConfig`, novi `PracticeConfig`), nikad u kod.
   - Kad menjaš podrazumevanu vrednost polja koje već postoji u assetu, ažuriraj **i asset** (kroz `eval` i `SerializedObject`/polje + `SetDirty` + `SaveAssets`), ne samo inicijalizator u kodu.
7. **Screenshotovi** idu u `Screenshots/v4_<commit>_<ime>.png`, portrait 1080×1920 (`GameViewUtil.SetPortrait()`). Folder je u `.gitignore`.
8. Ne diraj ništa van `~/Desktop/UnityProjects/Billiards`, osim `~/Desktop/UnityProjects/_kenney_downloads/` (download folder).
9. **Pool sloj ostaje čist C#** (`Pool/`: bez `MonoBehaviour`, `Time.`, `UnityEngine.Random`, `GameEvents`, `PlayerPrefs`) i **deterministički**: isti ulaz → bit-identičan izlaz (`PhysicsChecks.Determinism` je gate posle svake izmene fizike).
10. **8-ball pravila menjaš samo kako V4 kaže.** Bronze ostaje tačno kao danas (`RulesChecks` iz v3 moraju prolaziti nepromenjeni za Bronze).

### 0.1 Unity CLI i alati
- Sve iz v3 §0.1 važi (detached eval + `job wait`, buđenje Editora kroz `osascript`, `runInBackground`, screenshot kroz `ScreenCapture`, odloženo snimanje kroz `DOVirtual.DelayedCall`, merenje vremena kroz pretplatu na `GameEvents`).
- Iz v3 beleški (provereno u izvršenju v3):
  - Play mode radi domain reload: statički backup profila ne preživljava → koristi `EconomyChecks.BackupProfileToFile` / `RestoreProfileFromFile`.
  - Meč se sam pauzira kad Editor nije u fokusu → posle `editor_play` pozovi `GameEvents.RaiseResumeRequested()`.
  - Screenshot traži fokusiran GameView (`EditorWindow.GetWindow(GameView).Focus()`) i aktiviran Unity.
  - Dugi `eval`-ovi (> ~1 min) usporavaju kad Editor nije u fokusu; `ProgressionSim` traje ~13 min.
  - `PendingMatch` iz zaustavljenog Play testa pojede ★ pri sledećem startu (očekivano) — vrati profil posle testa.
- Postojeći debug alati: `MatchController.DebugSetupTable/DebugStart/DebugSetupWin/DebugBotVsBot`, `PlayerShotController.DebugSetAim/DebugReleaseAt/DebugSetBarPosition`. Dodaj `PlayerShotController.DebugSetSpin(Vector2 tip)` i `MatchController.DebugSetRules(RuleSetId)` (Editor-only).

---

## 1. Cilj i obim

Korisnik je igrao v3 build (checkpoint 3, 2026-10-25) i dao feedback (§1.1). Cilj v4:
1. **Udarac sa spinom** — igrač bira gde štap udara belu, a fizika to realno prenosi (follow, draw, stun, side, throw, squirt, masse). Guide pokazuje stvarnu putanju.
2. **Realniji osećaj** — snaga koja odgovara stvarnim udarcima, realniji zidovi i sudari, smirenija kamera posle udarca.
3. **Profesionalniji izgled** — kugle, čoja, lampa, rubovi rupa, font, aim guide, štapovi koji se vidno razlikuju.
4. **Pravila crne po diviziji** — svaka divizija donosi novo pravilo, kao sobe u 8 Ball Pool-u.
5. **Progresija koja drži prvih 1–7 dana bez dnevnih nagrada** — divizije kao jasan put sa proslavama i nagradama, ciljevi po diviziji, put do boljeg štapa i za igrača koji gubi.
6. **Čist build za MVP test** — bez simuliranih reklama, fake door-ova i ankete; analitika spremna za fazu 2 i proverena lokalno.

### 1.1 Feedback → zahtev
| # | Feedback (izvorno, skraćeno) | Zahtev |
|---|---|---|
| F1 | Podešavanje gde se udara bela (gore, dole, levo, desno), default centar; tutorijal ga uči; npr. udarac u dno vraća belu | **V1** fizika spina, **V2** spin kontrola, **V10** tutorijal |
| F2 | Kugle su previše svetle, treba profesionalan izgled | **V6** |
| F3 | Za nijansu tamnija čoja | **V6** |
| F4 | Posle udarca kamera se prebrzo odzumira; treba polako, kao čovek koji se uspravlja | **V7** |
| F5 | Belo svetlo gore (pravougaonik) je ružno i kroz njega se vidi kad se odzumira | **V6** lampa |
| F6 | Na meniju prvo ciljevi, ispod njih dugo PLAY | **V13** |
| F7 | Realnija fizika kugli (brzina, način kretanja) | **V1** |
| F8 | Drugačiji, lep font gde se svako slovo jasno vidi | **V8** |
| F9 | Putanja posle udarca lepša; pravougaonik je basic | **V3** aim guide |
| F10 | Štapovi da se vizuelno totalno razlikuju i da se jasno vidi razlika u karakteristikama | **V9** |
| F11 | Ručica (ruka) u tutorijalu lepša i boje kože | **V8** |
| F12 | Progresija kao system/game designer: divizije, taskovi, otključavanje štapova; ne samo happy path; povratak sutradan i za 3 dana | **V11, V12** |
| F13 | Ciljevi: kako se otključavaju, kako utiču na progresiju, popup sa svim i budućim | **V12** |
| F14 | Bolje istaći divizije i zvati ih „divisions“; vidljivi koraci; proslava milestone-a | **V11, V13** |
| F15 | Dugački privremeni natpisi u tutorijalu preklapaju druge stringove | **V8, V10** |
| F16 | Prikaz ubačenih kugli YOU / COACH u tutorijalu je ružan i nepotreban | **V10** |
| F17 | Kraći tutorijal, 3 udarca, uključujući gađanje mesta za belu | **V10** |
| F18 | Power prejak sa običnim štapom | **V1** (kriva snage) |
| F19 | „Objective done“ se preklapa sa CLAIM; opis cilja nikad tako sitan | **V8, V13** |
| F20 | Skloni anketu | **V14** |
| F21 | Dugme za pauzu zauzima previše | **V8** |
| F22 | Fake ads bezveze; kako validirati MVP bez fake reklama i ponuda | **V14**, §5.1–5.2 |
| F23 | Posle meča bolje od PLAY AGAIN, „no brainer“ | **V13** |
| F24 | Offer — „vidi šta ćeš“ | **V14** (uklanja se) |
| F25 | Okviri rupa svetlucaju | **V6** |
| F26 | TRY za štapove, da igrač oseti razliku | **V9** |
| F27 | Različita pravila za crnu po divizijama (istraži 8 Ball Pool) | **V4** |
| F28 | Da li je sve pokriveno analitikom; koje metrike bez notifikacija i fake door-ova | **V15**, §5.2 |

### 1.2 Odluke korisnika (2026-10-25)
| Tema | Odluka |
|---|---|
| Spin fizika | **Puna realna fizika** za bilo koju tačku na beloj: follow, draw, stun, side (zid), throw, squirt, masse (zbog nagiba štapa). |
| Aim guide i spin | Guide crta **stvarnu putanju** (predikcija simulira isto što i fizika). |
| Granica tačke udara | **Ista za sve štapove: 0.5R** od centra. Bez miscue-a. (Nema SPIN stat-a.) |
| Spin UI | Mala ikona bele iznad POWER trake; tap otvara veliku zumiranu belu sa markerom; dupli tap = centar; **posle svakog udarca vraća se na centar**. |
| Power | Nova kriva: MED ≈ 1.5 m/s, HARD ≈ 3.5 m/s, ista za sve štapove do 85%; vrh 85–100% zavisi od štapa (**POWER stat**: House 4.5 → Legend 6.5 m/s). |
| Aim guide stil | **Tanke linije sa fade-om**, ghost kao prsten, mala strelica na kraju linije pogođene kugle. |
| Lampa | **Prava bilijar lampa po sali** (zatvoreno telo, senila, tople sijalice), nije providna, sakriva se kad kamera ode iznad. |
| Font | **Rubik** (Google Fonts, OFL). Opisi mala+velika slova; dugmad i naslovi velika. |
| Štapovi | **Unikatan izgled po štapu + 4 stat-a**: AIM, CONTROL, TIMING, POWER, uz OVR. |
| TRY | **Vežbe bez ranga**: sto sa probnim udarcima za izabrani štap. |
| Pravila crne | Bronze: bilo koja rupa · Silver: koluješ rupu za 8 · Gold: protivnik blokira jednu rupu, ostalih 5 slobodno · Platinum: protivnik koluje rupu za tvoju 8 · Diamond: koluješ rupu za svaku kuglu, pa i za 8 · Grand Champion: kao Diamond, ali rupu za 8 koluje protivnik. Blokiranje i kolovanje protivnika: **bira jednom**, na tvom prvom potezu na crnoj, i ta rupa ostaje do kraja meča. |
| 8 u pogrešnu ili blokiranu rupu | **Poraz** (kao 8 Ball Pool). |
| Kolovanje (igrač) | **Obavezan tap** na rupu pre udarca (power je zaključan dok rupa nije izabrana). |
| Igrač bira za bota | Kad je bot na crnoj (Gold/Platinum/GC), igrač **tapne rupu, bez tajmera**. |
| Dnevni povratak | **Ništa dnevno** (nema login nagrada, dnevnih izazova ni bonusa prve pobede). |
| Zaglavljen igrač | Ako gubi jer nema dovoljno dobar štap za diviziju → **zadaci koji se rešavaju igranjem** (ne pobedom) daju novčiće za bolji štap. Bez zaštita od gubitka ★ i bez skrivenog olakšanja bota. |
| Ciljevi | **Setovi po diviziji** (6 po diviziji), 3 vidljiva na meniju, **ALL OBJECTIVES** popup (tekuća divizija + zaključan pregled sledeće), bonus za ceo set. |
| Nagrade napretka | **Novčići na svakom novom stage-u i diviziji** + proslava; nagrade se vide unapred. |
| Probe | **Uklanja se sve**: simulirane reklame (Star Shield, Double coins), Starter Pack, ONLINE · SOON, anketa. |
| Analitika | **Lokalni `AnalyticsService`** sada (log + check); Firebase u sledećoj fazi. |
| Tutorijal | **3 udarca** (nišan + snaga · release na zelenom · draw do kruga), posle 3 neuspeha ide dalje; **RulesPopup ostaje** pre prvog meča. |
| Posle meča | Veliko **NEXT MATCH ▶** sa portretom sledećeg protivnika; MENU malo dugme; bez auto-starta. |
| Bot i spin | Bronze/Silver: centar. **Gold+: bira spin** (centar/follow/draw/stun) simulacijom. |

### 1.3 Van obima
- Firebase, Crashlytics, Remote Config, AdMob, pravi IAP, In-App Review — sledeća faza (`release_plan.md` faza 2). Ovde se pravi samo `AnalyticsService` sa jednim mestom za Firebase.
- Dnevne nagrade, dnevni izazovi, login kalendar, notifikacije, PvP, leaderboard, battle pass, iOS.
- Jump shot, miscue, korisnička kontrola nagiba štapa (nagib i dalje određuje `CueClearance`).
- Landscape i tableti.

---

## 2. Polazno stanje (v3, urađeno)

| Oblast | Šta postoji (posle `bfc9a7b`) |
|---|---|
| Fizika | `PoolPhysics` 1 ms, deterministička; klizanje → kotrljanje (`BallState.Roll` = R·ω u ravni), bez bočnog spina; kugla–kugla bez trenja (restitucija 0.95); cushion: restitucija 0.78 konstantna, trenje tangente 0.1, `Roll` po normali → 0; `BallMotion` zatvorena forma za predikciju i bota; udarac uvek u centar (`Roll = 0`). |
| Snaga | `MatchConfig`: `MinSpeed` 0.35, `MaxSpeed` 6.5, `PowerExponent` 1.6 → p=0.5 daje 2.38 m/s; bela se kotrlja ~11 m (≈5 dužina stola). To je uzrok F18. |
| Težina | V8: štap × snaga (`ShotAccuracy`, `GreenScale/YellowScale/PeriodScale/ErrorScale`), bot σ po diviziji. |
| Guide | V1: tanka linija do prve prepreke + meke trake 2R (`GuideBandRenderer`, `AimGuideLayout`, `GuideChecks.LengthRule`). |
| Pravila | `EightBallRules`/`RulesState`: standardna 8-ball, bez kolovanja; 8 na break-u se vraća. |
| Bot | `BotBrain`: ghost kandidati lopta × rupa, simulacija najboljih, σ release; bez spina. |
| Meta | 5 divizija × 3 stage-a (★), checkpointi, vrući niz; 20 ciljeva po redu, 3 vidljiva; 6 štapova (AIM/CONTROL/TIMING/OVR); 5 sala; Ranked Road popup. |
| Probe | `RewardedAdService` + `SimulatedAdOverlay` (Star Shield, Double coins), `StarterPackPopup`, `ComingSoonPopup` (ONLINE), `SurveyPopup`. |
| Tutorijal | 5 lekcija (`TutorialConfig`), FTUE, `HintService`, `CoachMarkView` sa belom generisanom rukom. |
| Svetlo | Spot 3 m + kanopi sa emisivnim panelom (quad okrenut nadole — zato se kroz njega vidi odozgo, F5); Neutral tone mapping, bloom na emisiji, MSAA 4×. Čoja luma 0.53 (Watch). |
| UI | Kenney Future (sve velika slova, nema ★ — zvezdice su pisane rečima), kontrast: zeleno dugme 3.2:1 (sa konturom 5.7). |
| Profil | `ProfileService` v2 (`ProfileVersion` 2). |

**Baseline brojevi** (README, v3 commit 12): BotBalance 57.5 / 38.0 / 56.5; DivisionBalance prosečan B62 S58 G59 P51 D53; tempo prosečan: Silver 8, Club 6, Gold 22, Pro 19, Platinum 45, Master 50, Diamond 73 meča; meč medijana 3.7 min. Agent ih na početku commita 1 ponovo izmeri i zapiše kao „pre v4“.

**Poznati otvoreni gapovi iz v3:** kontrast fill zelenog 3.2:1 / plavog 3.3:1; performanse na uređaju nisu merene; slab igrač stiže u Gold ~D7.

---

## 3. Gameplay zahtevi

Za svaki zahtev: **Problem → Rešenje → Prihvatni kriterijumi**.

### V1 — Fizika: udarac van centra (spin), throw, squirt, masse, zidovi, kriva snage (F1, F7, F18)
**Problem:** udarac je uvek u centar, pa nema follow/draw/side kontrole. Snaga je prejaka (MED = 2.4 m/s). Zid je konstantan, a sudar kugli bez trenja (nema throw-a).

**Rešenje — stanje kugle (`Pool/BallState`):**
- Postojeći `Vel` (m/s) i `Roll` (R·ω u ravni, m/s) ostaju.
- Novo: `float Side` — R·ω_z (bočni spin oko vertikale, m/s; + = suprotno od kazaljke gledano odozgo).
- `IsMoving` uključuje `Side`.

**Rešenje — udarac štapa (`PoolPhysics.BeginShot`, `ShotParams` dobija `Vector2 Tip` i `float Elevation`):**
- `Tip = (a, b)` u jedinicama R: `a` levo/desno, `b` gore/dole; `|Tip| ≤ MatchConfig.SpinMaxOffset` (0.5). `Elevation` je ugao štapa iz `CueClearance` (stepeni).
- Brzina: `Vel = v0 · dir'`, gde je `dir'` pravac štapa zakrenut za **squirt** `−SquirtDegPerR · a` (bela skreće suprotno od strane udara).
- Kotrljanje: `Roll = (5/2) · b · v0 · dir` (b = 0.4 → `Roll = Vel`, kugla se odmah kotrlja; b < 0 → backspin/draw).
- Bočni spin: `Side = (5/2) · a · v0 · SideTransfer` (`SideTransfer` ≈ 1, knob za realnost).
- Iznad `|Tip| = 0.5` nema udarca (UI ne dozvoljava).

**Rešenje — kretanje (`Integrate`):**
- Klizanje i kotrljanje kao v3 (slip `Vel − Roll`, μs, 5/7). Draw radi sam od sebe: `Roll` suprotan od `Vel` → posle kontakta bela se vraća.
- **Bočni spin opada** konstantno: `Side −= sign(Side) · SideSpinDecel · dt` (do 0), nezavisno od klizanja/kotrljanja.
- **Masse/swerve:** dok kugla klizi, sa `Side ≠ 0` i `Elevation > SwerveMinElevation` dodaj bočno ubrzanje `SwerveFactor · Side · sin(Elevation)` normalno na `Vel`. Pri ravnom štapu (0–5°) efekat je zanemarljiv; pri nagibu uz rail je vidljiv. Guide ga crta (V3).

**Rešenje — kugla–kugla:**
- Normalna komponenta kao sada (restitucija 0.95).
- **Throw (trenje između kugli, `BallFriction` μb ≈ 0.06):** tangencijalna relativna brzina tačaka dodira (iz `Vel`, rezne komponente i `Side` bele) daje tangencijalni impuls ograničen sa `μb · normalni impuls`. Pogođena kugla dobija mali tangencijalni pomak pravca (cut-induced i spin-induced throw); bela gubi odgovarajući deo. `Side` bele se smanji za deo prenet trenjem; pogođena kugla ne dobija `Side` (zanemareno).
- `Roll` obe kugle se ne menja (kao v3).

**Rešenje — kugla–cushion:**
- **Restitucija zavisi od brzine:** `e(vn) = lerp(CushionRestitutionSlow, CushionRestitutionFast, vn / CushionFastSpeed)` (npr. 0.88 → 0.70).
- **Bočni spin na zidu:** brzina tačke dodira duž zida = tangenta `Vel` + `Side` (znak po strani). Trenje zida `CushionSpinFriction` (ograničeno sa `μc · (1+e) · |vn|`) menja tangentu: running english „otvara“ ugao, reverse „zatvara“. `Side` se smanjuje za prenet deo.
- `Roll` po normali → 0 (kao v3).

**Rešenje — kriva snage (`MatchConfig`, `ShotAccuracy.SpeedFromPull`):**
- Do `PowerSharedUntil` (0.85) kriva je ista za sve štapove: `speed = MinSpeed + (HardSpeed − MinSpeed) · (p / 0.85)^PowerExponent`, sa `HardSpeed` 3.5 m/s i eksponentom tako da je p = 0.5 → **1.5 m/s** (≈ 1.9).
- Od 0.85 do 1.0: linearno od `HardSpeed` do `CueDefinition.MaxSpeed` (POWER, V9: House 4.5 … Legend 6.5).
- Oznake SOFT/MED/HARD (15/50/85%) ostaju gde jesu. Očekivano: SOFT ≈ 0.5 m/s, MED ≈ 1.5 m/s, HARD ≈ 3.5 m/s.
- V8 skaliranje bara po `p` se ne menja (težina je i dalje po procentu povlačenja).

**Rešenje — predikcija (`ShotPredictor`):**
- Zatvorena forma (`BallMotion`) više ne pokriva swerve, throw i spin na zidu. **Predikcija pokreće istu `PoolPhysics` simulaciju** na prealociranoj kopiji (bela + sve kugle), od udarca do: prve prepreke + dužine strelice posle nje (V3), ili prve kugle koja upadne.
  - Snima putanju bele (do 32 tačke, prealocirano), tačku prvog kontakta, pravac pogođene kugle (sa throw-om), putanju bele posle kontakta i posle zida.
  - 0 B GC; računa se samo kad se promene nišan, snaga, spin ili kugle (ne svaki frejm); budžet ≤ 1.5 ms po računanju u Editoru (prijavi izmereno).
  - `BallMotion` ostaje za bota (procena brzine) i za brze provere.
- `ShotPredictor` dobija `Tip`, `Elevation` i brzinu kao ulaz.

**Novi brojevi u `MatchConfig`** (početne vrednosti, kalibracija u commitu 1): `SpinMaxOffset` 0.5, `SquirtDegPerR` (cilj 2° pri a = 0.5 → 4°/R), `SideTransfer` 1.0, `SideSpinDecel`, `SwerveFactor`, `SwerveMinElevation` 5°, `BallFriction` 0.06, `CushionRestitutionSlow` 0.88, `CushionRestitutionFast` 0.70, `CushionFastSpeed` 4 m/s, `CushionSpinFriction` 0.2, `HardSpeed` 3.5, `PowerSharedUntil` 0.85. `MaxSpeed` se seli u `CueDefinition.MaxSpeed`.

**Kriterijumi** (novi `PhysicsChecks.Spin()`, proširen `Collisions`; brzine pre kontakta su brzine bele posle udarca):

| Scenario | Cilj |
|---|---|
| Kriva snage | p 0.15 → 0.45–0.6 m/s; p 0.5 → 1.4–1.6; p 0.85 → 3.4–3.6; p 1.0 = `MaxSpeed` štapa |
| MED udarac bez prepreke (p 0.5, centar) | bela pređe 1.8–3.2 dužine stola (bilo ~5) |
| Natural roll (b = +0.4) | slip posle udarca < 0.02 m/s (kotrlja se odmah) |
| Stun (b tako da je `Roll` ≈ 0 na kontaktu), pun pogodak 0.5 m, 1.5 m/s | bela posle sudara pređe < 5 cm |
| Draw (b = −0.5), pun pogodak, 0.5 m, MED | bela se vrati 0.4–1.2 m |
| Follow (b = +0.5), pun pogodak, 0.5 m, MED | bela nastavi 0.5–1.5 m |
| Side a = ±0.5 u zid pod 45°, 1.5 m/s | izlazni ugao se razlikuje od bez-spina za 6–20°, running english otvara, reverse zatvara |
| Squirt a = 0.5 | bela odstupi 1.5–2.5° od linije štapa (pre kontakta, bez nagiba) |
| Throw: rez 30°, spor (0.5 m/s), centar | pogođena kugla odstupi 1.5–4° od ghost-ball linije; brz (3 m/s) < 1.5° |
| Masse: a = 0.5, nagib 30°, 1.5 m/s | putanja bele vidljivo zakrivljena (bočno odstupanje ≥ 3 cm na 1 m); nagib 3° → < 0.5 cm |
| Cushion restitucija | spor (0.5 m/s) odbitak ≥ 85% normalne brzine, brz (4 m/s) ≤ 75% |
| `Timings` | break 100% (House) → umirenje 5–9 s; break Legend ubaci u proseku ≥ break House (prijavi oba) |
| `StraightPot`, `NoTunneling(1000)`, `Determinism` | kao v3, uključujući nasumične `Tip` i `Elevation` |
| `PredictorChecks.MatchesSimulation` (prošireno) | 500 nasumičnih (pravac, snaga, Tip, nagib): ghost ±1 mm, `ObjectDir` ±0.5°, putanja bele pre i posle kontakta ±2 mm na dužini strelice |

- Brojeve iz tabele uporedi sa poznatim izvorima (Dr. Dave Alciatore, „The Illustrated Principles of Pool and Billiards“ / TP članci o throw-u i squirt-u). Gde odstupaju, prijavi; ciljevi su opsezi, ne tačne vrednosti.
- `BotBalanceCheck.Run(200)` u opsezima iz v3 (dobar ≥ 55, loš ≤ 42, isti 50 ± 8, dobar > isti > loš) — rekalibracija samo podacima (`BotProfile`), pre/posle u izveštaju.
- `ShotDifficultyCheck.PotRates` se ponovo meri (kriva snage menja brzinu za isti `p`); uslovi monotonosti iz v3 V8 ostaju, apsolutne stope se prijavljuju.
- Sekvenca screenshotova za checkpoint: `v4_1_draw_*.png`, `v4_1_follow_*.png`, `v4_1_side_cushion_*.png`, `v4_1_masse_*.png`.

### V2 — Spin kontrola (F1)
**Rešenje (`UI/Views/SpinControlView`, nov):**
- **Ikona:** mala bela kugla (~96 ref px) iznad POWER natpisa u desnoj koloni, sa crvenom tačkom na trenutnom mestu udara. Nema teksta.
- **Tap na ikonu** → overlay sa **velikom zumiranom belom** (~520 ref px, sa senkom i blagim sjajem, izgleda kao bela kugla iz igre) i **markerom** (crveni krug sa belim obodom, ~48 px):
  - drag pomera marker; granica je krug `SpinMaxOffset` (0.5R) — iznad toga je marker zaustavljen na ivici, a prsten granice je blago vidljiv;
  - tanke vodilje (krst) kroz centar, prigušene;
  - **dupli tap = centar**;
  - zatvara se tapom van kugle ili dugmetom ✓; aim guide se ažurira uživo dok se marker pomera (overlay ne pokriva sto potpuno — postavi ga u donju polovinu, poluprovidna pozadina).
- **Posle svakog udarca** spin se vraća u centar (i za igrača i za bota).
- Ikona prikazuje stanje kad spin nije centar (tačka pomerena + blagi puls okvira), da igrač ne zaboravi.
- Gameplay: `PlayerShotController` drži `Tip`; UI → gameplay kroz event `OnSpinChanged(Vector2 tip)`; reset kroz `OnSpinReset`. UI ne zna za `PlayerShotController` (sloj).
- `ShotFeedback` dobija `Tip` i `Elevation` (analitika, V15).
- U LOOK, Place (ball in hand) i tokom poteza bota ikona je sakrivena.

**Kriterijumi:**
- `ControlChecks.SpinMapping()`: (a) drag do ivice → `|Tip|` = 0.5 ± 0.005; (b) drag van kruga ne prelazi 0.5; (c) dupli tap → (0,0); (d) posle `OnShotFired` → (0,0).
- Ikona i overlay ne preklapaju power ručku, fine aim ni LOOK (`UiLayoutChecks`, V8).
- Screenshotovi `v4_2_spin_icon.png`, `v4_2_spin_overlay_draw.png`, `v4_2_spin_overlay_side.png`.

### V3 — Aim guide: tanke linije sa fade-om (F9)
**Problem:** meke trake širine 2R izgledaju kao „basic pravougaonik“.

**Rešenje (`GuideBandRenderer` ostaje kao mesh traka, menja se izgled; `AimGuideLayout` pravilo dužine iz v3 ostaje):**
- **Bela do prve prepreke:** tanka linija ~3 mm, bela 90% sa tamnom senkom 1 mm; sada **prati stvarnu putanju** (squirt, masse) — kriva se crta kao niz segmenata iz predikcije (V1).
- **Ghost lopta:** **prsten** (obod ~1.5 mm, bela 85%) + vrlo blag fill 10% i mekan sjaj; na ilegalnoj meti prsten prigušeno crvenkast (`#E06A5A`) kao u v3.
- **Pogođena kugla:** zlatna (`#FFD54A`) linija ~3 mm, od kugle u pravcu `ObjectDir` (sa throw-om), dužine strelice štapa; **alpha fade** od 90% do 20% ka kraju; na kraju **mala strelica** (trougao ~10 mm).
- **Bela posle kontakta / posle zida:** tanka bela linija ~2 mm, fade 70% → 0, **zakrivljena** po predikciji (draw/follow/stun, spin na zidu); bez strelice.
- **Spin vizuelno:** kad spin nije centar, kratki segment bele posle kontakta dobija 3 male tačke koje teku (0.12 m/s) u pravcu kretanja — jedini animirani element. Bez chevrona.
- Postojeći x-ray kroz kugle: 35% za linije.
- Scratch (bela u rupu): linija i rub rupe narandžasti, kao v3.
- Teksture: `guide_line_profile.png` (mekan rub), `guide_fade.png` (podužni fade), `guide_arrow.png`; generiše `GeneratedArtBuilder`.

**Kriterijumi:**
- `GuideChecks.LengthRule()` prolazi (pravilo dužine nepromenjeno), proširen: kriva bele prati predikciju ±2 mm.
- 0 B GC po frejmu u Aim fazi (120 frejmova, kao v3).
- Screenshotovi: `v4_2_guide_ball.png`, `v4_2_guide_draw.png`, `v4_2_guide_side_cushion.png`, `v4_2_guide_illegal.png`, `v4_2_guide_scratch.png`.

### V4 — Pravila crne po diviziji (F27)
**Istraživanje (8 Ball Pool, Miniclip; izvor: fan wiki „Game rooms“, `8ballpool.fandom.com/wiki/Game_rooms`, pregledano 2026-10-25; nije zvanično):**
- London, Sydney: *Standard* (8 u bilo koju rupu).
- Lisbon (ranije Moscow), Tokyo, Shanghai: *Call Pocket on 8 Ball*.
- Las Vegas, Jakarta, Toronto, Cairo, Dubai, Paris, Bangkok, Seoul, Mumbai: *Call Pocket on All Shots*.
- Rome, Berlin: *Cushion Shot on 8 Ball*; Osaka, Venice: *Call Pocket on All Shots + Cushion Shot on 8 Ball*.
- Specijalne: *No Guidelines* (Beijing), 9-ball sobe.
- Varijante „protivnik blokira rupu“ i „protivnik koluje tvoju 8“ **ne postoje u 8BP** — to su pravila korisnika (§1.2) i daju ulogu igraču i dok je protivnik na potezu.

**Pravila po diviziji (`DivisionDefinition.Rules` → `RuleSetId`):**

| Divizija | `RuleSetId` | Pravilo (EN kartica u igri) | Šta se proverava |
|---|---|---|---|
| Bronze | `Standard` | „Pot the 8 in any pocket.“ | kao danas |
| Silver | `CallEight` | „Call your pocket for the 8-ball.“ | pre svakog udarca na 8 strelac koluje rupu; 8 u drugu rupu = poraz |
| Gold | `BlockedPocket` | „Your opponent blocks one pocket for your 8-ball.“ | na prvom potezu strelca na crnoj protivnik bira 1 rupu; ostaje blokirana do kraja meča; 8 u bilo koju od ostalih 5 je ok (bez kolovanja); 8 u blokiranu = poraz |
| Platinum | `OpponentCallsEight` | „Your opponent picks the pocket for your 8-ball.“ | na prvom potezu strelca na crnoj protivnik bira 1 rupu; ostaje do kraja meča; 8 u drugu = poraz |
| Diamond | `CallAll` | „Call your pocket on every shot — the 8 too.“ | posle break-a, pre svakog udarca strelac koluje rupu (i na otvorenom stolu); 8 u nekolovanu = poraz |
| Grand Champion | `CallAllOpponentEight` | „Call every shot. Your opponent picks your 8-ball pocket.“ | kao Diamond za grupu; za 8 važi rupa koju je protivnik izabrao (jednom, ostaje) — strelac je ne koluje |

**Detalji pravila (važe gde se pravilo primenjuje):**
- **Break** je uvek izuzet od kolovanja; 8 na break-u se i dalje vraća (v3).
- **Kolovana rupa važi za jedan udarac** (Silver na 8, Diamond/GC na svaki).
- **Diamond/GC, grupa:** potez se nastavlja samo ako bar jedna kugla strelčeve grupe (ili bilo koja na otvorenom stolu, osim 8) upadne **u kolovanu rupu**. Ostale ubačene kugle ostaju dole; ako nijedna nije u kolovanoj rupi, potez prelazi na protivnika **bez faula**. Dodela grupe na otvorenom stolu: prva kugla u kolovanoj rupi određuje grupu.
- **Faul + 8** i dalje = poraz (v3). **8 prerano** = poraz (v3).
- **Nova `EndReason`:** `EightWrongPocket` (nekolovana ili ne-izabrana rupa), `EightBlockedPocket`. Tekstovi: „You pocketed the 8 in the wrong pocket.“, „You pocketed the 8 in a blocked pocket.“
- **Izbor protivnika (blok ili kolovanje za 8) se pravi jednom**, kad strelac **prvi put počne potez na crnoj** (grupa očišćena). Izbor važi do kraja meča, čak i ako strelac promaši i ponovo dođe na potez.

**Stanje (`Pool/RulesState`, čist C#):**
- `RuleSetId Rules`; `int[] EightPocket` po strani (−1 = nije izabrano; blokirana ili zadata rupa po pravilu); `int CalledPocket` za tekući udarac (−1 = nema).
- `bool NeedsCall(Side)` (da li strelac mora da koluje pre ovog udarca), `bool NeedsOpponentEightChoice(Side)` (protivnik treba da izabere rupu za strelčevu 8).
- `EightBallRules.Evaluate(state, shot)` čita `PocketEvent.PocketIndex` (već postoji) i `state.CalledPocket`/`EightPocket`.

**Tok poteza (`MatchController`):**
1. Početak poteza: ako `NeedsOpponentEightChoice(shooter)`:
   - strelac je **igrač** → bot bira (`BotBrain.ChooseEightPocketForOpponent`), banner „DINA BLOCKED THIS POCKET“ / „DINA PICKED YOUR 8-BALL POCKET“, rupa dobija stalni marker;
   - strelac je **bot** → igrač bira: kamera Watch (svih 6 rupa), banner „BLOCK A POCKET FOR DINA'S 8-BALL“ / „PICK THE POCKET FOR DINA'S 8-BALL“, igrač tapne rupu (**bez tajmera**), potvrda kratkim pulsom; tek tada bot igra.
2. Ako `NeedsCall(shooter)`:
   - igrač: power traka je zaključana sa natpisom „TAP A POCKET“ dok igrač ne izabere rupu (V5 UI); izbor se može menjati do udarca;
   - bot: koluje rupu svog izabranog kandidata (`BotDecision.TargetPocket` već postoji).
3. Udarac → `Evaluate` sa kolovanom rupom → `CalledPocket` se briše.

**Bot (`Pool/BotBrain`):**
- Kandidati za udarac poštuju pravila: na 8 samo dozvoljene rupe (ne blokirana; u Platinum/GC samo zadata); u Diamond/GC kandidat = lopta × rupa, a ta rupa je kolovanje.
- `ChooseEightPocketForOpponent(state, balls, side)`: za svaku rupu proceni najbolji udarac protivnika na 8 iz trenutnog rasporeda (isti skor kandidata kao `ChooseShot`).
  - **Blok (Gold):** blokira rupu sa **najboljim** skorom za protivnika.
  - **Zadavanje (Platinum/GC):** bira rupu sa **najlošijim** skorom među rupama koje imaju bar jedan direktan put (ako nijedna nema, najlošiju ukupno).
  - Deterministički uz `System.Random` iz meča.

**Prikaz pravila:**
- `RulesPopup` dobija red „DIVISION RULE“ sa pravilom trenutne divizije (+ mala dijagram-ikona: rupa sa ✓, rupa sa ⛔, ruka koja pokazuje).
- VS intro prikazuje čip pravila ispod imena sale.
- Na ulasku u novu diviziju: kartica „NEW RULE“ (V11 proslava).
- `HintService`: prvi put kad igrač mora da koluje / prvi put da bira za bota — coach mark na mini mapi rupa (V5).

**Novi `RulesChecks`** (svi postojeći ostaju i moraju proći za `Standard`):
- `CallEight_RightPocketWins`, `CallEight_WrongPocketLoses`, `CallEight_NoCallNeededForGroup`;
- `Blocked_ChosenOnceOnFirstEightTurn`, `Blocked_PersistsAfterMiss`, `Blocked_EightInBlockedLoses`, `Blocked_EightInOtherPocketWins`;
- `OpponentCalls_EightInPickedWins`, `OpponentCalls_EightElsewhereLoses`, `OpponentCalls_ChosenOnce`;
- `CallAll_OwnBallInCalledContinues`, `CallAll_OwnBallInOtherPassesNoFoul`, `CallAll_OpenTableGroupFromCalledPocket`, `CallAll_BreakExempt`, `CallAll_EightWrongPocketLoses`;
- `ChampionRules_GroupCalledEightPicked` (GC: grupa se koluje, 8 po izboru protivnika);
- `Rules_FoulWithEightStillLoses` i `Rules_EightOnBreakRespotted` za svih 6 skupova pravila;
- `BotRespectsRules(200)`: 200 nasumičnih stanja po skupu pravila → bot nikad ne bira kandidata u blokiranu/nedozvoljenu rupu; kolovanje bota = rupa kandidata.

**Kriterijumi:** svi `RulesChecks` zeleni; Bot vs Bot 20 mečeva po skupu pravila bez izuzetka i bez zaglavljenog poteza; screenshotovi `v4_3_call_eight.png`, `v4_3_block_pick.png` (igrač bira za bota), `v4_3_blocked_marker.png`, `v4_3_opponent_picked.png`, `v4_3_call_all.png`, `v4_3_rules_popup_gold.png`.

### V5 — Kolovanje rupe: UI
**Problem:** u Aim pogledu (prvo lice, nisko) često se vidi samo 1–3 rupe, pa tap na rupu u 3D nije uvek moguć.

**Rešenje (`UI/Views/PocketPickerView`, nov):**
- Kad `NeedsCall` (igrač na potezu): levo, iznad LOOK, pojavi se **mini mapa stola** (pogled odozgo, ~240×440 ref px, uspravno kao telefon u ruci), sa:
  - pozicijama kugli (male tačke u bojama), belom i linijom nišana;
  - **6 rupa kao dugmadi** (tap zona ≥ 88 ref px), predviđena rupa (iz guide-a) blago pulsira kao predlog;
  - izabrana rupa: zlatni prsten + ikona ruke; blokirana: crveni prsten sa ⛔; zadata (Platinum/GC): zlatni prsten sa bravicom.
- Tap na rupu u 3D (kad je vidljiva) radi isto — `TableTouchSurface` razlikuje tap (< 12 px, < 0.25 s) na rupu od drag-a nišana.
- Dok rupa nije izabrana: POWER ručka je zatamnjena sa natpisom „TAP A POCKET“; dodir na ručku → mini mapa zasvetli.
- Izabrana rupa je označena i na stolu (prsten na rubu rupe, u boji izbora).
- Igrač bira za bota (V4 tok 1): ista mapa se prikaže **na sredini ekrana, uvećana** (~520×940), uz Watch kameru; tap na rupu = izbor.
- Eventi: `OnPocketCallRequested(int pocket)` (UI → gameplay), `OnPocketCallChanged(int pocket, PocketCallKind kind)` (gameplay → UI), `OnOpponentEightPocketRequested(PocketChoiceKind)` / `OnOpponentEightPocketChosen(int pocket)`.

**Kriterijumi:**
- `eval`: Silver, igrač na 8, power zaključan dok `CalledPocket` = −1; tap na mapu → `CalledPocket` postavljen, power otključan; tap druge rupe menja izbor.
- Mapa ne preklapa LOOK, fine aim, power ni spin (`UiLayoutChecks`).
- Medijana dodatnog vremena po potezu sa kolovanjem prijavljena u `MatchLengthCheck` (pretpostavka igrača: +1.5 s po kolovanju).
- Screenshotovi `v4_3_picker_aim.png`, `v4_3_picker_bot_choice.png`.

### V6 — Izgled: kugle, čoja, lampa, rubovi rupa (F2, F3, F5, F25)
**Kugle (F2) — „previše svetle“:**
- Uzroci za proveru: belo albedo blizu 1.0, jak spot, velika refleksija svetlog panela kanopija u probe-u, exposure.
- Rešenje:
  - albedo bele kugle ~`#ECE8DF` (blago topla slonovača, ne čisto bela); boje brojeva ~10% manje zasićene;
  - smoothness 0.9 → ~0.82 (manji, mekši highlight), specular kao lakirana kugla (jedan jasan, mali highlight);
  - refleksiona proba se ponovo bake-uje sa novom lampom (V6 lampa) — refleksija postaje nekoliko toplih tačaka umesto belog pravougaonika;
  - exposure / intenzitet spota: čoja ostaje u ciljevima ispod, a bela kugla nije izgorela.
- **Merenje:** u Aim i Watch pogledu prosečna luma bele kugle 0.68–0.80 (prijavi pre/posle); pikseli sa lumom ≥ 0.98 ≤ 3% površine kugle (samo highlight).

**Čoja (F3) — „za nijansu tamnija“:**
- Boja čoje u svih 5 `VenueTheme` −10 do −12% vrednosti (V u HSV); cushion i dalje ~8% tamniji od čoje.
- **Merenje:** luma čoje u Watch 0.44–0.48 (bilo 0.53); ivice ≥ 85% centra (kao v3).

**Lampa (F5) — umesto belog pravougaonika:**
- `TablePrefabBuilder.BuildLamp` pravi **pravu bilijar lampu**: zatvoreno telo (gornja ploča + bočne strane, debljina ~4 cm), **3 senila** (Royal: 4) okrenuta nadole, sa toplim sijalicama (emisivni disk unutar senila, ~2700–3000 K izgled), lanci ili šipke do plafona.
  - Glavno svetlo ostaje **jedan spot** sakriven u lampi (jedini sa senkom) — ravnomernost čoje iz v3 ostaje (ivice ≥ 85% centra).
  - **Stil po sali** (`VenueTheme.LampStyle`, jedan parametrizovan builder):

    | Sala | Telo | Senila | Sijalice |
    |---|---|---|---|
    | Basement Bar | crni metal, lanci | zelena staklena kupola | toplo-žute |
    | Downtown Hall | industrijski sivi metal | konusna metalna, bela iznutra | neutralno tople |
    | Riverside Club | tamno drvo | krem tkanina, cilindar | tople |
    | Grand Lounge | mesing | mesingana kupola | tople |
    | Royal Masters | crno + zlatni rub | crna kupola sa zlatnim obodom (4) | tople |

  - Materijali: mat metal/drvo (ne emisivni); emisivni su samo diskovi sijalica i unutrašnjost senila (bloom samo tu).
- **Nije providna:** sve strane zatvorene (normale ka spolja, nema jednostranih quadova).
- **Ne smeta kameri:** kad je kamera iznad donje ivice lampe − 0.1 m, ili lampa zaklanja centar stola u kadru, lampa se glatko sakrije (dithered fade 0.25 s, bez transparentnog sortiranja). U Aim pogledu se lampa vidi samo u gornjem delu kadra kad je kamera dovoljno daleko.
- `CameraChecks.WatchFitsAllPockets` proširen: nijedna rupa nije zaklonjena lampom (raycast od kamere do centra svake rupe ne pogađa vidljivu lampu).
- Screenshotovi po sali: `v4_5_lamp_<sala>_watch.png`, `v4_5_lamp_<sala>_orbit_high.png` (kamera iznad lampe → lampa skrivena, nema „providnog pravougaonika“).

**Rubovi rupa (F25) — svetlucaju:**
- Uzrok (proveri): visok smoothness metalnih kapa + tanka geometrija → specular aliasing.
- Rešenje: smoothness kapa 0.85 → ~0.5, blagi roughness noise iz Poly Haven metala (ili generisan), zaobljen profil kape sa više segmenata (bez oštrih ivica), normale glatke; isto za ivicu obloge rupe.
- **Merenje:** 3 screenshota iste poze u razmaku od 0.1 s uz head sway (metod iz v3 V6), samo region kapa: max razlika piksela i % piksela > 10 — prijavi pre/posle (cilj: bar 50% manje).

**Performanse:** budžet iz v3 (≤ 150 draw calls i ≤ 150k trouglova u Aim pogledu). Lampa ≤ +8 draw calls po sali.

### V7 — Kamera posle udarca: polako, kao čovek (F4)
**Problem:** posle udarca kamera za ~0.45 s (`CameraBlendTime`) skoči u Watch.

**Rešenje (`CameraRig`):**
- Posle `OnShotFired`: kamera **ostaje u Aim pozi** `ShotHoldTime` (0.35 s) i prati belu pogledom (samo rotacija, ka beloj/pogođenoj kugli, glatko).
- Zatim **prelaz u Watch** traje `ShotRiseTime` (1.6 s) sa ease-in-out (smootherstep): prvo se kamera podiže (kao čovek koji se uspravlja), pa udaljava; target pogleda ostaje na kuglama u pokretu dok se ne umire ili dok prelaz ne završi.
- Brzina rotacije kamere ograničena (`MaxCameraTurnDegPerSec` 90°/s), bez skokova.
- Ako igrač dodirne sto tokom prelaza → prelazi u free look (v3 V2), bez skoka.
- Bot: kad bot udari, korisnik ostaje u svom pogledu (v3); Watch → Watch nema prelaza.
- Brojevi u `MatchConfig`: `ShotHoldTime`, `ShotRiseTime`, `MaxCameraTurnDegPerSec`.

**Kriterijumi:**
- `eval` sa pretplatom na `OnShotFired` i snimanjem poze kamere svakih 50 ms: Watch poza dostignuta za 1.95 ± 0.15 s; nijedan korak rotacije > 90°/s · Δt + 1°.
- Sekvenca `v4_5_rise_0.png … v4_5_rise_4.png` (0, 0.4, 0.8, 1.4, 2.0 s).

### V8 — Font, čitljivost, raspored teksta, pauza, ruka (F8, F11, F15, F19, F21)
**Font (F8):**
- **Rubik** (Google Fonts, SIL OFL 1.1): statički TTF Medium (tekst) i Bold (dugmad, naslovi). Download u `_kenney_downloads/fonts/rubik/`, kopija u `Assets/_Art/Fonts/Rubik/`, `CREDITS.md`.
- TMP font asseti se prave **builderom** (`FontAssetBuilder`, nov ili deo `ArtImportBuilder`): statički atlas (ASCII + `★ ☆ ✓ ✕ ▶ ◀ · — ’ “ ” …` + `⛔` ako postoji), SDF, padding za konturu.
  - **Proveri da li Rubik ima `★` (U+2605) i ostale znakove.** Ako nema → fallback TMP font **Noto Sans Symbols 2** (OFL) samo za te znakove. Nijedan znak ne sme da se prikaže kao kvadrat (`□`) — provera u `UiLayoutChecks.MissingGlyphs`.
  - Zvezdice se ponovo prikazuju kao ★/☆ (u v3 su pisane rečima jer Kenney Future nema znak).
- **Velika/mala slova:** naslovi, dugmad, bedževi i banneri VELIKIM slovima; opisi ciljeva, instrukcije tutorijala, hintovi, razlozi kraja meča, opisi štapova i pravila **mala+velika** (čitljivije).
- Svi `TMP_Text` u UI prefabima i scenama prelaze na nove asete (kroz buildere); stari Kenney Future ostaje samo ako ga neki sprite tekst koristi (inače ukloni).
- Kontura i senka teksta iz v3 V13 ostaju (TMP Distance Field materijal).

**Minimalne veličine (F19):**
- Opis cilja (ObjectiveRow): **≥ 36 ref px**, najviše 2 reda; nikad autosize ispod toga — kraći tekst ili 2 reda.
- Ostali tekst: **≥ 28 ref px** (sitne oznake ispod dugmeta ≥ 26).
- `UiLayoutChecks.MinTextSize()` meri **renderovanu** veličinu posle autosize-a u svim stanjima iz liste ispod.

**Bez preklapanja (F15, F19):**
- Nov `UiLayoutChecks.NoOverlap()` (Play mode, Editor/Checks): za listu stanja ekrana skupi sve aktivne `TMP_Text` (po `textBounds` u svetu) i dugmad; za svaki par koji nije roditelj/dete proveri preklapanje > 2 px → greška sa imenima.
  - Stanja: meni (nov igrač, sa CLAIM, sa svim ciljevima gotovim, Grand Champion), ALL OBJECTIVES, DIVISIONS, Cues, TRY, rezultat (pobeda, poraz, novi stage, nova divizija, set ciljeva gotov), VS intro, HUD (Aim, kolovanje, spin overlay, bira se rupa za bota, Watch, Place), pauza, Settings, tutorijal T1–T3 (svaka instrukcija + coach mark + poruke uspeha/neuspeha), RulesPopup za svako pravilo.
  - Pokreće se u commitu 6 i u svakom sledećem commitu koji menja UI.
- **Tutorijal/hint natpisi** (`TutorialView`, `CoachMarkView`, `ToastView`): jedan rezervisan prostor za instrukciju (gornji panel ispod HUD-a, fiksne visine, tekst do 3 reda) i jedan za privremene poruke (iznad fine aim reda); coach mark balon se postavlja tako da ne pokriva ni instrukciju ni cilj na koji pokazuje (proba 4 strane, uzima prvu slobodnu).

**Pauza (F21):**
- Dugme pauze: samo ikona ‖, **≤ 80×80 ref px** (bilo ~118), poluprovidna podloga 60%, u gornjem desnom uglu unutar safe area; ne preklapa panel protivnika. Hit zona može biti ~96 px (nevidljivo proširenje).

**Ruka u tutorijalu (F11):**
- Zamena za belu generisanu ruku: **Noto Emoji** „backhand index pointing“ sa **srednje svetlom bojom kože** (Apache 2.0, PNG 512) — download u `_kenney_downloads/noto_emoji/`, kopija u `Assets/_Art/Hints/`, `CREDITS.md`.
  - Varijante: ruka koja pokazuje (tap), ruka koja vuče (isti sprite, rotiran, sa tragom), dva prsta za pinch (dve ruke ili zaseban emoji ako postoji).
  - Meka senka ispod (generisana), bez bele konture.
  - Ako licenca/download ne odgovara → stani i pitaj (ne crtaj ruku proceduralno).
- `CoachMarkView` koristi novi sprite u svim gestovima (power, fine aim, free look, spin, kolovanje).

**Kontrast (gap iz v3):** zeleni i plavi fill dugmadi još ~10% tamniji ili tekst sa jačom konturom tako da `ContrastChecks.Buttons` pokaže fill ≥ 4.5:1 ili kontura ≥ 7:1; prijavi najslabije.

**Kriterijumi:** `UiLayoutChecks.NoOverlap()` 0 preklapanja, `MinTextSize()` 0 ispod praga, `MissingGlyphs()` 0; screenshotovi `v4_6_menu_font.png`, `v4_6_objective_row_done.png`, `v4_6_pause.png`, `v4_6_hand_power.png`, `v4_6_tutorial_text.png`.

### V9 — Štapovi: unikatan izgled, 4 stat-a, TRY vežbe (F10, F26)
**`CueCatalog.asset` (prerađen, isti id-jevi; nova polja `MaxSpeed`, `Style`):**

| # | Id | Ime | GreenHalf | YellowWidth | Period (s) | Linija (prečnika) | MaxSpeed (POWER) | Izgled |
|---|---|---|---|---|---|---|---|---|
| 1 | `house` | House Cue | 0.10 | 0.30 | 1.30 | 2 | 4.5 | svetli javor, prav konus, bez ukrasa, plava kreda |
| 2 | `club` | Club Cue | 0.12 | 0.30 | 1.40 | 3 | 5.0 | tamni orah, crvena linen omotnica, 2 crvena prstena |
| 3 | `pro` | Pro Cue | 0.14 | 0.30 | 1.55 | 4 | 5.4 | crni ebanovina, zlatni inlay romboidi na zadnjaku, kožna omotnica |
| 4 | `master` | Master Cue | 0.16 | 0.29 | 1.70 | 6 | 5.8 | plavi karbon (šara tkanja), srebrni metalni spoj, srebrna kapa |
| 5 | `champion` | Champion Cue | 0.18 | 0.28 | 1.85 | 8 | 6.2 | bordo lak, zlatni prstenovi, deblji zadnjak sa metalnim prstenom |
| 6 | `legend` | Legend Cue | 0.20 | 0.27 | 2.00 | 10 | 6.5 | zlatni metal sa emisivnim linijama (blagi puls), crni vrh |

- Cene ostaju iz v3 kao start (Club 400, Pro 1000, Master 1700, Champion 5000, Legend 10000); finalne iz `ProgressionSim` (V12).
- **Unikatan izgled** (`TablePrefabBuilder.FillCueMesh` + `GeneratedArtBuilder.CueAlbedo` po `CueStyle`):
  - **silueta:** profil konusa, dužina i položaj spoja, oblik zadnjaka (ravan / zaobljen / metalni prsten), debljina omotnice — bar 3 od ovih se razlikuju između svaka dva susedna štapa;
  - **materijal:** drvo / lak / karbon / metal (normal + smoothness po štapu), omotnica sa teksturom;
  - **PERFECT efekat po štapu** (V7 v3 kreda partikl dobija boju i oblik po štapu: House bela kreda, Club crvene iskre, Pro zlatne, Master plave, Champion bordo-zlatne, Legend zlatni prsten + sjaj štapa).
  - Štap u power traci (`PowerStripView`) koristi sprite tog štapa (već postoji) — renderuje se novi.
- **Rejtinzi** (`CueRatings`, računati, ne upisani):
  - AIM, CONTROL, TIMING kao v3;
  - **POWER** = `MaxSpeed`: 4.5 → 40, 6.5 → 95, linearno;
  - **OVR** = round(0.25·AIM + 0.35·CONTROL + 0.25·TIMING + 0.15·POWER). Prijavi tabelu OVR (cilj: rastući, razmak ≥ 8 između susednih).
- **Kartica u CuesPopup:** veliki render štapa (ceo, horizontalno), 4 trake sa brojem i ikonom (🎯 AIM · 🟩 CONTROL · ⏱ TIMING · 💥 POWER), tooltip po traci (npr. „POWER: harder breaks and power shots“), OVR bedž, razlika prema selektovanom štapu po svakoj traci („+12“ zeleno / „−5“ crveno), oznaka „Gold opponents use this cue“ (zamenjuje „Recommended for“), dugmad **TRY** · BUY/SELECT.

**TRY — vežbe bez ranga (`MatchMode.Practice`, `PracticeConfig.asset`):**
- TRY na bilo kom štapu (i posedovanom) → Match scena u modu `Practice` sa tim štapom, sala trenutne divizije, bez protivnika, bez HUD panela kugli.
- **3 probna udarca** (postavke u `PracticeConfig`, kao `TutorialStep` raspored):
  1. **Long straight, HARD** — „Feel the bar: see how wide the green stays at full power.“;
  2. **Cut 30° from 1.2 m** — „See how far the aim line reaches.“;
  3. **Break** (pun rack, ball in hand u kuhinji) — „Feel the power.“
- Svaki udarac se može ponoviti (RETRY) ili preskočiti (NEXT); posle 3. → ekran poređenja.
- Gornji panel tokom vežbe: „TRYING: PRO CUE“ + mala tabela razlike prema selektovanom štapu: „Green +40% · Bar 19% slower · Aim line ×2 · Power +0.9 m/s“.
- **Ekran poređenja** (`CueTryResultPopup`): stat trake oba štapa jedna uz drugu, broj PERFECT/GOOD u vežbi, BUY (ako ima novca) / „🪙 312 more“ + **PLAY** (vraća u meni) / **BACK TO CUES**.
- Neograničeno, besplatno; nema ★, novčića, ciljeva ni `PendingMatch`. Ne računa se u `MatchesPlayed`.
- Eventi: `OnCueTryRequested(string cueId)`, `OnCueTryFinished(string cueId, int shots, int perfect)`.

**Kriterijumi:**
- `CueChecks.NoIntersection(500)` za svih 6 novih mesh-eva; render 6 štapova jedan ispod drugog (`v4_7_cues_lineup.png`) — korisnik na checkpointu ocenjuje „totalno različito“.
- `eval`: TRY Pro → 3 udarca kroz `DebugReleaseAt` → ekran poređenja; profil nepromenjen (novčići, ★, ciljevi, `MatchesPlayed`).
- Break sa Legend ima veću početnu brzinu bele od House (6.5 vs 4.5 m/s) — `PhysicsChecks.Timings` prijavljuje.
- Screenshotovi `v4_7_cue_card_pro.png`, `v4_7_try_shot1.png`, `v4_7_try_compare.png`, `v4_7_perfect_<id>.png` (6).

### V10 — Tutorijal u 3 udarca (F1, F15, F16, F17)
**Problem:** 5 lekcija je dugo; natpisi se preklapaju; panel YOU / COACH je nepotreban.

**Rešenje (`TutorialConfig.asset`, 3 koraka; `TutorialStepKind`: `AimPower`, `Release`, `Spin`):**

| Korak | Sto | Uči | Bar | Uspeh / pomoć |
|---|---|---|---|---|
| T1 Aim & power | bela + 1 kugla, blag rez ~12°, 0.5 m od ugaone rupe | „Drag on the table to aim. Pull the cue down to shoot.“ Ruka: drag po stolu, pa hvatanje ručke i povlačenje do MED. | uvek zeleno | ubačena; posle 2 promašaja pokaži pravi nišan 1 s; prejako → „Too hard — try MED“ |
| T2 Release | ravno, 0.9 m | „Let go on GREEN — the ball goes exactly where you aim.“ | lakši (period ×1.6, zelena ×1.5) | ubačena sa Green ili Yellow; Red → namerena vs stvarna putanja + „You let go in red — try again“ |
| T3 Spin | ravno, kugla 0.45 m od rupe, bela 0.5 m iza; **zlatni krug** (r = 15 cm) na stolu ~0.5 m iza početnog mesta bele | „Hit the cue ball low to bring it back. Tap the cue ball icon and drag the dot down.“ Coach mark: ikona spina → overlay → povlačenje markera nadole. | lakši | ubačena **i** bela se zaustavi u krugu; pojedinačno: „Potted — now bring the cue ball back into the circle“ / „Lower on the cue ball = more draw“ |
| Kraj | — | „You're ready!“ → `RulesPopup` (sa pravilom Bronze) → prvi meč | — | — |

- **Posle 3 neuspešna pokušaja** korak ide dalje sa „Let's keep going“ (`MoveOnAfter` = 3 za sve korake).
- Uspeh T3 se proverava iz krajnje pozicije bele (`ShotResult.FinalPositions[0]`) i kruga iz `TutorialConfig`. Postavka mora biti dostižna: proveri headless da draw b = −0.5 na MED zaustavlja belu u krugu (podesi rastojanja tako da prolazi u ≥ 70% udaraca sa σ 45 ms i nasumičnim markerom u donjoj trećini).
- **Bez panela kugli** (YOU / COACH) u tutorijalu i u TRY vežbi — `MatchHudView` ih sakriva po `MatchStartInfo.IsTutorial`/`IsPractice`.
- Instrukcija u fiksnom gornjem panelu (V8), SKIP uvek vidljiv u njemu (ne preklapa naslov).
- **Kontekstualni hintovi u meču** (`HintService`, svaki jednom): postojeći (ball in hand, grupe, samo 8, FINE AIM, GREEN posle 2 Red, jak promašaj, free look, LOOK, CLAIM) + novi:
  - prvi scratch posle follow-a: „Hit lower on the cue ball to stop it or bring it back“ (coach mark na ikonu spina);
  - prvo kolovanje (Silver): coach mark na mini mapu rupa;
  - prvi izbor rupe za bota (Gold);
  - prva divizija gde igračev štap < štap divizije: „Gold opponents use the Pro Cue — try it in CUES“.
- Uklanja se hint „Lost a star“ za Star Shield (Shield više ne postoji); hint o gubitku ★ ostaje bez reklame.
- HOW TO PLAY u Settings pokreće ova 3 koraka.

**Kriterijumi:**
- `TutorialChecks.RunThrough()` (3 koraka headless kroz `DebugReleaseAt` + `DebugSetSpin`) → `OnTutorialCompleted(false)`; `Skip()`; `MoveOnAfter` posle 3 promašaja.
- `UiLayoutChecks` za sva stanja tutorijala (V8).
- Ceo tutorijal u Play mode-u, prosečan igrač bez grešaka: ≤ 90 s (prijavi izmereno).
- Screenshotovi `v4_10_t1.png`, `v4_10_t2.png`, `v4_10_t3_overlay.png`, `v4_10_t3_circle.png`, `v4_10_t_done.png`.

---

## 4. Meta zahtevi

### 4.0 Dizajn obrazloženje (zašto ovako)
Primeri igara sa divizijama i šta uzimamo:
- **Clash Royale / Brawl Stars (trophy road):** put se vidi unapred, nagrade su na putu, nova arena = nov sadržaj (karte, mapa). → DIVISIONS ekran sa nagradama na svakom koraku; nova divizija donosi **novu salu + novo pravilo + nov štap kod protivnika**.
- **8 Ball Pool (sobe):** svaka soba je nova težina **i** novo pravilo (call pocket, cushion). → V4 pravila po diviziji; pravilo je deo „novog sadržaja“.
- **Rocket League / League rank:** divizije sa koracima i zaštitom od pada ispod granice. → stage-ovi i checkpointi iz v3 ostaju.
- **Duolingo lige:** jasna proslava napretka, uvek vidiš koliko fali. → proslave (V11) i „X ★ to …“ svuda.

**Šta drži igrača bez dnevnih nagrada (odluka korisnika: ništa dnevno):**
- **Kadenca novog sadržaja** — prosečan igrač dobija nešto novo otprilike svakog dana:

  | Dan (prosečan, ~8 mečeva D1, ~5/dan posle) | Novo |
  |---|---|
  | D1 | tutorijal, Bronze (sala Basement), prvi ciljevi, Club Cue kupljen; Silver checkpoint → **novo pravilo „call the 8“**, nova sala |
  | D2 | Silver ciljevi (draw, follow), TRY Pro |
  | D3 | Gold checkpoint → **protivnik blokira rupu** (igrač bira i za bota), sala Riverside; Pro Cue |
  | D5–D7 | Platinum → **protivnik koluje tvoju 8**, sala Grand Lounge; Master Cue |
  | D2. nedelja | Diamond (koluje se svaki udarac), pa Grand Champion |

- **Uvek blizu sledećeg koraka:** sledeći stage (2–3 ★), sledeći cilj (3 vidljiva, bar 1 rešiv igranjem), sledeći štap (traka novčića), sledeća divizija (pravilo i sala vidljivi unapred na DIVISIONS).
- **Nedovršen cilj kao razlog za sutra:** ciljevi su kumulativni i vidljivi (setovi), igrač ostavlja meni sa „2/5 draw shots“.

**Ne samo happy path:**

| Situacija | Šta igra radi |
|---|---|
| Niz poraza u Bronze | Bronze ne skida ★; ciljevi „igranjem“ (ubaci kugle, PERFECT) i dalje napreduju; poraz daje novčiće. |
| Niz poraza u Silver | −1 ★, stage ne pada; ciljevi igranjem daju novčiće za Club/Pro. |
| Gold+ pad na checkpoint | ne ispod checkpointa (v3); poruka „You can't drop below GOLD III“; nagrade stage-ova se ne dobijaju ponovo pri ponovnom usponu (vidi V11). |
| Igrač gubi jer ima slabiji štap od divizije (zaglavljen ~40–45%) | **„Cue gap“** (odluka korisnika): set ciljeva te divizije ima ≥ 3 cilja rešiva bez pobede, a njihove nagrade + novčići za poraz daju štap divizije u ograničenom broju mečeva (`ProgressionSim` gate, V12); meni prikazuje karticu „Gold opponents use the Pro Cue · 🪙 412 / 1000 · TRY“. |
| Slab igrač i sa dobrim štapom | ostaje u diviziji ali i dalje dobija novčiće i ciljeve; sledeći štap (+1 iznad divizije) daje +6–10 p.p. (v3 V10); prijavljuje se u `ProgressionSim`. |
| Dobar igrač | vrući niz (+1 ★ od 3. pobede zaredom) — brže kroz sadržaj. |
| Vraća se posle više dana | ništa ne propada (bez decay-a, bez energije); meni odmah pokazuje sledeći korak. |
| Napusti meč / ubije aplikaciju | poraz (v3), bez dodatne kazne. |
| Preskoči tutorijal | spin, kolovanje i izbor rupe uče se kroz hintove na prvoj upotrebi. |
| Sve ciljeve divizije završio, a nije napredovao | i dalje dobija novčiće za pobedu/poraz; sledeći set je vidljiv kao zaključan sa uslovom „Reach Platinum“. |

### V11 — Divizije: istaknute, koraci, nagrade, proslave (F12, F14)
**Naziv:** u UI-ju svuda **„DIVISIONS“** (umesto „Ranked Road“): dugme na bedžu, naslov ekrana, tekstovi („3 ★ to GOLD DIVISION“). Imena: „BRONZE DIVISION“ … „DIAMOND DIVISION“, „GRAND CHAMPION“.

**Nagrade napretka (`DivisionCatalog`, novo; dodeljuju se automatski na rezultatu, ne CLAIM):**

| | Bronze | Silver | Gold | Platinum | Diamond | GC |
|---|---|---|---|---|---|---|
| Svaki novi stage (II, I) | 50 | 75 | 100 | 150 | 200 | — |
| Ulazak u diviziju (checkpoint) | — | 150 | 300 | 500 | 800 | 1000 |
| GC: svakih 10 ★ | — | — | — | — | — | 300 |

- Početne vrednosti; finalne iz `ProgressionSim` (V12).
- **Svaka nagrada samo jednom:** dodeljuje se ako je novi stage > `BestStageReached` (postojeći ključ). Pad pa ponovni uspon ne daje ponovo.

**DIVISIONS ekran (`DivisionsPopup`, zamenjuje `RankedRoadPopup`, full-height, skrol, otvara se na trenutnoj poziciji):**
```
┌────────────────────────────────────┐
│           DIVISIONS                │
│ ┌──────────────────────────────┐   │
│ │ 👑 GRAND CHAMPION            │   │  zaključano: „Reach Diamond I“
│ └──────────────────────────────┘   │
│ ┌──────────────────────────────┐   │
│ │ [badge] GOLD DIVISION  [sala]│   │  veliko zaglavlje u boji divizije,
│ │ Riverside Club · Pro Cue     │   │  thumbnail sale, štap protivnika
│ │ RULE: Opponent blocks a pocket│  │  pravilo crne (ikona + tekst)
│ │ ○ GOLD III ★★☆   🪙300 ✓     │   │  koraci kao čvorovi na liniji,
│ │ ● GOLD II  ★☆☆ ← YOU   🪙100 │   │  ★ unutar čvora, nagrada desno
│ │ ○ GOLD I   ☆☆☆        🪙100  │   │  (✓ kad je dobijena)
│ └──────────────────────────────┘   │
│        │ (linija puta)             │
│ ┌──────────────────────────────┐   │
│ │ [badge] SILVER DIVISION …    │   │  pređene divizije: ✓ i prigušene
│ └──────────────────────────────┘   │
│ [ CLOSE ]                          │
└────────────────────────────────────┘
```
- Svaka divizija je jedan **blok** sa jakim zaglavljem (boja i bedž divizije, ime sale, thumbnail, štap protivnika, **pravilo crne**, nagrada za ulazak). Koraci (stage-ovi) su čvorovi na vertikalnoj liniji, a tekući čvor je veći i svetli (puls).
- Između divizija: deblja linija i zastavica „CHECKPOINT — you can't drop below this“.
- Buduće divizije su vidljive (ne skrivene), prigušene, sa bravicom i uslovom.

**Proslave (`MatchResultPopup` + nov `CelebrationView`, preko celog ekrana):**

| Događaj | Proslava |
|---|---|
| +★ | zvezda uleti u bedž (postojeće), zvuk, laka vibracija |
| Novi stage | bedž se okrene (flip) na novi stage, rafal ★, „GOLD II“, nagrada count-up, srednji haptic |
| **Nova divizija** | full-screen sekvenca ~3 s (preskok tapom): zatamnjenje → stari bedž se razbije → novi veliki bedž sa zracima i konfetama u boji divizije → „SILVER DIVISION“ → kartica **„NEW RULE“** (pravilo crne sa ikonom) → kartica nove sale (thumbnail) → nagrada count-up; jak haptic, fanfara |
| Set ciljeva divizije gotov | „SILVER OBJECTIVES COMPLETE“ + bonus count-up |
| Grand Champion | kao nova divizija, kruna |
| Štap postao dostupan | baner „You can buy the Pro Cue!“ + TRY / CUES |

- `OnRankCelebration(RankCelebration)` se proširuje vrstama `StageUp`, `DivisionUp`, `ObjectiveSetComplete`, `ChampionMilestone`; `AudioManager` i `HapticsService` već slušaju.
- Bedž na meniju posle povratka iz meča sa novom divizijom: kratak sjaj + čip „NEW“ na DIVISIONS dok ga igrač ne otvori.

**Kriterijumi:**
- `EconomyChecks`: nagrada stage-a jednom (uspon, pad, ponovni uspon → jedna nagrada); nagrada divizije jednom; GC svakih 10 ★.
- Screenshotovi `v4_9_divisions_bronze.png`, `v4_9_divisions_gold.png`, `v4_9_celebration_division_*.png` (sekvenca 4 kadra), `v4_9_celebration_stage.png`, `v4_9_new_rule_card.png`.

### V12 — Ciljevi po diviziji, štapovi i tempo (F12, F13)
**Struktura (`ObjectiveCatalog.asset` prerađen):**
- **Set od 6 ciljeva po diviziji** (Bronze … Diamond = 30 ciljeva). GC nema set.
- Set se **otključava** ulaskom u diviziju. Nedovršeni ciljevi starijih setova ostaju aktivni.
- **Na meniju 3 vidljiva** (slotovi): redom najstariji nedovršen/nepokupljen iz najstarijeg seta, pa tekući set. Završen cilj → CLAIM (meni ili rezultat) → slot dobija sledeći.
- Napredak se broji **za sve otključane ciljeve istovremeno** (ne samo za 3 vidljiva) — igrač nikad ne „gubi“ napredak zato što cilj nije bio u slotu.
- **Bonus za ceo set** (svih 6 pokupljeno): novčići (tabela) + proslava.
- Svaki set ima **≥ 3 cilja „igranjem“** (napreduju i u porazu: ubačene kugle, PERFECT, spin udarci, rezovi), i **≤ 3 cilja pobedom**. Ciljevi uče veštinu divizije (spin u Silver/Gold, kolovanje u Silver/Diamond).
- Ciljevi ne daju ★ (odluka korisnika: samo novčići).

**Početni setovi** (redosled u setu = težina; nagrade početne, tuning u commitu 12; 🎮 = rešiv igranjem):

| Divizija | Ciljevi | Bonus seta |
|---|---|---|
| **Bronze** | 🎮 Pocket 8 balls (40) · 🎮 Release on PERFECT 5 times (40) · Win a match (50) · 🎮 Use FINE AIM and pot (3) (40) · 🎮 Pot 2 balls in one shot (60) · Win without a foul (60) | 100 |
| **Silver** | 🎮 Draw the cue ball back 30 cm+ (5) (70) · 🎮 Follow through and pot (5) (60) · 🎮 Pocket 20 balls in Silver (70) · Win by potting the 8 in your called pocket (2) (90) · 🎮 3 PERFECT releases in a row (70) · Win 2 matches in a row (100) | 150 |
| **Gold** | 🎮 Pot a cut of 45°+ (5) (100) · 🎮 Stun: pot straight and stop the cue ball within 10 cm (3) (100) · 🎮 Pocket 30 balls in Gold (90) · 🎮 Pot a ball from more than 1.5 m (3) (100) · Win 3 matches in Gold (150) · Run out: clear your group in one visit (200) | 250 |
| **Platinum** | 🎮 Pot with side spin (5) (140) · 🎮 Bank shot: pot after the ball hits a cushion (2) (160) · 🎮 Release on PERFECT 50 times in Platinum (150) · 🎮 Pot 3 balls in one visit (5) (150) · Win with 4+ opponent balls left (200) · Win 5 matches in Platinum (250) | 400 |
| **Diamond** | 🎮 Pocket 40 balls in called pockets (200) · 🎮 Draw the cue ball back 60 cm+ (5) (200) · 🎮 Pot 3 balls in one visit (10) (200) · Run out twice (300) · Win 3 in a row in Diamond (350) · Reach Grand Champion (400) | 600 |

- **Nove vrste** (`ObjectiveKind`), računaju se iz `ShotResult` + `ShotFeedback` u `MatchStatsRecorder` (čist, deli se sa `ProgressionSim`):
  - `DrawShots` (bela posle prvog kontakta krene unazad i pređe ≥ Param m suprotno od početnog pravca, uz ubačenu svoju kuglu);
  - `FollowPots` (b ≥ 0.25 i ubačena svoja kugla, bela nastavi ≥ 10 cm);
  - `StunPots` (ravan udarac ≤ 10°, ubačena, bela se zaustavi ≤ Param od tačke kontakta);
  - `SideSpinPots` (`|a|` ≥ 0.25 i ubačena svoja kugla);
  - `CalledPots` (svoja kugla u kolovanu rupu);
  - `CalledEightWins`;
  - `VisitPots` (≥ Param kugli u jednoj poseti);
  - `*InDivision` varijante postojećih (Param = indeks divizije).
- **ALL OBJECTIVES popup** (`ObjectivesPopup`, dugme „ALL ›“ na panelu ciljeva):
  - tabovi po diviziji (otključane + **sledeća zaključana**), otvara se na tekućoj;
  - redovi: tekst (≥ 36 px), traka, „3/5“, nagrada; stanja: aktivan, CLAIM, ✓ pokupljen, 🔒 (sledeća divizija: tekst i nagrada vidljivi, „Reach GOLD to unlock“);
  - na dnu: bonus seta sa napretkom „4/6“.
  - Dalje od sledeće divizije se ne prikazuje (da ne preplavi).

**Štapovi i „cue gap“:**
- Bot svake divizije igra „njen“ štap (v3). Kad je igračev **najbolji posedovani** štap slabiji od štapa divizije: na meniju je kartica **„Gold opponents use the Pro Cue“** sa trakom novčića, dugmetom TRY i strelicom ka ciljevima „igranjem“ (u panelu ciljeva ti ciljevi dobijaju oznaku 🪙 „cue coins“).
- Novčići za poraz po diviziji (`DivisionDefinition.LossCoins`, bilo fiksno 5): početno 5 / 8 / 12 / 16 / 20 / 25.

**Ciljevi tempa** (`ProgressionSim.Run(2000)`, prerađen: setovi ciljeva sa stopama iz simuliranih mečeva **sa spinom i pravilima divizije**, nagrade stage/divizija, novčići za poraz po diviziji; strategija: kupi štap divizije čim može, pa sledeći):

| Prekretnica | Cilj (medijana mečeva, prosečan) | ≈ dan |
|---|---|---|
| Silver checkpoint | 6–9 | D1 |
| Club Cue | 5–9 | D1 |
| Gold checkpoint | 20–28 | D3 |
| Pro Cue | 18–26 | D2–D3 |
| Platinum checkpoint | 40–50 | D6–D7 |
| Master Cue | 38–50 | D6–D7 |
| Diamond checkpoint | 70–90 | D2. nedelja |
| Champion / Legend Cue | 80+ / 120+ | dugoročno |

- **Gate „cue gap“ (nov):** za slabog (σ 65) i prosečnog igrača, medijana broja mečeva od ulaska u Silver/Gold/Platinum do posedovanja štapa te divizije **≤ 12** (slab) i **≤ 4** (prosečan). Prijavi i 90. percentil.
- Dobar igrač ~30–40% manje mečeva; slab do Gold checkpointa prijavljen (v3: ~D7, prihvaćeno).
- Tuning **samo podacima**, ovim redom: novčići za poraz → nagrade ciljeva (🎮) → nagrade stage/divizija → cene štapova → ★ po stage-u → bot σ. Tabela pre/posle.

**Kriterijumi:**
- `EconomyChecks.All()` prerađen (vidi §8): setovi, otključavanje, napredak za sve otključane, slotovi, CLAIM, bonus seta, nove vrste ciljeva (svaka kroz sintetički `ShotResult`), novčići za poraz po diviziji, nagrade jednom.
- `ProgressionSim` u ciljevima (tabela) + gate „cue gap“.
- Screenshotovi `v4_8_objectives_popup_silver.png`, `v4_8_objectives_locked_gold.png`, `v4_9_menu_cue_gap.png`.

### V13 — Ekrani: meni, rezultat (F6, F14, F19, F23)
**Glavni meni** (`MenuSceneBuilder`, prerađen):
```
┌────────────────────────────────────┐
│ [⚙]                    [🪙 1,240]  │
│           BILLIARDS                │
│  ┌──────────────────────────────┐  │
│  │ [badge] SILVER II  ★★☆       │  │  tap → DIVISIONS
│  │ 1 ★ to SILVER I · DIVISIONS ›│  │
│  └──────────────────────────────┘  │
│  OBJECTIVES                ALL ›   │
│  ┌──────────────────────────────┐  │
│  │ Draw the cue ball back 30 cm │  │  tekst ≥ 36 px, do 2 reda
│  │ ▓▓▓▓░░░░  3/5          🪙 70 │  │
│  ├──────────────────────────────┤  │
│  │ Win 2 matches in a row       │  │
│  │ ░░░░░░░░  0/1         🪙 100 │  │
│  ├──────────────────────────────┤  │
│  │ Pocket 20 balls in Silver ✓  │  │  gotov: zlatni okvir,
│  │ ▓▓▓▓▓▓▓▓ 20/20  [ CLAIM 🪙70]│  │  CLAIM zamenjuje nagradu
│  └──────────────────────────────┘  │
│ [▶            PLAY               ] │  puna širina (gutter 48 px), visina ~170
│  Ranked · Downtown Hall · vs CPU   │
│  [ CUES ]    Your cue: Club · 55   │
│  (kartica cue gap kad važi)        │
│                            v0.4.0  │
└────────────────────────────────────┘
```
- **Redosled (F6):** bedž divizije → **ciljevi** → **PLAY** (dugo, puna širina) → CUES + tvoj štap.
- **Red cilja (F19):** dva reda. Gore tekst (≥ 36 px), dole traka + „3/5“ + nagrada. Kad je gotov: ceo red dobija zlatni okvir, a **desni deo donjeg reda postaje CLAIM dugme sa nagradom** (nagrada se ne prikazuje dvaput). Nema posebnog natpisa „DONE“ koji bi se preklopio.
- Uklanja se: ONLINE · SOON, Starter Pack, NEXT CUE traka (zamenjuje je kartica cue gap kad važi, inače mala linija „Next cue: Pro · 🪙 412/1000“ ispod CUES).

**MatchResultPopup:**
- **Primarno dugme (F23): „NEXT MATCH ▶“** — veliko, zeleno, puna širina, sa portretom i imenom **sledećeg protivnika** („vs Dina Drift“) i salom (ako je nova divizija: „SILVER DIVISION · Downtown Hall“). Sledeći protivnik se bira pre prikaza rezultata (`MatchSetup` pripremljen). Posle animacija dugme blago pulsira.
- **MENU** je malo tekst-dugme ispod (sekundarno).
- Uklanja se: WATCH AD · DOUBLE COINS, WATCH AD · KEEP YOUR STAR.
- Ostaje: naslov, razlog, ★ animacija (+ vrući niz), proslave (V11), novčići (pobeda/poraz + nagrada stage-a/divizije kao zaseban red), završeni ciljevi sa CLAIM (isti red kao na meniju), baner štapa.
- **VS intro:** čip pravila crne ispod sale.
- **HUD:** pauza (V8), spin ikona (V2), mini mapa (V5), oznake rupa (V4).

**FTUE tok:** prvo pokretanje → tutorijal (V10, 3 koraka) → `RulesPopup` → prvi meč Bronze III (Rookie Ray) → rezultat → meni sa coach mark-om na bedžu („Win to earn ★. Each division brings a new 8-ball rule.“) i na ciljevima („Objectives pay coins — some count even when you lose“).

**Settings:** Sound, Music, Haptics, HOW TO PLAY, RULES, verzija. (Bez ankete.)

**Kriterijumi:** stanja profila kroz `eval` (nov, Bronze II sa CLAIM, Silver I pred checkpoint, Gold sa cue gap-om, sve ciljeve seta gotove, GC) → screenshot menija, rezultata (pobeda, poraz, novi stage, nova divizija), VS intro; `UiLayoutChecks` 0; `ContrastChecks`.

### V14 — Uklanjanje proba (F20, F22, F24)
Briše se (kod, prefabi kroz buildere, eventi, ključevi profila, README):
- `RewardedAdService`, `SimulatedAdOverlay`, `RewardedPlacement`, `OnRewardedAdRequested/Finished`, Star Shield i Double coins (u `ProfileService`, `MatchOutcome.StarShieldAvailable/DoubleCoinsAvailable`, `MatchResult` polja);
- `StarterPackPopup`, `ComingSoonPopup`, `OnFakeDoor`, `FakeDoorId/Action`, `OnOnlineRankedRequested`;
- `SurveyPopup`, `OnSurveyAnswered/Skipped`, `SurveyMissing`;
- `EconomyConfig` polja za rewarded, Starter Pack i anketu.
- **Grep posle commita mora biti prazan:** `grep -rnE "Rewarded|StarShield|DoubleCoins|StarterPack|FakeDoor|Survey|OnlineRanked|ComingSoon" Assets/_Scripts` (osim komentara o migraciji u `ProfileService`).
- README: ukloni upozorenje „this build is NOT for a store“ za simulirane reklame (više ih nema).

**Profil v3** (`ProfileService`, `ProfileVersion` 3):
- Migracija v2 → v3 **čuva**: `Coins`, `Wins`, `Losses`, `MatchesPlayed`, `WinStreak`, `LossStreak`, `Division`, `Stage`, `Stars`, `ChampionStars`, `BestStageReached`, `SelectedCue`, `CueOwned_*`, `TutorialDone`, `RulesSeen`, `HintsShown`, `ShotsTaken`, zvuk.
- **Briše:** `StarShields`, `RewardedDay` + brojači, `StarterPackShows`, `StarterPackTapped`, `OnlineNotify`, `SurveyDone`, `ObjectiveIndex`, `ObjectiveSlot_*`, `ObjectiveProgress_*`.
- **Novo:** `ObjectiveProgress_<id>`, `ObjectiveClaimed_<id>`, `ObjectiveSetBonus_<divisionId>`, `RewardGrantedStage` (= najviši stage za koji je nagrada data), `ChampionMilestones`, `InstallDay` (UTC datum prvog pokretanja, za analitiku), `SessionCount`, `MatchesToday` + `MatchesTodayDate`.
- Migracija: setovi ciljeva svih dostignutih divizija se otključavaju, napredak 0; `RewardGrantedStage` = `BestStageReached` (bez retroaktivnih nagrada).
- `MatchSetup.Mode`: `VsBot` | `Tutorial` | `Practice`; `PracticeCueId`.

---

## 5. Analitika (V15) — pokrivenost i MVP validacija (F22, F28)

### V15 — `AnalyticsService` (Systems, persistent)
**Problem (pregled v3):** README mapira GameEvents → analytics evente, ali:
- PLAY AGAIN i MENU dižu isti `OnPlayRequested` / `OnMenuRequested` bez izvora → „Play Again rate“ iz `release_plan` §0.1 se ne može izračunati pouzdano;
- `MatchResult` nema trajanje, broj udaraca, % PERFECT, faule, `match_index`, štap, pravilo;
- `ShotFeedback` nema ishod udarca (ubačena svoja, faul) ni spin;
- nema sesija, `matches_today`, prikaza ekrana (DIVISIONS, OBJECTIVES, CUES, TRY), kupovine sa kontekstom (da li je probao štap), performansi.

**Rešenje:**
- `Systems/AnalyticsService` sluša `GameEvents` i pravi **konačne analytics evente** (imena i parametri iz tabele ispod), sa zajedničkim parametrima (`app_version`, `session_index`, `days_since_install`, `division`, `stage`, `selected_cue`).
- **Izlaz u ovoj fazi:** `Debug.Log` u Editoru + JSONL fajl `Application.persistentDataPath/analytics.jsonl` (rotacija na 1 MB) u Development buildu; u release buildu samo u memoriji (poslednjih 200 za debug). **Jedno mesto** sa `// PHASE 2: FirebaseAnalytics.LogEvent(name, params)`. Bez interfejsa i fabrike.
- `AnalyticsService` je jedini koji zna imena analytics evenata; UI i gameplay dižu samo `GameEvents`.
- **Novi/izmenjeni GameEvents** (potrebni za analitiku):
  - `OnNextMatchRequested` (rezultat) — odvojeno od `OnPlayRequested` (meni) i `OnMenuRequested(MenuSource)`;
  - `OnScreenShown(ScreenId)` (Menu, Divisions, Objectives, Cues, CueTry, Settings, Rules, Result);
  - `OnShotSettled(ShotSettledInfo)` (posle umirenja kugli: shooter, ubačene svoje/protivničke, faul, kolovana rupa ok, spin, snaga, zona, ugao reza, rastojanje, putanja bele unazad);
  - `OnAppFocusChanged(bool)` (sesije: nova sesija posle ≥ 30 min u pozadini);
  - `MatchResult` proširen: `DurationSec`, `Shots`, `PerfectPct`, `Fouls`, `SpinShots`, `MatchIndex`, `CueId`, `RuleSet`, `EndReason` (enum, ne samo tekst), `ResultScreenSeconds` (meri `MatchResultPopup` do NEXT/MENU);
  - `OnCueTryRequested/Finished`, `OnPocketCallChanged`, `OnOpponentEightPocketChosen` (V5, V9).
- **Performanse:** na kraju meča `perf` event (prosečan FPS, 5. percentil FPS, model uređaja) — iz `FrameTimingSampler` (Systems, bez alokacija).

**Analytics eventi (v4):**

| Event | Kada | Parametri |
|---|---|---|
| `session_start` | prvo pokretanje i povratak posle ≥ 30 min | session_index, days_since_install |
| `tutorial_step` | početak / uspeh / neuspeh / prelazak / skip koraka | step (1–3), name (aim_power / release / spin), result, attempt |
| `tutorial_complete` | kraj ili skip | skipped, duration_s, attempts_total |
| `screen_view` | otvaranje ekrana | screen (menu / divisions / objectives / cues / cue_try / settings / rules / result) |
| `match_start` | početak meča | division, stage, rule_set, opponent, venue, cue, match_index, matches_today |
| `shot` | posle umirenja kugli (samo igrač) | shot_index, zone, power, spin_x, spin_y, is_break, fine_aim, called, called_ok, pocketed_own, pocketed_opp, foul, cut_deg, distance_m |
| `match_end` | kraj meča | result, reason, duration_s, shots, perfect_pct, fouls, spin_shots, match_index, stars_delta, streak_bonus, rank_before, rank_after, coins, cue, rule_set |
| `post_match_action` | NEXT MATCH ili MENU na rezultatu | action (next / menu), result, seconds_on_result |
| `forfeit` | predaja ili napušten meč | division, shots, reason (pause_forfeit / app_killed) |
| `rank_change` | posle meča kad se menja stage/divizija | from, to, kind (stage_up / stage_down / division_up / champion_milestone), reward |
| `objective_completed` / `objective_claimed` | završen / pokupljen | objective_id, division, reward, matches_since_unlock |
| `objective_set_completed` | ceo set | division, bonus |
| `cue_try` | početak i kraj TRY | cue, phase (start / end), shots, perfect |
| `cue_purchased` | kupovina | cue, coins_before, matches_played, tried_before, division |
| `cue_selected` | izbor štapa | cue |
| `eight_pocket_choice` | igrač bira rupu za bota | rule_set, pocket, seconds_to_choose |
| `settings_changed` | zvuk/muzika/haptic | setting, value |
| `perf` | kraj meča | fps_avg, fps_p5, device_model |

- **User properties:** `division`, `stage`, `best_stage`, `best_cue`, `selected_cue`, `matches_played`, `days_since_install`, `tutorial_done`, `app_version`.
- Retencija (D1/D3/D7) i trajanje sesije računaju se iz `first_open`/`session_start` (Firebase ih ima automatski; lokalni `session_start` ih pokriva za proveru).

**Kriterijumi:**
- Nov `AnalyticsChecks.Flow()` (Play mode): obrisan profil → tutorijal (headless) → prvi meč (`DebugSetupWin`) → rezultat → NEXT MATCH → forfeit → MENU → DIVISIONS → OBJECTIVES → CUES → TRY (3 udarca) → kupovina → Settings. Proveri da su se pojavili svi eventi iz tabele (osim `perf` i `eight_pocket_choice`, koji se proveravaju posebnim `eval`-om), **sa svim parametrima ne-null**, u ispravnom redosledu.
- `AnalyticsChecks.NoAllocPerShot`: `shot` event ne alocira više od jednog stringa po udarcu (log izvan meča ok).
- README: tabela „analytics event → GameEvents izvor → parametri“ zamenjuje v3 tabelu.

### 5.1 Da li se MVP može validirati bez fake ad-ova, ponuda i notifikacija?
**Kratko: da, za hipotezu koju testiramo — ne, za monetizaciju.**
- Hipoteza iz `release_plan.md` §0 je o **mehanici i povratku** („…dovoljno da se vrate sutra i sledeće nedelje“). Odluka GO / ITERATE / STOP po planu = **retencija + bar 2 signala mehanike**. Ništa od toga ne traži reklame ni ponude.
- Fake door i rewarded su merili samo **nameru plaćanja** (sporedna grupa). Bez njih se monetizacija ne meri u MVP-u. Posledica: posle GO, prvi korak je **pravi** rewarded (AdMob) — opt-in tada meri stvarno ponašanje, ne simulirano. Ovo je svestan rizik, upisan u §10.
- Notifikacije nisu ni bile u buildu; ne trebaju za merenje.

### 5.2 Metrike za odluku (MVP test)
Pragovi iz `release_plan.md` §0.1 gde postoje; **(p)** = moja procena, zaključati pre launch-a.

| Grupa | Metrika | Kako (event) | GO | ITERATE | STOP |
|---|---|---|---|---|---|
| **Retencija** (glavno) | D1 | kohorta `first_open` → `session_start` dan 1 | ≥ 30% | 20–30% | < 20% |
| | D3 | isto, dan 3 | ≥ 15% (p) | 8–15% (p) | < 8% (p) |
| | D7 | isto, dan 7 | ≥ 6% | 3–6% | < 3% |
| **Angažovanje** | mečeva po igraču na D0 | `match_end` | ≥ 3 | 2–3 | < 2 |
| | mečeva po DAU (D1–D7) | `match_end` / DAU | ≥ 4 (p) | 2–4 (p) | < 2 (p) |
| | sesija po DAU, trajanje sesije | `session_start`, Firebase engagement | prati | | |
| **FTUE** | tutorijal završen (ne skip) | `tutorial_complete` / `first_open` | ≥ 75% | 55–75% | < 55% |
| | gde pada (po koraku) | `tutorial_step` funnel | korak sa najvećim padom → iteracija | | |
| | prvi meč završen | `match_end` match_index = 1 | ≥ 60% | 40–60% | < 40% |
| | drugi meč počet | `match_start` match_index = 2 | ≥ 50% (p) | | |
| **Signal mehanike** | NEXT MATCH posle meča | `post_match_action` next / ukupno | ≥ 50% | 30–50% | < 30% |
| | % PERFECT raste od meča 1 do 10 | `match_end.perfect_pct` po match_index | raste | ravno | — |
| | upotreba spina (od meča 5) | `shot.spin_*` ≠ 0 / udarci | ≥ 15% (p) | 5–15% (p) | < 5% (p) |
| | predaja / napušten meč | `forfeit` / `match_start` | ≤ 10% (p) | 10–20% (p) | > 20% (p) |
| **Progresija** | dostigli Silver do D1 / Gold do D3 (od aktivnih) | `rank_change` | prati vs `ProgressionSim` (≈ 70% / 50%) | | |
| | zaglavljeni (≥ 12 mečeva bez stage-a) | `rank_change`, `match_end` | ≤ 20% aktivnih (p) | | |
| | poslednji događaj pre odlaska | poslednji `match_end` (poraz/pobeda, niz) | dijagnoza | | |
| **Meta** | kupili 1. štap do D1 | `cue_purchased` | ≥ 50% (p) | | |
| | TRY → kupovina istog štapa | `cue_try` → `cue_purchased` | prati | | |
| | ciljevi: % završenih po cilju | `objective_completed` | cilj < 10% posle 3 dana → pretežak | | |
| **Tehnika** (gate) | crash-free users | Crashlytics (faza 2) | ≥ 99.5% | — | < 99% |
| | FPS p5 ≥ 30 | `perf` | ≥ 90% mečeva (p) | | |
| **Kvalitativno** | ocena u Play Store-u, recenzije, 5 razgovora sa testerima | — | — | | |

- Uzorak i benchmark iz `release_plan.md` §0.1 (≥ 800 instalacija; D1 medijana ~22%).
- **Odluka** ostaje po planu: retencija + bar 2 signala mehanike. Ako je retencija u ITERATE, a mehanika dobra → problem je meta/sadržaj.

---

## 6. Eventi (novi i izmenjeni; upiši u README tabelu)
| Event | Payload | Ko diže | Ko sluša |
|---|---|---|---|
| `OnSpinChanged` / `OnSpinReset` | `Vector2 tip` / — | `SpinControlView` / `PlayerShotController` (posle udarca) | `PlayerShotController` / `SpinControlView` |
| `OnPocketCallRequested` | `int pocket` | `PocketPickerView`, `TableTouchSurface` | `PlayerShotController` |
| `OnPocketCallChanged` | `int pocket, PocketCallKind kind` | `PlayerShotController`, `MatchController` | `PocketPickerView`, `TableView` (oznake rupa), `PowerStripView` (zaključavanje) |
| `OnOpponentEightPocketRequested` / `OnOpponentEightPocketChosen` | `PocketChoiceKind` / `int pocket` | `MatchController` / `PocketPickerView` | `PocketPickerView`, `CameraRig` / `MatchController` |
| `OnShotFired` (izmenjen) | `ShotFeedback` + `Tip`, `Elevation`, `CalledPocket` | controlleri | postojeći + `AnalyticsService` |
| `OnShotSettled` | `ShotSettledInfo` | `MatchController` | `AnalyticsService`, `ProfileService` (ciljevi) |
| `OnMatchStarted` (izmenjen) | + `RuleSet`, `RuleText`, `IsPractice` | `MatchController` | HUD, `VsIntroView` |
| `OnMatchEnded` (izmenjen) | `MatchResult` + polja iz V15, + `NextOpponent` | `MatchController` | `MatchResultPopup`, `AnalyticsService` |
| `OnNextMatchRequested` | — | `MatchResultPopup` | `MatchController` |
| `OnMenuRequested` (izmenjen) | `MenuSource` (result / pause / practice) | popupi | `MatchController` |
| `OnScreenShown` | `ScreenId` | popupi, `MainMenuView` | `AnalyticsService` |
| `OnDivisionsRequested` (zamenjuje `OnRankedRoadRequested`) | — | `RankBadgeView` | `MenuController` |
| `OnObjectivesRequested` | — | panel ciljeva („ALL ›“) | `MenuController` |
| `OnRankCelebration` (proširen) | + `StageUp`, `DivisionUp`, `ObjectiveSetComplete`, `ChampionMilestone` | `MatchResultPopup`, `CelebrationView` | `AudioManager`, `HapticsService` |
| `OnCueTryRequested` / `OnCueTryFinished` | `string cueId` / `string cueId, int shots, int perfect` | `CueCardView` / `MatchController` (Practice) | `MenuController` / `AnalyticsService`, `CueTryResultPopup` |
| `OnAppFocusChanged` | `bool focused` | `Bootstrap` | `AnalyticsService` |
| **Uklonjeni** | `OnRewardedAdRequested/Finished`, `OnFakeDoor`, `OnOnlineRankedRequested`, `OnSurveyAnswered/Skipped`, `OnRankedRoadRequested` | | |

---

## 7. Konfiguracija (novi brojevi, sve u SO)
- **`MatchConfig`:**
  - V1: `SpinMaxOffset`, `SquirtDegPerR`, `SideTransfer`, `SideSpinDecel`, `SwerveFactor`, `SwerveMinElevation`, `BallFriction`, `CushionRestitutionSlow/Fast`, `CushionFastSpeed`, `CushionSpinFriction`, `HardSpeed`, `PowerSharedUntil`, `PowerExponent` (novi), `MinSpeed`; `MaxSpeed` se seli u `CueDefinition`;
  - V3: debljine i boje linija, fade, veličina strelice, brzina tačaka 0.12;
  - V5: veličina mini mape, tap zona rupe, prag tap-a (12 px, 0.25 s);
  - V6: boja bele kugle, smoothness kugli i kapa (ili u `VenueTheme`), lampa fade prag;
  - V7: `ShotHoldTime` 0.35, `ShotRiseTime` 1.6, `MaxCameraTurnDegPerSec` 90;
  - V8: `PauseButtonSize` 80, min veličine teksta (36/28/26).
- **`CueCatalog`:** V9 tabela (+ `MaxSpeed`, `Style`, PERFECT efekat).
- **`DivisionCatalog`:** + `Rules` (`RuleSetId`), `RuleText`, `StageReward`, `DivisionReward`, `LossCoins`; GC: `ChampionMilestoneStars` 10, `ChampionMilestoneReward` 300.
- **`ObjectiveCatalog`:** setovi po diviziji (V12), `SetBonus` po diviziji, nove vrste.
- **`BotProfile` po diviziji:** + `UsesSpin` (Gold+), `SpinVariants` (centar/follow/draw/stun), `SpinCandidates` (koliko najboljih kandidata dobija proveru spinom).
- **`EconomyConfig`:** bez rewarded/Starter Pack/ankete; `StartCoins` 0.
- **`TutorialConfig`:** V10 (3 koraka, krug za belu, `MoveOnAfter` 3).
- **`PracticeConfig`** (nov): 3 TRY udarca (V9).
- **`VenueTheme` × 5:** + `LampStyle` (telo, senila, broj senila, sijalice), tamnija čoja.

---

## 8. Nove i izmenjene verifikacione provere (`Editor/Checks`)
| Check | Šta |
|---|---|
| `PhysicsChecks.Spin()` + prošireni `Collisions`, `Timings`, `Determinism`, `NoTunneling` | V1 tabela |
| `PredictorChecks.MatchesSimulation()` (prošireno) | predikcija = simulacija sa spinom, throw-om, masse-om (V1) |
| `GuideChecks.LengthRule()` (prošireno) | pravilo dužine + kriva bele (V3) |
| `ControlChecks.SpinMapping()` | V2 |
| `RulesChecks.*` (novi, §V4) + `BotRespectsRules(200)` | V4 |
| `CameraChecks.WatchFitsAllPockets` (proširen: lampa ne zaklanja) + `ShotRise` | V6, V7 |
| `UiLayoutChecks.NoOverlap()` / `MinTextSize()` / `MissingGlyphs()` | V8 (i svi UI commitovi posle 6) |
| `ContrastChecks.Buttons` | V8 |
| `CueChecks.NoIntersection(500)` (6 novih mesh-eva) | V9 |
| `TutorialChecks.RunThrough()` / `Skip()` / `MoveOn()` | V10 |
| `EconomyChecks.All()` (prerađen) | start 0; ★ i vrući niz; poraz po pravilu divizije; novčići za poraz po diviziji; nagrade stage/divizija/GC **jednom** (uspon–pad–uspon); setovi ciljeva: otključavanje, napredak za sve otključane, slotovi, CLAIM, bonus seta; svaka nova vrsta cilja kroz sintetički `ShotResult`; TRY ne menja profil; kupovina bez novca odbijena; napušten meč = poraz; migracija v2 → v3 |
| `BotBalanceCheck.Run(200)` | v3 opsezi (V1) |
| `ShotDifficultyCheck.All` | `ReleaseTable` nepromenjen (±5 p.p.); `PotRates` monotono, prijava apsolutnih |
| `DivisionBalanceCheck.Run(400, strict: true)` | sa pravilima i spinom bota po diviziji; gate iz v3 (prosečan na cilju ±6, redosled dobar ≥ prosečan − 5, prosečan ≥ slab + 5) |
| `ProgressionSim.Run(2000)` | V12 tabela + gate „cue gap“ |
| `MatchLengthCheck.Run(100)` | medijana ≤ 6 min po diviziji (kolovanje i izbor rupe uključeni) |
| `AnalyticsChecks.Flow()` / `NoAllocPerShot` | V15 |
| Grep proba (V14) i slojeva (README) | prazno |

---

## 9. Plan commitova (12)

### Commit 1 — `physics: cue tip spin, throw, squirt, swerve, speed-dependent cushions, power curve, simulated prediction`
**Obim:** V1 (`BallState.Side`, `ShotParams.Tip/Elevation`, `PoolPhysics` udarac/kretanje/sudari/zidovi, kriva snage i `CueDefinition.MaxSpeed`, `ShotPredictor` preko simulacije, `BallView` rotacija sa `Side`), rekalibracija `MatchConfig`; bot i dalje centar (samo procena brzine preko nove krive).

**Verifikacija:**
- `PhysicsChecks.*` (uključujući `Spin`), `PredictorChecks` (500), `RulesChecks`, `AimChecks`, `CueChecks`, `BotBalanceCheck.Run(200)`, `ShotDifficultyCheck.All` — pre/posle brojevi;
- vreme predikcije (ms) i 0 B GC;
- sekvence screenshotova (draw, follow, side, masse). Do commita 2 spin se zadaje kroz `DebugSetSpin`.

### Commit 2 — `spin control & guide: cue ball spin picker, thin fading guide lines with real path`
**Obim:** V2, V3.

**Verifikacija:** `ControlChecks.SpinMapping`, `GuideChecks.LengthRule` (prošireno), 0 B GC u Aim fazi, screenshotovi.

### Commit 3 — `rules: division 8-ball rules, called and blocked pockets, pocket picker, bot calls and choices`
**Obim:** V4, V5 (`RuleSetId`, `RulesState`, `EightBallRules`, `MatchController` tok, `BotBrain` pravila i izbor rupe, `PocketPickerView`, oznake rupa u `TableView`, `RulesPopup` red pravila, VS intro čip, hintovi kolovanja). `DivisionCatalog.Rules` popunjen.

**Verifikacija:**
- svi `RulesChecks` (stari za `Standard` + novi) i `BotRespectsRules(200)`;
- Bot vs Bot 20 mečeva po skupu pravila;
- `eval` kriterijumi V5; screenshotovi.

### Commit 4 — `bot & balance: spin choice per division, rules-aware candidates, recalibration`
**Obim:** bot spin u Gold+ (`BotBrain`: za `SpinCandidates` najboljih kandidata simulira `SpinVariants`, bira po skoru sledećeg udarca i riziku scratch-a; Bronze/Silver centar), `DivisionBalanceCheck` sa pravilima, rekalibracija `BotProfile` σ.

**Verifikacija:**
- `BotBalanceCheck.Run(200)`, `DivisionBalanceCheck.Run(400, strict: true)`, `MatchLengthCheck.Run(100)` — tabele pre/posle;
- vreme razmišljanja bota (worker thread) ≤ 1.5 s medijana u Diamond (prijavi);
- Bot vs Bot u Gold: bot koristi draw/stun gde treba (prijavi % udaraca sa spinom po diviziji).

**→ CHECKPOINT 1:** APK. Korisnik testira:
- osećaj fizike: snaga (MED/HARD), draw/follow/stun, side na zidu, masse uz rail, throw;
- spin kontrolu (ikona, velika kugla, marker, dupli tap, reset posle udarca);
- novi guide (tanke linije, kriva bele, prsten ghost);
- pravila: Silver kolovanje 8, Gold blokiranje (i kad bira za bota), Platinum, Diamond kolovanje svakog udarca, GC (kroz debug profil);
- da li bot u višim divizijama deluje pametnije.

Čekaj potvrdu ili izmene.

### Commit 5 — `table look & camera: billiard lamps per venue, calmer balls, darker cloth, no rim shimmer, slow rise after the shot`
**Obim:** V6, V7.

**Verifikacija:**
- luma merenja (bela kugla, čoja, ivice/centar), shimmer rubova pre/posle;
- `CameraChecks` (lampa ne zaklanja), `ShotRise` sekvenca;
- draw calls i trouglovi po sali; screenshotovi.

### Commit 6 — `ui clarity: rubik font, minimum text sizes, no overlaps, objective rows, small pause, skin-tone hand, contrast`
**Obim:** V8 (font builder, svi UI prefabi i scene kroz buildere, `UiLayoutChecks`, red cilja, pauza, ruka, kontrast).

**Verifikacija:** `UiLayoutChecks.*` (0), `ContrastChecks.Buttons` (izveštaj), screenshotovi svih glavnih ekrana pre/posle.

### Commit 7 — `cues: unique looks, power stat, perfect effects per cue, try practice`
**Obim:** V9 (`CueCatalog` + `CueStyle`, mesh/albedo/materijali, `CueRatings` POWER/OVR, kartica, `PracticeConfig`, `MatchMode.Practice`, `CueTryResultPopup`).

**Verifikacija:** `CueChecks`, OVR tabela, `eval` TRY bez promene profila, screenshotovi (lineup, kartica, TRY, PERFECT po štapu).

### Commit 8 — `meta: division objective sets, stage and division rewards, cue gap, loss coins, profile v3, remove probes`
**Obim:** V12 podaci i logika, V11 nagrade (logika), V14 (uklanjanje proba), profil v3, `MatchStatsRecorder` nove vrste ciljeva.

**Verifikacija:**
- `EconomyChecks.All` (prerađen), grep proba prazan;
- Play: meč u Bronze i Silver do rezultata; napredak ciljeva (uključujući draw/follow kroz `DebugSetSpin`), nagrada stage-a jednom;
- migracija sa sačuvanim v2 profilom (rang, novčići i štapovi ostaju).

### Commit 9 — `screens: divisions, all objectives, celebrations, menu order, next match`
**Obim:** V11 (DIVISIONS, proslave), V12 ObjectivesPopup, V13 (meni, rezultat, VS intro, cue gap kartica, FTUE coach tekstovi).

**Verifikacija:**
- stanja profila kroz `eval` (V13 lista) → screenshotovi menija, DIVISIONS, OBJECTIVES, rezultata (pobeda, poraz, stage, divizija, set), proslava (sekvenca);
- `UiLayoutChecks` 0, `ContrastChecks`, grep slojeva.

**→ CHECKPOINT 2:** APK sa profilom resetovanim na Bronze III (tutorijal već završen). Korisnik testira: prvih ~10 mečeva, Silver checkpoint (proslava, kartica pravila, nova sala), ciljeve i ALL OBJECTIVES, CLAIM bez preklapanja, DIVISIONS ekran, NEXT MATCH, štapove (izgled, stat-ovi, TRY), font i čitljivost, lampu, kugle, čoju, kameru posle udarca, rubove rupa, pauzu.

Čekaj potvrdu.

### Commit 9.1 — `aiming feel: steady camera, full guide, bird's-eye pocket pick, pinch zoom, smooth rise`
Povratna informacija korisnika posle checkpointa 2 (2026-10-30). Odluke korisnika: (1) pticja perspektiva za **svako** biranje rupe, mini mapa nestaje; (2) zum se **resetuje posle udarca**; (3) kamera je **potpuno mirna dok se natеže štap**.

**Problemi i zahtevi**
| # | Šta korisnik vidi | Uzrok (iz koda) | Šta se radi |
|---|---|---|---|
| A | Na početku natezanja je snaga mala, putanja ne stiže do kugle, kamera se pomeri, pa se pomeri opet kad snaga stigne do kugle: kamera se trese pri svakom udarcu | `CameraRig.FitAimDistance` kadrira prema predikciji, a predikcija zavisi od trenutne snage | (1) **Vodilja do prve kugle se crta uvek**, pri bilo kojoj snazi: linija bele do ghost kugle i smer kugle koju gađaš; snaga menja samo ono posle kontakta (pravilo dužine iz V3). Ispod snage koja doseže kontakt: linija do kontakta puna, deo posle kontakta kratak i prigušen. (2) Kadar kamere se računa iz **geometrije bez snage** (bela, ghost, kugla, njena rupa), a čim počne natezanje (`TurnPhase.Pulling`) kadar se **zamrzne** do ispaljivanja. |
| B | Linija je tanka i sakrije je obojena kugla | linija leži na čoji, ispod kugli; debljina iz `MatchConfig` | debljina `GuideCueWidth` / `GuideObjectWidth` ×1.6 (podatak + asset); materijal vodilje crta **preko kugli** (ZTest Always, redosled posle neprozirnih), ali ne preko ivica stola; preko kugle malo prigušena da se kugla i dalje vidi. |
| C | Centriranje mesta udarca (reset / dupli tap centra) pomera štap | pomak vrha štapa prati `_tip` odmah | štap i vodilja se **ne pomeraju** pri izboru ili resetu spina; pomak vrha se prikazuje na belom (marker) a štap ga primenjuje tek pri natezanju, glatko. |
| D | Biranje rupe na malom stolu | `PocketPickerView` (mini mapa) | novi `CameraMode.PocketPick`: kamera u **pticu** (pitch ~80°, ceo sto: sve rupe i sve kugle), jedan glatki prelazak. Preko rupa tappable oznake sa zonom ≥ 120 px; naslov na vrhu govori **za šta se bira** („CALL YOUR POCKET“, „BLOCK A POCKET FOR THE 8“, „PICK THE 8-BALL POCKET FOR THE BOT“); iznad predložene rupe pulsira balon „TAP“ i strelica; izabrana dobija zlatni prsten i ruku; blokirana crveni prsten. Posle izbora kamera glatko ide u `Aim`. Mali dugme „POCKET“ u HUD-u vraća pticu dok se ne natežе (promena poziva). Mini mapa (prefab, builder, `PocketMapInfo` prikaz) se uklanja; `OnPocketMapChanged` ostaje za logiku/oznake rupa. |
| E | Odzumiranje dva prsta tokom nišanjenja | nije moguće u `Aim` | **pinch u `Aim`**: zum se kreće u rasponu `AimZoomMin` … `AimZoomMax` (min = čovek nagnut uz belu, ne ispod `CueClearance`; max = visina na kojoj se vidi ceo sto, kao `Watch`), dva prsta pomeraju kadar **levo-desno samo malo** (`AimPanMax` ≈ 0.25 m). Udarac radi normalno dok je zumirano (natezanje jednim prstom na traci; pinch se ignoriše dok traje natezanje). Zum se **resetuje posle udarca** i na početku sledećeg nišanjenja. Svi brojevi u `MatchConfig`. |
| F | Posle udarca odzumiranje je neprirodno, ekran se „vrti“ usred njega | `ShotRise` meša pomeranje pozicije, rotaciju ka `WatchRotation` i gaze praćenje (lag 0.25 s) i sway istovremeno; rotacija ide najkraćim putem i menja yaw | jedna interpolirana putanja od `Aim` do `Watch` poze (ease in-out, `ShotRiseTime` 1.6–2.0 s): pozicija i rotacija idu istom krivom, **bez promene yaw-a** osim koliko je potrebno, gaze praćenje i sway tek kad se stigne; `MaxCameraTurnDegPerSec` 90 → 35 u riziku; ubrzanje rotacije ograničeno (`CameraTurnAccel`). |

**Provere (nove)**
- `GuideChecks.ReachesContactAtAnyPower`: 200 nišana × snage 5–100 %, vodilja uvek stiže do prvog kontakta (greška < 1 mm), deo posle kontakta raste monotono sa snagom.
- `CameraChecks.AimStableWhilePulling`: za svaki nišan snaga 0→100 % u koracima, poza kamere se ne menja (< 0.5 mm i < 0.05°) od početka natezanja do ispaljivanja.
- `CameraChecks.AimZoomBounds`: zum i pomak uvek unutar raspona, štap ne ulazi u sto/rail na krajevima (`CueChecks.NoIntersection` na min zumu).
- `CameraChecks.ShotRiseSmooth` (proširuje `ShotRise`): maksimalna ugaona brzina ≤ 35 °/s, maksimalno ugaono ubrzanje ≤ limit, bez promene predznaka yaw brzine; 120 uzoraka u realnom `Time` (ne Editor fps).
- `ControlChecks.SpinCenterKeepsCue`: reset spina i dupli tap centra ne menjaju poziciju/rotaciju štapa ni vodilje.
- `UiLayoutChecks`: stanja `pocket_birdseye_call`, `pocket_birdseye_block`, `pocket_birdseye_pick` (0 preklapanja, oznake rupa ≥ min veličine), `hud_zoomed`.
- Play: kamera sa 120 snimaka tokom natezanja (identični), pinch sa `touch2_path`-ekvivalentom iz `eval`, udarac pod zumom, kadar posle udarca (4 slike `v4_9_1_rise_*.png`), pticu po rupi (3 slike).

**Obim:** `CameraRig`, `PlayerShotController`, `AimGuideView` + `GuideBandRenderer` (materijal), `PocketPickerView` (zamena `PocketBirdView`), `TableTouchSurface`, `InputRouter`, `MatchConfig` + asset, builderi za HUD i prefabove, `README` (CameraMode tabela, eventi).
**Verifikacija:** gore navedene provere, postojeće `CameraChecks` i `GuideChecks` ostaju zelene, `UiLayoutChecks` 0, grep slojeva prazan, screenshotovi `v4_9_1_*`. Bez checkpointa; korisnik ocenjuje na checkpointu 3 (ili odmah ako želi APK).
**Rizici:** pinch i natezanje dele ekran, pa mora jasno razdvajanje dodira po prstima; pticu treba da prati ista logika poziva kao mini mapa (`RulesChecks.DivisionRules`, bot izbor rupe ne smeju da se menjaju); zamrznut kadar mora da ostane dobar i za dalji nišan (kadar se računa za ceo put, ne samo za trenutnu snagu).

### Commit 10 — `tutorial: three shots with spin, no ball panels, clear text, new hints`
**Obim:** V10 (+ FTUE redosled iz V13).

**Verifikacija:** `TutorialChecks.RunThrough/Skip/MoveOn`, dostižnost T3 (≥ 70% headless), `UiLayoutChecks` za tutorijal, vreme tutorijala, obrisan profil → tutorijal → pravila → prvi meč → meni sa coach mark-ovima; screenshotovi.

### Commit 11 — `analytics: local analytics service, event payloads, sessions, perf sampling, flow check`
**Obim:** V15 (`AnalyticsService`, `FrameTimingSampler`, novi/izmenjeni eventi, `AnalyticsChecks`, README tabela, §5.2 metrike u README sekciji „MVP metrics“).

**Verifikacija:** `AnalyticsChecks.Flow` i `NoAllocPerShot`; primer `analytics.jsonl` iz jednog toka priložen u izveštaju (prvih ~40 linija); grep slojeva.

### Commit 12 — `balance & build: progression sim with sets and rewards, division balance, match length, performance, README, APK 0.4.0`
**Obim:** `ProgressionSim` (setovi, nagrade, novčići za poraz, cue gap gate), tuning podataka do ciljeva (tabela pre/posle), `DivisionBalanceCheck`, `MatchLengthCheck`, merenje na uređaju (ako je povezan), README (kontrole sa spinom i kolovanjem, fizika V1, pravila po diviziji, meta, eventi, analitika, checks), `CREDITS.md` (Rubik, Noto, nove teksture), verzija **0.4.0**, version code 4.

**Verifikacija:** svi checks; brojevi tempa, balansa i cue gap-a u izveštaju; APK + `aapt`; DoD (§10).

**→ CHECKPOINT 3:** APK sa obrisanim profilom. Korisnik igra 30–60 min kao novi igrač i prolazi sve tačke feedbacka (§1.1).

---

## 10. Definition of Done
- [ ] V1 Spin (follow/draw/stun/side), throw, squirt, masse, zidovi zavisni od brzine, nova kriva snage sa POWER po štapu; predikcija = simulacija (`PhysicsChecks.Spin`, `PredictorChecks`).
- [ ] V2 Spin ikona + velika bela sa markerom (0.5R, dupli tap centar, reset posle udarca) (`SpinMapping`).
- [ ] V3 Tanke linije sa fade-om, prsten ghost, strelica, kriva bele po stvarnoj putanji (`GuideChecks`, 0 B GC).
- [ ] V4 Pravila crne za 6 nivoa (Bronze … GC), izbor protivnika jednom, poraz za pogrešnu/blokiranu rupu, bot poštuje pravila i bira rupu (`RulesChecks`).
- [ ] V5 Obavezno kolovanje kroz mini mapu/tap, igrač bira rupu za bota bez tajmera.
- [ ] V6 Prava lampa po sali (nije providna, sakriva se), mirnije kugle, tamnija čoja, rubovi bez svetlucanja (merenja).
- [ ] V7 Kamera posle udarca: zadržavanje + polako uspravljanje (~2 s), bez skokova.
- [ ] V8 Rubik (★ prikazan), mala+velika slova u opisima, min veličine, 0 preklapanja (`UiLayoutChecks`), mala pauza, ruka boje kože, kontrast.
- [ ] V9 6 štapova vidno različitih + AIM/CONTROL/TIMING/POWER/OVR + PERFECT efekat po štapu + TRY vežbe.
- [ ] V10 Tutorijal 3 udarca (uključujući draw u krug), bez panela kugli, RulesPopup, novi hintovi.
- [ ] V11 DIVISIONS ekran, nagrade stage/divizija jednom, proslave (stage, divizija sa novim pravilom, set ciljeva).
- [ ] V12 Setovi ciljeva po diviziji (≥ 3 „igranjem“), ALL OBJECTIVES sa zaključanom sledećom, cue gap, tempo u ciljevima (`ProgressionSim` + gate).
- [ ] V13 Meni: bedž → ciljevi → dugo PLAY; red cilja bez preklapanja; NEXT MATCH sa sledećim protivnikom; MENU sekundarno.
- [ ] V14 Probe uklonjene (grep prazan), profil v3 sa migracijom.
- [ ] V15 `AnalyticsService` + `AnalyticsChecks.Flow`, README tabele evenata i MVP metrika.
- [ ] Bronze pravila nepromenjena; `BotBalanceCheck` i `DivisionBalanceCheck` u opsezima; grep slojeva prazan.
- [ ] 12 commitova (+ 9.1) na `main`, pushovano; 3 checkpointa potvrđena od korisnika; APK 0.4.0.

---

## 11. Rizici
| Rizik | Šta uraditi |
|---|---|
| Puna fizika spina (throw, squirt, masse, zid) menja osećaj i balans više od očekivanog | commit 1 je sam za sebe; svaki efekat ima svoj knob u `MatchConfig` i opseg u V1 tabeli; pre/posle brojevi; korisnik ocenjuje na checkpointu 1 pre meta posla |
| Masse zbog automatskog nagiba štapa uz rail (`CueClearance`) iznenadi igrača | guide crta stvarnu krivu; `SwerveMinElevation`; ako na checkpointu smeta → smanji `SwerveFactor` (podatak) |
| Predikcija kroz simulaciju je skupa na telefonu | računa se samo na promenu ulaza, prealocirano, 0 B GC; budžet ≤ 1.5 ms u Editoru; meri na uređaju u commitu 12; fallback: ređe računanje dok se prst pomera (svaki 2. frejm) |
| Bot sa spinom predugo razmišlja | worker thread; `SpinCandidates` ograničava broj simulacija; medijana ≤ 1.5 s (commit 4) |
| Obavezno kolovanje u Diamond/GC usporava meč i nervira | mini mapa sa predlogom (pulsira) = 1 tap; `MatchLengthCheck` po diviziji; ako > 6 min → stani i pitaj |
| Igrač ne razume pravilo nove divizije | kartica NEW RULE na proslavi, čip na VS intro-u, red u RulesPopup, hint na prvoj upotrebi; analitika `match_end.reason` (`EightWrongPocket`) po diviziji |
| Bez dnevnih nagrada D1/D3 slabiji | kadenca sadržaja (nova divizija/pravilo/sala/štap svaka 1–2 dana), nedovršeni ciljevi; meri se pošteno; dnevne nagrade su A/B kandidat posle GO (`release_plan.md` faza 7) |
| Bez fake door-a i rewarded-a nema signala monetizacije | svesna odluka (§5.1); prvi korak posle GO je pravi rewarded |
| Slab igrač i dalje zaglavljen | cue gap gate u `ProgressionSim`; ciljevi „igranjem“; novčići za poraz po diviziji; analitika „zaglavljeni“ |
| Rubik nema ★ ili druge znakove | fallback Noto Sans Symbols 2; `UiLayoutChecks.MissingGlyphs` |
| Lampa zaklanja sto u Watch/Orbit | fade po visini i zaklanjanju; `CameraChecks` sa raycast-om |
| Promena fonta pokvari raspored svuda | `UiLayoutChecks` na listi svih stanja posle commita 6 i svakog UI commita |
| Uklanjanje proba ostavi mrtav kod ili ključeve | grep (V14) + migracija v3 u `EconomyChecks` |
| Nagrade stage-a se farmuju padom i usponom | `RewardGrantedStage`; `EconomyChecks` uspon–pad–uspon |
| Licence (Rubik, Noto Emoji, Noto Symbols) | OFL / Apache 2.0, upis u `CREDITS.md`; ako nešto nije jasno → stani i pitaj |
