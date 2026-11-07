# Implementation Definition v5 — Billiards (mirna kamera, spin po nišanu, tajmer poteza, premium izgled)

Ovaj dokument je **jedini ulaz** za agenta koji radi iteraciju v5. Pročitaj ga ceo pre prvog koraka.
Kad nešto nije pokriveno, **stani i pitaj korisnika**. Ne pretpostavljaj.

**Nadograđuje** `implementation_definition_iteration_v4.md`. Iz v4 je urađeno sve do commita 10.1. Posle toga su commitovani haptics za iOS i web build. Poslednji commit je `1b53f34`.
Gde se ovaj dokument i v4 razlikuju, **važi ovaj**.

Prateći dokumenti (čitaj, ne menjaj osim README-a):
- `README.md`: arhitektura, eventi, builderi, provere, slojevi.
- `implementation_definition_iteration_v4.md`: §0 (pravila rada) i §0.1 (Unity CLI na ovom Mac-u) **važe i ovde**. Pre izmene pročitaj i commit 9.1 i 10.1, jer opisuju kako kamera i vodilja rade sada.
- Memorija korisnika: Editor van fokusa zaustavlja kompajliranje i Play. Rešava se kroz `osascript` (System Events) i `runInBackground` eval.

Svi tekstovi u igri su na **engleskom**.

**Van obima v5:** v4 commit 11 (analytics) i commit 12 (balance & build 0.4.0) nisu u logu. Ne radi ih u ovoj iteraciji, osim ako korisnik ne kaže drugačije.

**Necommitovane izmene u radnom stablu** (`LiberationSans SDF - Fallback.asset`, `ProjectSettings.asset`): pre prvog commita pitaj korisnika da li ulaze u commit. Ne briši ih i ne vraćaj ih.

---

## 0. Pravila rada za agenta

1. Radiš kroz **9 commitova** (§4). Svaki commit je kompajlabilna i pokretljiva celina. Posle svakog commita uradi `git commit` + `git push` na `main` (remote `origin`). Nikad ne radi force push.
2. **Pre svakog commita:** `unity recompile` sa 0 grešaka, prazan grep slojeva (README „Layer rules“), sve postojeće `Editor/Checks` zelene ili svesno rekalibrisane tamo gde ovaj dokument to kaže (prijavi pre/posle), plus verifikacija tog commita.
3. **Nikad ne tvrdiš da nešto radi ako nisi proverio.** Ono što ne možeš da proveriš (osećaj na telefonu, haptic, FPS bez uređaja) napiši eksplicitno.
4. **Posle svakog commita** pošalji kratak izveštaj korisniku **na srpskom**: šta je urađeno, kako je provereno, šta nije provereno, putanje screenshotova i commit hash.
5. **Checkpointi** (§5) su posle commita **6, 7 i 9**. Na svakom: `AndroidBuilder.Build()`, provera sa `aapt` (package, minSdk 26, VIBRATE), `adb install -r` ako je telefon povezan (`adb devices`). Javi tačno šta da testira i **čekaj potvrdu**.
6. Scene, prefabe i generisane assete menjaš **samo kroz buildere** (`Editor/Builders`), nikad ručno YAML. Svi brojevi idu u ScriptableObject-e. Kad menjaš podrazumevanu vrednost postojećeg polja, ažuriraj **i asset** (`SerializedObject` + `SetDirty` + `SaveAssets`).
7. **Screenshotovi:** `Screenshots/v5_<commit>_<ime>.png`, portrait 1080×1920 (`GameViewUtil.SetPortrait()`). Za svaku vizuelnu izmenu napravi **pre/posle** par sa iste kamere.
8. Pool sloj ostaje čist C# i deterministički (`PhysicsChecks.Determinism` je gate posle svake izmene fizike). UI ne zna za controllere (grep slojeva).
9. Novi asseti trećih strana: samo CC0/OFL (Poly Haven, Kenney, Google Fonts) i upis u `CREDITS.md`. Ako licenca nije jasna, **stani i pitaj**.
10. **Stani i pitaj** ako: Editor nije dostupan CLI-ju, build pada iz neočiglednog razloga, rešenje krši slojeve, izmena fizike menja balans van opsega (§3 V2), performanse na uređaju padnu ispod budžeta (§3 V8), ili nešto nije pokriveno ovim dokumentom.

---

## 1. Feedback → zahtev

| # | Feedback (skraćeno) | Zahtev |
|---|---|---|
| F1 | Kamera se drma gore/dole dok aim prelazi sa kugle na kuglu (break i svaki udarac); nekad se posle udarca kamera na sekundu naglo pomeri | **V1** |
| F2 | Udarac u desni deo bele pomera aim levo (i obrnuto). Proveriti da li je tako u realnosti | **V2** |
| F3 | Ikonica šake na MOVE BALL je manja od teksta | **V3** |
| F4 | Linija putanje bele počinje od štapa, a treba od bele | **V2** (vodilja) |
| F5 | Rupe previše ispupčene iz ptičje perspektive; sto i rupe se neprirodno seku | **V4** |
| F6 | Ruka na POWER traci stalno se pojavljuje i posle tutorijala | **V3** |
| F7 | Tajmer za potez: vidljiv, nenametljiv, bez pritiska, isti broj sekundi svuda | **V6** |
| F8 | Viši, profesionalniji art style: okruženje, čoja, okvir, štapovi, power bar, release bar. Elegancija, a ne više detalja | **V7, V8** |
| F9 | U LOOK-u iznad neke tačke lusteri nestanu | **V5** |

### 1.1 Odluke korisnika (2026-11-07)
| Tema | Odluka |
|---|---|
| Spin i nišan | **Bela ide tačno po nišanu.** Squirt se uklanja, pa bela kreće niz liniju nišana bez obzira na bočni spin. Spin i dalje deluje posle kontakta: throw na pogođenoj kugli **ostaje**, a ostaju i odbijanje od zida i follow/draw. |
| Lampe | Lampa se sakriva **samo kad bi stvarno zaklonila pogled na sto**. Danas se sakriva i kad ne smeta (pravilo „kamera iznad donje ivice lampe“). |
| Tajmer: trajanje | **45 s**, isto za sve divizije i sve štapove. |
| Tajmer: istek | **Faul → protivnik dobija ball in hand.** Ako je štap nategnut, udarac se otkazuje. |
| Tajmer: mesto | **Donja ivica panela igrača** (YOU / protivnik): tanka linija koja se prazni. Bez broja i u neutralnoj boji do poslednjih 10 s, zatim blag amber i broj sekundi. Bez zvuka. Jedan lagani haptic na 5 s. |
| Tajmer: obim | **Samo rangirani mečevi.** Linija se prikazuje **i na panelu bota** dok razmišlja (za bota nikad ne istekne). U tutorijalu i TRY vežbama tajmera nema. |
| Ruka na POWER | **Nikad posle tutorijala**, ni kad je tutorijal završen ni kad je preskočen: ni posle 8 s čekanja, ni u prva 3 udarca, ni na tap ispod ručke (tada samo zasvetli ručka). U tutorijalu radi kao sada. |
| Rupe | **Mesingane kape ostaju**, ali ravne sa rail-om. Rupa je manja i uvučena kao na pravom stolu, a rail je zasečen po uglu jaw-a. |
| Art pravac | **Klasični klub, suzdržano:** tamno drvo sa pravim lakom, mesing samo kao akcent, čoja sa finom teksturom, soba u polumraku sa nekoliko smislenih elemenata. UI: tamno staklo, tanke linije, jedan akcent. |
| Art obim | **Prvo jedna sala, pa ostale.** Prva je **Basement Bar** (Bronze, prva sala koju igrač vidi). Posle potvrde korisnika stil se prenosi na ostale 4 sale. |
| Testiranje | **APK na Androidu** na svakom checkpointu. |

---

## 2. Polazno stanje (iz koda, 2026-11-07)

| Tema | Kako radi sada | Fajl |
|---|---|---|
| Kadar nišana | `FitAimDistance()` traži najmanju udaljenost u **koracima od 5 cm** na kojoj su bela, pogođena kugla i njena destinacija (`SetFocus`, do 3 tačke) unutar `AimFitArea`. Udaljenost ide kroz `SmoothDamp` (`AimFramingSmooth`). | `Gameplay/CameraRig.cs` ~360, ~497 |
| Visina i pogled | `AimPose`: visina = f(tilt, distanca × `AimRiseSlope`), zatim `max` sa `CueClearance.MinEyeHeight` i sa visinom iznad nagiba štapa (`CueElevation`). Tačka pogleda `along` se računa **direktno** iz fokus tačaka (prosek × 0.6, clamp 0.4–1.6 m), **bez smoothinga**. | `CameraRig.AimPose` ~434 |
| Fokus | `PlayerShotController.FrameShot()` svaki frejm postavlja fokus iz geometrijske predikcije. Kad nišan pređe na drugu kuglu, fokus tačke **skoknu** (druga kugla, druga destinacija). | `Gameplay/PlayerShotController.cs` ~403 |
| Posle udarca | `ApplyHeadMotion` je isključen dok traje `Pulling`. Kad se pređe u `Shooting`, uključuje se **odmah**: Perlin sway nastavlja sa trenutne vrednosti, a `StrikeKick` (0.035 m) gurne kameru napred u istom frejmu. | `CameraRig.ApplyHeadMotion` ~525 |
| Squirt | `PoolPhysics` rotira početni smer bele za `SquirtDegPerR × a` (4°/R, do 2° pri 0.5R). Swerve (`SwerveFactor` 0.5, od `SwerveMinElevation` 5°) savija putanju. Predikcija = simulacija, pa vodilja pokazuje pomak. | `Pool/PoolPhysics.cs` ~84, `MatchConfig.asset` |
| Vodilja bele | `AimGuideLayout.BuildLine` već preskače deo ispod bele (radius). Materijal vodilje od 9.1 ima `ZTest Always` (crta se preko kugli), pa se crta i **preko bele**. Iz kamere iza bele zato izgleda kao da linija kreće od vrha štapa. | `Pool/AimGuideLayout.cs`, `Gameplay/GuideBandRenderer.cs` |
| MOVE BALL ikonica | `UIPrefabBuilder` za dugme sa tekstom: `iconSize = fontSize × 1.2` (38 → ~46 px). Sprite `HintHandPath` ima mnogo providnog ruba, pa je vidljiva šaka ~20 px, a tekst ~38 px. | `Editor/Builders/UIPrefabBuilder.cs` ~57, ~302 |
| Ruka na POWER | Prikazuje se: (1) u prvih `PowerHintShots` (3) udaraca, po `ProfileService.ShotsTaken`; (2) **posle 8 s neaktivnosti, uvek**; (3) na tap ispod ručke. | `UI/Views/PowerStripView.cs`, `MatchController.cs` ~272 |
| Rupe | Rupa je krug `CaptureRadius + PocketHoleExtra` oko `pocket.Center`, isečen u rail. Kapa je polukružni mesingani prsten (`CapWidth` 32 mm, `CapHeight` 4.5 mm). U ptici se vidi da krug i kapa izlaze preko spoljne ivice rail-a, da u ugaonu rupu ulazi svetli klin (kraj cushion-a ili cloth-a) i da se u bočnoj vidi zeleni komad cushion-a. | `Editor/Builders/TablePrefabBuilder.cs` ~309, ~374, ~610–720 |
| Lampa | `LampView.HidesFor`: sakriva se ako je kamera iznad `bottom − HideMargin` **ili** ako lampa preseca zrak kamera → centar stola. Donja ivica lampe je 1.1 m iznad čoje, a LOOK ide do pitch 85° i 4 m. Zato se sakriva i u Watch (protivnikov potez), i kad ne zaklanja sto. | `Gameplay/LampView.cs`, `RoomPrefabBuilder.cs` ~159 |
| Tajmer | Ne postoji. | — |
| Sale | basement (Bronze), downtown (Silver), riverside (Gold), lounge (Platinum), royal (Diamond). Sobe se prave u `RoomPrefabBuilder.Styles`, a boje i materijali dolaze iz `VenueTheme`. | `DataAssetBuilder.cs` ~160 |

---

## 3. Zahtevi

### V1 — Mirna kamera: prelazak sa kugle na kuglu i trenutak udarca (F1)

**Cilj:** dok igrač pomera nišan (prstom ili fine aim-om), kamera se **ne pomera gore/dole** i ne „cima“ kad nišan pređe sa jedne kugle na drugu (break: trougao kugli; bilo koji udarac: grupa kugli). Od početka poteza do kraja kretanja kugli nema naglih skokova pozicije ni rotacije.

**Prvo izmeri, pa menjaj.** Proširi `AimFeelChecks.JitterStart/JitterResult` i napravi bazni izveštaj pre izmene:
- **Break sweep:** bela na break poziciji, nišan se pomera preko celog trougla brzinom 20°/s i 5°/s (realni drag), pa preko istog opsega korakom fine aim-a. Beleži se po frejmu (60 fps, `Time` realan): visina kamere, pitch, yaw, distanca.
- **Opšti sweep:** 20 nasumičnih rasporeda u igri, isti sweep preko svih kugli.
- **Udarac:** poza kamere po frejmu od 0.5 s pre puštanja do 1.0 s posle.
- Prijavi koji član pravi skokove: `along`, `FitAimDistance` (koraci od 5 cm), `CueClearance.MinEyeHeight`, `CueElevation` (štap uz rail ili kuglu menja nagib, a nagib diže oči), sway ili kick.

**Šta se radi (pristup):**
1. **Kadar se ne menja dok prst vuče nišan, osim kad mora.** Dok se nišan menja (drag ili fine aim, uz kratko „smirivanje“ `AimFrameSettleTime` ≈ 0.3 s posle poslednje promene), kadar može samo da se **odmakne** kad bi fokus tačka izašla iz ekrana. Ne primiče se i ne menja visinu zbog druge kugle u fokusu. Kad se nišan smiri, kadar polako pređe na novu vrednost.
2. **Sve komponente kadra prolaze kroz isti glatki prelaz:** `along`, distanca i konačna visina oka. Pri tome:
   - `FitAimDistance` računaj kontinualno (binarna pretraga, kao `PickPose`), ne u koracima od 5 cm;
   - `CueElevation` za kameru je izglađen (ili se uzima tek kad se nišan smiri), da prolazak štapa pored kugle ili rail-a ne diže oči skokovito;
   - ograniči brzinu promene pitch-a i visine (`AimMaxPitchDegPerSec`, `AimMaxHeightPerSec`).
3. **Trenutak udarca:** ukloni `StrikeKick` (0 u assetu) ili ga zameni glatkom krivom. Preporuka je ukloniti ga, jer korisnik traži da nema naglih pomeraja. Sway posle `Pulling` vraćaj postepeno (fade-in ≈ 0.6 s), a ne odmah.
4. Ponovo izmeri ceo tok (Aim → Shooting → hold → rise → Watch → Aim) i ukloni svaki preostali skok. Proveri i prelaz Watch → Aim na početku sledećeg poteza.

Postojeća pravila iz 9.1 ostaju: kadar zamrznut tokom natezanja, spin i snaga ne pomeraju kameru, pinch zum, rise ≤ 35 °/s.

**Pragovi (nova provera `CameraChecks.AimSweepSteady`):**
- dok traje sweep: promena visine oka ≤ 2 mm po frejmu i ≤ 1.5 cm ukupnog „ljuljanja“ (razlika max−min bez monotonog trenda), pitch ≤ 0.15° po frejmu, bez promene znaka brzine visine više od jednom u 0.5 s (nema oscilacije gore/dole);
- **trenutak udarca** (`CameraChecks.NoJitterAfterShot`, proširiti): u prozoru −0.5 s … +1.0 s skok pozicije ≤ 1 mm po frejmu iznad glatke krive, a ugaono ubrzanje ispod istog limita kao `ShotRiseSmooth`.

**Brojevi u `MatchConfig`:** `AimFrameSettleTime`, `AimMaxPitchDegPerSec`, `AimMaxHeightPerSec`, `HeadSwayFadeIn`, `StrikeKick` (0).

---

### V2 — Spin: bela ide po nišanu; vodilja počinje od bele (F2, F4)

**Istraživanje (za izveštaj, sažeto):** u realnosti bočni spin stvarno skreće belu na suprotnu stranu. To je *squirt*, tipično 1–3° i zavisi od štapa. Swerve je zatim delom vraća, a throw blago skreće pogođenu kuglu (Dr. Dave Alciatore, billiards.colostate.edu). Igra je dakle bila fizički tačna, ali je korisnik izabrao da **bela ide tačno po nišanu** (§1.1).

**Šta se radi:**
1. `SquirtDegPerR = 0` (asset + podrazumevana vrednost u kodu).
2. **Swerve pre prvog kontakta = 0.** Bez toga bi nagib štapa uz rail i dalje savijao belu ka strani spina, pa bi se „aim pomerao“ na drugu stranu. Preporuka: `SwerveFactor = 0` (podatak). Ako fizika ima swerve i posle kontakta i to bi se izgubilo, prijavi pa pitaj.
3. Ostaje: throw (spin i cut), follow/draw/stun, efekat spina na zidu. Vodilja i dalje = simulacija.
4. Štap ostaje paralelan liniji nišana (vrh pomeren na tačku udara, kao sada). Izbor ili reset spina ne pomera ni štap ni vodilju (`ControlChecks.SpinCenterKeepsCue` iz 9.1).
5. **Vodilja bele počinje od bele kugle, vizuelno.** Bela kugla (i štap) se crtaju **posle** vodilje, preko render queue-a ili stencil-a, pa bela normalno zaklanja deo linije iza sebe, kao što pogođena kugla počinje od svoje ivice. Linija i dalje ide preko **ostalih** kugli (odluka 9.1 B). Preporuka je render queue (jednostavnije). Stencil je rezerva ako queue pokvari senke ili refleksije.

**Provere:**
- `PhysicsChecks.Spin`: rekalibracija. Za bočni spin pri bilo kojoj snazi i nagibu, početni smer bele odstupa od nišana < 0.01°, a putanja do prvog kontakta je prava (< 0.5 mm). Throw i follow/draw ostaju u opsezima iz v4.
- `PhysicsChecks.Determinism`, `PredictorChecks.MatchesSimulation`, `GuideChecks.*` zelene.
- **Balans:** `BotBalanceCheck.Run(200)` i `DivisionBalanceCheck.Run(400, strict: true)` pre/posle. Gate je isti kao README (prosečan igrač ±6 p.p.). Ako izađe iz opsega, **stani i pitaj**.
- Screenshot pre/posle: nišan sa spinom levo, centar i desno na istoj kugli (vodilja se ne pomera). Kamera iza bele: linija kreće od prednje ivice bele, ne od vrha štapa.

---

### V3 — HUD: MOVE BALL ikonica, ruka na POWER (F3, F6)

1. **MOVE BALL ikonica:** vidljiva šaka (alpha bounds, ne rect) visoka je kao font labela (38 px reference), ±10%. Rešenje: tight sprite (trim providnog ruba u `GeneratedArtBuilder` ili poseban sprite za ikonice), pa `iconSize` po vidljivoj visini. Isto važi i za ruku u ball-in-hand prikazu, ako koristi isti sprite. Proveri i ostala dugmad sa ikonicom (LOOK, POCKET…). Ako imaju isti problem, **prijavi, ne menjaj bez pitanja**.
2. **Ruka na POWER:** `MatchStartInfo` dobija flag (npr. `PowerHintsEnabled`) koji `MatchController` postavlja na `true` samo u `MatchMode.Tutorial`. UI ne sme da zna za `ProfileService`. Kad je flag `false`:
   - nema hinta u prvih N udaraca;
   - nema idle hinta posle 8 s;
   - tap ispod ručke samo zasvetli ručku (`FlashGrip`), bez ruke.

   U tutorijalu ostaje sve kao sada.
3. Provere: `UiLayoutChecks` (0 preklapanja, MOVE BALL stanje), nova `UiLayoutChecks.IconMatchesText` (vidljiva visina ikonice / font size u 0.9–1.1). Play: rangirani meč, 15 s bez dodira, tap ispod ručke. Ruka se nijednom ne pojavi (proveri `_hintGroup.alpha` kroz eval). U tutorijalu se pojavi.

---

### V4 — Rupe kao na pravom stolu (F5)

**Referenca (proveri u izvoru pre izmene i navedi ga u izveštaju):** WPA specifikacija stola. Usta ugaone rupe su ~4.5–4.625", bočne ~5–5.125". Ugao jaw-a je ~142° za ugaone i ~104° za bočne rupe. Rupa je većim delom **ispod** rail-a, a iz ptice se vidi zasek u rail-u po liniji jaw-a i tamna rupa iza njega.

**Šta se radi (samo izgled, fizika se ne menja):**
1. Vidljiva rupa je **manja i uvučena**: krug otvora je pomeren ka spoljnoj strani i delom ispod rail-a, tako da iz ptice rupa ne „izlazi“ iz okvira. Spoljna ivica okvira (rail + kapa) ostaje neprekinuta.
2. Rail je zasečen po linijama jaw-a (ravne stranice pod uglom do kruga rupe), ne kružnim izrezom.
3. **Mesingana kapa** ostaje kao akcent. Uža je (≈ 15–20 mm), skoro ravna sa vrhom rail-a (visina ≤ 1.5 mm, blago zaobljena), prati zasek i **nikad ne izlazi** preko spoljne ivice rail-a. Boje po sali i dalje dolaze iz `VenueTheme` (caps).
4. **Bez preseka:** nijedan mesh (cloth, cushion/jaw, rail, kapa) ne sme da uđe u valjak rupe ispod nivoa čoje, osim lip-a čoje i liner-a. Krajevi cushion-a su zatvoreni (end cap) i ne vire u rupu.
5. Ubačena kugla i dalje pada i ostaje vidljiva u liner-u (v3 V7). Markeri u ptici (`PocketPick`) i dalje se tačno poklapaju sa rupom.

**Provere:**
- nova `LookChecks.PocketIntersections`: uzorkovanje svih verteksa i trouglova cloth, cushion, rail i cap mesh-eva, 0 tačaka unutar `HoleRadius − ε` ispod `FeltHeight` (osim lip/liner);
- `LookChecks.PocketsInsideFrame`: u ptici je svaka kapa i rupa unutar spoljne konture okvira;
- `PhysicsChecks.All`, `PredictorChecks` i `RulesChecks` nepromenjeni (fizika ista);
- screenshotovi pre/posle: ptica (sve 6 rupa), zum ugaone i bočne rupe, Aim pogled na rupu, kugla u rupi.

---

### V5 — Lampe se sakrivaju samo kad zaklanjaju sto (F9)

**Pravilo:** lampa je vidljiva uvek, **osim** kad bi na ekranu prekrila deo stola koji igrač gleda.

1. Ukloni pravilo visine (`eye.y > bottom − HideMargin`).
2. Novo pravilo zaklanjanja u screen space-u: projektuj bounds svake lampe (ili sva tela lampe) i igračku površinu (čoja do spoljne ivice rail-a, rupe). Lampa se sakriva ako preklapanje prelazi `LampOccludeThreshold` (npr. 2% površine stola na ekranu) **ili** ako prekriva bilo koju kuglu ili rupu. Lampa je bliža kameri od stola na tom mestu.
3. Histereza (drugi prag za ponovno pojavljivanje) i dither fade ostaju (0.25 s → po potrebi 0.35 s), da nema treperenja na granici.
4. Ponašanje po modu:
   - **LOOK:** važi pravilo iznad. Kad kamera ide visoko ali gleda koso pa lampa nije između, lampa ostaje.
   - **Watch:** isto. Ako se lampa i dalje sakriva u običnom Watch kadru, prijavi sa screenshotom pre nego što menjaš Watch kameru.
   - **Ptica (PocketPick, Place):** isto pravilo. U praksi lampa zaklanja sto pa se sakriva, ali dok kamera leti.
5. Provere:
   - `CameraChecks` sa raycast-om kroz lampe: u svim test pozama nijedna vidljiva lampa ne prekriva kuglu ili rupu;
   - nova `LookChecks.LampVisibleWhenClear`: LOOK orbit 0–360° yaw × pitch 12–85°, beleži kad je lampa sakrivena. Nijedan slučaj „sakrivena, a ne zaklanja“, i ne više od 2 promene stanja po prolazu;
   - screenshotovi: LOOK visoko-koso (lampa vidljiva), LOOK iznad stola (lampa sklonjena), Watch, ptica.

---

### V6 — Tajmer poteza (F7)

**UX brief:** igrač treba da zna koliko mu je vremena ostalo, ali da ne oseća pritisak. Zato:
- nema brojanja, crvene boje ni zvuka dok ima dovoljno vremena;
- tajmer je na mestu koje već znači „čiji je potez“ (panel igrača), ne u centru pažnje.

**Izgled:**
- **Linija na donjoj ivici aktivnog panela** (YOU ili protivnik), unutar ivice panela. Debljina ~4 px reference, pune širine panela, prazni se zdesna nalevo glatko (ne skokovito po sekundi). Boja: neutralna svetla (bela ~55% alpha ili svetla nijansa akcenta panela).
- **Poslednjih 10 s:** linija pređe u blag amber (ne crvenu) za ~0.3 s, a na desnom kraju linije se pojavi mali broj sekundi (min veličina teksta iz v4 V8, Rubik). Bez pulsiranja i treperenja.
- **5 s:** jedan lagani haptic (`HapticsService`, light). Bez zvuka.
- **Istek:** toast `FOUL! Time ran out` (nova vrednost u `EightBallRules.FoulText`), standardni tok faula, protivnik dobija ball in hand.
- Panel bota: ista linija dok bot razmišlja (prazni se sporo, nikad ne istekne).

**Pravila (bez dubokog dizajna):**
| Situacija | Ponašanje |
|---|---|
| Početak | Kad igrač dobije kontrolu (posle turn banera i prelaza kamere), u fazi Aim, PlaceCueBall ili PocketPick. |
| Teče | Aim, Look, Pulling, PlaceCueBall, biranje svoje rupe (ptica), pinch zum. |
| Pauza | Pause popup, app u pozadini (meč se već pauzira), igrač bira rupu **za bota** (v4: bez tajmera). |
| Zaustavlja se | Na ispaljenje (`OnShotFired`). Sledeći potez kreće od punih 45 s. |
| Istek dok se nateže | Natezanje se otkazuje (kao „cancel“), pa faul. |
| Istek na crnoj | Običan faul (ball in hand), **ne** gubi se meč. |
| Tutorijal, TRY | Nema tajmera. |

**Arhitektura:**
- Pool: `FoulType.ShotClock` + `EightBallRules` primenjuje faul bez udarca (prelaz poteza, `BallInHand = true`). Bez `Time`: vreme meri gameplay sloj.
- Gameplay: `MatchController` (ili mali `ShotClock` u `Gameplay/`) meri vreme (`Time.deltaTime` meča, pauza staje) i diže evente.
- Eventi (upiši u README tabelu):
  - `OnShotClockChanged(side, remaining01, secondsLeft)` → `PlayerPanelView`;
  - `OnShotClockWarning(side)` → `HapticsService`;
  - istek ide kroz postojeći `OnFoul`.
- `MatchConfig`: `ShotClockSeconds` 45, `ShotClockCalmUntilSeconds` 10, `ShotClockHapticAtSeconds` 5.
- Debug: `MatchController.DebugExpireShotClock()` i stavka u **Tools → Billiards → Debug**.

**Provere:**
- `RulesChecks.ShotClockFoul`: istek → protivnik, ball in hand, grupe i stanje crne nepromenjeni, meč se ne završava;
- Play: istek u Aim, tokom natezanja, tokom ball in hand i tokom biranja rupe; pauza zaustavlja tajmer (±0.1 s); tutorijal i TRY nemaju liniju;
- `UiLayoutChecks`: stanja `hud_clock_calm`, `hud_clock_warn`, bot panel. 0 preklapanja i min veličina broja;
- `ContrastChecks`: broj na amber liniji je čitljiv na panelu;
- `MatchLengthCheck.Run(100)` nepromenjen (tajmer ne utiče na simulaciju);
- screenshotovi `v5_6_clock_calm`, `v5_6_clock_warn`, `v5_6_clock_bot`, `v5_6_clock_foul`.

---

### V7 — Premium izgled: pravac i Basement Bar (F8)

**Princip (obavezan):** elegancija kroz **manje i bolje**, ne kroz više detalja.
- Svaki element prolazi pitanje „da li ovo izgleda kao pravi, skup predmet“. Ako ne, prvo ga **pojednostavi** ili **ukloni**, pa tek onda dodaj detalj.
- Paleta po sali: jedno drvo, jedan metal (akcent), jedna tkanina, jedna boja svetla.
- Bez čisto bele, bez zasićenih boja van čoje i jednog neon akcenta.

**Pravac:** klasični klub, suzdržano (§1.1). Sto je najsvetliji objekat u kadru, a soba je u polumraku i čitljiva.

**Redosled:** sve stavke ispod prvo za **Basement Bar** (Bronze). Pre/posle screenshotovi iz Aim, Watch, LOOK (nisko i visoko) i ptice → **CHECKPOINT A** (§5). Tek posle potvrde ide V8.

| Element | Danas (problem) | Cilj |
|---|---|---|
| **Okruženje** | Nasumični elementi (stolice duž zida, police sa flašama kao štapići), jako zasićen crveni tepih sa zvezdama, veliki „CLUB“ neon | Soba sa pričom „podrumski bar“: tamna drvena lamperija ili cigla, šank sa policama u pozadini sa nekoliko toplih tačaka svetla, zidni stalak za štapove, tabla za poene (perle na žici), 2 zidne svetiljke (emissive, tople). Pod: tamne daske i prigušen tepih ispod stola. Jedan mali neon akcent umesto velikog. Manje objekata nego sada, ali smisleno raspoređenih. Sve iz `RoomPrefabBuilder.Styles` + Poly Haven teksture (`PolyHavenBuilder`). |
| **Čoja** | Ravna boja | Fina tkana mikro-normal tekstura (tile bez vidljivog ponavljanja ni iz niskog LOOK-a), blaga tonska varijacija velike frekvencije, slab sjaj pod niskim uglom. Boja po sali ostaje (`VenueTheme`). |
| **Okvir stola** | Svetlo drvo bez dubine, bele tačke kao „diamonds“, kockaste noge | PBR orah ili mahagoni sa lakom (smoothness ~0.7, refleksija iz probe), diamonds kao uložen sedef u obliku romba, tanka mesingana ili crna linija između cushion-a i drveta, pregača sa jednostavnom profilacijom, noge sužene ili tokarene umesto kocke. |
| **Rupe** | (V4) | Iz V4, materijal kape usklađen sa metalom sale. |
| **Štapovi** (sto i POWER traka) | v4 V9: unikatni po štapu | Zadrži identitet svakog štapa, podigni kvalitet: vrh sa plavom kredom, beli ferrule, javorov shaft sa finim vlaknima, prsten na spoju, forearm sa inlay šiljcima, wrap (koža ili lan), butt sleeve, gumeni završetak, lak. Slika štapa na POWER traci se generiše iz istog izgleda. `CueChecks.Looks` i `NoIntersection` zelene. |
| **POWER traka** | Široki panel, velika slika štapa, SOFT/MED/HARD veliki | Uži stub od tamnog stakla (1 px svetla ivica, blag blur ili tamni gradijent), tanak track, ručka kao mala zlatna kapsula, SOFT/MED/HARD kao male oznake niskog kontrasta pored tankih crtica, fill kao suptilan topli gradijent, procenat mali iznad ručke. **Geometrija interakcije ostaje** (`PullTravelPixels`, grab zona, mapiranje): `ControlChecks.PullMapping` mora proći bez rekalibracije. |
| **Release bar** | Sjajni crveno-žuto-zeleni blokovi (v3 izgled) | Tanji zaobljeni track (~24–28 px), zone u **prigušenim** tonovima (crvena → terakota, žuta → amber, zelena → smirena zelena), blaga unutrašnja senka, marker kao tanka bela igla sa malim trouglom, zelena zona blago svetli dok je marker u njoj. Natpis „LET GO ON GREEN“ manji, sa razmakom slova. Flash posle puštanja suptilniji. Logika i dimenzije zona iz `ShotAccuracy` ostaju. |

**Merenja i provere (posle Basement-a):**
- Luma (kao v3 V6): čoja ~0.50–0.55, ivice ≥ 85% centra, soba 0.10–0.16. Soba mora ostati čitljiva (screenshot).
- `ContrastChecks` i `UiLayoutChecks` (svi postojeći state-ovi) zeleni.
- **Budžet performansi** (meri u Editoru, a na uređaju ako je povezan): broj draw call-ova sobe i stola ≤ danas + 10%, bez novih realtime svetala (soba ostaje ambient + emissive + bake), memorija tekstura ≤ +25 MB, FPS na uređaju ≥ kao pre. Ako prekoračiš, **stani i pitaj**.
- `CREDITS.md` za svaki novi asset.

---

### V8 — Premium izgled: ostale 4 sale (F8)

Posle potvrde pravca na CHECKPOINT A: isti principi i kvalitet za downtown, riverside, lounge i royal. Svaka sala zadržava svoj identitet (paleta i tip lampe iz v4 V6) i dobija svoju malu „priču“ sobe, uz istu meru: malo objekata, svi smisleni. Ispravke koje korisnik da na checkpointu A primeni prvo na Basement, pa na ostale.

Provere: iste kao V7, po sali. Screenshotovi `v5_8_<venue>_{aim,watch,look,bird}.png`. Thumbnail sala (`VenueTheme`) regenerisan.

---

## 4. Plan commitova (9)

| # | Commit poruka | Obim | Verifikacija |
|---|---|---|---|
| 1 | `camera: steady aim framing while sweeping across balls, no jolt on the shot` | V1 | bazni izveštaj pre/posle, `CameraChecks.AimSweepSteady`, `NoJitterAfterShot`, svi `CameraChecks`, `AimFeelChecks` |
| 2 | `aim: cue ball leaves on the aim line with side spin, guide starts at the cue ball` | V2 | `PhysicsChecks` (Spin rekalibrisan, Determinism), `PredictorChecks`, `GuideChecks`, `ControlChecks`, balans pre/posle, screenshotovi |
| 3 | `hud: move-ball icon at text size, power hand only in the tutorial` | V3 | `UiLayoutChecks` + `IconMatchesText`, Play test ruke |
| 4 | `pockets: real pocket cut, flush brass caps, nothing inside the holes` | V4 | `LookChecks.PocketIntersections`, `PocketsInsideFrame`, fizika nepromenjena, screenshotovi |
| 5 | `lamps: hide only when they block the table` | V5 | `LookChecks.LampVisibleWhenClear`, `CameraChecks`, screenshotovi |
| 6 | `shot clock: 45 s turn timer on the player panels, timeout foul with ball in hand` | V6 | `RulesChecks.ShotClockFoul`, Play scenariji, `UiLayoutChecks`, `ContrastChecks`, README eventi → **CHECKPOINT 1** |
| 7 | `art: basement bar - room, cloth, frame, cues, power strip, release bar` | V7 | merenja, budžet, provere, pre/posle screenshotovi → **CHECKPOINT A** |
| 8 | `art: downtown, riverside, lounge, royal in the same style` | V8 | iste provere po sali, thumbnails |
| 9 | `polish & build: README, credits, all checks, APK` | README (kamera, spin bez squirt-a, tajmer, eventi, Table look), `CREDITS.md`, svi checks, APK | DoD (§6) → **CHECKPOINT 2** |

Svaki commit završava se sa:
```
Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
```

---

## 5. Checkpointi

| Checkpoint | Posle | Šta korisnik testira na telefonu (APK) |
|---|---|---|
| **1** | commit 6 | Break: prevuci nišan preko trougla (kamera mirna). Prelazi sa kugle na kuglu u običnim potezima. Trenutak udarca i 1 s posle (bez trzaja). Spin levo i desno (vodilja i bela po nišanu). Vodilja kreće od bele. MOVE BALL ikonica. Nema ruke na POWER-u (sačekaj 15 s, tapni ispod ručke). Rupe iz ptice i izbliza. LOOK svuda (lampe nestaju samo kad zaklanjaju sto). Tajmer: pusti da istekne jednom, pogledaj ga i na botu. |
| **A** | commit 7 | Basement Bar: soba, čoja, okvir, štapovi, POWER traka, release bar. Pre/posle screenshotovi u izveštaju. **Odluka:** pravac odobren ili lista ispravki. Bez potvrde se ne radi V8. |
| **2** | commit 9 | Svih 5 sala (Bronze → Diamond preko debug-a ili profila), ceo meč, regresija checkpointa 1. |

---

## 6. Definition of Done
- [ ] V1 Kamera mirna pri prelasku sa kugle na kuglu i u trenutku udarca (`AimSweepSteady`, `NoJitterAfterShot`), 9.1 pravila nepromenjena.
- [ ] V2 Squirt i swerve pre kontakta = 0, bela po nišanu, throw ostaje. Vodilja vizuelno počinje od bele. Balans u opsegu.
- [ ] V3 MOVE BALL ikonica visoka kao font (±10%). Ruka na POWER samo u tutorijalu.
- [ ] V4 Rupe uvučene, rail zasečen po jaw-u, kape ravne i unutar okvira, 0 preseka. Fizika ista.
- [ ] V5 Lampe vidljive osim kad zaklanjaju sto, bez treperenja.
- [ ] V6 Tajmer 45 s na panelu (i botu), mirno do 10 s, amber + broj, haptic na 5 s, istek = faul + ball in hand, nema ga u tutorijalu i TRY.
- [ ] V7 Basement Bar u novom stilu, odobren na checkpointu A, u budžetu performansi.
- [ ] V8 Ostale 4 sale u istom stilu.
- [ ] Svi postojeći i novi checks zeleni, grep slojeva prazan, README i CREDITS ažurirani, 9 commitova pushovano, checkpointi 1, A i 2 potvrđeni.

---

## 7. Rizici
| Rizik | Šta uraditi |
|---|---|
| Kadar koji se ne primiče dok prst vuče može ostati predaleko | Posle smirivanja (`AimFrameSettleTime`) se glatko vrati na optimalan. Ako korisnik na checkpointu 1 kaže da je spor, smanji vreme (podatak). |
| Uklanjanje squirt-a i swerve-a menja balans bota | Bot koristi istu fiziku. Proveri pre/posle. Ako izađe iz opsega, stani i pitaj. |
| `SwerveFactor = 0` gasi i masse posle kontakta | Proveri u kodu gde se swerve primenjuje. Ako je zajednički, predloži razdvajanje (pre/posle kontakta) i pitaj. |
| Render queue za belu pokvari senku ili refleksiju | Rezerva: stencil maska bele za materijal vodilje. |
| Nova geometrija rupa promeni gde izgleda da kugla upada | Fizika je ista. Proveri vizuelno da kugla ulazi u vidljivi otvor (screenshot sekvenca pada). |
| Pravilo zaklanjanja lampe treperi na granici | Histereza + minimalno vreme u stanju (npr. 0.3 s). Provera broja promena po orbit prolazu. |
| Tajmer stvara pritisak uprkos dizajnu | Neutralna boja, bez broja do 10 s, bez zvuka, 45 s. Ako korisnik i dalje oseća pritisak: duže vreme ili kraći „warn“ prozor (podaci). |
| Art posao naraste u „više stvari“ | Princip iz V7. U izveštaju navedi šta je **uklonjeno**, ne samo šta je dodato. |
| Teksture i mesh-evi spuste FPS na telefonu | Budžet iz V7. Mipmap i kompresija kao v3 V6. Meri na uređaju. |
| Licence novih asseta | Samo CC0/OFL, upis u `CREDITS.md`. Ako licenca nije jasna, stani i pitaj. |
