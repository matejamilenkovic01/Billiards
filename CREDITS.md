# Credits

## Kenney (CC0 1.0) — https://kenney.nl
Only the files used by the game are copied into the project; each pack folder keeps its `License.txt`.

| Pack | Used for | In project |
|---|---|---|
| [UI Pack](https://kenney.nl/assets/ui-pack) | buttons, slider, panel, Kenney Future font | `Assets/_Art/Kenney/UIPack` |
| [Game Icons](https://kenney.nl/assets/game-icons) | pause, settings, home, lock, star, trophy, player icons | `Assets/_Art/Kenney/GameIcons` |
| [UI Audio](https://kenney.nl/assets/ui-audio) | button click | `Assets/_Audio/Kenney/UIAudio` |
| [Interface Sounds](https://kenney.nl/assets/interface-sounds) | popup, whoosh, toast, error, turn start, purchase, perfect ding, coin tick, combo chime (bong) | `Assets/_Audio/Kenney/InterfaceSounds` |
| [Impact Sounds](https://kenney.nl/assets/impact-sounds) | placeholders, pitched for billiards (v3 V7): ball click (plate light ×5), cushion (soft medium ×5), cue hit soft/hard (wood light/medium ×5), break (plate heavy ×3), pocket drop (wood heavy ×3), pocket rattle (plank medium ×3) | `Assets/_Audio/Kenney/ImpactSounds` |
| [Digital Audio](https://kenney.nl/assets/digital-audio) | foul | `Assets/_Audio/Kenney/DigitalAudio` |
| [Music Jingles](https://kenney.nl/assets/music-jingles) | win / lose jingles (Sax 02 / Sax 01) | `Assets/_Audio/Kenney/MusicJingles` |

## Poly Haven (CC0 1.0) — https://polyhaven.com
1K textures, downloaded to `_kenney_downloads/polyhaven/` and processed by `PolyHavenBuilder` (only the albedo,
OpenGL normal and a smoothness map from the roughness are copied into the project).

| Texture | Used for | In project |
|---|---|---|
| [Lacquered Cherry Wood](https://polyhaven.com/a/lacquered_cherry_wood) | table rails | `Assets/_Art/PolyHaven/lacquered_cherry_wood` |
| [Scuba Suede](https://polyhaven.com/a/scuba_suede) | cloth and cushions (made grey, tinted by the venue) | `Assets/_Art/PolyHaven/scuba_suede` |
| [Brown Leather](https://polyhaven.com/a/brown_leather) | pocket liners (with a baked depth gradient) | `Assets/_Art/PolyHaven/brown_leather` |

## Music
- **"Coffee House Bump"** by **oatcog** — https://opengameart.org/content/coffee-house-bump — **CC0**.
  File: `Assets/_Audio/Music/coffee_house_bump.wav` (original `little_cafe_0.wav`, unmodified).

## Generated
The ball-roll and room-ambience loops are synthesised by `Assets/_Scripts/Editor/Builders/GeneratedAudio.cs` (placeholders).
Real billiard recordings dropped into `_kenney_downloads/billiard_sfx/` (names in the gameplay doc, P2) replace the
placeholders on the next Systems Prefab build (`SystemsPrefabBuilder.Sounds`).

Ball textures and icons, cloth and wood, cue art, coin / energy / hand / eye icons and UI shapes are generated
by `Assets/_Scripts/Editor/Builders/GeneratedArtBuilder.cs`.
