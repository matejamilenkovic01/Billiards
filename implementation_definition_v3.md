# Implementation Definition v3 — Billiards (gameplay polish + meta progresija)

Ovaj dokument je **jedini ulaz** za agenta koji igru dovodi do verzije za closed test. Čitaj ga ceo pre prvog koraka.
Kad nešto nije pokriveno — **stani i pitaj korisnika**, ne pretpostavljaj.

**Zamenjuje:**
- ostatak `gameplay_redesign_implementation_definition.md` (njegovi commitovi 1–5 su urađeni, vidi §2);
- ceo `meta_design_implementation_definition.md`.

Oba dokumenta ostaju u repou kao istorija. Gde se ovaj dokument i oni razlikuju, **važi ovaj**.

Prateći dokumenti (čitaj, ne menjaj):
- `README.md` — arhitektura, eventi, builderi, checks.
- `implementation_definition.md` — slojevi (4.3), konvencije (4.9), CLI rad (3) i verifikacioni alati (8) **važe i ovde**.
- `release_plan.md` — kontekst proizvoda (hipoteza, metrike, faza 2 sa Firebase/AdMob, koja **nije** deo ovog dokumenta).

Svi tekstovi u igri su na **engleskom**.

---

## 0. Pravila rada za agenta

1. Radiš kroz **12 commitova** (§8). Svaki commit je kompajlabilna, pokretljiva celina. Posle svakog commita uradi `git commit` + `git push` na `main` (remote `origin`). Nikad force push.
2. **Pre svakog commita:**
   - `unity recompile` mora dati 0 grešaka;
   - grep provere slojeva (README „Layer rules“) moraju biti prazne — i komentari se računaju, pa ne pominji imena controllera u `UI/`;
   - svi `Editor/Checks` moraju prolaziti, stari i novi;
   - uradi verifikaciju iz tog commita.

   Nikad ne tvrdiš da nešto radi ako nisi proverio. Ono što ne možeš da proveriš (osećaj na telefonu, haptic, zvuk) napiši eksplicitno.
3. **Posle svakog commita** pošalji kratak izveštaj korisniku **na srpskom**: šta je urađeno, kako je provereno, šta nije provereno, putanje screenshotova i commit hash.
4. **Checkpointi sa korisnikom** su posle commita **4, 8 i 12**:
   - napravi APK (`AndroidBuilder.Build()`) i proveri ga sa `aapt` (package, minSdk 26, VIBRATE);
   - ako je telefon povezan (`adb devices`), instaliraj APK;
   - javi korisniku šta tačno da testira i **čekaj potvrdu** pre nastavka.
5. **Stani i pitaj** ako:
   - Editor nije dostupan CLI-ju;
   - build pada iz neočiglednog razloga;
   - rešenje bi kršilo slojeve;
   - balans ne može da uđe u ciljeve ni posle tuninga podataka;
   - ili nešto nije pokriveno ovim dokumentom.
6. Scene, prefabe i generisane assete menjaš **samo kroz buildere** (`Editor/Builders`), nikad ručno YAML. Svi brojevi idu u ScriptableObject-e (`MatchConfig`, `CueCatalog`, `ArenaCatalog`, `EconomyConfig`, `BotProfile`…), nikad u kod.
   - Kad menjaš podrazumevanu vrednost polja koje već postoji u assetu, ažuriraj **i asset** (kroz `eval` i `SerializedObject`/polje + `SetDirty` + `SaveAssets`), ne samo inicijalizator u kodu.
7. **Screenshotovi** idu u `Screenshots/v3_<commit>_<ime>.png`, portrait 1080×1920 (`GameViewUtil.SetPortrait()`). Folder je u `.gitignore`.
8. Ne diraj ništa van `~/Desktop/UnityProjects/Billiards`, osim `~/Desktop/UnityProjects/_kenney_downloads/` (download folder).

### 0.1 Unity CLI na ovom Mac-u (provereno 2026-10-19/20)
- Ako je Editor zatvoren: `unity open .` iz foldera projekta, pa sačekaj `unity status` → `ready` (manje od minut).
- **`unity command eval` sinhrono često pada** sa „Main thread operation timed out after 5000ms“ kad Editor nije u fokusu.
  - Uvek šalji `unity command eval --detach --format json --code '...'`, pa `unity job wait <id> --format json`.
  - Dok čekaš, na svake ~3 s probudi Editor: `osascript -e 'tell application "System Events" to tell process "Unity" to get name of every window'` (timeout je ok).
  - Isto važi za `unity recompile` i `editor_play`/`editor_stop`.
- `--timeout` kod `unity command` je u **sekundama**.
- `unity recompile` ponekad vrati „Network error … recompile_status“ zbog domain reload-a. Ponovi ga: tada vraća `up_to_date`/`completed`.
- Posle `editor_play` postavi `UnityEngine.Application.runInBackground = true` kroz `eval`, inače Play mode stoji. Posle `editor_stop` vrati na `false`.
- **Screenshot:** `capture_game_view` piše samo pod `Assets/` i ignoriše portrait. Koristi `UnityEngine.ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Screenshots/x.png"))` iz `eval` u Play mode-u.
  - Eval roundtrip traje 2–4 s. Za kratka stanja (banneri, pečati, letenje kugle) zato snimaj odloženo u Unity-ju: `DG.Tweening.DOVirtual.DelayedCall(0.6f, () => ScreenCapture.CaptureScreenshot(...), false);` u istom `eval`-u u kom izazivaš stanje.
- **Merenje vremena između događaja:** u `eval` pretplati lambdu na `GameEvents.*` koja dopisuje `Time.time` u privremeni string, pa ga pročitaj sledećim `eval`-om. Obriši ga posle.
- **Editor profil:** Play testovi menjaju PlayerPrefs (novčići, pobede, `PendingMatch`, flagovi). Pre Play testa zapamti profil, a posle `editor_stop` ga vrati.
- FPS iz nefokusiranog Editora (~12) nije merodavan. FPS meri samo na uređaju.
- `PopupManager` prvi put u meču prikazuje `RulesPopup` dok je `RulesSeen = 0`. Za Play testove postavi `RulesSeen = 1` ili zatvori popup.
- Postojeći debug alati (Editor-only):
  - `MatchController.DebugSetupTable(group, numbers[], positions[], ballInHand)`, `DebugStart`, `DebugSetupWin`, `DebugBotVsBot`;
  - `PlayerShotController.DebugSetAim`, `DebugReleaseAt(bar, pull)`, `DebugSetBarPosition`.

---

## 1. Cilj i obim

Testeri su igrali build posle gameplay commita 1–4 i dali novi feedback (§1.1). Cilj ovog dokumenta:
1. **Udarac** treba da bude jasan, realan i fer. Težina dolazi iz nišanjenja i release-a, a štap i snaga je menjaju predvidljivo.
2. **Sto, sto i soba** treba da izgledaju profesionalno: svetlo, bez treperenja, rupe kao prave rupe.
3. **Progresija** treba da drži igrača prvih 1–7 dana: arene sa ulogom, štapovi sa rejtinzima, energija, vidljiv napredak, AI protivnici po areni.
4. Merna mesta za `release_plan.md` (rewarded mesta, fake door, anketa) treba da imaju smisla u dizajnu, a ne da budu nalepljena.

### 1.1 Feedback → zahtev
| # | Feedback (izvorno, skraćeno) | Zahtev |
|---|---|---|
| F1 | Putanja bele treba da ide do prve prepreke (kugla ili zid — zid „imitira kuglu“); posle toga samo dužina strelice | **V1** aim guide |
| F2 | Putanja da bude finija: tanka linija bele, blaže i sporije strelice, površina posle odbijanja | **V1** |
| F3 | Ciljanje protivničke kugle da se komunicira elegantno, bez jakog X-a; bez teksta ako može | **V1** |
| F4 | Free look dok protivnik igra: pinch zoom, jedan prst kretanje, ne ispod stola; mora biti razumljivo | **V2** kamera |
| F5 | Look na svom potezu: ne mogu da dođem do realnih uglova (npr. iz ugla stola pogled na susednu rupu kraće strane) | **V2** |
| F6 | U Look stanju se preklapaju BACK TO SHOT i tekst „Drag to look. Tap a ball to aim.“ | **V2** |
| F7 | Vrh štapa (obojeni deo) se ne vidi i izgleda udubljeno | **V3** štap |
| F8 | Pogođene kugle imaju nerealno veliku brzinu u odnosu na belu | **V4** fizika (klizanje → kotrljanje) |
| F9 | Power: traka ode do dna, a štap samo do pola; oznake SOFT/MED/HARD zbunjuju | **V5** power kontrola |
| F10 | Rupe: zelena čoja se preklapa sa rupom, rupa izgleda kao crni valjak | **V6** sto i rupe |
| F11 | Ambijent previše mračan; detalji u pozadini trepere; svetlo stola bledo; kugle i sto da izgledaju profesionalnije; smanji mrdanje naslova na meniju | **V6**, **V7** |
| F12 | Težina release-a po štapu i snazi (najgori štap + lak udarac = lako … najbolji štap + najteži udarac = srednje); težinu određuju nišanjenje + release (brzina kursora, veličina zelene/žute) | **V8** model težine |
| F13 | Tutorijal koji lako objašnjava udarac, od lakših ka srednjim | **V9** |
| F14 | Arene i štapovi: progresija za dane 1, 3, 7; štapovi sa rejtinzima i OVR; energija; botovi po ciljnom win rate-u; uvek blizu sledećeg otključavanja i jasno prikazano; na areni se igra u ulog te arene (nije tura) | **V10–V12** meta |
| F15 | Bela slova na dugmadima se slabo vide | **V13** kontrast |
| — | iz gameplay dokumenta (nije urađeno): sto/soba/materijali (G3), juice (G4), tutorijal i hintovi (G10), performanse i build | **V6, V7, V9, V14** |
| — | iz meta dokumenta: rewarded, fake door, anketa — u novom dizajnu | **V12** |

### 1.2 Odluke korisnika (2026-10-20)
| Tema | Odluka |
|---|---|
| Fizika | Pravi model **klizanje → kotrljanje** za sve kugle (V4). Bitno je da bude urađeno dobro. |
| Power | Štap u traci prati prst **1:1**, hvata se **uvek za vrh**, a opseg je cela traka. SOFT/MED/HARD stoje na fiksnim mestima (V5). |
| Free look | **Orbit** kamera: 1 prst kruženje i nagib, pinch zoom, 2 prsta pomeranje tačke; nikad ispod rail-a. Isti sistem i za LOOK. Kad bot udari, korisnik ostaje u svom pogledu. Mora se brzo i jasno objasniti (V2). |
| Aim guide | Po predlogu V1. Dužina posle odbijanja bele od zida = dužina linije pogođene kugle za taj štap. |
| Ilegalna meta | Prigušeno crvenkasto, bez X-a; bez teksta ako se namera vidi bez njega (V1). |
| Treperenje | To je treperenje tekstura, ne kamera. Smanji i mrdanje naslova na meniju (V7). |
| Svetlo | Rasveta kao na **TV turniru** (jaka, ravnomerna, neutralno-topla), soba umereno osvetljena (V6). |
| Težina release-a | Po predlogu V8: snaga utiče na zonu i brzinu bara, uživo dok povlačiš. Isti model važi za bota. **Dužina i ugao udarca ne utiču na bar** — nišanjenje je već teže samo po sebi. |
| Energija | Samo za bolje štapove; osnovni štap je bez energije (V11). |
| Progresija | Agent odlučuje detalje. **Arene su ulozi, ne tura:** igra se za ulog arene i ne prelazi se pobedom određenog protivnika (V10). |
| Težina protivnika | **Fiksna po areni** (V10). |
| Zvukovi | Kenney placeholderi; mapiranje je spremno za zamenu pravim bilijarskim zvucima (V7). |
| Rewarded i fake door | Zadržavaju se i moraju imati smisla u dizajnu: mesto za reklamu ima pravu vrednost, a sadržaj Starter Pack-a se uklapa u progresiju (V12). |

### 1.3 Van obima
- Firebase, Crashlytics, Remote Config, AdMob, pravi IAP i In-App Review — faza 2 iz `release_plan.md`. Ovde se prave samo mesta i eventi za njih.
- PvP, spin/english, dnevne nagrade, misije, XP/leveli, leaderboard, battle pass, iOS.
- Landscape i tableti (Android 16 ignoriše portrait lock na ≥ 600dp — rešava se u Play Console-u, faza 2).

---

## 2. Polazno stanje (urađeno, ne radi ponovo)

| Commit | Šta postoji |
|---|---|
| `982f7fe` | 2 PLAYERS mod uklonjen. |
| `a3a6571` | `DefaultAim` (neutralan početni nišan); `GuideBandRenderer` + `GuideBand`/`GhostBall` shaderi (trake 2R, chevroni, 3D ghost); `CueClearance` (dinamička elevacija štapa do 50°, K-66 profil cushion-a); `AimChecks`, `CueChecks`. |
| `7a7c9f1` | `CameraMode.Watch` (sve 6 rupa u kadru), perspektivni ball in hand (`Place`, pitch 50–60°, prevlačenje bele sa hover-om); `CameraChecks`; CameraRig primenjuje turn/phase evente u `LateUpdate`. |
| `84d928f` | Raspored 3.1 (power desno, LOOK/MOVE BALL, fine aim/release bar dole); `PowerStripView` sa prilagodljivim opsegom (V5 ga menja); horizontalni fine aim točkić + ◀ ▶; horizontalni release bar sa zamrzavanjem i prikazom namerene/stvarne putanje; `ControlChecks`. |
| `e568c9e` | Faul/ball-in-hand/grupa banneri (`FoulInfo`, `?` → pravilo), `RulesPopup` (auto pre prvog meča, Pause → RULES), toastovi posle poteza. |

**Checkpoint 1 gameplay dokumenta je korisnik potvrdio na telefonu** (build `84d928f`). Feedback u §1.1 je iz tog i kasnijeg testiranja.

**Postojeće provere** (moraju i dalje prolaziti, ili se svesno rekalibrišu kad V4/V8 to traže):
- `PhysicsChecks.All`, `RulesChecks.All`, `PredictorChecks.MatchesSimulation`, `BotBalanceCheck.Run(200)`, `EconomyChecks.All`;
- `AimChecks.DefaultAimNotAPot(500)`, `CueChecks.NoIntersection(500)`, `CameraChecks.All`, `ControlChecks.PullRangeReachable`.

**Baseline brojevi** (pre V4/V8):
- BotBalance: dobar 84.0% / loš 20.5% / isti kao bot 49.5%;
- break: umirenje 5.2–7.5 s;
- 30% udarac: bela pređe 1.47 dužine stola;
- pun pogodak: pogođena kugla dobija 97% brzine bele (F8).

---

## 3. Gameplay zahtevi

Za svaki zahtev: **Problem → Rešenje → Prihvatni kriterijumi**.

### V1 — Aim guide: do prve prepreke, pa dužina strelice
**Problem:** traka bele širine 2R sa brzim chevronima je „glasna“. Posle odbijanja putanja ide predaleko. Ilegalna meta ima agresivan crveni X.

**Rešenje — pravilo dužine (jedno pravilo za sve):**
- **Bela** se crta od svoje prednje ivice **do prve prepreke**: ghost lopte (ako je prvo kugla), tačke udara u zid (ako je prvo zid) ili ulaza u rupu.
- Posle prve prepreke crta se **samo dužina strelice štapa** (`CueDefinition.ObjectGuideDiameters × 2R`):
  - pogođena kugla: od svoje dalje ivice u pravcu `ObjectDir`;
  - bela posle udarca u kuglu: po stvarnoj putanji posle kontakta (V4: zakrivljena zbog follow-a), ista dužina;
  - bela posle zida: od tačke udara u odbijenom pravcu, ista dužina.
- Nema drugog odbijanja ni druge kugle posle zida. Ako se na putu posle zida nalazi kugla, putanja staje na njoj (ghost lopta se ne crta).

**Rešenje — izgled:**
- **Putanja bele do prepreke:** tanka linija, ~4 mm, bela 85% sa tamnom senkom 1.5 mm (čitljiva i preko bele kugle). Bez trake i bez chevrona. Posle prepreke se 4 mm ne prikazuje.
- **Ghost lopta:** ostaje kao sada (3D, 28% telo + fresnel obod).
- **„Dužina strelice“ (pogođena kugla, bela posle kontakta, bela posle zida):** meka traka širine 2R:
  - fill 20–30%, kontura 1 mm na 35%;
  - na kraju zaobljen vrh strelice;
  - 2–3 mekana chevrona (alpha ~45%, mekane ivice) koji teku **0.12 m/s** (bilo 0.35).
  - Boje: pogođena kugla zlatna `#FFD54A`; bela posle kontakta/zida bela.
- **Tangenta bele** se više ne crta kao zasebna isprekidana linija; to sada radi bela traka posle kontakta.
- **Ilegalna meta (protivnička kugla ili 8 pre vremena):**
  - linija bele i ghost postaju prigušeno crvenkasti (`#E06A5A`, ghost 22%);
  - ghost blago „pulsira“ jednom (0.4 s) kad nišan prvi put pređe na ilegalnu kuglu;
  - nema trake pogođene kugle;
  - **bez X-a i bez teksta.** Postojeći toast-hint „Hit your own group first“ ostaje samo prvi put u meču (već tako radi).
- **Bela u rupu (scratch):** linija i rub te rupe narandžasti (postojeće), bez trake posle.
- Postojeći x-ray (vidljivo kroz kugle) ostaje za tanku liniju na 35%, za trake na 25%.

**Tehnika:**
- Proširi `GuideBandRenderer` (već bez alokacija) za meke trake.
- Tanku liniju crtaj kao traku širine 4 mm sa svojim profilom (`guide_line_profile.png`) — isti shader, druga tekstura/materijal. Ne vraćaj se na LineRenderer.
- Zaobljen vrh: generisana tekstura vrha (`guide_tip.png`) na zasebnom quadu.

**Kriterijumi:**
- `PredictorChecks.MatchesSimulation()` prolazi (proširen za V4, vidi tamo).
- Novi `GuideChecks.LengthRule()`: 200 nasumičnih nišana. Za svaki proveri:
  - (a) linija bele se završava na prvoj prepreci (±1 mm);
  - (b) svaka traka posle prepreke ima dužinu `ObjectGuideDiameters × 2R` (±1 mm);
  - (c) posle zida postoji tačno jedan segment.
- 0 B GC po frejmu u Aim fazi (merenje iz commita 2 gameplay dokumenta: 120 poziva `Update`, `GC.GetAllocatedBytesForCurrentThread`).
- Screenshotovi: `v3_3_guide_ball.png`, `v3_3_guide_cushion.png`, `v3_3_guide_illegal.png`, `v3_3_guide_scratch.png`, `v3_3_guide_close_white.png` (Aim-low, bela u prvom planu).

### V2 — Free look i Look (jedna orbit kamera)
**Problem:**
- Dok bot igra, korisnik ne može da gleda gde želi.
- Look na svom potezu kruži po fiksnom prstenu i ne dozvoljava realne uglove.
- U Look stanju se dugme i tekst preklapaju.

**Rešenje — `CameraMode.Orbit`, zajednički za oba slučaja:**
- **Pivot** je tačka na ravni čoje. Kamera je na sfernim koordinatama oko pivota (yaw, pitch, distance).
- **Gestovi** (prepoznaje ih `TableTouchSurface`, šalje UI → gameplay evente sa delta vrednostima):
  - **1 prst drag:** horizontalno = yaw, vertikalno = pitch;
  - **2 prsta pinch:** distance (zoom);
  - **2 prsta drag (pan):** pomera pivot po čoji.
  - Miš u Editoru: levi drag = orbit, točkić = zoom, desni drag = pan.
- **Granice** (svi brojevi u `MatchConfig`):
  - pivot: unutar stola + 0.3 m;
  - pitch: 12°–85° (85° je skoro odozgo; 12° je pogled preko rail-a, npr. iz ugla na susednu rupu kraće strane);
  - distance: 0.35–4.0 m;
  - **visina kamere ≥ `RailTop + 0.05` iznad čoje** — kamera se nikad ne spušta ispod rail-a.
  - Unutar okvira stola kamera ne sme da uđe u telo stola: ako je horizontalno iznad stola, visina ≥ visina kugle + 0.10 m.
  - Granice se primenjuju posle svakog gesta, glatko (SmoothDamp 0.08 s), bez skoka.
- **Inercija:** posle puštanja 1-prst orbite kratko klizanje (decay 0.25 s), da deluje prirodno.
- **Na svom potezu (LOOK):**
  - ulaz: dugme LOOK, pivot = trenutna meta (ghost) ili bela, početna poza = trenutna Aim poza;
  - tap na kuglu = nišan ka njoj + povratak u Aim (postojeće ponašanje);
  - BACK TO SHOT vraća u Aim.
- **Tokom poteza bota (free look):**
  - default ostaje Watch (svih 6 rupa);
  - prvi drag ili pinch prebacuje u Orbit (pivot = centar stola, početna poza = trenutna Watch poza);
  - kad bot udari, korisnik **ostaje** u svom pogledu;
  - povratak u Watch: čip **„↺ TABLE VIEW“** gore levo ispod panela ili dvostruki tap;
  - na početku svakog sledećeg poteza kamera se vraća normalno (igrač → Aim, bot → Watch).
- **Kako se objašnjava (F4 — brzo i jasno):**
  - Prvi put kad je bot na potezu (`HintService`, `HintsShown` bit `FreeLook`), coach mark preko celog ekrana bez zatamnjenja stola, ~3 s ili do prvog dodira:
    - animirana ghost ruka pokaže 1-prst kružni drag pa 2-prsta pinch;
    - balon: **„Drag to look around · Pinch to zoom“**.
  - Isti coach mark se prikaže i prvi put kad igrač pritisne LOOK.
  - Kad je free look aktivan, uvek je vidljiv mali čip sa ikonama ✋ i 🤏 (bez teksta) + „↺ TABLE VIEW“.
- **Look UI (F6):**
  - BACK TO SHOT je dugme dole levo (mesto LOOK-a);
  - hint tekst „Drag to look · Pinch to zoom · Tap a ball to aim“ je jedan red **iznad** reda dugmadi, sa sopstvenim prostorom;
  - ne sme da se preklapa ni sa power kolonom ni sa fine aim redom.

**Kriterijumi:**
- Novi `CameraChecks.OrbitBounds()`: 1000 nasumičnih sekvenci gestova (yaw, pitch, zoom, pan, uključujući ekstremne). Posle svake proveri:
  - kamera nije ispod `RailTop + 0.05`;
  - nije unutar zapremine stola ni unutar kugle;
  - pivot je u granicama.
  - Rezultat: 0 prekršaja.
- `eval` sekvenca „iz ugla stola na susednu rupu kraće strane“ (pivot kod rupe 1, yaw duž kraće strane, pitch 15°) → rupe 0 i 1 su obe u kadru → screenshot `v3_4_look_corner.png`.
- Screenshotovi: `v3_4_freelook_bot.png` (zoom tokom poteza bota), `v3_4_look_ui.png` (bez preklapanja), `v3_4_freelook_coach.png`.
- Tokom poteza bota free look ne menja ništa u toku meča (`BotBalanceCheck` nepromenjen; potez bota se odvija isto).

### V3 — Štap: vrh koji se vidi
**Problem:** obojeni vrh (koža + ferula) se ne vidi i deluje udubljeno. Verovatni uzroci, proveri sva tri:
- kapa vrha u `TablePrefabBuilder.FillCueMesh`/`AddCap` je ravna i/ili sa obrnutom normalom;
- u albedo stripu boja vrha zauzima premalo `u`;
- ferula je iste boje kao drvo.

**Rešenje:**
- **Ferula:** ~25 mm bela-slonovača (smoothness 0.6), odmah iza vrha.
- **Vrh:** ~12 mm, plavo-sivkasta koža sa kredom (`#4A6FA5`, matte), **ispupčena kupola** (dome, 3–4 prstena lathe-a), normale ka spolja.
- Albedo strip po štapu (`GeneratedArtBuilder.CueAlbedo`): vrh i ferula na kraju `u`, tačno po dužinama.
- Provera da kapa ima pravilne normale i winding (renderuj close-up iz 3 ugla).

**Kriterijum:** screenshot `v3_3_cue_tip.png` (Aim-low, vrh 15 cm od kamere): ferula i plavi vrh jasno vidljivi, ispupčeni.

### V4 — Fizika: klizanje → kotrljanje (F8)
**Problem:** sudar je tačan (elastičan, restitucija 0.95), ali nema faze klizanja. U stvarnosti:
- pogođena kugla posle sudara **klizi**, pa trenje čoje za par cm prevede je u kotrljanje sa **5/7** početne brzine;
- bela pri centralnom udarcu sa veće daljine stigne do kontakta **kotrljajući se**, pa posle punog pogotka nastavi napred sa **~2/7** brzine (prirodni follow);
- izbliza još klizi, pa stane (stun).

Bez ovoga pogođene kugle deluju prebrzo i nerealno.

**Rešenje (`Pool/PoolPhysics`, i dalje čist C#, fiksni korak 1 ms, deterministički):**
- `BallState` dobija `Vector2 Roll` — „brzinu kotrljanja“ u m/s: `R·ω` projektovano na ravan, pa je kotrljanje bez klizanja `Roll == Vel`. Bočni spin (oko vertikale) se ne modeluje.
- **Klizanje:** slip `u = Vel − Roll`. Dok je `|u| > ε`:
  - `Vel -= μs·g·û·dt`;
  - `Roll += (5/2)·μs·g·û·dt`;
  - slip opada brzinom `7/2·μs·g`.

  Kad `|u| ≤ ε`, postavi `Roll = Vel` (kotrljanje).
- **Kotrljanje:** postojeće usporavanje (`RollDecel` + `LinearDamping·|v|`) primenjuje se na `Vel`, a `Roll = Vel`.
- **Udarac štapa** (centar bele, nema spina): `Vel = v0·dir`, `Roll = 0` → bela kreće klizeći.
- **Kugla–kugla:** normalna komponenta `Vel` se razmenjuje kao sada; `Roll` obe kugle se **ne menja** (sudar bez trenja između kugli). Posledica: pogođena kugla klizi pa se kotrlja na 5/7; bela koja se kotrljala dobije follow.
- **Kugla–cushion:** `Vel` kao sada (refleksija normale sa restitucijom, trenje tangente). Komponenta `Roll` po normali postaje 0, pa kugla kratko klizi od cushion-a.
- **Novi brojevi u `MatchConfig`:** `SlideFriction` (μs, početno 0.2), `RollSlipEpsilon` (0.005 m/s). Postojeći `RollDecel`/`LinearDamping` se rekalibrišu da `Timings` ostane u ciljevima.
- **Prikaz rotacije** (`BallView`): ugao kotrljanja ide iz `Roll`, a ne iz `Vel`. Kugla koja klizi vidljivo „kliže“ bez rotacije — to je realno i dobro izgleda.
- **`ShotPredictor`:**
  - pre prvog kontakta putanja ostaje prava (bela kreće sa `Roll = 0`, slip je paralelan brzini);
  - stanje bele u trenutku kontakta (`Vel`, `Roll`) računa se analitički po daljini (klizanje do prelaska u kotrljanje, pa kotrljanje);
  - **putanja bele posle kontakta** je parabola dok klizi (konstantno ubrzanje u pravcu `−û`), pa prava. `Prediction` dobija niz od najviše 8 tačaka za crtanje (prealociran) do dužine strelice (V1).
- **`BotBrain`:** formula potrebne brzine (`SpeedToCover`) uzima u obzir fazu klizanja — gubitak 2/7 brzine pogođene kugle. Odluke i težine kandidata se ne menjaju, samo procena snage. **Pravila ostaju ista.**
- `PhysicsChecks.Determinism` je gate: isti ulaz → bit-identičan izlaz.

**Kriterijumi** (proširen `PhysicsChecks.Collisions`, novi `PhysicsChecks.SlideRoll`):

| Scenario | Cilj |
|---|---|
| Pun pogodak izbliza (bela 0.15 m od mete, 2 m/s) | bela posle sudara < 0.1 m/s i pređe < 5 cm (stun); meta odmah posle sudara 93–97% brzine bele, posle 0.3 m kotrlja se sa 66–73% |
| Pun pogodak izdaleka (bela 1.2 m od mete) | bela stigne kotrljajući se; posle sudara nastavi napred i pređe 15–40% puta mete (follow) |
| Rez 30° izdaleka | bela posle kontakta skrene od tangente ka napred (zakrivljena putanja), poklapa se sa predikcijom ±2 mm na prvih 0.3 m |
| Kugla posle udarca štapa | klizi prvih 5–25 cm zavisno od snage, pa se kotrlja; prelaz bez skoka u brzini |
| `Timings` | break 100% → umirenje 5–9 s; 30% udarac → bela pređe 1–1.6 dužine stola |
| `StraightPot` | 50/50 zelenih straight-in udaraca i dalje ubacuje |
| `NoTunneling(1000)` | 0 prekršaja |
| `PredictorChecks.MatchesSimulation` | ghost ±1 mm, `ObjectDir` ±0.5°, **nova** putanja bele posle kontakta ±2 mm do dužine strelice |

- `BotBalanceCheck.Run(200)` posle V4 + V8 mora biti u starim opsezima (dobar ≥ 70%, loš ≤ 30%, isti ~50 ± 8%). Ako nije, tuning **samo podacima** (`BotProfile`), pa prijavi pre/posle.
- Kratak video ili sekvenca screenshotova pre/posle (`v3_1_slide_*.png`) za checkpoint, da korisnik vidi razliku.

### V5 — Power kontrola: štap prati prst 1:1 (F9)
**Problem:** prilagodljiv opseg (prst ide do dna trake, a štap u traci najviše 300 px) plus fiksne oznake ne govore istu stvar.

**Rešenje:**
- Traka (desna kolona, raspored iz `84d928f`) prikazuje štap sa **ručkom na vrhu** — zaobljen „grip“ prsten, jasno vidljiv, sa malim sjajem.
- **Hvata se uvek ručka:**
  - hit area je gornjih ~40% kolone (veliko, lako za pogoditi);
  - dodir bilo gde u toj zoni = uhvaćena ručka;
  - dodir niže u koloni ne povlači ništa — samo kratko zasvetli ručka i krene ghost-ruka hint („ovde se hvata“).
- Štap u traci se pomera **tačno koliko i prst** (1 ref px = 1 ref px). `pull01 = clamp01(dragDown / PullTravelPixels)`.
  - `PullTravelPixels` = dužina od ručke u mirovanju do dna trake minus 20 px (≈ 900 ref px; tačna vrednost iz rasporeda, u `MatchConfig`).
  - Na 100% ručka je na dnu trake.
- **SOFT / MED / HARD** su fiksne oznake na traci, poravnate sa pozicijom ručke za 15% / 50% / 85% snage. Uz ručku se kreće mala strelica „pokazivač“, pa igrač vidi „sad sam na MED“.
- Fill (belo → toplo narandžasto) ide od vrha do ručke.
- POWER natpis i ↓ ostaju gore. Ghost-ruka hint ostaje sa istim pravilom (prva 3 udarca + 8 s neaktivnosti), ali animira hvatanje **ručke** i povlačenje do MED.
- Otkazivanje: vraćanje ručke iznad 5% i puštanje (postojeće).
- `ControlChecks.PullRangeReachable` se zamenjuje sa `ControlChecks.PullMapping()`:
  - (a) iz bilo koje tačke hit area, prst ima dovoljno ekrana ispod za 100%;
  - (b) mapiranje je 1:1 (drag X px → ručka X px);
  - (c) oznake su na 15/50/85%.

**Kriterijum:** `eval` sa pravim `PointerEventData`:
- dodir na vrhu zone pa drag 50% `PullTravelPixels` → `pull01 = 0.50`, a ručka je pomerena tačno toliko;
- dodir u donjem delu kolone → nema `OnPullChanged`.

Screenshotovi `v3_3_power_idle.png`, `v3_3_power_med.png`, `v3_3_power_full.png`.

### V6 — Sto, rupe, svetlo (F10, F11; preuzima G3 gameplay dokumenta)
**Problem:** rupe su crni valjci sa čojom preko otvora; cushion je spojen sa rail-om stepenikom od ~2 cm; materijali su ravni; svetlo je bledo i mračno.

**Rešenje — rupe** (`TablePrefabBuilder`):
1. **Otvor:**
   - čoja se **završava tačno na ivici rupe**, bez preklapanja. Proveri notch u `FeltMesh` (radijus = `HoleRadius`) i ukloni sve što viri preko otvora (jaw-ovi, cushion, rail iseci);
   - čoja se savija preko ivice u rupu (zaobljen spoj ~8 mm, 6–8 segmenata).
2. **Obloga (liner):**
   - kožni levak koji se sužava (vrh = rupa, dno ~85% radijusa), dubine `PocketDepth`, sa zaobljenim dnom;
   - vertikalni gradijent kroz vertex boje (gore tamno-smeđe, dole skoro crno), smoothness 0.3;
   - **nema crnog valjka:** zid je vidljiv kao koža, sa senkom.
3. **Kape rupa:** zaobljene metalne kape preko rail-a oko otvora, materijal iz `VenueTheme.CapMaterial`.
4. Ubačena kugla ostaje vidljiva na dnu rupe do kraja poteza (V7).

**Rešenje — sto:**
1. **Cushion i rail:**
   - K-66 profil već postoji (nos 36 mm → 45 mm);
   - **spusti `RailTop` na ~46 mm** i dodaj zaobljenu/skošenu gornju ivicu rail-a (chamfer), da se cushion i rail spoje bez stepenika;
   - `CueChecks` i `CameraChecks` moraju i dalje proći (`CueClearance` čita iste brojeve iz `MatchConfig`).
   - Cushion je presvučen istom čojom (~8% tamniji).
   - Fizička geometrija (`TableGeometry`) se ne menja: nos cushion-a vizuelno mora biti tačno na liniji kontakta.
2. **Dijamanti:** ovalni sedefasti inlay-i, blago emisivni.
3. **Materijali (PBR, URP Lit):**
   - drvo, čoja i koža iz **Poly Haven** (CC0, 1K, albedo + normal + roughness);
   - download u `_kenney_downloads/polyhaven/<ime>/`, kopiraj samo korišćeno u `Assets/_Art/PolyHaven/<ime>/`, upiši u `CREDITS.md`;
   - ako download ne radi → zadrži generisane teksture i javi.
4. **Kugle:** smoothness 0.9; **reflection probe** iznad stola (baked u builderu, 128 px); **blob kontaktna senka** ispod svake kugle (~2.2R, alpha 0.5, nestaje pri upadu).

**Rešenje — svetlo (TV-turnir stil, odluka korisnika):**
- **Sto:** pravougaona „lampa“ iznad stola (vidljiva u Watch/Orbit pogledu) sa emisivnim panelom.
  - Glavno svetlo je jedan spot ili area-like set: jak, **ravnomeran** po celoj čoji (osvetljenost ivica stola ≥ 85% centra), neutralno-topao (~4500 K).
  - Jedino svetlo sa senkom; meke senke.
- **Soba:** umereno osvetljena (ambient + emisija + light probe), jasno vidljiva, ali tamnija od stola (stol ~2× svetliji). Nema „crnog ponora“.
- **Post:** vinjeta slabija (0.38 → ~0.2), bloom samo na emisiji iznad praga; tone mapping Neutral ili ACES (izaberi screenshotom); exposure tako da bela kugla nije izgorela.

**Rešenje — treperenje tekstura (F11):**
- Uzrok je visokofrekventni detalj daleko od kamere (drvo, tapet, tepih, police, ivice dijamanata).
- Sve world teksture: mipmaps uključene, trilinear, aniso 4–8; detail i normal intenzitet smanjen na daljini.
- MSAA 4× na URP asset-u za Android, ako FPS dozvoli (meri na uređaju, inače 2×).
- Tanke svetle geometrije (police, letvice) bez oštrih kontrastnih šara.
- Proveri sa 3 screenshota iste poze u razmaku od 0.1 s uz uključen head sway: razlika piksela u pozadini mora biti mala. Prijavi max razliku pre/posle.

**Kriterijumi:**
- Svi `PhysicsChecks` (geometrija nepromenjena), `CueChecks`, `CameraChecks`.
- Screenshotovi:
  - `v3_5_pocket_corner_close.png` i `v3_5_pocket_side_close.png` (uporedi sa `c2_pocket_corner.png`);
  - `v3_5_ball_on_cushion.png` (nos na kugli, < 1 mm);
  - `v3_5_aim_low.png`, `v3_5_watch.png`, `v3_5_room.png`.
- **Merenje osvetljenosti:** prosečan luma čoje u Watch pogledu ≥ 0.45, sobe 0.15–0.3 (prijavi brojeve sa screenshota).
- Performanse (budžet, meri na uređaju u commitu 12): ≤ 150 draw calls i ≤ 150k trouglova u Aim pogledu; 60 FPS na S23; ≥ 30 stabilnih na starom Note-u. Ako ne prolazi: bloom off → rezolucija senke → MSAA 2× → pojednostavi sobu.

### V7 — Juice, zvuk, meni (preuzima G4; F11 deo)
Preuzima se **G4 iz gameplay dokumenta u celosti**. Kratko:
- **Upad u rupu:** animacija sa padom i odskokom, kugla ostaje na dnu do kraja poteza; `PocketEvent` dobija `Velocity` (samo beleženje).
- **Sjaj ruba rupe** (zeleno legalno / crveno scratch ili rano 8 / belo protivnikova), haptic po vrsti.
- **Leteća kugla do panela:** `OnBallPocketedFx(number, screenPos, PocketFxKind)` → `PocketFlyerView`, slot se zasivi uz punch.
- **Zvuk:**
  - `Sound` unos sa `AudioClip[] Clips`, bez ponavljanja prethodnog;
  - id-jevi `Break`, `CueHitSoft`, `CueHitHard`, `PocketDrop`, `PocketRattle`, `BallRollLoop`, `RoomAmbience`;
  - combo ton +1 polustepen po kugli u potezu (do +4);
  - roll loop po Σ|v|.
  - **Placeholderi:** Kenney impact zvukovi, sa pitch/volumen varijacijom da liče na bilijar (klik = kraći, viši; cushion = tup, niži; pocket = drvo, niže).
  - Mapiranje u `SystemsPrefabBuilder` je tabela `SoundId → lista fajlova`. Kad korisnik ubaci prave fajlove (`_kenney_downloads/billiard_sfx/`, imena iz gameplay dok. P2), menja se samo tabela. `CREDITS.md` upiše izvore.
- **Pečati umesto toastova** PERFECT / GOOD / MISS iznad bele, kreda partikl na PERFECT, bar blesne.
- **Meni (F11):** amplituda bob-a naslova „BILLIARDS“ se smanjuje (`IdleMotion` 16 → ~5 px, period ~2 s).

**Kriterijumi:** iz G4 (`eval` „2 kugle u istom potezu“ → 2 `OnBallPocketedFx` sa ispravnim `kind`, panel −2, kugle vidljive u rupi do kraja poteza). `PhysicsChecks.Determinism` posle `Velocity`. Screenshotovi `v3_6_pocket_glow.png`, `v3_6_flyer.png`, `v3_6_perfect_stamp.png`. Zvuk i haptic potvrđuje korisnik.

### V8 — Težina udarca: štap × snaga (F12)
**Princip:** težinu udarca određuju dve stvari:
1. **Postavljanje pravca** — prirodno je teže za dug i rezan udarac (veća greška ugla daje veći promašaj). Bar se zbog toga **ne menja**.
2. **Release** — veličina zelene/žute zone i brzina kursora, koje zavise od **štapa** i **snage**.

**Model** (`Pool/ShotAccuracy`, menja se):
- Štap (`CueDefinition`) daje bazu: `GreenHalf`, `YellowWidth`, `Period`.
- Snaga `p ∈ [0,1]` množi bazu (`MatchConfig`, krive kao 2 vrednosti + lerp):

  | Parametar | p = 0 (najmekše) | p = 1 (najjače) |
  |---|---|---|
  | `GreenScale` (širina zelene) | 1.6 | 0.55 |
  | `YellowScale` (širina žute) | 1.2 | 0.85 |
  | `PeriodScale` (period bara; veći = sporije) | 1.35 | 0.72 |
  | `ErrorScale` (stepeni greške žuto/crveno) | 0.7 | 1.35 |

- Pull ispod praga (5%) nema bar, kao sada.
- **Bar se menja uživo dok povlačiš:** zone se sužavaju i kursor ubrzava kako snaga raste.
  - `OnReleaseMeterChanged` dobija i trenutne `greenHalf`, `yellowEnd`, `period`. `ReleaseBarView` ih glatko animira (0.1 s).
  - Igrač vidi i oseća da je jak udarac teži.
- Kursor se kreće po pozicionoj fazi, a ne po vremenu: kad se period promeni dok povlačiš, pozicija ne sme da skoči (akumuliraj fazu: `phase += dt / period(p)`).
- **Bot koristi isti model.** Umesto fiksnih šansi Green/Yellow/Red, bot ima **timing skill** `TimingSigmaMs` (vidi V10).
  - Release bota se simulira kao vremenska greška `N(0, σ)` oko centra zelene, pri prozorima za njegov štap i izabranu snagu.
  - Šta to znači za balans: bot koji mora jako da udara greši češće, kao i čovek.
  - `BotBrain.Release` se menja tako; ostatak `BotBrain`-a ostaje isti.
- **Simulirani igrači** u balans proverama (`BotBalanceCheck`, `ArenaBalanceCheck`) takođe se modeluju preko σ: dobar 30 ms, prosečan 45 ms, slab 65 ms.

**Ciljevi** — verovatnoća zelenog / zeleno+žuto za igrača σ = 45 ms. Ovo je provereni proračun modela gore, sa štapovima iz V11; tolerancija ±5 p.p.:

| Štap | p = 0.2 | p = 0.5 | p = 0.8 | p = 1.0 |
|---|---|---|---|---|
| House (najgori) | 78 / 100 | 58 / 100 | 36 / 97 | 23 / 89 |
| Club | 89 / 100 | 70 / 100 | 45 / 98 | 29 / 93 |
| Pro | 96 / 100 | 82 / 100 | 56 / 99 | 37 / 96 |
| Master | 99 / 100 | 91 / 100 | 67 / 100 | 45 / 98 |
| Champion | 100 / 100 | 96 / 100 | 77 / 100 | 54 / 99 |
| Legend (najbolji) | 100 / 100 | 99 / 100 | 85 / 100 | 62 / 99 |

Ovo daje tačno koncept korisnika:
- najgori štap + lak udarac → lako;
- najgori + srednji → teško;
- najgori + najjači → najteže;
- najbolji + najjači → srednje.

**Kriterijumi:**
- Nov `ShotDifficultyCheck.ReleaseTable()`: analitički i Monte Carlo (20k uzoraka po ćeliji) → tabela iznad ±5 p.p.
- Nov `ShotDifficultyCheck.PotRates()`: 3 referentna udarca × 6 štapova × 4 snage × 2000 uzoraka kroz pravu fiziku (V4), igrač σ = 45 ms + mali šum nišana (σ 0.15°). Prijavi stopu ubacivanja.
  - Referentni udarci: lak (straight-in 0.5 m), srednji (rez 30°, 1 m), težak (rez 45°, 1.8 m).
  - Uslov: monotono opada sa snagom i raste sa štapom.
  - Za lak udarac sa House štapom na p = 0.2 stopa ubacivanja mora biti ≥ 80%; za težak udarac sa Legend štapom na p = 1.0 između 35% i 60%.
- `eval`: pull od 0.2 do 1.0 tokom povlačenja → `ReleaseBarView` prikazuje sužavanje zone (screenshotovi `v3_2_bar_soft.png`, `v3_2_bar_hard.png`).
- `BotBalanceCheck.Run(200)` u opsezima (V4).

### V9 — Tutorijal i kontekstualni hintovi (preuzima G10, usklađeno sa V2, V5, V8)
Preuzima se **G10 iz gameplay dokumenta** (`TutorialController`, `TutorialConfig`, `CoachMarkView`, `CoachTarget`, `HintService`, `OnTutorialStep`, `OnTutorialCompleted`, skip, HOW TO PLAY u Settings), sa ovim izmenama:

| Korak | Sto | Uči | Pravilo bara | Uspeh |
|---|---|---|---|---|
| T1 Power | bela + 1 kugla ravno, 0.45 m od ugaone rupe | „Grab the cue at the top and drag it DOWN. Further = harder. Let go to shoot.“ Ghost ruka hvata ručku (V5). | bar isključen (uvek zeleno) | ubačena; ako je predaleko ili premalo: „Too soft — pull further“ / „Too hard — try MED“ |
| T2 Aim | rez ~20° | drag po stolu = nišan; „Put the ghost ball where the gold line points into the pocket.“; coach mark na FINE AIM | bar isključen | ubačena; posle 2 promašaja: pravi nišan 1 s |
| T3 Release | isto kao T2, druga strana | „Let go on GREEN: the ball goes exactly where you aimed. Left of green = left, right = right.“ | period ×1.6, zelena +50%, mek udarac (preporučen MED ili manje) | ubačena sa Green ili Yellow; Red → namerena vs stvarna putanja + objašnjenje |
| T4 Power vs control | straight-in na 1.2 m | „Harder shots shrink the green and speed up the bar. Use only the power you need.“ — igrač vidi kako se bar menja dok povlači | normalan bar (V8) | ubačena na ≤ 60% snage, ili 2 pokušaja |
| T5 Ball in hand | scratch situacija, 2 kugle | „BALL IN HAND: drag the cue ball anywhere, then shoot.“ | normalan | ubačena |
| Kraj | — | „You're ready!“ → `RulesPopup` | — | — |

- Sve postavke kugli su u `TutorialConfig`. Brzina i zone bara u T1–T3 su prilagođeni tako da prosečan igrač prolazi iz prvog ili drugog pokušaja.
- **Kontekstualni hintovi u pravom meču** (`HintService`, svaki samo prvi put):
  - prvi faul (banner + `?` već postoji);
  - prvi ball in hand;
  - prva dodela grupe;
  - prvi put samo 8 ostala;
  - posle 5 udaraca bez FINE AIM: coach mark na točkiću;
  - posle 2 uzastopna Red release-a: „Let go on GREEN for a straight shot“;
  - **prvi jak udarac (> 85%) koji je promašio:** „Big power makes the green smaller. Try less.“;
  - **prvi potez bota: free look coach mark (V2);**
  - prvi pritisak na LOOK (V2);
  - prvi put kad štap ostane bez energije (V11).
- **Kriterijumi:** iz G10:
  - `TutorialChecks.RunThrough()` headless kroz debug release → `OnTutorialCompleted(false)`, flag postavljen;
  - skip → `OnTutorialCompleted(true)`;
  - svaki hint forsiran jednom i ne ponavlja se;
  - screenshotovi `v3_10_t1..t5.png`.

---

## 4. Meta zahtevi

### V10 — Arene sa ulogom i AI protivnici
**Princip (odluka korisnika):** arena je **sto sa ulogom**, kao u 8 Ball Pool-u.
- Igrač bira arenu, plaća ulaz i igra protiv AI protivnika te arene. Pobeda donosi nagradu arene.
- Nema „ture“ i nema pobeđivanja određenih protivnika redom.
- **Težina je fiksna po areni.** Protivnik je nasumičan AI lik iz spiska arene (zbog osećaja i raznolikosti), ali svi u areni imaju istu težinu.

**Otključavanje:** sledeća arena se otključava posle **N pobeda u prethodnoj areni**. Napredak je stalno vidljiv („Downtown Hall · 2/3 wins to unlock“). To je jasno, merljivo i uvek „blizu“.

**`ArenaCatalog.asset`** (početne vrednosti; finalne određuje `ProgressionSim` u commitu 12):

| # | Id | Ime | Tema | Ulaz | Nagrada (pobeda) | Otključava se | Očekivani štap | Bot štap | Bot σ (ms) | Protivnici (CPU likovi) |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `basement` | **Basement Bar** | Basement | **0** | 40 | start | House | House | 75 | Rookie Ray, Lucky Lin, Big Sal |
| 2 | `downtown` | **Downtown Hall** | Downtown | 80 | 200 | 3 pobede u areni 1 | Club | Club | 62 | Dina Drift, Marco Mumbles, Hustler Hank |
| 3 | `riverside` | **Riverside Club** | Riverside | 250 | 650 | 5 pobeda u areni 2 | Pro | Pro | 54 | Ivy Bennett, Theo Park, The Duchess |
| 4 | `grand` | **Grand Lounge** | Grand | 700 | 1900 | 6 pobeda u areni 3 | Master | Master | 47 | Viktor Kane, Mei Tanaka, The Professor |
| 5 | `royal` | **Royal Masters** | Royal | 2000 | 5500 | 8 pobeda u areni 4 | Champion | Champion | 42 | Sofia Reyes, Grant Holloway, The Baron |

- **Arena 1 je uvek besplatna.** Nema softlocka.
- **Nagrada ~2.5–2.75× ulaza.** Protiv bota igrač pobeđuje ~60% (vidi ciljeve), pa EV po meču raste sa arenom, i napredak se oseća. Pri 2× ulazu (PvP standard) EV bi bio ~0 i napredak bi bio mučenje.
- Gubitak boli (ulaz), pa pobeda ima težinu.
- Bot σ je početna vrednost. `ArenaBalanceCheck` je kalibriše do ciljeva.
- Ostali bot parametri po areni (u `BotProfile` asset po areni, `_ScriptableObjects/Arenas/Bot_<arenaId>.asset`):
  - `MaxCutAngle` (arena 1: 55°, 2: 65°, 3–5: 75°);
  - `CandidatesToSimulate` (2/3/4/5/5);
  - safety sklonost;
  - tajminzi poteza.
- **Bot štap = očekivani štap arene** — fer, i ide uz V8.
- **Likovi:** ime, portret, `CPU` oznaka i jedna rečenica karaktera. Opisi su iz meta dokumenta §4.2; nova 3 lika za arenu 5 smisli u istom tonu, izmišljena imena.
  - Ličnost je samo prikaz i tajming (npr. „Big Sal: break uvek 100%“, „Dina: sporije misli“).
  - Težina je ista za sve u areni.
- **Ciljevi pobede** (`ArenaBalanceCheck.Run(200)`, igrač sa **očekivanim štapom** arene):

  | Igrač (σ) | Arena 1 | Arena 2 | Arena 3 | Arena 4 | Arena 5 |
  |---|---|---|---|---|---|
  | prosečan (45 ms) | ~62% | ~60% | ~58% | ~56% | ~55% |
  | dobar (30 ms) | ≥ 75% | ≥ 72% | ≥ 70% | ≥ 68% | ≥ 66% |
  | slab (65 ms) | ~48% | ~45% | ~42% | ~40% | ~38% |

  - Tolerancija ±6 p.p.
  - **Jedan štap bolji od očekivanog:** +6 do +10 p.p. Tako se oseti da novi štap pomaže, a ne garantuje.
  - **Jedan štap slabiji:** −6 do −10 p.p.
- **Tempo meča:** potez bota ~6 s (think 2.0 ± 0.5, aim do 3.8, pull do 5.2, strike 5.6), +1.5 s za lik koji „sporije misli“. Tap tokom „thinking“ ubrzava bota ×2 (samo tajming). Medijana trajanja meča ≤ 6 min (`MatchLengthCheck`).

### V11 — Štapovi sa rejtinzima, OVR i energija
**`CueCatalog.asset`** (prerađen; zamenjuje 5 starih štapova):

| # | Id | Ime | Cena | GreenHalf | YellowWidth | Period (s) | Linija (prečnika) | Energija (max) | Dopuna (coins) | Izgled |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `house` | House Cue | start | 0.10 | 0.30 | 1.30 | 2 | **∞** | — | svetlo drvo |
| 2 | `club` | Club Cue | 250 | 0.12 | 0.30 | 1.40 | 3 | 8 | 30 | tamno drvo + crvena traka |
| 3 | `pro` | Pro Cue | 600 | 0.14 | 0.30 | 1.55 | 4 | 10 | 60 | crno + zlatna traka |
| 4 | `master` | Master Cue | 1600 | 0.16 | 0.29 | 1.70 | 6 | 12 | 160 | plava + srebrna |
| 5 | `champion` | Champion Cue | 4500 | 0.18 | 0.28 | 1.85 | 8 | 14 | 400 | bordo + zlatni prstenovi |
| 6 | `legend` | Legend Cue | 12000 | 0.20 | 0.27 | 2.00 | 10 | 16 | 1000 | zlatna, blagi emisivni sjaj |

**Rejtinzi** (0–99, izračunati iz podataka, ne upisani ručno — `CueRatings` u `Data/`):
- **AIM** = dužina linije: 2 → 40, 10 → 95, linearno;
- **CONTROL** = širina zelene + žute: House → 45, Legend → 95;
- **TIMING** = sporost bara (period): 1.30 → 45, 2.00 → 95;
- **OVR** = round(0.3·AIM + 0.4·CONTROL + 0.3·TIMING). Očekivano: House ~44, Club ~55, Pro ~64, Master ~74, Champion ~84, Legend ~95.

Prikaz na kartici: tri trake sa brojem i tooltipom (šta znači, npr. „CONTROL: wider green and yellow zones“), veliki OVR bedž, i **„+X vs your cue“** razlike prema selektovanom štapu (zeleno). Igrač odmah vidi šta dobija.

**Energija** (odluka korisnika: samo bolji štapovi):
- Svaki štap osim House ima energiju; jedan meč troši 1 (skida se na startu meča, kao ulaz).
- **Regeneracija:** +1 na `EnergyRegenMinutes` (početno 20 min), samo dok nije puno. Čuva se `CueEnergy_<id>` + `CueEnergyTime_<id>` (UTC timestamp poslednje dopune); izračunava se pri čitanju, bez servisa koji radi u pozadini.
- **Dopuna:** novčićima (cena iz tabele) ili **gledanjem reklame** (`RewardedPlacement.RefillEnergy`, puna dopuna, 1× po štapu dnevno).
- Ako je selektovanom štapu energija 0 pri pokretanju meča: `NoEnergyPopup`. Sadržaj:
  - „Your Pro Cue needs a rest.“ + vreme do sledeće energije;
  - **REFILL · 🪙 60**;
  - **WATCH AD · REFILL**;
  - **PLAY WITH HOUSE CUE** (uvek moguće — nema softlocka);
  - Close.
- Zašto ovako: energija daje novčićima drugu svrhu osim cena, i daje razlog za povratak (regeneracija). Osnovni štap garantuje da igrač nikad ne čeka.
- **Kupovina** ne selektuje automatski. Kartica pokaže SELECT sa pulsom + toast, kao sada. Novi štap stiže sa punom energijom.

### V12 — Ekonomija, napredak, rewarded i fake door koji imaju smisla
**`EconomyConfig.asset`:** `StartCoins` 150, `EnergyRegenMinutes` 20, rewarded limiti, uslovi fake door-a i ankete (dole).

**Ciljevi tempa** (`ProgressionSim.Run(2000)`, Monte Carlo sa verovatnoćama pobede iz `ArenaBalanceCheck`; prosečan igrač; strategija:
- kupi sledeći štap čim može a da mu ostane ulaz trenutne arene;
- igra najvišu otključanu arenu čiji ulaz može da plati;
- dopunjava energiju novčićima kad mu se isplati, inače igra House štapom;
- rewarded se ne koristi — konzervativno).

Pretpostavka sesija: **D1 ~8 mečeva, posle ~5 dnevno**.

| Prekretnica | Cilj (medijana mečeva) | ≈ dan |
|---|---|---|
| Arena 2 otključana | 4–6 | D1 |
| Club Cue | 6–9 | D1 |
| Arena 3 | 14–18 | D2–D3 |
| Pro Cue | 20–28 | D3–D4 |
| Arena 4 | 35–45 | D6–D7 |
| Master Cue | 45–55 | D8–D9 |
| Arena 5 | 60–72 | D10–D12 |
| Champion / Legend | 65–80 / 80+ | D2 nedelja+ (dugoročni cilj) |

Ovi ciljevi su provereni grubom simulacijom iz tabela V10/V11 (prosečan: Club 8, arena 3 ~15, Pro ~29, arena 4 ~41). **Pro je trenutno kasno** — prvo podesi njega (nagrada arene 2/3 ili cena Pro-a).
- Dobar igrač mora da stiže brže (~30–40% manje mečeva). Slab sporije, ali da ipak dođe do arene 3 do ~D5.
- Tuning **samo podacima**, ovim redom: nagrade → cene štapova → pobede za otključavanje → bot σ.
- Prijavi tabelu pre/posle.

**„Uvek blizu sledećeg“ — prikaz napretka (obavezno na svim mestima ispod):**
- **Meni:** dve trake napretka, uvek vidljive:
  - **„NEXT CUE · Pro Cue · 🪙 412 / 600“** (ikona štapa, % fill);
  - **„NEXT ARENA · Riverside Club · 3 / 5 wins“**.
- **Posle meča (rezultat):**
  - obe trake se animiraju sa starog na novo stanje (count-up);
  - kad nešto postane dostupno: pečat **„UNLOCKED!“** / **„YOU CAN BUY Pro Cue!“** + zvuk + haptic + dugme koje vodi tamo.
- **Arena kartica:** zaključana prikazuje „Win 2 more in Downtown Hall“ sa trakom.
- **Cue kartica:** „🪙 188 more“ sa trakom, ili BUY.

**Rewarded mesta (simulirana u ovoj fazi; svako ima jasnu vrednost u ekonomiji):**

| Placement | Gde | Šta daje | Limit | Zašto ima smisla |
|---|---|---|---|---|
| `DoublePrize` | rezultat pobede, arena 2+ | još jedna nagrada (ne ulaz) | 1× po meču | najveći i najprirodniji trenutak; ubrzava kupovinu sledećeg štapa |
| `FreeCoins` | `NotEnoughCoinsPopup`, meni kad je igrač ispod ulaza najniže plaćene otključane arene | coins = ulaz te arene | 3× dnevno | rešava „zaglavio sam“ bez forsiranja arene 1 |
| `RefillEnergy` | `NoEnergyPopup` | puna energija štapa | 1× po štapu dnevno | daje vrednost energiji i čuva bolji štap u igri |

- `RewardedAdService` (Systems, persistent) u ovoj fazi prikazuje `SimulatedAdOverlay` („Ad (test) · 3“, odbrojavanje 3 s, CLOSE).
  - Diže `OnRewardedAdFinished(placement, rewarded)`; prekid = bez nagrade.
  - Oznaka `// PHASE 2: replace with AdMob rewarded` na jednom mestu. Ne pravi interfejs ni fabriku.
- README i DoD jasno kažu: **build iz ove faze nije za store** dok se simulacija ne zameni.

**Fake door (ništa se ne naplaćuje i ništa se ne dodeljuje — dodela bi iskrivila meru):**
- **Starter Pack** — sadržaj se uklapa u progresiju:
  - **„Pro Cue + 1,500 coins + 24h unlimited energy“, $1.99.** Pro je D3–D4 cilj, a 1,500 novčića je ~1 ulaz + kupovina Master-a u areni 3–4. Vrednost je očigledna baš kad igraču treba.
  - **Kada:** posle `StarterPackAfterMatches` = 4 završena meča, **ili** prvi put kad štapu ponestane energije — šta god je pre. Max 1× po sesiji, 3× ukupno; nikad posle 2 uzastopna poraza.
  - **BUY** → „The shop opens soon! Thanks for your interest ❤️“ + prekidač **Notify me** + OK.
  - Prikazuje se kao kartica sa renderom Pro štapa, njegovim OVR-om i „+20 OVR vs House“ — povezano sa sistemom rejtinga.
- **ONLINE · coming soon** na meniju → „Online matches against real players are coming.“ + **Notify me**.
- Eventi: `OnFakeDoor(FakeDoorId, FakeDoorAction)` (`Shown`, `Tapped`, `NotifyOn`, `Closed`).

**Anketa** (`SurveyPopup`, iz meta dokumenta §6.9):
- posle 6. završenog meča, pri povratku u meni, jednom ukupno;
- pitanja: release bar 😀😐🙁 + chip „šta fali“ (*Spin*, *Online*, *More arenas*, *Better graphics*, *Easier aiming*, *Faster opponents*, *Nothing*).

**Profil** (`ProfileService` v2, jedini vlasnik PlayerPrefs profila):
- `ProfileVersion` 2. Ako je < 2, obriši ključeve profila (ne zvuk) i inicijalizuj (nema pravih korisnika; upiši u README).
- Ključevi:
  - `Coins`, `Wins`, `Losses`, `MatchesPlayed`;
  - `ArenaWins_<id>`, `SelectedCue`, `CueOwned_<id>`, `CueEnergy_<id>`, `CueEnergyTime_<id>`;
  - `PendingMatch` (samo statistika: napušten meč = poraz, ulaz je već plaćen);
  - `RewardedDay` + brojači po placementu;
  - `StarterPackShows`, `StarterPackTapped`, `OnlineNotify`, `SurveyDone`;
  - `TutorialDone`, `RulesSeen`, `HintsShown`, `ShotsTaken`.
- **Uklanja se:** `WinsToUnlock` po štapu, stari Win +10 / Loss −1, kazna −1 za napušten meč, tekstovi „WIN +10 COINS“.
- `MatchSetup`: `Mode` (`VsBot` | `Tutorial`), `ArenaId`, `OpponentId` (nasumičan lik arene, bira meni).

### V13 — Ekrani (UI) i kontrast
Svi popupi nasleđuju `PopupBase`, a dugmad su `UIButton`. UI dobija podatke samo kroz payload/snapshot (`ProfileSnapshot`, `ArenaViewData`, `CueViewData`, `OpponentViewData`).

**Kontrast (F15) — važi za sva dugmad:**
- labela ima tamnu konturu (TMP outline 0.2–0.25, boja `#1B140E`) i blagu senku;
- svetle varijante dugmadi (siva, žuta) dobijaju **tamni tekst** (`#1B140E`) umesto belog;
- plava, zelena i crvena su tamnije za ~15%.
- Proveri kontrast teksta prema pozadini dugmeta (WCAG ≥ 4.5:1) na screenshotovima; prijavi najslabije dugme.

**Glavni meni** (`MenuSceneBuilder`, prerađen):
```
┌────────────────────────────────────┐
│ [⚙]                    [🪙 1,240]  │
│           BILLIARDS                │
│          8-BALL ARENAS             │
│  ┌──────────────────────────────┐  │
│  │ [thumb] RIVERSIDE CLUB       │  │  izabrana arena (poslednja igrana ili najviša otključana)
│  │ Entry 250 · Win 650   [◀ ▶]  │  │  strelice menjaju arenu
│  └──────────────────────────────┘  │
│  [        ▶  PLAY            ]     │
│  NEXT CUE  ▓▓▓▓▓▓░░░  412 / 600    │  trake napretka (V12)
│  NEXT ARENA ▓▓▓░░░░░  3 / 5 wins   │
│  [ ⭐ CUES ]   [ 🌐 ONLINE · soon ] │
│  Your cue: Pro Cue · OVR 64 · ⚡ 7/10│
│                          v0.3.0    │
└────────────────────────────────────┘
```
- 3D sto u pozadini koristi temu izabrane arene.
- PLAY:
  - ako nema za ulaz → `NotEnoughCoinsPopup` (WATCH AD · +ulaz, PLAY BASEMENT BAR · FREE, Close);
  - ako je energija 0 → `NoEnergyPopup` (V11).
- **Arena izbor** je horizontalni karusel kartica (thumbnail teme 512×256, ime, ulaz/nagrada, stanje Locked sa trakom / Open).
- **CuesPopup:** 6 kartica (V11), sortirano po OVR; stanje Owned / SELECTED / BUY · cena (disabled + „🪙 X more“); energija sa REFILL. Ispod kartica: „Better cues: longer aim line, wider green, slower bar.“
- **Meč:**
  - **VS intro** (~2 s): levo YOU, desno portret i ime protivnika + `CPU`, ime arene, ulaz „leti“ u pot pa „WIN 650“; u areni 1 „FREE · WIN 40“. Zatim postojeći coin flip.
  - HUD: ime i portret protivnika umesto „OPPONENT“. Banneri: „DINA'S TURN“.
  - Ulaz i energija se skidaju pri ulasku u scenu. Forfeit ili napušten meč = poraz, bez dodatne kazne.
- **MatchResultPopup:**
  - **Pobeda:** „YOU WIN!“, razlog, nagrada count-up, trake napretka (V12), pečati otključavanja, **WATCH AD · DOUBLE** (arena 2+), **PLAY AGAIN**, **MENU**.
  - **Poraz:** „YOU LOSE“, razlog, „Entry lost (−250)“ / „No entry lost“, trake napretka (bez promene), **PLAY AGAIN · 250**, **MENU**.
- **Portreti:** ako korisnik nije dao PNG-ove (`_kenney_downloads/portraits/<id>.png`, 256×256), `GeneratedArtBuilder` pravi avatar (krug u boji arene, silueta, inicijali), deterministički po id-u.
- **FTUE tok:**
  - prvo pokretanje (`TutorialDone = 0`) → tutorijal (V9) → `RulesPopup` → prvi meč u Basement Bar-u (Rookie Ray, ulaz 0) → meni sa coach mark-om na traci „NEXT ARENA“ („Win 3 here to unlock Downtown Hall“);
  - svako sledeće pokretanje → meni;
  - skip tutorijala takođe vodi u prvi meč.
- **Settings:** postojeći + HOW TO PLAY + RULES + Haptics on/off + verzija.

### V14 — Performanse, README, build
- Merenje na uređaju (ako je povezan): Development build, FPS log, draw calls/trouglovi iz `UnityStats` u Aim, Watch i Orbit pogledu.
- README:
  - kontrole (orbit gestovi), raspored, model težine (V8 tabela), fizika (V4);
  - eventi (svi novi), tuning tabele (`ArenaCatalog`, `CueCatalog`, `EconomyConfig`, `MatchConfig`);
  - novi checks, upozorenje o simuliranim reklamama;
  - mapiranje „event → analytics event“ za fazu 2 (`release_plan.md` §6).
- `CREDITS.md`: Poly Haven, zvukovi, portreti.
- Verzija **0.3.0**, version code 3.

---

## 5. Eventi (novi i izmenjeni; upiši u README tabelu)
| Event | Payload | Ko diže | Ko sluša |
|---|---|---|---|
| `OnOrbitDrag` / `OnOrbitPinch` / `OnOrbitPan` | `Vector2 delta` / `float scaleDelta` / `Vector2 delta` | `TableTouchSurface` | `PlayerShotController` (LOOK), `CameraRig` (free look) |
| `OnTableViewRequested` | — | čip „↺ TABLE VIEW“, dvostruki tap | `CameraRig` |
| `OnReleaseMeterChanged` (izmenjen) | `float position, float power01, float greenHalf, float yellowEnd, float period` | `PlayerShotController` | `ReleaseBarView`, `PowerStripView` |
| `OnBallPocketedFx` | `int number, Vector2 screenPos, PocketFxKind kind` | `TableView` | `PocketFlyerView`, `PlayerPanelView`, `AudioManager`, `HapticsService` |
| `OnCoachMarkRequested` / `OnCoachMarkCleared` | `CoachTarget, string` / — | `TutorialController`, `HintService` | `CoachMarkView` |
| `OnTutorialStep` / `OnTutorialCompleted` | `int, TutorialStepResult` / `bool skipped` | `TutorialController` | `ProfileService`, kontroler scene |
| `OnArenaSelected` | `string arenaId` | `MainMenuView` | `MenuController` |
| `OnPlayRequested` (izmenjen: igra izabranu arenu) | — | `MainMenuView`, `MatchResultPopup` | `MenuController` / `MatchController` |
| `OnCueRefillRequested` (vraćen, novčići) | `string cueId` | `CueCardView`, `NoEnergyPopup` | `ProfileService` |
| `OnPlayWithStarterCueRequested` | — | `NoEnergyPopup` | `MenuController` |
| `OnRewardedAdRequested` / `OnRewardedAdFinished` | `RewardedPlacement` / `RewardedPlacement, bool` | popupi / `RewardedAdService` | `RewardedAdService` / `ProfileService`, popupi |
| `OnFakeDoor` | `FakeDoorId, FakeDoorAction` | `StarterPackPopup`, `ComingSoonPopup`, `MainMenuView` | `ProfileService` |
| `OnSurveyAnswered` / `OnSurveySkipped` | `int score, int missingMask` / — | `SurveyPopup` | `ProfileService` |
| `OnArenaUnlocked` / `OnCueAffordable` | `string id` | `ProfileService` (posle rezultata) | `AudioManager`, `HapticsService`, `MatchResultPopup` |
| `OnMatchStarted` (izmenjen) | + `OpponentName`, `OpponentPortrait`, `ArenaName`, `Entry`, `Prize` | `MatchController` | HUD, `VsIntroView` |

---

## 6. Konfiguracija (novi brojevi, sve u SO)
- **`MatchConfig`:**
  - V4: `SlideFriction`, `RollSlipEpsilon`;
  - V5: `PullTravelPixels`, `PullGrabZone` (0.4 kolone), oznake 0.15/0.5/0.85;
  - V8: krive snage `GreenScale0/1`, `YellowScale0/1`, `PeriodScale0/1`, `ErrorScale0/1`;
  - V2: orbit granice (pitch 12–85, distance 0.35–4.0, pivot margina 0.3, smooth 0.08, inercija 0.25) i pragovi gestova;
  - V1: širine i boje traka, brzina chevrona 0.12;
  - V6: osvetljenost, vinjeta, MSAA;
  - `RailTop` spušten.
- **`CueCatalog`:** V11 tabela + energija.
- **`ArenaCatalog`:** V10 tabela + likovi.
- **`BotProfile` po areni:** `TimingSigmaMs`, `MaxCutAngle`, `CandidatesToSimulate`, tajminzi.
- **`EconomyConfig`:** V12.
- **`TutorialConfig`:** V9.
- **`VenueTheme` × 5:** Basement, Downtown, Riverside, Grand, Royal. Infrastruktura iz G3 gameplay dokumenta (čoja, drvo, kape, svetlo, soba, neon, ambient) + `TableLightColor`/`Intensity`. Sve teme imaju TV-turnir svetlo stola, a razlikuju se bojom čoje, kapama, sobom i neonom.

---

## 7. Nove verifikacione provere (`Editor/Checks`)
| Check | Šta |
|---|---|
| `PhysicsChecks.SlideRoll()` + proširen `Collisions` | V4 tabela |
| `PredictorChecks.MatchesSimulation()` (proširen) | putanja bele posle kontakta ±2 mm |
| `GuideChecks.LengthRule()` | V1 pravilo dužine |
| `ShotDifficultyCheck.ReleaseTable()` / `PotRates()` | V8 tabele |
| `ControlChecks.PullMapping()` | V5 (zamenjuje `PullRangeReachable`) |
| `CameraChecks.OrbitBounds()` | V2 (uz postojeće `WatchFitsAllPockets`, `PlaceFitsArea`) |
| `TutorialChecks.RunThrough()` | V9 |
| `ArenaBalanceCheck.Run(200)` | V10 ciljevi pobede po areni × 3 igrača × očekivani / ±1 štap |
| `ProgressionSim.Run(2000)` | V12 prekretnice (prosečan, dobar, slab) |
| `MatchLengthCheck.Run(100)` | medijana meča ≤ 6 min |
| `EconomyChecks.All()` (prerađen) | start 150; ulaz i energija na startu; nagrada samo na pobedi; arena 1 uvek igriva sa 0; zaključana arena odbijena; kupovina bez novca odbijena; energija regen po satu (simuliran sat); refill; rewarded limiti i reset sledećeg dana; double prize 1×; fake door ne menja stanje; migracija v1 → v2 |
| postojeći `RulesChecks.All`, `AimChecks`, `CueChecks`, `BotBalanceCheck.Run(200)` | moraju prolaziti |

---

## 8. Plan commitova (12)

### Commit 1 — `physics: sliding to rolling, follow and stun, predictor and bot speed`
**Obim:** V4 (`BallState.Roll`, `PoolPhysics`, `BallView` rotacija iz `Roll`, `ShotPredictor` putanja posle kontakta, `BotBrain` procena brzine), rekalibracija `MatchConfig`.

**Verifikacija:**
- `PhysicsChecks.*` (uključujući `SlideRoll`), `PredictorChecks`, `RulesChecks`, `AimChecks`, `BotBalanceCheck.Run(200)` — prijavi pre/posle brojeve;
- screenshot sekvenca pun pogodak izbliza/izdaleka.

### Commit 2 — `shot difficulty: power-scaled release window, live bar, bot timing model`
**Obim:** V8 (`ShotAccuracy`, `PlayerShotController` faza bara, `ReleaseBarView` uživo, `BotBrain.Release` preko σ, simulirani igrači u `BotBalanceCheck` preko σ).

**Verifikacija:**
- `ShotDifficultyCheck.*` (tabele u izveštaju);
- `BotBalanceCheck.Run(200)`;
- screenshotovi bara meko/jako.

### Commit 3 — `aim & cue: path to first obstacle, soft arrow bands, muted illegal, cue tip, 1:1 power grip`
**Obim:** V1, V3, V5.

**Verifikacija:**
- `GuideChecks.LengthRule`, `ControlChecks.PullMapping`, `PredictorChecks`;
- 0 B GC u Aim fazi;
- `eval` kriterijumi V5;
- screenshotovi.

### Commit 4 — `camera: orbit free look on the opponent's turn and in look mode, gesture hints`
**Obim:** V2 (uključujući coach mark za free look i ispravku preklapanja u Look-u).

**Verifikacija:**
- `CameraChecks.OrbitBounds` + postojeći `CameraChecks`;
- `eval` „ugao na susednu rupu“;
- Bot vs Bot ceo meč bez izuzetaka, uz aktivan free look;
- screenshotovi.

**→ CHECKPOINT 1:** APK. Korisnik testira:
- osećaj fizike (pogođene kugle, follow/stun);
- težinu po štapu i snazi (sa trenutnim štapovima, pre V11);
- novu putanju i ilegalnu metu;
- vrh štapa;
- power 1:1;
- free look tokom poteza bota i LOOK (uključujući ugao na susednu rupu).

Čekaj potvrdu ili izmene.

### Commit 5 — `table art: pockets, rails, PBR materials, tournament lighting, reflections, no shimmer`
**Obim:** V6 (uključujući `VenueTheme` infrastrukturu i jednu temu `Riverside` kao default za meč i tutorijal).

**Verifikacija:**
- `PhysicsChecks`, `CueChecks`, `CameraChecks`;
- screenshotovi V6;
- luma merenja;
- treperenje pre/posle;
- draw calls i trouglovi (prijavi).

### Commit 6 — `juice: pocket drop and glow, ball flyer, audio variants, release stamps, calmer menu title`
**Obim:** V7.

**Verifikacija:**
- kriterijumi G4;
- `PhysicsChecks.Determinism`;
- `CREDITS.md` ažuriran.

### Commit 7 — `meta core: arenas with stakes, cues with ratings and energy, economy, profile v2, bots per arena`
**Obim:** V10, V11, V12 (podaci, `ProfileService` v2, `MatchSetup`, `MatchController` ulaz/nagrada/energija, `BotProfile` po areni, migracija). Privremeno: postojeći meni PLAY igra najvišu otključanu arenu.

**Verifikacija:**
- `EconomyChecks.All`, `ArenaBalanceCheck.Run(200)` (prvi prolaz, prijavi);
- `grep -rn "WinsToUnlock" Assets/_Scripts` prazan;
- meč u areni 1 i 2 do rezultata.

### Commit 8 — `menu & screens: arena carousel, progress bars, cues with ratings, energy, vs intro, results, contrast`
**Obim:** V13 (bez FTUE), V12 prikaz napretka, `NotEnoughCoinsPopup`, `NoEnergyPopup`, kontrast dugmadi.

**Verifikacija:**
- kroz `eval` postavi profile u stanja: nov igrač, sredina arene 2, pred kupovinu Pro-a, 0 novčića, štap bez energije, sve otključano;
- screenshot menija, karusela, Cues-a, VS intro-a, rezultata (pobeda sa otključavanjem, poraz) za svako stanje;
- kontrast izveštaj;
- grep slojeva.

**→ CHECKPOINT 2:** APK sa obrisanim profilom (osim tutorijala). Korisnik testira: prvih ~10 mečeva, otključavanje arene 2, kupovinu Club-a, energiju, jasnoću napretka i kontrast dugmadi.

Čekaj potvrdu.

### Commit 9 — `venues: five venue themes, room variants, thumbnails`
**Obim:** 5 `VenueTheme`, parametrizovan `RoomPrefabBuilder` (jedan builder, 5 varijanti iz parametara), thumbnail render.

**Verifikacija:**
- Aim i Watch screenshot po temi;
- `CameraChecks` 100%;
- draw calls po temi u budžetu.

### Commit 10 — `tutorial & ftue: lessons, coach marks, contextual hints, first match`
**Obim:** V9 + FTUE tok (V13).

**Verifikacija:**
- `TutorialChecks.RunThrough`, skip;
- HOW TO PLAY;
- svaki hint jednom;
- obrisan profil → tutorijal → pravila → prvi meč → meni sa coach mark-om; drugo pokretanje → meni;
- screenshotovi.

### Commit 11 — `monetization probes: simulated rewarded, starter pack and online fake doors, survey`
**Obim:** V12 rewarded, fake door, anketa.

**Verifikacija:**
- `FreeCoins` 3× pa 4. odbijen, sledeći dan ponovo (simuliran sat);
- `DoublePrize` jednom;
- `RefillEnergy` 1× po štapu dnevno;
- prekid → bez nagrade;
- Starter Pack uslovi (4. meč ili prva prazna energija, 1×/sesija, 3× ukupno, ne posle 2 poraza), BUY ne menja stanje;
- Notify se čuva; anketa posle 6. meča, jednom;
- screenshotovi.

### Commit 12 — `balance & build: arena balance, progression sim, match length, performance, README, APK`
**Obim:** `ArenaBalanceCheck`, `ProgressionSim`, `MatchLengthCheck`, tuning podataka do ciljeva (tabela pre/posle), V14.

**Verifikacija:** svi checks; brojevi tempa i balansa u izveštaju; APK 0.3.0 + `aapt`; DoD (§9).

**→ CHECKPOINT 3:** APK sa obrisanim profilom. Korisnik igra 30–60 min kao novi igrač i ponovo prolazi sve tačke feedbacka (§1.1).

---

## 9. Definition of Done
- [ ] V1 Bela do prve prepreke (tanka linija), posle samo dužina strelice (meke trake, spori chevroni), ilegalna meta prigušena bez X-a (`GuideChecks`).
- [ ] V2 Orbit free look tokom poteza bota i u LOOK-u (1 prst, pinch, pan), nikad ispod rail-a (`OrbitBounds` 0), coach mark, bez preklapanja UI-ja, ugao na susednu rupu moguć.
- [ ] V3 Vrh štapa i ferula vidljivi, ispupčeni.
- [ ] V4 Klizanje → kotrljanje, stun/follow, predikcija putanje posle kontakta (`PhysicsChecks`, `PredictorChecks`).
- [ ] V5 Power 1:1 sa ručkom na vrhu, fiksne SOFT/MED/HARD oznake (`PullMapping`).
- [ ] V6 Rupe bez preklapanja čoje i bez „valjka“, rail bez stepenika, PBR, TV svetlo, reflection probe, blob senke, bez treperenja, svetlina u ciljevima.
- [ ] V7 Juice iz G4, placeholder bilijar zvuci sa spremnim mapiranjem, smiren naslov menija.
- [ ] V8 Težina štap × snaga, bar uživo, bot isti model (`ShotDifficultyCheck` tabele).
- [ ] V9 Tutorijal T1–T5 + skip + HOW TO PLAY + hintovi (uključujući free look i snagu).
- [ ] V10 5 arena sa ulogom, otključavanje pobedama, težina fiksna po areni, CPU likovi (`ArenaBalanceCheck` u ciljevima).
- [ ] V11 6 štapova sa AIM/CONTROL/TIMING/OVR, energija za bolje štapove, House bez energije.
- [ ] V12 Prekretnice u ciljevima (`ProgressionSim`), trake napretka svuda, rewarded mesta sa vrednošću, Starter Pack u skladu sa progresijom, ONLINE fake door, anketa.
- [ ] V13 Novi meni, karusel arena, Cues, VS intro, rezultat sa napretkom, kontrast dugmadi ≥ 4.5:1, FTUE tok.
- [ ] V14 Performanse izmerene (ili objašnjeno zašto ne), README, CREDITS, APK 0.3.0.
- [ ] Pravila 8-ball nepromenjena (`RulesChecks`); fizika i bot promenjeni samo kako V4/V8 kažu; `BotBalanceCheck` u opsezima.
- [ ] Grep provere slojeva prazne.
- [ ] 12 commitova na `main`, pushovano; 3 checkpointa potvrđena od korisnika.

---

## 10. Rizici
| Rizik | Šta uraditi |
|---|---|
| V4 menja osećaj i balans više od očekivanog | Commit 1 je sam za sebe; pre/posle brojevi + sekvenca za korisnika na checkpointu 1; `SlideFriction` je jedini glavni knob |
| Predikcija posle kontakta se ne poklapa sa simulacijom | analitička parabola iz istih formula kao `PoolPhysics`; `PredictorChecks` je gate (±2 mm) |
| Jak udarac postane „nemoguć“ | V8 tabela: najjači udarac sa House štapom je i dalje 23% zeleno / 89% zeleno+žuto; žuto je mala greška; T4 uči doziranje snage |
| Orbit kamera zbunjuje stariju publiku | default ostaje Watch; free look tek na gest; coach mark sa animacijom; čip „TABLE VIEW“ i dvostruki tap uvek vraćaju |
| Energija iritira | House bez energije; regen 20 min; refill rewarded; `NoEnergyPopup` uvek nudi igru odmah |
| Ekonomija se pokvari | `ProgressionSim` i `ArenaBalanceCheck` su gate; tuning samo podacima; svi brojevi u SO (kasnije Remote Config) |
| TV svetlo + bloom = bleštanje na beloj kugli | exposure podešen screenshotom; bloom samo na emisiji; luma ciljevi |
| Poly Haven teksture prevelike | 1K, ASTC, mipmaps; ako APK raste > 15 MB zbog tekstura → 512 |
| Simulirane reklame odu na store | DoD + README upozorenje + `PHASE 2` komentar; closed test tek posle faze 2 |
| 5 soba = 5× sadržaja, prekoračen budžet | jedan parametrizovan builder, deljeni materijali, provera po temi u commitu 9 |
