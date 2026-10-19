# Implementation Definition — Meta Design (Billiards, faza „polish 2/2“)

Ovaj dokument je **jedini ulaz** za agenta koji pravi novi meta sistem: Tour (arene + AI protivnici), ekonomiju, štapove, novi meni, FTUE tok, fake door ponude i anketu.
Čitaj ga ceo pre prvog koraka. Kad nešto nije pokriveno — **stani i pitaj korisnika**, ne pretpostavljaj.

Prateći dokumenti (čitaj, ne menjaj):
- `README.md` — arhitektura, eventi, builderi, checks.
- `implementation_definition.md` — originalna specifikacija; slojevi (4.3), konvencije (4.9), CLI (3) i verifikacioni alati (8) **važe i ovde**.
- `gameplay_redesign_implementation_definition.md` — **mora biti završen pre ovog dokumenta** (vidi 2. Preduslovi). Odatle koristiš `VenueTheme`, tutorijal (`OnTutorialCompleted`), `RulesPopup`, `HintService`, `CoachMarkView`, nove kontrole.
- `release_plan.md` §2 — izvor ovog dizajna (zašto petlja ovako izgleda).

---

## 0. Pravila rada za agenta

1. Radiš kroz **7 commitova** (sekcija 8). Svaki je kompajlabilna, pokretljiva celina. Posle svakog: `git commit` + `git push` na `main`. Nikad force push.
2. Pre svakog commita: `unity recompile` 0 grešaka, grep provere slojeva (README „Layer rules“) prazne, svi `Editor/Checks` prolaze (stari i novi), plus verifikacija iz commita. Ne tvrdiš da radi ako nisi proverio; šta ne možeš da proveriš, napiši.
3. Izveštaj posle svakog commita: šta, kako provereno, šta nije, screenshotovi (`Screenshots/m<commit>_*.png`, portrait 1080×1920), commit hash.
4. **Checkpointi sa korisnikom** posle commita 4 i 7: APK + šta da testira. **Čekaj potvrdu.**
5. **Ne menjaš** fiziku, pravila, `BotBrain` logiku, kontrole udarca, kameru ni aim guide. Ako balans to zahteva, menjaju se samo **podaci** (`BotProfile` asseti, cene, nagrade).
6. Scene, prefabi i asseti samo kroz **buildere**. Svi brojevi u SO-ovima (sekcija 4), nikad u kodu. Brojevi su grupisani tako da ih kasnije (faza Remote Config, `release_plan.md` §6) može pregaziti jedno mesto.
7. Unity CLI napomene iz gameplay dokumenta §0.8 važe: osascript za zaglavljen recompile, `runInBackground = true` u Play mode-u, FPS samo sa uređaja.
8. Ne diraj ništa van `~/Desktop/UnityProjects/Billiards` osim `_kenney_downloads/`.

---

## 1. Cilj i obim

**Cilj:** najtanja verzija 8 Ball Pool petlje, bez PvP-a, koja:
1. igraču daje **razlog da se vrati** (napredak kroz arene, bolji štap, sledeći protivnik);
2. **meri** hipotezu iz `release_plan.md` §0: retention, funnel napretka, signale mehanike, nameru plaćanja (fake door);
3. je iskrena: protivnici su **AI likovi**, jasno označeni `CPU`.

**Petlja:**
```
Meni → PLAY (sledeći protivnik) ili TOUR (izbor) → plati ulaz → VS intro → meč protiv AI lika
  ↑                                                                    ↓
  └── kupi bolji štap ←── novčići ←── nagrada (+ bonus za prvu pobedu) ←── pobeda
                                      pobedi bosa arene → otključana sledeća arena
```

| # | Zahtev |
|---|---|
| M1 | **Tour:** 4 arene × 3 AI protivnika; treći je boss. Pobeda otključava sledećeg; boss otključava sledeću arenu. Pobeđeni se mogu igrati ponovo. |
| M2 | **Ekonomija:** novčići; ulaz se plaća na početku meča; nagrada na pobedi; jednokratni bonus za prvu pobedu nad svakim protivnikom. **Arena 1 je uvek besplatna** (nema softlocka). Bez energije. |
| M3 | **Štapovi:** 4 štapa (House, Club, Pro, Master), kupuju se novčićima. Bolji = širi release prozor, sporiji bar, duža linija. Jedan selektovan. |
| M4 | **AI likovi:** ime, portret, `CPU` oznaka, jedna rečenica karaktera, težina (zvezdice); svaki ima svoj `BotProfile`. |
| M5 | **Arene vizuelno različite** kroz `VenueTheme` (čoja, svetlo, soba, neon). |
| M6 | **Novi meni:** veliko PLAY (nastavi kod sledećeg protivnika), TOUR, CUES, ONLINE (coming soon), Settings, novčići. |
| M7 | **Meč prikaz:** VS intro sa ulazom i nagradom, HUD sa imenom i portretom protivnika, nov rezultat popup (nagrada, napredak, otključavanje, sledeći korak). |
| M8 | **FTUE:** prvo pokretanje → tutorijal → pravila → prvi meč protiv Rookie Ray-a → meni sa jasnim sledećim korakom. |
| M9 | **Rewarded „ad“ mesta** (simulirana u ovoj fazi): „Double prize“ i „+50 coins“. |
| M10 | **Fake door:** Starter Pack ponuda i ONLINE (coming soon) sa „Notify me“. |
| M11 | **Anketa** posle 5. meča (1 pitanje + izbor „šta fali“). |
| M12 | **Tempo:** medijana trajanja meča ≤ 6 min; potez bota ~6 s. |

**Van obima:** prave reklame (AdMob), pravi IAP, In-App Review, analitika (Firebase), Remote Config — sve faza 2 iz `release_plan.md`. Ovde samo **mesta i eventi** za njih, da faza 2 bude „uključi SDK“. Takođe van obima: dnevne nagrade, misije, XP/leveli, leaderboard, battle pass, PvP, spin.

---

## 2. Preduslovi

| # | Šta | Provera |
|---|---|---|
| P1 | Gameplay dokument završen: 9 commitova na `main`, checkpoint 2 potvrđen | `git log` sadrži `polish & build: performance pass, README, APK`; postoje `VenueTheme`, `Club.asset`, `TutorialController`, `RulesPopup`, `HintService`. Ako ne → **stani i javi** |
| P2 | Editor otvoren, `unity status` → ready, git čist (planski dokumenti su commitovani u gameplay commitu 1) | agent |
| P3 | (opciono) **Portreti AI likova** 256×256 PNG, ilustrovani (npr. AI generisani od strane korisnika), u `_kenney_downloads/portraits/<opponentId>.png` | korisnik. Ako nema → generisani avatari (5.3), bez čekanja |

---

## 3. Šta se uklanja

| Uklanja se | Zamena |
|---|---|
| Energija štapa: `MaxEnergy`, `RefillPrice`, `InfiniteEnergy`, `SaveKeys.CueEnergy`, `OnCueRefillRequested`, `ConsumeEnergyForMatch`, `CanStartMatch` (energija), `NoEnergyPopup` | ulaz u arenu + `NotEnoughCoinsPopup` |
| 5 štapova, otključavanje po broju pobeda (`WinsToUnlock`), `Legend Cue` | 4 štapa, samo cena |
| Win +10 / Loss −1, tekstovi „Win +10 coins“ u meniju | nagrade po areni (4.1) |
| `PendingMatch` kazna −1 | ulaz je već plaćen na startu. `PendingMatch` ostaje samo za statistiku (napušten meč = poraz u `Losses`) + toast „Last match was abandoned — entry lost“ |
| `BotProfile.asset` (jedan za sve) | `BotProfile` po protivniku (`_ScriptableObjects/Opponents/Bot_<id>.asset`) |
| Stari meni raspored (Play / Settings / Cues) | 6. |
| Generički „OPPONENT“ u HUD-u i bannerima | ime protivnika |

**Migracija profila:** nema pravih korisnika, pa nema migracije podataka.
- `ProfileService` čita `SaveKeys.ProfileVersion`. Ako je < 2 → obriši sve ključeve profila (ne zvuk) i inicijalizuj nov profil (verzija 2).
- Zabeleži u README.

---

## 4. Podaci i brojevi

Sve u SO-ovima pod `Assets/_ScriptableObjects/`. Gradi ih `DataAssetBuilder` iz specifikacija u kodu (kao `CueSpecs` sada), idempotentno. Vrednosti ispod su **početne**; finalne određuje balans pass (commit 7) uz brojeve iz `Editor/Checks`.

### 4.1 Arene — `TourCatalog.asset`
| # | Id | Ime | Tema (`VenueTheme`) | Ulaz | Nagrada (pobeda) | Bonus za prvu pobedu nad protivnikom |
|---|---|---|---|---|---|---|
| 1 | `basement` | **Basement Bar** | `Basement` | **0** | 25 | +25 |
| 2 | `downtown` | **Downtown Hall** | `Downtown` | 50 | 120 | +100 |
| 3 | `riverside` | **Riverside Club** | `Riverside` | 200 | 480 | +300 |
| 4 | `grand` | **Grand Lounge** | `Grand` | 800 | 1900 | +1000 |

**Zašto nagrada nije 2× ulaz** (kao u 8 Ball Pool PvP-u):
- Protiv bota prosečan igrač pobeđuje ~50–55%. Pri 2× ulazu očekivana zarada je ~0 po meču, pa napredak i kupovina štapova postaju mučenje.
- ~2.4× + bonus za prvu pobedu daje osećaj rasta. Gubitak i dalje boli (ulaz), pa pobeda ima težinu.

Start: **100 novčića** (`EconomyConfig.StartCoins`).

### 4.2 Protivnici — `OpponentDefinition` (u `TourCatalog`, 3 po areni)
| Arena | Id | Ime | Opis (EN, 1 rečenica) | Zvezdice | Release G/Y/R % (početno) | MaxCut | Kandidata | Ličnost (samo prikaz/tajming) |
|---|---|---|---|---|---|---|---|---|
| 1 | `ray` | Rookie Ray | „Just learned what a bridge is.“ | ★ | 20 / 40 / 40 | 55° | 2 | brzo misli |
| 1 | `lin` | Lucky Lin | „Swears every shot was planned.“ | ★ | 28 / 42 / 30 | 60° | 2 | — |
| 1 | `sal` | **Big Sal** (boss) | „Breaks like a freight train.“ | ★★ | 35 / 42 / 23 | 65° | 3 | uvek break 100% |
| 2 | `dina` | Dina Drift | „Never in a hurry. Never.“ | ★★ | 40 / 40 / 20 | 75° | 3 | sporije misli |
| 2 | `marco` | Marco Mumbles | „Talks to the balls. They listen.“ | ★★ | 44 / 40 / 16 | 75° | 3 | — |
| 2 | `hank` | **Hustler Hank** (boss) | „Plays worse when there's no money on it.“ | ★★★ | 48 / 38 / 14 | 75° | 4 | — |
| 3 | `ivy` | Ivy Bennett | „Club champion, three years running.“ | ★★★ | 52 / 36 / 12 | 75° | 5 | — |
| 3 | `theo` | Theo Park | „Calculates angles for fun.“ | ★★★ | 56 / 34 / 10 | 75° | 5 | sporije misli |
| 3 | `duchess` | **The Duchess** (boss) | „Has never been seen to miss a straight one.“ | ★★★★ | 60 / 32 / 8 | 75° | 5 | — |
| 4 | `viktor` | Viktor Kane | „Ice in his veins, chalk on his hands.“ | ★★★★ | 64 / 30 / 6 | 75° | 5 | — |
| 4 | `mei` | Mei Tanaka | „Plays the table, not the player.“ | ★★★★ | 68 / 27 / 5 | 75° | 5 | — |
| 4 | `professor` | **The Professor** (boss) | „Wrote the book. Literally.“ | ★★★★★ | 72 / 24 / 4 | 75° | 5 | sporije misli |

- Imena su izmišljena; ne koristi imena stvarnih igrača.
- Težina se menja **samo kroz postojeća `BotProfile` polja** (`GreenChance/YellowChance/RedChance`, `MaxCutAngle`, `CandidatesToSimulate`, `BreakPowerMin/Max`, tajminzi). `BotBrain` se ne menja.
- Bot štap (`BotProfile.GreenHalf/YellowWidth`) ostaje Club vrednosti za sve.

### 4.3 Štapovi — `CueCatalog.asset` (prerađen)
| Id | Ime | Cena | GreenHalf | YellowWidth | Period (s) | Linija (prečnika) | Izgled |
|---|---|---|---|---|---|---|---|
| `house` | House Cue | start (owned) | 0.10 | 0.30 | 1.30 | 2 | svetlo drvo |
| `club` | Club Cue | 300 | 0.13 | 0.30 | 1.45 | 4 | tamno drvo + crvena traka |
| `pro` | Pro Cue | 1200 | 0.16 | 0.30 | 1.60 | 6 | crno + zlatna traka |
| `master` | Master Cue | 4500 | 0.19 | 0.28 | 1.80 | 9 | plava + srebrna, blagi sjaj |

- Bez otključavanja po pobedama: svaki se može kupiti čim igrač ima dovoljno novčića. Novčići su jedina kapija, što je jednostavnije i jasnije.
- Kupovina ne selektuje automatski: kartica pokaže SELECT sa pulsom + toast (postojeće ponašanje).

### 4.4 `EconomyConfig.asset` (nov)
| Polje | Vrednost |
|---|---|
| `StartCoins` | 100 |
| `RewardedCoinsAmount` | 50 |
| `RewardedCoinsDailyCap` | 3 |
| `DoublePrizeMinVenue` | 2 (ne nudi se u areni 1) |
| `StarterPackAfterMatches` | 3 |
| `StarterPackMaxShows` | 3 (max 1 po sesiji) |
| `StarterPackLabel` / `StarterPackPrice` | „Pro Cue + 1000 coins“ / „$1.99“ (samo tekst) |
| `SurveyAfterMatches` | 5 |
| `BotTurnTargetSeconds` | ~6 (vidi M12) |

### 4.5 Ciljevi tempa (proverava `ProgressionSim`, commit 7)
Referentni „prosečan igrač“ = release distribucija **45/40/15**. To je pretpostavka; pravu distribuciju meri analitika posle launcha (`shot.zone`).
- Kupuje sledeći štap čim može, igra sledećeg neporaženog protivnika ako ima za ulaz, inače najjaču pristupačnu arenu, inače arenu 1. Rewarded se ne koristi u simulaciji (konzervativno).

| Prekretnica | Cilj (medijana mečeva od početka) |
|---|---|
| Arena 2 otključana | 4–6 (prva 1–2 sesije) |
| Club Cue kupljen | 8–14 |
| Arena 3 otključana | 15–25 |
| Pro Cue kupljen | 25–40 |
| Arena 4 otključana | 35–55 |
| Master Cue kupljen | 70–110 |
| Tour završen (Professor pobeđen) | 60–100 |

**Ciljevi pobede** (`ArenaBalanceCheck`, prosečan igrač sa „očekivanim“ štapom za tu arenu):

| Arena | Očekivani štap | Protivnik 1 / 2 / boss |
|---|---|---|
| 1 | House | ~80% / ~70% / ~60% |
| 2 | House | ~60% / ~55% / ~50% |
| 3 | Club | ~55% / ~50% / ~45% |
| 4 | Pro | ~50% / ~45% / ~40% |

- Tolerancija ±7 p.p.
- „Dobar igrač“ (75/20/5) sa istim štapovima: ≥ 70% svuda, ≥ 60% protiv bosa arene 4.
- Ako tempo ili pobede nisu u opsegu, menjaj **podatke** ovim redom: release odds protivnika → nagrade/bonusi → cene štapova. Prijavi pre/posle brojeve.

---

## 5. Sistemi

### 5.1 `ProfileService` (prerađen; i dalje jedini vlasnik PlayerPrefs profila)
**Stanje (`SaveKeys`):**
- `ProfileVersion` (2), `Coins`, `Wins`, `Losses`, `MatchesPlayed`
- `SelectedCue`, `CueOwned_<id>`
- `OpponentBeaten_<id>` (0/1)
- `PendingMatch`
- `RewardedCoinsDay` (yyyyMMdd), `RewardedCoinsToday`
- `StarterPackShows`, `StarterPackTapped`, `OnlineNotify`, `SurveyDone`
- iz gameplay dokumenta: `TutorialDone`, `RulesSeen`, `HintsShown`
- `SfxVolume`/`MusicVolume` ostaju kod `AudioManager`-a

**API za controllere:**
- `ProfileSnapshot GetSnapshot()` — novčići, štapovi, tour stanje (`TourSnapshot`).
- `OpponentDefinition NextOpponent()` — prvi neporaženi u najnižoj otključanoj areni; posle završenog toura: boss arene 4.
- `bool IsVenueUnlocked(venueId)`, `bool IsOpponentUnlocked(opponentId)`.
- `bool CanAffordEntry(opponentId, out int missing)`.
- `void PayEntry(opponentId)` — skida ulaz, postavlja `PendingMatch = 1`, `MatchesPlayed++`.
- `MatchReward ApplyMatchResult(opponentId, bool won)` — vraća `Prize`, `FirstWinBonus`, `NewBalance`, `NewlyUnlockedOpponentId`, `NewlyUnlockedVenueId`, `TourCompleted`; briše `PendingMatch`.
- `int GrantRewardedCoins()` (poštuje dnevni limit, vraća dodeljeno ili 0), `int DoublePrize(MatchReward)` (dodaje još jednu nagradu, ne bonus).
- `bool TryBuy(cueId, out reason)`, `bool TrySelect(cueId, out reason)` (kao sada, bez energije).
- Flagovi za fake door i anketu.

Sluša `OnCueBuyRequested` / `OnCueSelectRequested`. Diže `OnProfileChanged` / `OnPurchaseFailed`.

### 5.2 `MatchSetup` (proširen)
`Mode` (`VsBot` | `Tutorial`), `OpponentId`, `VenueId`. Postavlja ga meni (ili Play Again / Next Opponent) pre učitavanja Match scene. `MatchController` iz njih dobija `BotProfile` protivnika, `VenueTheme` i ulaz/nagradu.

### 5.3 `RewardedAdService` (Systems, persistent) — simulacija u ovoj fazi
- `void Show(RewardedPlacement placement)` → u ovoj fazi prikazuje `SimulatedAdOverlay`: full-screen tamni panel „Ad (test) · 3“ sa odbrojavanjem 3 s, pa **CLOSE**. Diže `OnRewardedAdFinished(placement, true)`. Zatvaranje pre kraja → `false`.
- Placementi: `DoublePrize`, `FreeCoins`.
- **Faza 2 zamenjuje telo ove klase AdMob pozivom.** API i eventi ostaju isti. Ne pravi interfejs ni fabriku. Oznaka `// PHASE 2: replace with AdMob rewarded` na jednom mestu.
- README i DoD jasno kažu: **build iz ove faze nije za store** dok se simulacija ne zameni.

### 5.4 Generisani portreti (fallback za P3)
Ako korisnik nije dao portrete, `GeneratedArtBuilder` pravi 256×256 avatar po protivniku:
- krug sa bojom arene;
- jednostavna silueta glave i ramena (proceduralno);
- inicijali (postojeći bitmap font);
- zvezdice težine kao mali bedž.

Boss dobija zlatni okvir. Deterministički seed po id-u.

---

## 6. Ekrani i tok (UI)

Svi popupi nasleđuju `PopupBase`, dugmad su `UIButton`. UI dobija podatke samo kroz payload/snapshot (`TourSnapshot`, `VenueViewData`, `OpponentViewData`). Slojevi kao u 4.3 originalnog dokumenta.

### 6.1 Glavni meni (`MenuSceneBuilder`, prerađen)
```
┌────────────────────────────────────┐
│ [⚙]                    [🪙 1,240]  │
│           BILLIARDS                │   (naslov ostaje dok meta ne dobije ime iz ASO odluke)
│          8-BALL TOUR               │
│  ┌──────────────────────────────┐  │
│  │ [portret] NEXT: Lucky Lin  ★ │  │   kartica sledećeg protivnika
│  │ Basement Bar · Entry FREE    │  │
│  │ Win 25 · First win +25       │  │
│  └──────────────────────────────┘  │
│  [        ▶  PLAY            ]     │   = igraj NextOpponent
│  [ 🏆 TOUR ]      [ ⭐ CUES ]      │
│  [ 🌐 ONLINE · coming soon ]       │   fake door (M10)
│  Your cue: Club Cue  ▸             │
│                          v0.2.0    │
└────────────────────────────────────┘
```
- 3D sto u pozadini koristi temu arene sledećeg protivnika.
- PLAY: ako nema za ulaz → `NotEnoughCoinsPopup`.
- Posle FTUE prvog meča: coach mark na TOUR „Beat 3 rivals to unlock the next venue“ (jednom, `HintService`).

### 6.2 `TourPopup` (full-height)
- Vertikalni scroll, 4 kartice arena: thumbnail teme (render iz builder-a, 512×256), ime, „Entry 200 · Win 480“, napredak „2/3 beaten“.
- Stanje: **Locked** (zatamnjeno, katanac, „Beat Big Sal to unlock“) / **Open** / **Completed** (✓).
- Tap na otvorenu arenu → `VenuePopup`.

### 6.3 `VenuePopup`
- Header teme, 3 kartice protivnika: portret, ime, `CPU`, opis, zvezdice, stanje (**Beaten ✓** / **Next** / **Locked** „Beat Lucky Lin first“), dugme **PLAY · Entry 50**.
- Boss kartica veća, zlatni okvir, oznaka `BOSS`.
- Ako nema za ulaz: dugme disabled sa „Need 30 more“ + mali link `+50 coins (ad)` ako je dnevni limit slobodan.

### 6.4 `NotEnoughCoinsPopup` (zamenjuje `NoEnergyPopup`)
„Entry is 200 coins. You have 120.“ Dugmad (redom):
- **WATCH AD · +50** (ako je limit slobodan)
- **PLAY BASEMENT BAR · FREE**
- Close

### 6.5 Meč — prikaz (`MatchSceneBuilder`, `MatchController` integracija)
1. **VS intro** (zamenjuje „COIN TOSS“ banner, ~2.0 s):
   - levo YOU (ikona), desno portret protivnika + ime + zvezdice, ime arene;
   - novčići ulaza „lete“ u pot u sredini, pa „WIN 120“;
   - u areni 1: „FREE · WIN 25“;
   - zatim postojeći coin flip („YOU BREAK“ / „DINA BREAKS“).
2. HUD: desni panel prikazuje **portret i ime protivnika** umesto „OPPONENT“. Banneri: „DINA'S TURN“.
3. Ulaz se plaća u `MatchController` pri ulasku u scenu (`PayEntry`). Forfeit / napušten meč = poraz bez dodatne kazne.
4. Potez bota: tajminzi u `BotProfile` skraćeni na ~6 s ukupno (Think ~2.0 ± 0.5, AimEnd ~3.8, PullEnd ~5.2, StrikeAt ~5.6). Protivnici sa „sporije misli“ +1.5 s.
   - **Tap bilo gde tokom „thinking“ faze ubrzava bota ×2.** Bez promena odluke, samo tajming.

### 6.6 `MatchResultPopup` (prerađen)
- **Pobeda:**
  - „YOU WIN!“, razlog;
  - nagrada count-up „+120“ i, ako ima, „FIRST WIN BONUS +100“ kao drugi red;
  - napredak arene (3 kružića, novi se popuni);
  - ako je otključan novi protivnik ili arena: kartica „NEW VENUE UNLOCKED: Riverside Club“ sa thumbnail-om.
  - Dugmad: **NEXT OPPONENT** (primarno ako postoji novi), **PLAY AGAIN**, **MENU**.
  - U areni 2+: **WATCH AD · DOUBLE** (`DoublePrize`, jednom po meču).
- **Poraz:** „YOU LOSE“, razlog, „Entry lost (−50)“ ili „No entry lost“ u areni 1. Dugmad: **REMATCH · Entry 50**, **MENU**. Ako nema za ulaz: REMATCH → `NotEnoughCoinsPopup`.
- **Tour završen:** posebna kartica „TOUR CHAMPION!“ + konfete.
- Sve novo-otključano mora biti očigledno: pečat + zvuk + haptic (koristi postojeće zvukove Purchase/Win).

### 6.7 `CuesPopup` (prerađen)
- 4 kartice: slika štapa, ime, tri stat trake sa tooltipom (**Aim Line**, **Release Window**, **Bar Speed**), stanje **Owned / SELECTED / BUY · 🪙 1200** (disabled + „Need 230 more“).
- Bez energije i refill-a.
- Ispod kartica malo „Better cues make the green zone wider and the bar slower.“

### 6.8 Fake door (M10)
- **`StarterPackPopup`:**
  - Prikazuje se na meniju posle `StarterPackAfterMatches` završenih mečeva; max 1× po sesiji, max `StarterPackMaxShows` ukupno; ne posle poraza od bosa (loš trenutak).
  - Sadržaj: render Pro štapa + „+1000 coins“, cena „$1.99“, dugme **BUY**, Close.
  - BUY → popup sadržaj se zameni: „The shop opens soon! Thanks for your interest ❤️“ + prekidač **Notify me** + OK.
  - **Ništa se ne naplaćuje, ništa se ne dodeljuje** (dodela bi iskrivila meru).
- **`ComingSoonPopup` (ONLINE):** „Online matches against real players are coming.“ + prekidač **Notify me** + OK.
- Eventi: `OnFakeDoor(FakeDoorId id, FakeDoorAction action)`, akcije `Shown`, `Tapped`, `NotifyOn`, `Closed`.

### 6.9 `SurveyPopup` (M11)
- Posle `SurveyAfterMatches` završenih mečeva, pri povratku u meni, jednom ukupno.
- 1) „How do you like the release bar?“ — 😀 😐 🙁.
- 2) „What would make it better?“ — chip izbor, više odgovora: *Spin / english*, *Online matches*, *More venues*, *Better graphics*, *Easier aiming*, *Faster opponents*, *Nothing — it's great*.
- **SEND** / **Skip**. Event `OnSurveyAnswered(int releaseBarScore, int missingMask)` ili `OnSurveySkipped`.

### 6.10 FTUE tok (M8)
```
Prvo pokretanje (TutorialDone = 0)
  → Match scena, Mode = Tutorial (gameplay dokument G10)
  → OnTutorialCompleted(skipped)
  → RulesPopup (RulesSeen = 0)
  → MatchSetup: VsBot, Rookie Ray, Basement Bar → prvi meč (ulaz 0)
  → rezultat → meni + coach mark na TOUR
Svako sledeće pokretanje → meni
```
- Rookie Ray je kalibrisan tako da prosečan igrač pobeđuje ~80%. Prvi meč treba da bude pobeda za većinu.
- Ako igrač preskoči tutorijal, i dalje ide na prvi meč (hintovi iz `HintService` pokrivaju osnove).

### 6.11 Settings
Postojeći popup + **HOW TO PLAY** (gameplay dokument) + **RULES** + verzija. Bez novih stavki.

---

## 7. Eventi (novi / izmenjeni; upiši u README tabelu)
| Event | Payload | Ko diže | Ko sluša |
|---|---|---|---|
| `OnPlayRequested` (izmenjen: igra `NextOpponent`) | — | `MainMenuView` | `MenuController` |
| `OnTourRequested` | — | `MainMenuView` | `MenuController` |
| `OnVenueRequested` | `string venueId` | `TourPopup` | `MenuController` |
| `OnOpponentPlayRequested` | `string opponentId` | `VenuePopup`, `MatchResultPopup` (Next/Rematch) | `MenuController` / `MatchController` |
| `OnNextOpponentRequested` | — | `MatchResultPopup` | `MatchController` |
| `OnRewardedAdRequested` | `RewardedPlacement` | `MatchResultPopup`, `NotEnoughCoinsPopup`, `VenuePopup` | `RewardedAdService` |
| `OnRewardedAdFinished` | `RewardedPlacement, bool rewarded` | `RewardedAdService` | `ProfileService` (dodela), popupi (osveži) |
| `OnFakeDoor` | `FakeDoorId, FakeDoorAction` | `StarterPackPopup`, `ComingSoonPopup`, `MainMenuView` | `ProfileService` (flagovi) |
| `OnSurveyAnswered` / `OnSurveySkipped` | `int score, int missingMask` / — | `SurveyPopup` | `ProfileService` |
| `OnOpponentBeaten` / `OnVenueUnlocked` / `OnTourCompleted` | `string id` | `ProfileService` (iz `ApplyMatchResult`) | `AudioManager`, `HapticsService` |
| `OnMatchStarted` (izmenjen) | `MatchStartInfo` + `OpponentName`, `OpponentPortrait`, `VenueName`, `Entry`, `Prize` | `MatchController` | HUD, `VsIntroView` |
| uklonjeni: `OnCueRefillRequested`, `OnTwoPlayersRequested` | | | |

**Za fazu 2 (analitika):** svi meta trenuci već imaju event iznad ili postojeći (`OnMatchStarted`, `OnMatchEnded`, `OnShotFired`, `OnTutorialStep`). Faza 2 dodaje jednog slušaoca (`AnalyticsService`) bez diranja ovih klasa. Mapiranje na listu iz `release_plan.md` §6 upiši u README kao tabelu „event → analytics event“ (bez implementacije).

---

## 8. Plan commitova (7)

### Commit 1 — `meta core: tour data, economy, profile v2, match integration`
**Obim:**
- `TourCatalog`, `OpponentDefinition`, `EconomyConfig`, prerađen `CueCatalog`/`CueDefinition`;
- 12 `Bot_<id>.asset`, `DataAssetBuilder`;
- `ProfileService` v2 + migracija;
- `MatchSetup` proširenje;
- `MatchController`: `PayEntry` / `ApplyMatchResult`, `BotProfile` i `VenueTheme` iz setup-a;
- uklanjanje energije (sekcija 3);
- privremeno: postojeće dugme PLAY igra `NextOpponent`; minimalni `NotEnoughCoinsPopup`.

**Verifikacija:**
- novi `EconomyChecks.All()`:
  - start 100;
  - ulaz se skida na startu;
  - nagrada + bonus samo prvi put;
  - poraz ne vraća ulaz;
  - arena 1 uvek igriva sa 0 novčića;
  - zaključan protivnik/arena odbijen;
  - kupovina bez novca odbijena;
  - select neposedovanog odbijen;
  - rewarded limit 3/dan i reset sledećeg dana (simuliran datum);
  - double prize jednom;
  - migracija v1 → v2 čisti profil;
  - napušten meč → `Losses+1`, bez dodatnog skidanja.
- `grep -rn "Energy\|Refill\|WinsToUnlock" Assets/_Scripts` prazan (osim komentara u README istoriji);
- meč protiv Rookie Ray-a do rezultata.

### Commit 2 — `venues: four venue themes and room variants`
**Obim:**
- 4 `VenueTheme` asseta (`Basement`, `Downtown`, `Riverside`, `Grand`) na infrastrukturi iz gameplay dokumenta:

| Tema | Čoja | Svetlo | Kape | Soba | Neon |
|---|---|---|---|---|---|
| Basement | zelena | toplo, prigušeno | mesing, izlizan | cigla + drvo, rack, bar stolica | „BAR“ crven |
| Downtown | plava | hladnije, fluorescentno | hrom | više stolova u pozadini, svetliji zidovi | „POOL“ plav |
| Riverside | tamnozelena | toplo-zlatno | mesing | tamno drvo, kožne fotelje, slike | „CLUB“ ćilibar |
| Grand | bordo | zlatno, sjajnije | hrom/zlato | lamperija, luster, tepih | „GRAND“ beo |

- `RoomPrefabBuilder` parametrizovan (jedan builder, 4 prefaba iz parametara, ne 4 kopije koda);
- thumbnail render svake teme (512×256) za Tour kartice;
- `Club.asset` iz gameplay dokumenta postaje `Riverside` ili ostaje kao default za tutorijal (odluči i zabeleži).

**Verifikacija:**
- screenshot Aim i Watch pogleda u svakoj temi (`m2_<tema>_aim.png`, `m2_<tema>_watch.png`);
- `CameraChecks` i dalje 100% (soba ne sme da uđe u fit);
- draw calls i trouglovi po temi u budžetu iz gameplay dokumenta G3 (prijavi).

### Commit 3 — `menu & tour: main menu, tour and venue popups, cues rework, portraits`
**Obim:**
- 6.1, 6.2, 6.3, 6.4, 6.7;
- portreti (P3 ili 5.4);
- `MenuController` tok;
- 3D sto u meniju u temi sledećeg protivnika.

**Verifikacija:**
- kroz `eval` postavi profile u stanja: nov igrač, sredina arene 2, završen tour, 0 novčića;
- screenshot menija, Tour-a, Venue-a i Cues-a za svako stanje;
- disabled i „Need X more“ stanja;
- grep slojeva.

### Commit 4 — `match presentation: vs intro, opponent identity, result popup, faster bot turns`
**Obim:** 6.5, 6.6, skraćeni tajminzi bota + tap-to-speed-up, `OnOpponentBeaten` / `OnVenueUnlocked` / `OnTourCompleted` feedback.
**Verifikacija:**
- ceo tok: Menu → PLAY → VS intro → meč (pobeda forsirana postojećim debug alatom „Set Up Winning Shot“) → rezultat sa nagradom + bonusom → NEXT OPPONENT → … → pobeda nad Big Sal-om → „NEW VENUE UNLOCKED“;
- poraz → REMATCH;
- screenshot svakog stanja popupa;
- izmeri prosečno trajanje poteza bota iz loga (cilj ~6 s).

**→ CHECKPOINT 1:** APK. Korisnik prolazi: prvi meč, napredak kroz arenu 1, otključavanje arene 2, kupovina štapa, osećaj tempa i težine. Čekaj potvrdu.

### Commit 5 — `ftue: first launch flow from tutorial to first match`
**Obim:** 6.10, coach mark na TOUR posle prvog meča.
**Verifikacija:**
- obriši profil kroz `eval` → pokretanje vodi u tutorijal → posle `OnTutorialCompleted` pravila → meč protiv Ray-a (ulaz 0) → meni sa coach mark-om;
- drugo pokretanje → meni;
- skip varijanta takođe vodi u prvi meč.

### Commit 6 — `monetization probes: simulated rewarded, starter pack and online fake doors, survey`
**Obim:** 5.3, 6.8, 6.9, `SimulatedAdOverlay`, eventi iz 7.
**Verifikacija:**
- rewarded `FreeCoins` 3× pa 4. odbijen; sledeći (simulirani) dan ponovo radi;
- `DoublePrize` duplira samo nagradu i samo jednom;
- prekid simuliranog ada → nema nagrade;
- Starter Pack se pojavljuje posle 3. meča, ne više od 1× po sesiji ni 3× ukupno, BUY ne menja novčiće ni štapove;
- ONLINE Notify se čuva;
- anketa posle 5. meča, jednom;
- screenshotovi.

### Commit 7 — `balance & build: arena balance, progression sim, README, APK`
**Obim:**
- `ArenaBalanceCheck.Run(200)` — po protivniku, dva referentna igrača (4.5);
- `ProgressionSim.Run(1000)` — Monte Carlo kroz tour sa verovatnoćama pobeda iz `ArenaBalanceCheck` (brzo, bez punih simulacija mečeva), prijavi medijane prekretnica iz 4.5;
- `MatchLengthCheck` — iz headless bot-vs-bot mečeva: broj poteza × tajminzi → medijana trajanja meča (cilj ≤ 6 min);
- tuning podataka do ciljeva (redosled iz 4.5), tabela pre/posle;
- README: meta sekcija, ekonomija, tabele 4.1–4.4, eventi 7, mapiranje za analitiku, upozorenje o simuliranim reklamama;
- `AndroidBuilder.Build()`, `aapt` provera, verzija **0.2.0**.

**Verifikacija:** svi checks; brojevi tempa i balansa u izveštaju; DoD (9).

**→ CHECKPOINT 2:** APK. Korisnik igra 30–60 min kao novi igrač (obrisan profil) i potvrđuje tempo, težinu i jasnoću menija.

---

## 9. Definition of Done
- [ ] M1 4 arene × 3 protivnika, otključavanje po pobedi nad prethodnim i bosom.
- [ ] M2 Ulaz na startu, nagrada + bonus za prvu pobedu, arena 1 uvek besplatna, nema energije (`EconomyChecks` prolazi).
- [ ] M3 4 štapa, samo cena, Owned/Selected/Buy, stat trake sa tooltipom.
- [ ] M4 Portret, ime, `CPU`, opis, zvezdice; `BotProfile` po protivniku.
- [ ] M5 4 vizuelno različite arene, u budžetu performansi.
- [ ] M6 Novi meni sa NEXT kartom i PLAY = sledeći protivnik; TOUR, CUES, ONLINE (soon), Settings.
- [ ] M7 VS intro, ime i portret protivnika u HUD-u, nov rezultat popup sa napretkom i otključavanjem.
- [ ] M8 FTUE: tutorijal → pravila → prvi meč protiv Ray-a → meni sa coach mark-om.
- [ ] M9 Simulirani rewarded (Double prize, +50 coins, limit 3/dan), jasno označen kao zamena za fazu 2.
- [ ] M10 Starter Pack i ONLINE fake door, bez naplate i bez dodele, sa Notify me.
- [ ] M11 Anketa posle 5. meča, jednom.
- [ ] M12 Potez bota ~6 s, tap ubrzava; medijana meča ≤ 6 min (`MatchLengthCheck`).
- [ ] Tempo i pobede u ciljevima iz 4.5 (ili odstupanja obrazložena i odobrena od korisnika).
- [ ] Fizika, pravila, `BotBrain`, kontrole, kamera nepromenjeni; svi stari checks prolaze.
- [ ] Grep provere slojeva prazne; README ažuriran (uključujući mapiranje evenata za analitiku).
- [ ] 7 commitova na `main`, pushovano; 2 checkpointa potvrđena.

---

## 10. Rizici
| Rizik | Šta uraditi |
|---|---|
| „Prosečan igrač“ 45/40/15 nije stvaran igrač | Ciljevi su polazna tačka; posle launcha prava distribucija iz analitike + Remote Config. Zato su svi brojevi u SO-ovima. |
| Bot sa visokim Green% deluje nepobedivo („cheating AI“) | Boss arene 4 max ~72% Green; prikaži zvezdice pre meča; poraz pokazuje razlog |
| Ekonomija se „pokvari“ (previše ili premalo novca) | `ProgressionSim` je gate; tuning samo podacima |
| Igrač bez novca zaglavljen | Arena 1 uvek besplatna + `FreeCoins` rewarded + jasne opcije u `NotEnoughCoinsPopup` |
| Fake door iritira | Max 1×/sesija, 3× ukupno, ne posle poraza; iskrena poruka |
| Simulirane reklame odu na store | DoD + README upozorenje + komentar `PHASE 2`; closed test tek posle faze 2 |
| 4 sobe = 4× sadržaja, prekoračen budžet performansi | Jedan parametrizovan builder, deljeni materijali, provera po temi u commitu 2 |
| Generisani portreti izgledaju jeftino | P3: korisnik može kasnije da ubaci prave PNG-ove; builder ih automatski preuzima |
| Tap-to-speed-up menja ishod bota | Menja samo tajminge (scaled time unutar `BotController`), ne odluku; `BotBalanceCheck` nepromenjen |
