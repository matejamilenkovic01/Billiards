# Release plan — Billiards (od prototipa do odluke posle release-a)

Napisano 2026-10-19. Vremenski okvir: **~12 nedelja × 20 h** (~240 h). Budžet: **500 € ukupno**.

---

## 0. Cilj, hipoteza, kriterijum uspeha

**Ciljevi (po prioritetu):** 1) učenje celog procesa + portfolio, 2) validacija mehanike, 3) ako prođe — nastavak razvoja.

**Hipoteza koju testiramo:**
> Igrači koji vole bilijar uživaju u first-person udarcu gde je veština i *trenutak puštanja* (release bar), ne samo nišan — dovoljno da se vrate sutra i sledeće nedelje, i bez PvP-a.

**Šta NE testiramo sada:** PvP, LTV, iOS, monetizaciju pravim novcem.

### 0.1 Metrike i pragovi (zaključavaju se PRE launch-a, ne posle)

Benchmark (GameAnalytics 2026, podaci 2025, sve igre): D1 medijana ~22%, gornjih 25% ~30%, gornjih 10% ~40%; D7 medijana ~4%, gornjih 25% 6–7%.

| Grupa | Metrika | GO | ITERATE | STOP / pivot |
|---|---|---|---|---|
| **Retention** (glavno) | D1 | ≥ 30% | 20–30% | < 20% |
| | D7 | ≥ 6% | 3–6% | < 3% |
| **FTUE** | tutorijal završen | ≥ 75% | 55–75% | < 55% |
| | prvi meč završen (ne napušten) | ≥ 60% | 40–60% | < 40% |
| **Signal mehanike** | mečeva po igraču na D0 | ≥ 3 | 2–3 | < 2 |
| | „Play Again“ posle meča | ≥ 50% | 30–50% | < 30% |
| | % PERFECT raste od meča 1 do meča 10 (igrač uči) | raste | ravno | — |
| | anketa „sviđa ti se release bar?“ pozitivno | ≥ 60% | 40–60% | < 40% |
| **Tehnika** (gate, ne opcija) | crash-free users | ≥ 99.5% | — | < 99% |
| | Android vitals: user-perceived crash / ANR | < 1.09% / < 0.47% (Google „bad behavior“ pragovi) | | |
| **Marketabilnost** | CPI na Filipinima | ≤ 0.40 $ | 0.40–0.80 $ | > 0.80 $ |
| **Namera plaćanja** (fake door) | tap na Starter Pack / videli ponudu | ≥ 3% | 1–3% | < 1% |
| | opt-in na rewarded ad (od DAU) | ≥ 20% | | |

Napomene:
- Pragovi za CPI, fake door i rewarded opt-in su **moja procena**, nisu benchmark. Ostali su vezani za izvore iznad.
- Plaćeni korisnici iz jeftinih zemalja obično imaju **niži** retention od globalnog proseka → D1 30% tamo je jak rezultat.
- **Odluka** = retention + bar 2 signala mehanike. Ako je retention u ITERATE, a signali mehanike dobri → problem je meta/sadržaj, ne mehanika → vredi iteracija.
- Uzorak: cilj **≥ 800 instalacija** u kohorti. Pri D1 ~30% greška je ~±3 p.p. (dovoljno za odluku, ne za fine A/B testove).

---

## 1. Ključne odluke (sa obrazloženjem)

| Tema | Odluka | Zašto / alternativa |
|---|---|---|
| Platforma | **Samo Android (Google Play)** | 25 $ jednokratno vs Apple 99 $/god; jeftiniji UA; build već postoji; igrači bilijara u ciljnim zemljama su pretežno na Androidu. iOS tek posle GO. |
| Tržište testa | **Filipini** (primarno), opciono **Indija** | Filipini: jaka bilijar kultura, engleski, jeftin CPI (orijentir za JI Aziju ~0.15–0.60 $ za casual Android). Indija: najveće tržište po downloadima i najjeftiniji CPI (~0.05–0.30 $), ali slabiji kvalitet saobraćaja. Prihod 8 Ball Pool-a je ~50% iz SAD (Sensor Tower, 2018) — SAD je kasnije tržište za monetizaciju, ne za test od 500 €. |
| Release zemlje | Production samo u test zemljama (PH, IN) | Čist signal, manje GDPR/consent komplikacija (EU). Serbija/prijatelji ostaju na closed test track-u. |
| Server | **Nema sopstvenog servera u MVP-u** | Firebase je „backend“: analitika, crash, Remote Config, A/B. Pravi server tek za PvP (sekcija 9). |
| Analitika | **Firebase** (Analytics + Crashlytics + Remote Config + A/B Testing) | Besplatno, nativno se vezuje za Google Ads (UA optimizacija), industrijski standard (dobro za portfolio). Alternativa: GameAnalytics (bolji gotovi game dashboardi + benchmark poređenje) — dodaj ga samo ako ti GA4 izveštaji budu muka. |
| Monetizacija u testu | **Rewarded ads (AdMob) + fake door** ponude | Meri namere bez pravih plaćanja. Pravi IAP (opcija d) tek posle GO — vidi 1.2. |
| PvP | Ne. **Botovi su AI likovi** (jasno označeni) | Tvoja deterministička fizika kasnije omogućava asinhroni PvP bez real-time servera. |
| 2 PLAYERS | **Briše se** | Razvodnjava metrike, nije deo hipoteze. |
| Stari sistem novčića/štapova/energije | **Briše se, novi dizajn** (sekcija 2) | Energija po štapu nije imala smisla bez pravog načina trošenja novčića. |
| Spin | **Ne u v1** | Vidi 1.3. |
| Nalog | **Lični (personal) Play Console** | Firma/preduzetnik nije potreban za test. Vidi 1.4 za porez i Nordeus. |

### 1.1 Šta je fake door
Dugme/ponuda za nešto što **još ne postoji**, da bi se izmerilo koliko ljudi to želi.
- Primer 1: posle 3. meča popup **„Starter Pack — Pro Cue + 1000 coins — $1.99“**. Tap na „Buy“ → poruka „Shop opens soon! Thanks for your interest ❤️“ — **nikad se ne naplaćuje**. Meriš `fake_door_shown` i `fake_door_tapped`.
- Primer 2: na meniju dugme **„ONLINE PvP — coming soon“** → tap → „Notify me“ prekidač. Meriš interes za PvP pre nego što potrošiš nedelje na njega.
- Rizik: deo igrača se razočara. Ublažavanje: prikaži ponudu najviše 1× po sesiji, poruka iskrena, odmah zatvori.
- Rizik politike: nisam našao pravilo Google Play-a koje eksplicitno zabranjuje fake door. Zato poruka mora odmah reći da kupovina ne postoji i ništa se ne naplaćuje. To ostaje procena, ne proverena činjenica.

### 1.2 Zašto ne pravi IAP (d) sada
- **Plus:** pravo plaćanje je najjači signal (jači od tapa na fake door). Srbija podržava merchant nalog (provereno). Google uzima 15% (na prvih 1M $/god).
- **Minus:** sa ~1000 korisnika na Filipinima i tipičnom stopom plaćanja <1% dobiješ **~3–10 kupovina** — statistički beznačajno.
  - Implementacija (Unity IAP + Play Billing + testiranje + refund slučajevi): ~2–4 dana.
  - Uz to ide administracija: payments profile, porez na prihod iz inostranstva.
- **Odluka:** fake door sada; pravi IAP je prvi korak faze posle GO.

### 1.3 Spin
„Spin“ u bilijaru = udaranje bele **van centra** (gore = follow, dole = draw/povlačenje, levo/desno = side). Time igrač pozicionira belu za sledeći udarac. 8 Ball Pool ga ima (krug za mesto udara). Ovo nije „Spin & Win“ točak sreće — to je druga stvar, takođe nije za v1.
- Za tvoju „skilled“ poziciju spin je dugoročno bitan (pozicioniranje bele je *prava* veština bilijara).
- Ali: menja fiziku (klizanje vs kotrljanje, efekat posle kontakta), predikciju, bota i UI. To je ~2–3 nedelje rada, a **ne testira release bar**.
- v1: bez spina. U anketi i feedbacku pitaj „šta fali?“. Ako bude GO, prvo dodaj samo **follow/draw** (najveći dobitak, najmanje posla).

### 1.4 Nordeus, nalog, porez — rizici koji blokiraju
1. **Ugovor o radu (Nordeus, deo Take-Two):** pre objave pročitaj klauzule o intelektualnoj svojini (IP), sporednim projektima i konkurenciji. **Traži pisanu saglasnost** (HR / lead) za lični nekomercijalni projekat.
   - Radi na svom računaru, u svoje vreme, bez koda, alata i podataka firme.
   - To što Top Eleven nije bilijar ne znači da klauzula ne pokriva „bilo koju igru“. Ne pretpostavljaj. **Ovo je blokator u Fazi 0.**
2. **Lični Play Console nalog** (25 $). Verifikacija identiteta može trajati nekoliko dana — otvori ga prve nedelje.
3. **Pravilo 12 testera / 14 dana:** novi lični nalozi moraju imati closed test sa **≥ 12 testera uključenih 14 dana zaredom** pre production pristupa.
   - Ovo potvrđuju third-party izvori; Google-ovu stranicu nisam direktno pročitao, proveri u Console-u.
   - Jedan izvor kaže da se odbrojavanje resetuje ako padne ispod 12 → **uključi 15–20 testera**.
4. **Novac:**
   - **Play prodaja:** payments profile (merchant) → isplata na devizni račun.
   - **AdMob:** isplata wire transferom, mesečno, kad stanje pređe prag (iznos vidiš u nalogu). U testu prag verovatno nećeš ni dostići.
   - **Porez:** prihod iz inostranstva kao fizičko lice prijavljuješ sam. **Pitaj knjigovođu pre prve isplate** (nisam proverio tačnu proceduru u Srbiji).
   - Preduzetnik (paušal) ima smisla tek kad prihod postane realan.
5. **Target API 36** (obavezno za nove app-ove od 31.08.2026 — provereno, developer.android.com). Posledica: na ekranima ≥ 600dp (tableti, foldovi) Android 16 **ignoriše zaključanu portrait orijentaciju**. Ili podrži landscape, ili isključi tablete u Play Console device catalog-u (lakše za v1).

---

## 2. Game design MVP-a (novi, od nule)

Princip: **najtanja verzija 8 Ball Pool petlje** (ulaznica → nagrada → napredak kroz arene → bolji štap), sa botovima umesto PvP-a.

### 2.1 Petlja
```
Meni → izaberi arenu → plati ulaznicu → meč protiv AI lika → pobeda = nagrada (2× ulaznica)
      ↑                                                              ↓
      └──── kupi bolji štap (lakši release) ←── novčići ←── pobedi bosa → otključaj sledeću arenu
```

### 2.2 Arene (Tour) — 4 lokacije, svaka 3 AI protivnika
| # | Arena | Ulaz | Nagrada | Protivnici (poslednji = boss) | Vizuelno |
|---|---|---|---|---|---|
| 1 | **Basement Bar** | 0 (besplatno) | 20 | Rookie Ray, Lucky Lin, **Big Sal** | toplo, prigušeno, neon |
| 2 | **Downtown Hall** | 50 | 100 | … | fluorescentno, više stolova |
| 3 | **Riverside Club** | 200 | 400 | … | drvo, kožne fotelje |
| 4 | **Grand Lounge** | 800 | 1600 | … , **The Professor** | zlatno svetlo, crvena čoja |

- Pobediš protivnika → otključan sledeći. Pobediš bosa → otključana sledeća arena. Pobeđene možeš ponovo igrati za novčiće.
- **Arena 1 je uvek besplatna** → nema softlocka bez novčića (zamenjuje „beskonačnu energiju“).
- Arena = isti sto, drugačija boja čoje, svetlo i dekor sobe. Jeftino jer već imaš builder-e za sobu.
- AI lik = `BotProfile` po liku: šanse release-a (Green/Yellow/Red), sklonost safety udarcima, brzina poteza.
  - Prikaz: ime, portret, „CPU“ oznaka, jedna rečenica karaktera. Bez pretvaranja da je pravi igrač.
- Bar jedan lik po areni ima „ličnost“ (npr. Big Sal uvek razbija punom snagom) → osećaj da su različiti.

### 2.3 Štapovi (4, kupuju se novčićima, **bez energije**)
| Štap | Cena | Release zona | Brzina bara | Linija pogođene kugle |
|---|---|---|---|---|
| House Cue | start | uska | brza | kratka |
| Club Cue | ~5 pobeda u areni 2 | srednja | srednja | srednja |
| Pro Cue | ~5 pobeda u areni 3 | šira | sporija | duža |
| Master Cue | ~5 pobeda u areni 4 | najšira | najsporija | najduža |

- Balans: boss arene 3/4 treba da bude **teško ali moguće** bez boljeg štapa. Štap pomaže, ne garantuje. Proveri `BotBalanceCheck`-om.
- Sve vrednosti idu u **Remote Config**, da se menjaju bez nove verzije.

### 2.4 Rewarded ads (AdMob)
- „**Double your prize**“ na popupu pobede (arene 2+).
- „**+50 coins**“ kad nemaš za ulaznicu (max 3/dan).
- Ništa interstitial (forsirane reklame) — kvari retention signal.

### 2.5 Fake door (vidi 1.1)
- Starter Pack posle 3. meča.
- „Online PvP — coming soon“ na meniju.

### 2.6 Ostalo u v1
- **Anketa u igri** posle 5. meča, 1 pitanje: „How do you like the release bar?“ 😀 😐 🙁 + opciono jedno slobodno polje „What's missing?“.
- **In-App Review** (Play API) posle 3. pobede.
- Settings: Sound, Music, Haptics, Privacy policy link, Send feedback (mailto), verzija.
- **Van v1:** dnevne nagrade, misije, leaderboard, battle pass, XP/leveli, spin, PvP, iOS.
  - Dnevna nagrada veštački diže D1/D7 i maskira signal mehanike. Ako bude GO, dodaj je kao A/B test.

---

## 3. Tutorijal (FTUE) — prva 2–3 minuta

Prvi start ide **direktno u tutorijal** (bez menija), u Basement Bar. Svaki korak je jedan event (`tutorial_step`).

| Korak | Postavka stola | Učiš | Pravilo |
|---|---|---|---|
| T1 | bela + 1 kugla ravno ispred rupe | **povuci štap dole = snaga**, pusti | release bar isključen (uvek zeleno) |
| T2 | kugla pod blagim uglom | **drag levo/desno = nišan**, ghost ball i linija | bar isključen |
| T3 | ista postavka kao T2 | **release bar**: spor bar, velika zelena zona. „Release on GREEN = perfectly straight.“ | ako pusti u crvenom: **uspori, prikaži nameravanu vs stvarnu putanju** („Released in red → ball went off line“), ponovi |
| T4 | rez pod većim uglom | sve zajedno + fine aim točak | normalan bar |
| → | **prvi meč** protiv Rookie Ray-a | pravila kroz kontekstualne hintove (prvi faul, ball in hand, dodela grupe, 8-ica) | Rookie Ray je namerno slab — prvi meč treba da bude pobeda za većinu |

**Polish release bara** (pre tutorijala, jer tutorijal uči ono što bar radi):
- **Izazov:** vertikalni bar gde „iznad zelenog = levo, ispod = desno“ nije intuitivan. Opcije:
  - (a) **horizontalni bar**: levo = kugla ide levo. Najintuitivnije, ali menja layout.
  - (b) vertikalni bar + strelica ←/→ na trenutnoj poziciji. Manja izmena.
- Posle svakog udarca kratko (0.5 s) prikaži **nameravanu liniju vs stvarnu**. Igrač vidi uzrok promašaja → nema osećaja „nepravedno“.
- Toast PERFECT / GOOD / MISS već postoji. Dodaj zvuk i haptic razliku između zona.
- Pre implementacije pusti 2–3 testera da probaju (a) i (b) prototip.

---

## 4. Ime i ASO (store optimizacija)

- **Izazov:**
  - „Realistic“ u nazivu obećava vizuelni nivo koji sada nemaš → loše recenzije („not realistic at all“). Tvoja prava prednost je **first-person + chill + skill**.
  - „8 Ball Pool“ je zaštićen naziv (Miniclip) — ne koristi tu tačnu frazu. „8 Ball“ i „Pool“ odvojeno koriste mnogi.
- Naslov na Google Play-u: max **30 znakova**; bez „#1“, „best“ i nabacivanja ključnih reči.
- Predlozi (proveri dostupnost pretragom na Play-u pre izbora):
  - `Cue Club: 3D Pool & Billiards` (29)
  - `Pool Hall 3D: 8 Ball Billiards` (30)
  - `First Shot: 3D Pool Billiards` (29) — nagoveštava first-person
  - `Chalk & Cue: 3D Pool` (20) — chill vibe, kraće; ključne reči ostaju za opis
- Kratak opis (80): `First-person 3D pool. Aim, pull back, release on green. Chill offline 8-ball.`
- Ključne reči u opisu: pool, billiards, 8 ball, 3D, first person, offline, cue, chill.
- **Store asseti:**
  - ikonica (render iz igre: vrh štapa + bela kugla, close-up)
  - 4–6 screenshotova sa tekstom („Aim like you're at the table“, „Release on green“, „Beat 12 AI rivals“)
  - **video od 15–30 s first-person udarca** — to je ceo pitch
- Posle launch-a: **Store listing experiments** u Play Console-u (besplatni A/B za ikonicu i screenshotove).

---

## 5. Tehnička stabilnost

**Pre release-a:**
1. **Uređaji:** S24 Ultra i S23 (high-end); stari Note = proxy za slabe telefone, koji su većina na Filipinima i u Indiji.
   - Cilj: stabilnih **60 FPS na S23**, **≥ 30 FPS bez pada na Note-u**.
   - Quality nivoi (senke, post-processing, MSAA) automatski po uređaju.
2. **Unity Profiler na uređaju** (Development build): CPU fizike u break-u, GC alokacije po frejmu (cilj ~0 u meču), memorija.
3. **Veličina AAB/download:** cilj < 80 MB (manji download = bolja konverzija u zemljama sa skupim internetom).
4. **Play Console pre-launch report** (automatski kad uploaduješ na test track): crashovi i screenshotovi na desetinama uređaja, besplatno.
5. **Closed test (14 dana, 15–20 ljudi)** = i stabilnost i kvalitativni feedback. Crashlytics prati sve.
6. **Checklista pre production-a:** 0 poznatih crashova, pause/resume (poziv, notifikacija), gubitak interneta (reklame ne smeju da blokiraju), prekid usred meča, prvi start bez interneta, mali ekran, ekran sa notch-om.

**Posle release-a (dnevno prve 2 nedelje, ~10 min):**
- **Crashlytics:** crash-free users, novi problemi.
- **Play Console → Android vitals:** crash i ANR stopa. Iznad praga Google smanjuje vidljivost u store-u.
- **Firebase Analytics:** instalacije, funnel tutorijala, D1.
- **Google Ads:** CPI, potrošnja.
- **Recenzije:** odgovaraj svima. Recenzije su besplatan kvalitativni feedback.

---

## 6. Analitika — event plan

| Event | Parametri |
|---|---|
| `tutorial_step` | step (1–4), result |
| `tutorial_complete` | duration_s |
| `match_start` | arena, opponent, cue, match_index (koji meč igrača po redu) |
| `shot` | zone (perfect/good/miss), power, is_break, pocketed_own, foul |
| `match_end` | result, reason, duration_s, shots, perfect_pct, fouls, match_index |
| `play_again_tap` / `menu_tap` | posle meča |
| `forfeit` | arena, turn_count |
| `arena_unlocked` / `opponent_beaten` | arena, opponent |
| `cue_purchased` | cue, coins_before |
| `rewarded_offer` | placement, action (shown/tapped/completed/failed) |
| `fake_door` | id (starter_pack/pvp), action (shown/tapped/notify_on) |
| `survey_answer` | q, value |

- **User properties:** `current_arena`, `best_cue`, `total_wins`.
- **Remote Config parametri:** šanse release-a po AI liku, ekonomija (ulaz/nagrada/cene), brzina bara i zone po štapu, tutorial varijanta, uslovi za fake door.
- **Retention** se gleda u GA4 Cohort exploration. **Funnel** tutorijala u Funnel exploration.
- Opciono: BigQuery export (besplatna kvota) za sopstvene upite — dobro za portfolio.

---

## 7. Budžet (500 €)

| Stavka | € | Napomena |
|---|---|---|
| Google Play Console nalog | ~23 | 25 $ jednokratno |
| Art | 0–50 | Prvo **besplatno**: Poly Haven (CC0 PBR drvo/čoja/HDRI). Kupuj samo ako screenshotovi i dalje izgledaju „prototipski“ (npr. paket modela stola/štapova ili portreti likova). |
| **UA — glavna kohorta** | **300** | Google Ads App campaign, Filipini, ~15 €/dan × ~20 dana. Orijentir: 650–1500 instalacija pri CPI 0.20–0.45 $ (pretpostavka; tvoj stvarni CPI je prvi rezultat testa). |
| **UA — druga kohorta (iteracija)** | 80 | Posle prve analize: verzija sa popravkama ili A/B (npr. brzina bara), ~1 nedelja. |
| Rezerva | ~47 | Nepredviđeno: dodatni asset, UA za drugu zemlju, porez/naknade banke. |
| Privacy policy / sajt | 0 | GitHub Pages ili Google Sites |
| Firebase, AdMob, Crashlytics | 0 | besplatni nivoi su dovoljni |

**Zašto Google Ads, a ne Meta/TikTok:**
- Google Ads se nativno vezuje za Play i Firebase (atribucija instalacija bez plaćenog MMP alata) i optimizuje i na in-app evente (npr. `tutorial_complete`).
- Meta traži dodatni SDK i setup.
- **Besplatan UA paralelno:** kratki first-person klipovi na TikTok / YouTube Shorts / Reddit (r/AndroidGaming, r/billiards). Dobro i za portfolio (devlog).

---

## 8. Plan po fazama

### Faza 0 — Osnova (nedelja 1, ~15 h)
- [ ] **Pisana saglasnost Nordeus-a** (blokator za javnu objavu; razvoj može da teče paralelno).
- [ ] Otvori Play Console (lični) + verifikacija identiteta.
- [ ] Firebase projekat, AdMob nalog (bez povezivanja aplikacije dok ne postoji na Play-u).
- [ ] Pregled konkurencije: 10 najboljih pool igara na Play-u (ocena, broj downloada, šta kritikuju u 1–2★ recenzijama, ima li neka first-person + timing). 1–2 h. Potvrdi da je diferencijacija stvarna.
- [ ] Izbor imena (proveri da nije zauzeto na Play-u).
- [ ] Zaključaj pragove iz 0.1 (ovaj dokument).
- [ ] Pronađi i javi se 15–20 closed testera (Nordeus kolege ne mogu biti glavna grupa ako saglasnost to ne pokriva).

### Faza 1 — Gameplay polish + novi meta (nedelje 2–5, ~80 h)
- [ ] Ukloni 2 PLAYERS, stari CueCatalog/energiju/ekonomiju.
- [ ] Release bar polish (3) — prototip (a) vs (b), 2–3 testera biraju.
- [ ] Shot feel: zvuk/haptic po zoni, prikaz namerena vs stvarna putanja.
- [ ] Arene + AI likovi + nova ekonomija + 4 štapa (2.2–2.3), balans kroz `BotBalanceCheck`/`EconomyChecks`.
- [ ] Vizuelni upgrade (Poly Haven teksture, svetlo po areni), meni redizajn sa izborom arene.
- [ ] Tutorijal (3).
- [ ] Target API 36, **AAB**, **upload keystore** (backup na 2 mesta!), Play App Signing, isključi tablete ili podrži landscape.
- [ ] Performance pass na starom Note-u (5).

### Faza 2 — Instrumentacija, reklame, compliance (nedelja 6, ~25 h)
- [ ] Firebase Analytics (eventi iz 6), Crashlytics, Remote Config (+ default vrednosti u kodu).
- [ ] AdMob rewarded (test ad unit-i dok si u razvoju!) + fake door + anketa + In-App Review.
- [ ] Privacy policy (navedi Firebase i AdMob), Play **Data safety** forma, **Content rating** (IARC), **Target audience: 18+** (izbegava Families politiku i ograničenja reklama za decu), Ads declaration.
- [ ] Debug meni za testiranje: reset profila, dodaj novčiće, preskoči tutorijal.

### Faza 3 — Closed test (nedelje 6–8, 14+ dana, paralelno sa polishom)
- [ ] Internal testing track (ti i 2–3 ljudi) → pa **closed testing** sa 15–20 testera.
- [ ] Pre-launch report → popravi crashove.
- [ ] Prati sve kroz Firebase. Retention prijatelja **nije** pravi signal, ali funnel tutorijala i crash jesu.
- [ ] Kratak razgovor (5 min) sa 5 testera: gde su zapeli, šta im je dosadno, šta fali.
- [ ] Store listing: ikonica, screenshotovi, video, opis (4).
- [ ] Posle 14 dana → **prijava za production access** (Google pregleda; može potrajati nekoliko dana).

### Faza 4 — Launch (nedelja 9)
- [ ] Production release samo u PH (+ IN ako odlučimo).
- [ ] Proveri da Firebase eventi stižu iz production builda (DebugView).
- [ ] Poveži AdMob sa aplikacijom na Play-u.
- [ ] Google Ads App campaign: Filipini, cilj = instalacije (prvih ~5 dana), 15 €/dan, 3–5 kreativa (video first-person udarca je glavni).

### Faza 5 — Merenje (nedelje 9–11)
- [ ] Dnevno 10 min po checklisti iz 5. Ne menjaj igru prve 2 nedelje osim crash fixeva — kvari kohortu.
- [ ] Posle ~5 dana: ako CPI > 0.80 $ → promeni kreative, ne igru.
- [ ] Gasi kampanju na ~800–1000 instalacija ili 300 €.
- [ ] Čekaj D7 poslednje kohorte.

### Faza 6 — Odluka (nedelja 12)
- [ ] Popuni tabelu 0.1 stvarnim brojevima.
- [ ] **GO** → Faza 7.
- [ ] **ITERATE** → jedna iteracija (najveći pad u funnelu ili A/B kroz Remote Config) + druga kohorta (80 €), pa ponovna odluka.
- [ ] **STOP** → piši case study.
- [ ] U svakom slučaju: **portfolio writeup** — problem, hipoteza, šta je izgrađeno, metrike, šta si naučio. To je glavni cilj #1, i vredi i uz negativan rezultat.

### Faza 7 — Posle GO (okvirno, planiramo tada)
1. Pravi IAP (starter pack, štapovi), merchant nalog, knjigovođa / preduzetnik ako prihod raste.
2. Follow/draw spin.
3. Dnevne nagrade / misije (A/B).
4. **Asinhroni PvP** (sekcija 9).
5. iOS (99 $/god, Mac imaš).
6. Više zemalja (SAD/UK za monetizaciju — skuplji UA, potreban dokazan ARPDAU).
7. Tek tada **LTV**: ARPDAU × lifetime (suma retention krive).

---

## 9. Backend — kad i kako (za kasnije)

**Sada:** Firebase je dovoljan. Bez servera ne moraš da brineš o hostingu, skaliranju, cenama, bezbednosti i varanju.

**Kad dođe PvP — tvoja prednost:** fizika (`Assets/_Scripts/Pool`) je čist C# bez Unity scene i deterministička. Udarac = (pravac, snaga, greška). Zato:
- **Asinhroni PvP** (potezi kao u šahu preko interneta, igrač ne mora biti online): klijent šalje udarac, server ga **sam simulira** istim C# kodom i vraća rezultat → server je autoritativan, varanje je teško.
- Mali broj korisnika nije problem (nema čekanja na matchmaking u realnom vremenu). Može i protiv „ghost“ snimaka pravih igrača.
- Napomena: float rezultati mogu blago da se razlikuju između ARM telefona i x64 servera. Zato je server uvek autoritativan, a klijent samo prikazuje njegov rezultat.

**Opcije:**
| Opcija | Jezik | Hosting | Za | Protiv |
|---|---|---|---|---|
| **Unity Gaming Services** (Cloud Code, Cloud Save, Economy, Leaderboards) | **C#** — isti Pool kod radi na serveru | Unity (managed), besplatni nivo | isti jezik i alat, integrisano u Unity | vezan za Unity platformu |
| **Firebase** (Firestore + Cloud Functions) | TypeScript | Google (serverless) | već ga koristiš, jeftino, odlično za async | fizika bi morala da se prepiše u TS ili server ne simulira |
| **Nakama** (open source game server) | Go/TS/Lua | sopstveni VPS (~5–10 €/mes) ili Heroic Cloud | sve (real-time, matchmaking, chat) | najviše operativnog rada |
| PlayFab | C# (Azure Functions) | Microsoft | zreo LiveOps | kompleksniji setup |

**Preporuka za kasnije:** UGS Cloud Code (C#), jer se tvoj `Pool/` kod može direktno koristiti na serveru.

**Kako AI pomaže:** piše Cloud Code funkcije, šemu podataka, testove i simulaciju opterećenja; objašnjava koncepte dok učiš. Ti moraš da razumeš i pregledaš:
- **bezbednost** (šta klijent sme da pošalje, validacija)
- **troškove** (limiti besplatnog nivoa, alerti na potrošnju)
- **migracije podataka**

AI greši najčešće baš tu: preširoke dozvole i nepostojeća validacija.

---

## 10. Najveći rizici

| Rizik | Verovatnoća | Ublažavanje |
|---|---|---|
| Nordeus ugovor ne dozvoljava objavu | srednja | Faza 0, pisana saglasnost pre objave |
| Loš retention zbog nedostatka PvP-a, ne mehanike | visoka | merimo signale mehanike odvojeno (0.1) |
| CPI veći od očekivanog → premalo instalacija | srednja | Indija kao jeftinija rezerva, promena kreativa, besplatni UA kanali |
| Release bar deluje „nepravedno“ | srednja | prikaz namerena vs stvarna putanja, anketa |
| Prototipski vizual → loša konverzija store stranice | srednja | Poly Haven upgrade, video first-person udarca, Store listing experiments |
| Slabi telefoni u PH/IN → loš FPS i crashovi | srednja | Note kao proxy, quality nivoi, pre-launch report |
| Kašnjenje zbog 14-dnevnog closed testa | sigurno | počinje čim je build stabilan; polish ide paralelno |

---

## Izvori
- Pravilo 12 testera / 14 dana (third-party, proveriti u Console-u): [testerscommunity.com](https://www.testerscommunity.com/blog/google-play-closed-testing-requirements-2026), [extendsclass.com](https://extendsclass.com/blog/google-plays-closed-testing-requirement-what-developers-need-to-know-in-2026)
- Target API 36 od 31.08.2026: [developer.android.com](https://developer.android.com/google/play/requirements/target-sdk)
- Srbija — developer i merchant registracija podržane: [Google Play Console Help](https://support.google.com/googleplay/android-developer/answer/9306917)
- Retention benchmarkovi: [GameAnalytics 2026 (gamedevreports)](https://gamedevreports.substack.com/p/gameanalytics-mobile-and-pc-game), [GameAnalytics 2025](https://gamedevreports.substack.com/p/gameanalytics-mobile-gaming-benchmarks)
- 8 Ball Pool prihod po zemljama (2018): [Sensor Tower](https://sensortower.com/blog/8-ball-pool-revenue); udeo Miniclip-a u pool igrama: [Guinness](https://www.guinnessworldrecords.com/world-records/662271-most-downloaded-8-ball-pool-mobile-game)
- CPI orijentiri: [FoxData 2026](https://foxdata.com/en/blogs/2026-mobile-game-user-acquisition-cost-benchmarks-how-much-should-you-spend/), [Linkrunner](https://linkrunner.io/tools/cost-per-install-benchmark), [Business of Apps](https://www.businessofapps.com/ads/cpi/research/cost-per-install/)
- AdMob isplate: [AdMob Help](https://support.google.com/admob/answer/2772140)
