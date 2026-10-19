# Implementation Definition — Gameplay Redesign (Billiards, faza „polish 1/2“)

Ovaj dokument je **jedini ulaz** za agenta koji radi gameplay redizajn postojeće igre. Čitaj ga ceo pre prvog koraka.
Kad nešto nije pokriveno — **stani i pitaj korisnika**, ne pretpostavljaj.

Prateći dokumenti (čitaj, ne menjaj):
- `README.md` — trenutna arhitektura, eventi, builderi, checks.
- `implementation_definition.md` — originalna specifikacija (slojevi 4.3, konvencije 4.9, CLI rad 3, verifikacioni alati 8). **Sve konvencije odande važe i ovde**, osim gde ovaj dokument kaže drugačije.
- `release_plan.md` — kontekst proizvoda (zašto ovo radimo).

Posle ovog dokumenta ide `meta_design_implementation_definition.md` (arene, AI likovi, ekonomija, FTUE). **Ne radi ništa iz meta dokumenta ovde.** Postojeća ekonomija (novčići, 5 štapova, energija) ostaje netaknuta dok je meta dokument ne zameni.

---

## 0. Pravila rada za agenta

1. Radiš kroz **9 commitova** (sekcija 6). Svaki commit je kompajlabilna, pokretljiva celina. Posle svakog: `git commit` + `git push` na `main` (remote `origin` već postoji). Nikad force push.
2. Pre svakog commita prolaziš **verifikaciju** iz tog koraka: `unity recompile` 0 grešaka, grep provere slojeva (README „Layer rules“) prazne, svi postojeći `Editor/Checks` prolaze, plus nove provere iz commita. Nikad ne tvrdiš da nešto radi ako nisi proverio. Ono što ne možeš proveriti (osećaj na telefonu, haptic, zvuk) napiši eksplicitno.
3. Posle svakog commita kratak izveštaj korisniku: šta je urađeno, kako je provereno, šta nije provereno, putanje screenshotova, commit hash.
4. **Checkpointi sa korisnikom** (posle commita 4 i 9): napravi APK (`AndroidBuilder.Build()`), javi korisniku šta da testira na telefonu i **čekaj potvrdu** pre nastavka.
5. Stani i pitaj ako: Editor nije dostupan CLI-ju, nedostaju zvukovi iz preduslova P2 (osim ako korisnik kaže „nastavi sa placeholderima“), build pada iz neočiglednog razloga, rešenje bi kršilo pravila slojeva, ili bi izmena promenila pravila igre / fiziku (`Pool/*` logiku). Ovaj dokument **ne menja fiziku ni pravila** — samo prikaz, kontrole, kameru i UX.
6. Sve scene, prefabe i generisane assete menjaš **kroz buildere** (`Editor/Builders`), nikad ručno YAML. Novi brojevi idu u `MatchConfig` (ili novi SO), ne u kod.
7. Screenshotovi idu u `Screenshots/` sa prefiksom `g<commit>_` (npr. `g3_watch_from_head.png`). Uvek portrait 1080×1920 (`GameViewUtil.SetPortrait()`), za kameru dodatno 1080×2400 (19.5:9).
8. **Unity CLI na ovom Mac-u** (proverena zapažanja):
   - Ako `unity recompile` stoji na „triggered“, probudi Editor: `osascript -e 'tell application "System Events" to tell process "Unity" to get name of every window'`. Timeout je ok.
   - Posle `editor_play` postavi `UnityEngine.Application.runInBackground = true` kroz `eval`, inače Play mode stoji. Posle `editor_stop` vrati na `false`.
   - FPS iz nefokusiranog Editora (~12) nije merodavan. FPS meri samo na uređaju.
9. Ne diraj ništa van `~/Desktop/UnityProjects/Billiards` osim `~/Desktop/UnityProjects/_kenney_downloads/` (download folder).

---

## 1. Cilj i obim

Grupa testera je igrala prototip i dala 9 tačaka feedbacka. Cilj je da udarac, kamera i sto budu **jasni, fer i zadovoljavajući** za igrača koji želi „skilled, first-person, chill“ bilijar, bez objašnjavanja uživo.

| # | Feedback (izvorno) | Zahtev |
|---|---|---|
| F1 | Loša pozicija kamere; nisu sve rupe vidljive; kugla upadne, a ne skontam | **G1** Watch kamera + **G4** feedback ubacivanja |
| F2 | Bar za fino nišanjenje nije prepoznatljiv | **G2** novi fine aim |
| F3 | Rupe izgledaju kao crni valjci; sto i okolina neuverljivi | **G3** sto, rupe, materijali, svetlo, soba |
| F4 | Fali satisfakcija: ubacivanje, perfect, zvuk sudara/upada, animacija ka ubačenim | **G4** juice |
| F5 | Pravila nejasna (ball in hand); ne 2D pogled | **G5** 3D ball in hand + jasnoća pravila |
| F6 | Štap prolazi kroz sto / deluje providno | **G6** elevacija štapa + kamera van geometrije |
| F7 | Stariji igrač nije shvatio da je povlačenje = snaga; mislio da kreće od „PULL“ | **G7** power kontrola |
| F8 | Početni nišan je idealan udarac | **G8** neutralan početni nišan |
| F9 | Strelica se ne vidi preko svetle kugle | **G9** nov aim guide |
| — | iz `release_plan.md` §3 | **G10** tutorijal + kontekstualni hintovi, **G11** release bar polish |
| — | čišćenje | **G0** uklanjanje 2 PLAYERS moda |

**Van obima:** arene, AI likovi, ekonomija, novi meni, štapovi (sve u meta dokumentu); analitika, reklame; spin; promene pravila 8-ball; promene `PoolPhysics`/`EightBallRules`/`BotBrain` logike (osim čitanja podataka koje već računaju).

---

## 2. Preduslovi

| # | Šta | Ko | Status |
|---|---|---|---|
| P1 | Editor otvoren na Billiards, `unity status` → ready, git čist na `main` **osim** necommitovanih `release_plan.md`, `gameplay_redesign_implementation_definition.md` i `meta_design_implementation_definition.md` — njih uključi u commit 1 | agent proverava | — |
| P2 | **Bilijarski zvukovi** (lista ispod), CC0 ili kupljeni sa licencom za komercijalnu upotrebu, u `~/Desktop/UnityProjects/_kenney_downloads/billiard_sfx/` | **korisnik** | otvoreno |
| P3 | Poly Haven teksture (CC0) — agent ih sam skida | agent | — |

**P2 — lista zvukova.** Generički Kenney impact zvukovi su glavni razlog što udarci „ne zvuče kao bilijar“; ovo je najveća razlika u osećaju za malo novca. Imena fajlova:
```
ball_click_01..06      kugla–kugla, različite jačine
break_01..02           razbijanje trougla (gust niz klikova)
cue_hit_soft_01..03    štap–bela, mek udarac
cue_hit_hard_01..03    štap–bela, jak udarac
cushion_01..04         kugla–cushion (tup, gumeni)
pocket_drop_01..04     kugla upada u rupu (tup „thunk“)
pocket_rattle_01..02   kugla se kotrlja/klepeće u rupi
ball_roll_loop         kotrljanje po čoji, seamless loop 2–4 s
room_ambience_loop     (opciono) tih ambijent sale, loop
```
Izvori:
- (a) Freesound.org sa filterom **License: Creative Commons 0** — traži nalog, korisnik skida.
- (b) kupljen paket (itch.io / Unity Asset Store, ~5–20 €, budžet iz `release_plan.md` §7) — sačuvaj licencu.
- (c) snimanje telefonom na pravom stolu.

Agent beleži izvor i licencu svakog fajla u `CREDITS.md`. Ako P2 nije spremno na commitu 7, agent pita korisnika. Uz pristanak nastavlja sa Kenney placeholderima (postojećim), a mapiranje ostavlja spremno za zamenu fajlova.

---

## 3. Dizajn po zahtevima

Za svaki zahtev: **Problem → Uzrok u kodu → Rešenje → Prihvatni kriterijumi**.

### G0 — Uklanjanje 2 PLAYERS moda
- **Uzrok:** `MatchSetup.Mode` / `IsTwoPlayers` se koristi na ~34 mesta: `OnTwoPlayersRequested`, `TurnInfo.IsTwoPlayers`, `MatchResult.IsTwoPlayers`, `TurnBannerView`, `MainMenuView`, `MatchController`. To je mod iz zezanja, razvodnjava test i kompleksnost.
- **Rešenje:**
  - Ukloni enum vrednost `TwoPlayers` i sve grane. `MatchSetup` zadrži kao mesto za mod sledećeg meča (G10 dodaje `Tutorial`).
  - Ukloni dugme „2 PLAYERS“ iz menija kroz `MenuSceneBuilder`/`UIPrefabBuilder`; ostatak menija ne menjaj.
  - Ažuriraj README (Modes tabela, Events tabela).
- **Kriterijum:** `grep -rn "TwoPlayers\|IsTwoPlayers" Assets/_Scripts` prazan; meč protiv bota radi od menija do rezultata.

### G1 — Watch kamera (posle udarca i potez protivnika)
- **Problem:** posle udarca kamera „se uspravi“ iza bele (`CameraMode.Follow`) i prati belu. Na portrait ekranu udaljene ili bočne rupe ispadaju iz kadra (vidi `Screenshots/c7_fp_watch.png`: donje rupe nisu vidljive). Igrač ne vidi da je kugla upala.
- **Pravilo (nepregovarivo):** dok se kugle kreću i dok protivnik nišani, **svih 6 rupa** (tačka na rubu rupe na visini rail-a) je unutar `WatchFitArea` viewporta. `WatchFitArea` = ispod player panela, iznad dna; početno `Rect(0.03, 0.06, 0.94, 0.78)`, u `MatchConfig`.
- **Rešenje — novi režim `CameraMode.Watch`, zamenjuje `Follow` i `Opponent`:**
  1. **Gde stoji:** na **kraćoj strani stola** (head ili foot), na strani na kojoj je stajao strelac. Za igrača: kraj bliži poziciji Aim kamere. Za bota: kraj iza bele bota u odnosu na njegov pravac. Portrait ekran je visok, a sto dugačak; samo pogled niz dužu osu staje u kadar bez dalekog udaljavanja.
  2. **Visina i udaljenost:** od `StandingEyeHeight` iznad poda, korak unazad i blago gore (`AimRiseSlope` logika) dok sve rupe ne uđu u `WatchFitArea`. Koristi isti obrazac pretrage kao `FitDistance` (binarna pretraga). Pitch se računa tako da centar stola bude na ~55% visine fit oblasti. Rezultat je pozicija čoveka koji stoji na kraju stola (first-person ostaje), ne top-down.
  3. **Pogled prati akciju u granicama:**
     - Gaze se pomera ka beloj kugli (dok je igrač gledao svoj udarac) ili ka pravcu udarca bota.
     - Ograničenje: posle svake promene gaze-a proveri da su svih 6 rupa i dalje u fit oblasti (6 × `WorldToViewportPoint`, jeftino). Ako nisu, smanji pomeraj (lerp ka centru) dok ne budu.
  4. **Prelaz:** iz Aim posle `StandUpDelay` (0.35 s), blend `StandUpTime` (0.8 s), SmoothStep. Head sway ostaje.
  5. **Bot nišani:** ista Watch poza, kraj stola iza bele bota. Igrač vidi štap bota i ceo sto. Stari `OpponentSpot` (bočni pogled) se briše.
- **Aim režim:** postojeći `FitAimDistance` ostaje.
  - Proveri da se odredišna rupa pogođene kugle uključuje u fokus. `FrameShot` sada uzima tačku do 2 m niz `ObjectDir` — zameni je **centrom rupe** ka kojoj `ObjectDir` vodi, kad je ugao do rupe < 8°.
- **Kriterijum (automatski):** novi `Editor/Checks/CameraChecks.WatchFitsAllPockets()`.
  - Za 300 nasumičnih pozicija bele × nasumičnih pravaca, za oba strelca, na aspektima 9:16, 9:19.5 i 9:21: postavi rig u finalnu Watch pozu (bez blenda) i proveri da je svih 6 rupa u `WatchFitArea`.
  - Prijavi broj prolaza; mora biti 100%.
- **Kriterijum (vizuelni):** screenshotovi `g3_watch_head.png`, `g3_watch_foot.png`, `g3_watch_bot.png` — 6 rupa vidljivo, HUD ne prekriva rupe.

### G2 — Fine aim koji se prepoznaje
- **Problem:** vertikalna traka sa crticama levo (`FineAimWheelView`) ne izgleda kao kontrola. Vertikalni drag menja *horizontalni* pravac, što nije intuitivno. Nema oznake.
- **Rešenje:**
  1. **Horizontalni točkić na dnu ekrana** (vidi raspored 3.1): drag levo = štap se okreće levo, drag desno = desno. Pokret prsta i pokret nišana su u istoj osi.
  2. **Izgled:**
     - Nazubljen točkić (generisan sprite): vertikalne crtice sa gradijentom senke na krajevima, kao valjak; crtice klize sa prstom.
     - Natpis `FINE AIM` iznad, ikonice ◀ ▶ na krajevima.
     - Dok se drži: točkić posvetli i lagani haptic tick na svakih 0.1°.
  3. **Dugmad za korak** ◀ ▶ na krajevima, za starije igrače i precizan rad:
     - tap = 0.05°;
     - držanje = ponavljanje 12/s, posle 0.6 s ubrzava do 0.2° po koraku.
  4. Osetljivost: `FineAimDegreesPerPixel` ostaje 0.01°/ref px (tuning u `MatchConfig`). Tastatura A/D ostaje.
  5. Vidljiv samo u `Aim` fazi. Tokom `Pulling` njegovo mesto zauzima release bar (G11). **Dno ekrana = levo/desno, desna strana = snaga.**
- **Kriterijum:**
  - `eval` simulira drag od +100 ref px → nišan rotiran za tačno +1.0° (znak: desno).
  - Tap na ▶ = −0.05° ili +0.05° (dokumentuj znak).
  - Screenshot `g4_aim_controls.png`.

### G3 — Sto, rupe, materijali, svetlo, soba
- **Problem:** `TablePrefabBuilder.PocketMesh` je crni cilindar (`Screenshots/c2_pocket_corner.png`). Cushion-i i rail-ovi su kutije. Materijali su ravne proceduralne teksture.
- **Rešenje — rupe:**
  1. **Grlo rupe:** čoja se savija preko ivice u rupu (zaobljen spoj, radijus ~8 mm, 6–8 segmenata), bez oštre crne ivice. Čoja sa cushion nosa prelazi u jaws.
  2. **Obloga (liner):**
     - Kožni/gumeni levak koji se sužava (vrh = rupa, dno ~85% radijusa), dubine `PocketDepth`, sa zaobljenim dnom (polusfera).
     - Materijal: tamna koža, smoothness 0.3.
     - **Vertikalni gradijent kroz vertex boje** (gore tamno-smeđe, dole skoro crno) daje osećaj dubine umesto ravne crne.
  3. **Kape rupa:** na uglovima i sredinama zaobljene metalne kape (mesing ili hrom, prema `VenueTheme`) preko rail-a oko otvora, kao na pravim stolovima.
  4. Ubačena kugla **ostaje vidljiva na dnu rupe** do kraja poteza (G4), pa nestaje. Rupa mora biti dovoljno duboka i osvetljena da se to vidi odozgo.
- **Rešenje — sto:**
  1. **Cushion:** trougaoni profil (nos na visini ~63% prečnika kugle, kao pravi K-66 profil), presvučen čojom (isti `Felt` materijal, ~8% tamniji). Zamenjuje kutije.
     - Fizička geometrija (`TableGeometry`) se **ne menja**: nos cushion-a vizuelno mora biti tačno na liniji kontakta iz `TableGeometry`. Proveri screenshotom kugle prislonjene uz cushion.
  2. **Rail:** drvo sa zaobljenom/skošenom gornjom ivicom (chamfer mesh). Dijamanti kao sedefasti inlay-i (ovalni, blago emisivni).
  3. **Materijali (PBR, URP Lit):**
     - Drvo, čoja i koža iz **Poly Haven** (CC0, https://polyhaven.com/textures), 1K, albedo + normal (+ roughness).
     - Skidaj u `_kenney_downloads/polyhaven/<ime>/`, kopiraj samo korišćene fajlove u `Assets/_Art/PolyHaven/<ime>/`, zabeleži u `CREDITS.md`.
     - Čoja: fina tkanina, tileable, normal slabog intenziteta (bez moare efekta na telefonu — proveri na screenshotu iz Aim-low ugla).
     - Ako download ne radi → zadrži generisane teksture i javi.
  4. **Kugle:**
     - Smoothness 0.9.
     - **Reflection probe** iznad stola (baked u builderu, 128 px) da kugle reflektuju lampu i sobu. Ovo je najveći pojedinačni dobitak „realnosti“.
     - **Kontaktna senka:** blob decal ispod svake kugle — quad sa radijalnim gradijentom, ~2.2R, alpha 0.5, prati kuglu, nestaje pri upadu.
  5. **Svetlo:**
     - Lampa iznad stola vidljiva u kadru (abažur, emisivne sijalice). Topao spot sa mekom senkom ostaje jedino svetlo sa senkom.
     - Blagi falloff ka ivicama stola, okolina tamnija.
     - Post: vinjeta + slab bloom samo na emisivnim delovima.
- **Rešenje — soba (`RoomPrefabBuilder`, nadogradnja postojećeg):**
  - Drveni pod (Poly Haven daske).
  - Zidovi: lamperija do pola, tapet iznad.
  - Postojeći šank, police, rack za štapove, slike — dobijaju nove materijale.
  - Jedan **neonski natpis** („POOL“ ili „8“) kao fokus chill atmosfere, emisivan, bez realtime svetla.
  - U pozadini 1 drugi sto u polumraku.
  - Sve van stola: **bez realtime senki**, bez dodatnih realtime svetala (emisija + ambient + light probe ako treba).
- **`VenueTheme` (infrastruktura za meta dokument):**
  - Novi ScriptableObject `Data/VenueTheme.cs`: `Id`, `DisplayName`, `FeltColor`, `WoodMaterial`, `CapMaterial` (mesing/hrom), `LampColor`, `LampIntensity`, `RoomVariant` (enum ili prefab), `NeonText`, `NeonColor`, `AmbientColor`.
  - Match scena ga primenjuje pri startu: material property blokovi, instanciranje prefaba sobe.
  - U ovom dokumentu napravi **jedan** asset: `Club.asset` (trenutni izgled, unapređen). Meta dokument dodaje 4 arene.
  - `MatchSetup.Venue` nosi izabranu temu (default `Club`).
- **Performanse (budžet, meri na uređaju u commitu 9):** ≤ 150 draw calls i ≤ 150k trouglova u Aim pogledu; jedno svetlo sa senkom; 60 FPS na S23, ≥ 30 stabilnih na starom Note-u. Ako ne prolazi: prvo isključi bloom, pa smanji rezoluciju senke, pa pojednostavi sobu.
- **Kriterijum:** screenshotovi `g6_pocket_corner_close.png`, `g6_pocket_side_close.png` (isti ugao kao `c2_pocket_corner.png` radi poređenja), `g6_aim_low.png`, `g6_watch.png`, `g6_room.png`; `PhysicsChecks.*` nepromenjeni.

### G4 — Juice: ubacivanje, perfect, zvuk
- **Problem:** pri upadu kugla klizi ka centru rupe i smanjuje se (`BallView.PlayPocket`), pa nestane. Zvuci su generički (`impactPlate_light`). Nema veze između rupe i panela.
- **Rešenje — upad u rupu:**
  1. **Animacija upada** (`BallView`, samo prikaz):
     - Kugla nastavlja ulaznom brzinom preko ruba, pa pada sa gravitacijom (~0.15–0.25 s) i blago odskoči jednom od dna obloge.
     - Ostaje vidljiva na dnu do kraja poteza (`MatchController.Resolve` → sakrij sve ubačene).
     - Potrebni podaci: dodaj `Vector2 Velocity` u `PocketEvent` — **samo beleženje**, `PoolPhysics` logika se ne menja. Proveri da `PhysicsChecks.Determinism` i dalje prolazi.
  2. **Zvuk:** `pocket_drop` (jačina po ulaznoj brzini) + posle ~0.1 s `pocket_rattle`.
     - **Combo:** svaka sledeća ubačena kugla u istom potezu ima ton višlji za +1 polustepen (do +4). Tihi „chime“ sloj daje osećaj serije.
  3. **Sjaj rupe:** kratak prsten svetla na rubu rupe (0.4 s, ease out):
     - zelen = legalno ubačena kugla strelčeve grupe (ili bilo koja na open table);
     - crven = bela kugla (scratch) ili 8 pre vremena;
     - neutralno beo = protivnikova kugla.
  4. **Haptic:** medium za legalno ubačenu, light za ostalo.
  5. **Leteća kugla do panela:**
     - Posle 0.3 s UI ikonica kugle (postojeći 96×96 sprite) se pojavi na ekranskoj poziciji rupe i leti lukom (0.45 s, OutCubic, kratak trail) do svog slota u YOU/OPPONENT panelu.
     - Kad stigne, slot se zasivi uz punch, a brojač „3 left“ se smanji.
     - Na open table leti ka panelu strelca. Bela kugla ne leti.
     - Slojevi: UI ne zna za kameru. Novi event `OnBallPocketedFx(int number, Vector2 screenPos, PocketFxKind kind)` diže gameplay strana (`TableView`, kamera daje `WorldToScreenPoint`). `PlayerPanelView` i novi `PocketFlyerView` (UI) ga slušaju.
- **Rešenje — udarac i sudari:**
  1. `SoundId` dobija varijante: `Sound` unos ima `AudioClip[] Clips`, bira nasumično bez ponavljanja prethodnog.
     - Novi id-jevi: `Break`, `CueHitSoft`, `CueHitHard`, `PocketDrop`, `PocketRattle`, `BallRollLoop`, `RoomAmbience`.
     - `CueHit` se bira po snazi (< 0.5 soft, inače hard).
     - Prvi sudar na break-u: `Break` umesto pojedinačnih klikova (prvih 150 ms).
  2. **Kotrljanje:** jedan loop izvor; volumen = `clamp01(Σ|v| / 4 m/s) × 0.5`, pitch 0.9–1.1 po brzini; utišava se kad sve stane (fade 0.2 s).
  3. Throttle sudara ostaje (25 ms); jačina i dalje po brzini udara.
- **Rešenje — release feedback:**
  - **Perfect:**
    - bar blesne zelenim;
    - pečat `PERFECT` (scale punch, 0.5 s) iznad bele kugle na ekranu, ne kao generički toast;
    - oštar „crack“ sloj na udarcu + kratak chime;
    - **puff krede** (mali partikl, 8–12 čestica, plavičasto-beo) na mestu kontakta štapa i bele;
    - haptic strong-kratak.
  - **Good:** žut blesak bara, pečat `GOOD`.
  - **Miss:** bar se kratko protrese, pečat `MISS` + prikaz namerene putanje (G11).
- **Kriterijum:**
  - `eval` forsira scenario „ubaci 2 kugle u istom potezu“ (debug postavka stola) → 2 eventa `OnBallPocketedFx` sa ispravnim `kind`; panel brojač −2; kugle vidljive u rupi do kraja poteza.
  - Screenshotovi `g7_pocket_glow.png`, `g7_flyer_midair.png`, `g7_perfect_stamp.png`.
  - Zvuk i haptic potvrđuje korisnik na checkpointu.

### G5 — Ball in hand u 3D + jasna pravila
- **Problem:** ball in hand prebacuje kameru u skoro top-down (75°, `CameraMode.Place`), što deluje kao 2D. Testeri očekuju različita pravila (bilijar ima mnogo varijanti) i ne znaju zašto je neko dobio ball in hand.
- **Rešenje — kamera i kontrola:**
  1. `CameraMode.Place` postaje **„stojim iznad stola sa kuglom u ruci“**:
     - first-person poza sa strane na kojoj igrač stoji (break: head kraj stola, kuhinja ispred; posle faula: kraj stola bliži trenutnoj beloj);
     - visina stojećeg čoveka, pitch 50–60°, perspektiva;
     - fit: dozvoljena oblast (kuhinja ili ceo sto) + bela kugla u `PlaceFitArea`.
  2. **Direktno prevlačenje** bele u tom pogledu: raycast na ravan čoje (postojeći `ScreenToTable`). Bela prati prst, lebdi 2 cm iznad čoje sa senkom ispod (osećaj „u ruci“). Na pusti se spusti (0.12 s) uz tih „tap“ zvuk.
  3. **Nevalidna pozicija:** ghost crven, bela se vrati na poslednju validnu (postojeća logika). Kuhinja: osvetljena oblast + linija (postojeće, prilagodi perspektivi).
  4. Posle `DONE` ili prvog drag-a na prazan deo stola → Aim, kamera se spusti iza bele.
     - U Aim fazi, ako je ball in hand, **drag koji počne na beloj kugli** (unutar 2.5R na ekranu) direktno je pomera bez vraćanja u Place. Dugme `MOVE BALL` ostaje kao vidljiva alternativa.
- **Rešenje — pravila:**
  1. **Kartica „House Rules“** (popup; `RulesPopup` nasleđuje `PopupBase`), 6 redova sa ikonicama:
     - „Pocket all of your group — solids (1–7) or stripes (9–15) — then the 8-ball.“
     - „Your group is the first ball you pocket after the break.“
     - „Hit one of your own balls first.“
     - „After contact, a ball must reach a rail or drop in.“
     - „Foul → your opponent gets **ball in hand**: they place the cue ball anywhere.“
     - „The 8-ball can go in any pocket. Pocket it early, or with a foul, and you lose.“
     - Prikazuje se: automatski pre prvog meča (flag `RulesSeen`, čuva `ProfileService`), i uvek iz Pause → `RULES`.
  2. **Faul banner** (zamenjuje generički toast u `ToastView.HandleFoul`), dvoredni banner kroz `TurnBannerView`:
     - red 1: `FOUL — Wrong ball first` / `Cue ball pocketed` / `No ball hit` / `No rail after contact`;
     - red 2 (posledica): `Opponent has ball in hand` ili `You have ball in hand — place the cue ball anywhere`;
     - mala `?` ikonica otvara `RulesPopup` na tom pravilu (scroll + highlight reda);
     - trajanje 2.2 s; potez ne kreće dok banner ne ode (input blokiran).
  3. **Ball in hand banner:** `BALL IN HAND · Place the cue ball anywhere` / break: `BREAK · Place the cue ball behind the line`.
  4. **Dodela grupe:** banner `YOU ARE SOLIDS (1–7)` sa ikonicom pune kugle, 1.6 s.
  5. **Nastavak poteza:** toast `Nice! Shoot again`. **Kraj poteza bez faula:** toast `No ball pocketed — opponent's turn`.
- **Kriterijum:**
  - Postojeći `RulesChecks.All()` nepromenjen.
  - `eval` forsira svaki `FoulType` → screenshot `g5_foul_<tip>.png`.
  - Screenshotovi `g3_place_break.png`, `g3_place_anywhere.png` (perspektiva, ne top-down), `g5_rules_popup.png`.

### G6 — Štap ne prolazi kroz sto
- **Problem:** štap ima fiksnu elevaciju `CueElevation = 5°` (`CueView.ApplyPose`). Kad je bela blizu cushion-a, drška prolazi kroz cushion, rail i okvir, pa deluje „providno“.
- **Prvi korak — reprodukcija:** postavi belu na 1.5R, 3R i 6R od svakog cushion-a, sa pravcem od cushion-a. Napravi screenshotove u Aim-low i Aim-high. Utvrdi da li postoji i drugi uzrok: kamera ulazi u rail/okvir ili near clip plane seče štap. Ako jeste, reši i to (pravilo ispod).
- **Rešenje:**
  1. **Dinamička elevacija** (kao pravi igrač koji podigne dršku preko rail-a):
     - Elevacija = najmanji ugao u `[CueElevation, CueMaxElevation = 35°]` pri kome **donja ivica štapa** prolazi iznad sve geometrije iza bele sa razmakom `CueClearance = 4 mm`.
     - Geometrija: cushion (`CushionTop`), rail (`RailTop`), kape rupa, i **druge kugle** iza bele (vrh kugle = 2R).
     - Računa se analitički u table-space: uzorkuj ~24 tačke duž štapa (od vrha ka dršci), visina potrebna u svakoj tački = vrh prepreke + clearance + radijus štapa u toj tački.
     - Promena je glatka (SmoothDamp 0.08 s); dok se povlači, elevacija je zaključana.
  2. Isto važi za štap bota.
  3. Samo prikaz: elevacija **ne utiče** na fiziku ni na smer udarca (nema masé-a).
  4. **Kamera nikad u geometriji:** u Aim pozi, ako je kamera iznad rail-a ili okvira, visina ≥ `RailTop + 0.05 m`. Near clip plane ≤ 0.02 m. Proveri da štap ima opaque materijal sa ZWrite.
- **Kriterijum:** novi `Editor/Checks/CueChecks.NoIntersection(500)`.
  - 500 nasumičnih pozicija bele (60% u pojasu 0–8R od cushion-a ili kugli) × nasumičan pravac.
  - Izračunaj pozu štapa i proveri da nijedna od 48 tačaka na donjoj ivici štapa nije unutar zapremine cushion-a, rail-a ili kugle.
  - Mora biti 0 preseka. Screenshotovi `g2_cue_near_rail_low.png`, `g2_cue_near_rail_high.png`.

### G7 — Power kontrola koja se razume
- **Problem:**
  - Natpis „PULL“ je na **dnu** trake, pa deluje kao mesto odakle se kreće.
  - Uz to, opseg povlačenja je fiksnih 800 ref px od tačke dodira (`PowerStripView`, `PullRangePixels`). Ko krene nisko **fizički ne može** da dostigne punu snagu.
  - Ništa ne kaže da je dužina povlačenja = snaga.
- **Rešenje:**
  1. **Natpis i oblik:**
     - Na vrhu trake `POWER` sa ikonicom, ispod animirana strelica ↓. Natpis „PULL“ na dnu se uklanja.
     - Uz traku tri oznake: `SOFT` (gore), `MEDIUM`, `HARD` (dole).
     - Fill uz štap jedne boje (belo → toplo narandžasto). Bez zelene, žute i crvene, da se ne meša sa release zonama.
  2. **„Uhvatio sam štap“:** na pointer down štap u traci odmah dobije sjaj i lagani haptic, pre bilo kakvog pomeranja.
  3. **Prilagodljiv opseg:** `range = clamp(udaljenost od tačke dodira do dna trake − 20 px, PullMinRangePixels = 320, PullRangePixels = 800)`. 100% je uvek dostižno. Mapiranje je i dalje linearno od tačke dodira.
  4. **Idle hint:** ghost ruka pritiska gornji deo štapa i vuče na dole, petlja na 3 s.
     - Prikazuje se dok igrač nema **3 uspešna udarca** u ukupnoj istoriji (flag u profilu).
     - Kasnije se prikazuje i ako igrač u Aim fazi ne dodirne traku 8 s.
  5. Hit oblast: cela desna kolona (~22% širine) između HUD-a gore i bottom kontrola.
- **Kriterijum:**
  - `eval`: pointer down na 85% visine trake pa drag do dna → `pull01` dostigne 1.0.
  - Pointer down na vrhu i drag 400 px → 0.5.
  - Screenshotovi `g4_power_idle_hint.png`, `g4_power_pulling.png`.

### G8 — Neutralan početni nišan
- **Problem:** `PlayerShotController.HandleTurnStarted` i `HandlePlaceCueBallConfirmed` postavljaju `_aim = _advisor.SuggestAim(...)`, tj. bot bira najbolji udarac umesto igrača. Igra je tada samo „udari“.
- **Rešenje:**
  1. Početni nišan = **pun pogodak u centar najbliže legalne, neblokirane mete** (prava linija bela → centar mete).
  2. Ako ta linija po `ShotPredictor`-u ubacuje metu (pravi „straight-in“), rotiraj nišan za nasumičnih ±(2°–4°), da i tada igrač mora da nanišani.
  3. Break: pun pogodak u vrh trougla (prirodno, ne „rešava“ ništa).
  4. Ako nema neblokirane legalne mete: pravac ka najbližoj legalnoj meti bez obzira na blokadu.
  5. `BotBrain` ostaje samo za bota. `PlayerShotController` više ne koristi `_advisor`.
  6. Magnet (`AimMagnetDegrees`) ostaje: hvata *pun pogodak* (centar kugle), ne liniju ubacivanja.
- **Kriterijum:** novi `Editor/Checks/AimChecks.DefaultAimNotAPot(500)`.
  - Na 500 nasumičnih stanja stola (posle simuliranog break-a + nasumičnih udaraca) predikcija početnog nišana **ne** ubacuje metu u ≥ 99% slučajeva.
  - Svi slučajevi pogađaju legalnu metu kad postoji neblokirana.
  - Prijavi brojeve.

### G9 — Aim guide koji se vidi
- **Problem:**
  - Linije su 6 mm bele `LineRenderer` linije na čoji. Putanja bele počinje u centru bele, pa je ispod same kugle nevidljiva.
  - Bela linija preko bele, žute ili svetle kugle nema kontrast.
  - Ghost ball je tanak prsten.
  - `Prediction.CueDeflectDir` se računa, ali se ne prikazuje.
- **Rešenje — „traka putanje“ sa strelicama:**
  1. **Putanja bele = traka širine 2R:** koridor koji bela prebriše, pa se odmah vidi da li okrzne drugu kuglu.
     - Fill: belo 18% alpha.
     - Ivice: tamna kontura 1.5 mm (crno 55%), tako da traka ima kontrast i na beloj kugli i na svetloj čoji.
     - Po sredini animirani **chevroni** (›) koji teku ka meti (0.35 m/s, razmak 5 cm, belo 85% sa tamnom senkom).
     - **Počinje na prednjoj ivici bele** (ne ispod nje), završava na ghost lopti.
  2. **Ghost ball:** 3D providna sfera na tački kontakta (belo 28% + fresnel obod 70%), umesto prstena. U first-person pogledu odmah se vidi „tu će bela biti“.
  3. **Pravac pogođene kugle:**
     - Traka širine 2R u **akcentnoj boji** (topla zlatna `#FFD54A`, 45% fill), tamna kontura, chevroni, vrh strelice.
     - Počinje na **daljoj ivici pogođene kugle** (nije ispod nje). Dužina po štapu (`ObjectGuideDiameters`, postojeće).
  4. **Putanja bele posle kontakta** (tangenta, `CueDeflectDir`): tanka isprekidana bela linija, 1.5 prečnika, 50% alpha. Ne prikazuje se pri punom pogotku.
  5. **Stanja:**
     - Ilegalna meta: traka bele i ghost crveni, X na ghost lopti, bez object trake.
     - Bela ide u rupu: traka narandžasta, rub te rupe svetli narandžasto.
     - Odbijanje od cushion-a: traka se nastavlja posle odbijanja (postojeći `HasBounce`).
  6. **Tehnika:**
     - Jedan `GuideBandRenderer` (MonoBehaviour u Gameplay) gradi quad-strip mesh u table-space. Prealocirani nizovi, **bez alokacija po frejmu**.
     - Unlit transparent shader (URP-kompatibilan, ručno napisan ili Shader Graph): UV.x duž trake za chevrone (scroll kroz `_MainTex_ST`, postojeći obrazac), UV.y preko trake za konturu.
     - Render queue posle čoje, pre kugli. Lift 2.5 mm (postojeći) sa depth offset-om protiv z-fightinga.
     - Chevron i kontura tekstura generišu se u `GeneratedArtBuilder`.
- **Kriterijum:**
  - `PredictorChecks.MatchesSimulation()` nepromenjen.
  - Screenshotovi `g2_guide_white_ball_close.png` (Aim-low, bela u prvom planu: traka čitljiva preko i oko bele), `g2_guide_yellow_target.png` (meta 1 ili 9), `g2_guide_illegal.png`, `g2_guide_scratch.png`, `g2_guide_bounce.png`.
  - Profiler: 0 B GC alokacija po frejmu u Aim fazi.

### G10 — Tutorijal i kontekstualni hintovi
- **Princip:** UI mora biti razumljiv **i bez** tutorijala (G2, G7, G9). Tutorijal samo ubrzava učenje.
- **Mod:** `MatchSetup.Mode = Tutorial` → Match scena učitava `TutorialController` umesto bota.
  - Nema novčića, energije ni nagrade. Bez coin toss-a.
  - Kraj: event `OnTutorialCompleted(bool skipped)`. U ovom dokumentu posle njega vodi na Menu; meta dokument to preusmerava na prvi meč.
- **Koraci** (posebna postavka stola, `TutorialStep` podaci u `TutorialConfig` SO):

| Korak | Sto | Vidljive kontrole | Instrukcija (EN) | Uspeh | Pomoć posle neuspeha |
|---|---|---|---|---|---|
| T1 Power | bela + 1 kugla, ravno ispred ugaone rupe, 0.45 m | samo POWER | „Put your finger on the cue and drag DOWN. The further you pull, the harder you hit. Let go to shoot.“ | ubačena | „Too soft — pull further“ / „Too hard — pull less“ (po ishodu) |
| T2 Aim | kugla pod ~20° rezom | POWER + drag po stolu + FINE AIM | „Drag on the table to aim. Put the ghost ball where the gold line points into the pocket.“ + coach mark na FINE AIM: „Use the wheel for tiny adjustments.“ | ubačena | posle 2 promašaja: kratko pokaži pravi nišan (ghost) 1 s |
| T3 Release | isto kao T2, druga strana | sve + release bar | „While you hold, the marker moves. Let go on GREEN and the ball goes exactly where you aimed. Left of green = left, right of green = right.“ | ubačena sa Green ili Yellow | replay: namerena vs stvarna putanja (G11) + „You let go in RED — the shot went off line.“ |
| T4 Ball in hand | bela u rupi (scratch situacija), 2 kugle | Place + sve | „Your opponent fouled: BALL IN HAND. Drag the cue ball anywhere, then shoot.“ | ubačena | — |
| Kraj | — | — | „You're ready! Rules: pocket your group, then the 8.“ → `RulesPopup` | — | — |

  - T1 i T2: release bar isključen (uvek Green), bar sakriven.
  - T3: period ×1.6 i Green širi za 50% (samo u tutorijalu).
- **Coach marks:**
  - Generička komponenta `CoachMarkView` (UI): zatamnjenje sa „rupom“ oko ciljane kontrole, ghost ruka animira gest, tekst u balonu.
  - Kontrola se cilja preko `CoachTarget` id-a (enum), ne direktne reference. Gameplay diže `OnCoachMarkRequested(CoachTarget, string)`.
- **Skip:** dugme `SKIP` gore desno (posle potvrde). `OnTutorialCompleted(true)`.
- **Persistencija:** `TutorialDone` (int 0/1), `HintsShown` (bitmask). Čita i piše samo `ProfileService`.
  - Settings dobija dugme `HOW TO PLAY` → pokreće tutorijal ponovo.
- **Kontekstualni hintovi u pravom meču** (svaki samo prvi put, coach mark ili kratak balon):
  - prvi faul (već pokriven G5 bannerom + `?`);
  - prvi ball in hand: „Drag the cue ball anywhere“;
  - prva dodela grupe;
  - prvi put samo 8 ostala: „Pocket the 8-ball to win!“;
  - posle 5 udaraca bez korišćenja FINE AIM: coach mark na točkiću;
  - posle 2 uzastopna Red release-a: „Let go on GREEN for a straight shot“.
- **Eventi za kasniju analitiku:** `OnTutorialStep(int step, TutorialStepResult result)` (Started/Succeeded/Failed/Skipped).
- **Kriterijum:**
  - `eval` provede ceo tutorijal debug release-om (Green, tačan nišan iz koraka) → `OnTutorialCompleted(false)`, flag postavljen.
  - Skip → `OnTutorialCompleted(true)`.
  - Screenshot svakog koraka `g8_t1..t4.png` + coach mark.

### G11 — Release bar polish
- **Odluka (umesto A/B prototipa iz `release_plan.md`):** **horizontalni bar na dnu ekrana**, na mestu fine aim točkića (koji je skriven tokom povlačenja).
  - Marker levo od zelenog = kugla ide **levo**, desno = **desno**. Smer na baru = smer greške; ne treba objašnjavati da „gore znači levo“.
  - Isti prostor (dno = levo/desno) za fine aim i za grešku gradi jedan mentalni model.
  - Ako korisnik na checkpointu 1 kaže da je vertikalni bar bio bolji, vrati se na vertikalni sa strelicom ←/→. Ne pravi oba.
- **Mapiranje:** `ShotAccuracy` se **ne menja** (bot i balans ostaju isti). View mapira poziciju tako da se strana prikaza poklapa sa znakom `AngleErrorDeg`.
  - Proveri znak eksperimentalno: `DebugReleaseAt` sa pozicijom u žutom levom delu → kugla mora otići levo od nišana (u table-space, gledano iz Aim kamere).
- **Izgled:**
  - Širina ~70% ekrana, visina ~70 ref px.
  - Zone kao sada (crveno–žuto–zeleno–žuto–crveno), veličine po štapu.
  - Marker: vertikalna crta sa trouglom gore i dole, senka.
  - Zelena zona pulsira dok je marker u njoj (postojeće ponašanje).
- **Posle puštanja:**
  - marker se zamrzne 0.5 s na mestu puštanja, zona blesne (G4);
  - za Yellow i Red: **namerena putanja** (isprekidana bela) i **stvarni pravac** (puna, u boji zone) vidljivi **1.2 s** (bilo 0.6 s, `MissGhostDuration`) od bele do mete. Igrač vidi uzrok promašaja.
- Početak oscilacije: od levog kraja (kao sada od donjeg); prvi prolaz kroz zeleno posle ~¼ perioda.
- **Kriterijum:**
  - `eval`: release na −0.5 i +0.5 → znak ugla greške se poklapa sa stranom prikaza.
  - Screenshotovi `g4_release_bar_pulling.png`, `g4_release_miss_replay.png`.
  - `BotBalanceCheck.Run(200)` u istim opsezima kao pre (nepromenjeno, jer se `ShotAccuracy` ne menja).

### 3.1 Novi raspored ekrana (portrait, ref 1080×1920)
```
┌──────────────────────────────────────┐
│ [YOU ●●●●○○○ 4 left] 8 [OPP ●●●●●○○] [II]│  ~0–12%  paneli + pauza
│                                        │
│                               ┌──────┐ │
│                               │POWER │ │  desna kolona ~78–98% širine,
│           3D pogled           │  ↓   │ │  ~22–80% visine
│     drag ↔ = nišan            │ ┃cue │ │
│     drag ↕ = nagib            │ SOFT │ │
│                               │ MED  │ │
│                               │ HARD │ │
│                               └──────┘ │
│ [👁 LOOK]  [✋ MOVE BALL]              │  ~82–88%
│ ◀ |||||||| FINE AIM |||||||| ▶         │  ~89–97%  Aim: fine aim
│   (tokom povlačenja: RELEASE bar)      │           Pulling: release bar
└──────────────────────────────────────┘
```
- Sve u safe area (`SafeArea`).
- Kontrole se ne preklapaju sa `WatchFitArea` tokom Watch-a (kontrole su tada skrivene).

### 3.2 Novi i izmenjeni eventi (upiši ih u README tabelu)
| Event | Payload | Ko diže | Ko sluša |
|---|---|---|---|
| `OnBallPocketedFx` | `int number, Vector2 screenPos, PocketFxKind kind` | `TableView` (posle `OnBallPocketed`) | `PocketFlyerView`, `PlayerPanelView`, `AudioManager`, `HapticsService` |
| `OnCoachMarkRequested` / `OnCoachMarkCleared` | `CoachTarget, string text` / — | `TutorialController`, `HintService` | `CoachMarkView` |
| `OnTutorialStep` | `int step, TutorialStepResult` | `TutorialController` | (kasnije analitika) |
| `OnTutorialCompleted` | `bool skipped` | `TutorialController` | `ProfileService`, controller scene |
| `OnRulesRequested` | `RuleId focus` (ili None) | `PausePopup`, faul banner `?` | `MatchController` → `PopupManager.Show<RulesPopup>` |
| `OnFineAimStep` | `int direction` (±1), `bool repeating` | dugmad ◀ ▶ | `PlayerShotController` |
| uklonjen: `OnTwoPlayersRequested` | | | |

`HintService` (Systems, persistent): prati uslove kontekstualnih hintova i pita `ProfileService` za `HintsShown`.

---

## 4. Konfiguracija (novi brojevi u `MatchConfig`)
`WatchFitArea`, `CueMaxElevation` (35), `CueClearance` (0.004), `PlaceFitArea`, `PlacePitchMin/Max` (50/60), `PlaceHoverHeight` (0.02), `PullMinRangePixels` (320), `FineAimStepDegrees` (0.05), `FineAimRepeatRate` (12), `FineAimMaxStepDegrees` (0.2), `MissGhostDuration` (1.2), `PocketGlowDuration` (0.4), `PocketFlyerDelay` (0.3), `PocketFlyerDuration` (0.45), `ComboPitchSemitones` (1), `RollLoopFullSpeed` (4).
Tutorijal: `TutorialConfig` SO (koraci, postavke kugli, multiplikatori bara).
Tema: `VenueTheme` SO (`_ScriptableObjects/Venues/Club.asset`).

---

## 5. Nove verifikacione provere (`Editor/Checks`)
| Check | Šta |
|---|---|
| `CameraChecks.WatchFitsAllPockets()` | G1: 100% prolaza na 3 aspekta, oba strelca |
| `CameraChecks.PlaceFitsArea()` | G5: kuhinja / ceo sto + bela u `PlaceFitArea`, perspektiva (pitch 50–60°) |
| `CueChecks.NoIntersection(500)` | G6: 0 preseka štapa sa geometrijom |
| `AimChecks.DefaultAimNotAPot(500)` | G8: ≥ 99% ne-ubacujućih početnih nišana |
| `ControlChecks.PullRangeReachable()` | G7: 100% dostižno iz bilo koje tačke dodira |
| `TutorialChecks.RunThrough()` | G10: ceo tutorijal headless kroz debug release |
| postojeći `PhysicsChecks.*`, `RulesChecks.All`, `PredictorChecks`, `BotBalanceCheck.Run(200)`, `EconomyChecks.All` | moraju i dalje prolaziti |

---

## 6. Plan commitova (9)

### Commit 1 — `cleanup: remove two-player mode`
**Obim:** G0.
**Verifikacija:**
- grep iz G0 prazan;
- Menu → Play → meč → rezultat (screenshot `g1_menu.png`);
- svi checks prolaze.

### Commit 2 — `aim: neutral default aim, guide bands, ghost ball, cue elevation`
**Obim:** G8, G9, G6.
**Verifikacija:**
- `AimChecks`, `CueChecks`, `PredictorChecks`;
- screenshotovi iz G6 i G9;
- 0 B GC po frejmu u Aim fazi (Profiler kroz `eval` ili `GC.GetAllocatedBytesForCurrentThread` razlika preko 120 frejmova).

### Commit 3 — `camera: watch framing for shots and opponent, 3D ball in hand`
**Obim:** G1, G5 (kamera i kontrola ball in hand).
**Verifikacija:**
- `CameraChecks.*`;
- screenshotovi iz G1 i G5;
- Bot vs Bot (postojeći debug) ceo meč bez izuzetaka.

### Commit 4 — `controls: power strip affordance, horizontal fine aim and release bar`
**Obim:** G7, G2, G11, novi raspored 3.1 (`UIPrefabBuilder`, `MatchSceneBuilder`).
**Verifikacija:**
- `ControlChecks`;
- `eval` kriterijumi iz G2, G7, G11;
- grep slojeva;
- `BotBalanceCheck.Run(200)` (prijavi brojeve);
- screenshotovi.

**→ CHECKPOINT 1:** APK. Korisnik testira na telefonu:
- nišanjenje, fine aim, povlačenje (uključujući starta nisko na traci);
- release bar levo/desno;
- ball in hand;
- da li se sve rupe vide posle udarca.

Čekaj potvrdu ili izmene pre commita 5.

### Commit 5 — `rules clarity: foul and ball-in-hand banners, rules popup`
**Obim:** G5 (pravila), `RulesPopup`, `RulesSeen`, Pause → RULES.
**Verifikacija:**
- svaki `FoulType` forsiran → screenshot;
- `RulesPopup` iz pauze i iz `?`;
- prvi meč automatski pokaže pravila, drugi ne (flag).

### Commit 6 — `table art: pockets, cushions, rails, materials, lighting, room, venue theme`
**Obim:** G3 (uključujući `VenueTheme` + `Club.asset`, Poly Haven, reflection probe, blob senke).
**Verifikacija:**
- svi `PhysicsChecks` (geometrija nepromenjena);
- screenshotovi iz G3, uključujući poređenje sa `c2_pocket_corner.png`;
- broj draw call-ova i trouglova iz Frame Debugger-a / `UnityStats` kroz `eval` (prijavi).

### Commit 7 — `juice: pocket drop and glow, ball flyer, billiard audio, release stamps`
**Obim:** G4 (uključujući `PocketEvent.Velocity`, `OnBallPocketedFx`, `PocketFlyerView`, varijante zvukova, roll loop, kreda partikl, pečati).
**Verifikacija:**
- `PhysicsChecks.Determinism` (posle dodavanja `Velocity`);
- kriterijumi iz G4;
- `CREDITS.md` ažuriran (zvukovi, Poly Haven).

### Commit 8 — `tutorial: lessons, coach marks, contextual hints`
**Obim:** G10.
**Verifikacija:**
- `TutorialChecks.RunThrough()`;
- skip;
- `HOW TO PLAY` iz Settings;
- svaki kontekstualni hint forsiran jednom i ne ponavlja se;
- screenshotovi.

### Commit 9 — `polish & build: performance pass, README, APK`
**Obim:**
- performanse na uređaju (ako je povezan: `adb`, FPS log iz build-a sa `Development` flagom);
- README (kontrole, raspored, eventi 3.2, tuning, novi checks);
- `AndroidBuilder.Build()`, provera `aapt`.

**Verifikacija:** svi checks; APK veličina zabeležena; DoD lista (7).

**→ CHECKPOINT 2:** APK. Korisnik ponovo prolazi svih 9 tačaka feedbacka i potvrđuje.

---

## 7. Definition of Done
- [ ] G0 Nema 2 PLAYERS koda ni UI-ja.
- [ ] G1 Svih 6 rupa u kadru dok se kugle kreću i dok bot nišani (`CameraChecks` 100%).
- [ ] G2 Horizontalni FINE AIM sa natpisom, dugmićima ◀ ▶ i ispravnim smerom.
- [ ] G3 Rupe sa grlom, oblogom, gradijentom i kapama; profilisani cushion-i; PBR materijali; reflection probe; blob senke; unapređena soba; `VenueTheme` + `Club.asset`.
- [ ] G4 Animacija upada sa zadržavanjem u rupi, sjaj rupe, leteća kugla do panela, bilijarski zvuci (ili odobreni placeholderi), roll loop, combo ton, pečati PERFECT/GOOD/MISS, kreda.
- [ ] G5 Ball in hand u perspektivi sa direktnim prevlačenjem; faul i ball in hand banneri sa posledicom; `RulesPopup` (auto prvi put + Pause + `?`).
- [ ] G6 Štap nikad ne seče sto ni kugle (`CueChecks` 0); kamera nikad u geometriji.
- [ ] G7 POWER natpis gore, SOFT/MED/HARD, prilagodljiv opseg, idle hint.
- [ ] G8 Početni nišan nije gotov udarac (`AimChecks` ≥ 99%).
- [ ] G9 Trake putanje sa konturom i chevronima, 3D ghost ball, tangenta bele, stanja ilegalno/scratch.
- [ ] G10 Tutorijal T1–T4 + skip + HOW TO PLAY + kontekstualni hintovi.
- [ ] G11 Horizontalni release bar, ispravan smer, zamrzavanje + namerena/stvarna putanja 1.2 s.
- [ ] Fizika, pravila i balans bota nepromenjeni (svi stari checks prolaze, brojevi prijavljeni).
- [ ] Grep provere slojeva prazne; README i `CREDITS.md` ažurirani.
- [ ] 9 commitova na `main`, pushovano; 2 checkpointa potvrđena od korisnika.

---

## 8. Rizici
| Rizik | Šta uraditi |
|---|---|
| Watch kamera iz kraja stola deluje daleko i „gubi“ first-person | Stojeća visina, sway i gaze prate akciju u granicama fit-a; pokaži screenshot korisniku na checkpointu |
| Profilisan cushion se vizuelno ne poklapa sa fizičkom linijom kontakta | Mesh se gradi iz `TableGeometry` segmenata (isti izvor); screenshot kugle uz cushion, odstupanje < 1 mm |
| Poly Haven teksture prevelike za mobilni | 1K, ASTC kompresija, mipmaps; ako APK raste > 15 MB zbog tekstura, smanji na 512 |
| Transparent trake + bloom = bleštanje na telefonu | Trake bez HDR boja; bloom samo na emisiji iznad praga |
| Zvukovi nisu stigli (P2) | Pitaj korisnika; placeholderi samo uz njegovu potvrdu |
| Dinamička elevacija štapa „skače“ | SmoothDamp, zaključavanje tokom povlačenja, minimalna promena 0.5° pre ažuriranja |
| Ulazna brzina u `PocketEvent` menja determinizam | Samo čitanje postojećeg stanja u trenutku upada; `PhysicsChecks.Determinism` je gate |
| Coach mark preklapa kontrolu koju objašnjava | Rupa u zatamnjenju po `RectTransform` mete + padding; screenshot svakog koraka |
| Horizontalni release bar ne odgovara korisniku | Checkpoint 1; vraćanje na vertikalni sa strelicom, bez paralelne implementacije |
