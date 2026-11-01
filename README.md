# Chai Dhaba Tycoon

Walk-and-serve tycoon for CrazyGames, built in Unity 6.3 (URP) from the Game Design Document.
This milestone is the **Stage 1 vertical slice (Roadside Stall, 0:00–~11:00)**. The GDD says to get
this part right before anything else, because it's what players judge the game on in the first minutes
(CrazyGames' Basic Launch measures it).

## Run it

1. Open `Assets/ChaiTycoon/Scenes/Main.unity` and press Play. The whole game builds itself from
   code; the scene only holds a camera, a light and the `Game` object.
2. Controls: a floating joystick for mouse and touch alike (press on empty ground, drag to steer, keep
   holding to keep walking, even without moving the pointer), or WASD / arrow keys. Everything else happens by walking onto zones.
3. Editor-only debug keys: **F9** gives +1000 cash and **F10** toggles 4× speed.
   There is no Reset save button any more; to start over in the editor, clear PlayerPrefs (Edit › Clear All PlayerPrefs).

## What's in the slice

| GDD area | Status |
|---|---|---|
| Core loop: pick up → carry stack (wobbles) → serve → cash pile → collect | Done |
| Counter trays: stand still at a counter for 0.6 s (a disc fills in the floor ring) and whatever you carry goes on the trays, customer or no customer (8 per lane, two rows). The customer at the front is served first, and what the customer stepping up right now ordered is handed over instead. The trays are shared along the counter: a customer at any lane is handed a matching item from the nearest tray while Rafi or a waiter stands at their lane. Only Rafi puts things down; staff just hand food over | Done |
| HUD inventory: bottom-left card with the stack fill (3/4, FULL in red) and one slot per carried item with a count; it bumps on every pick-up and hand-over | Done |
| Hotspots react: floor rings for stations, counters, rickshaw spots, tables and the till breathe while idle, and pop, grow and light up (with a puff and a tick sound) when Rafi steps on them | Done |
| Order bubbles: compact, and they turn see-through while Rafi or a helper is behind them. Queue: the front customer stands a step back from the counter, the rest 1 m apart | Done |
| UI feel: buttons grow under the mouse, squash on press and spring back with a bounce; they spring in when they appear (staggered when a panel opens); panels shrink and fade when closed; red dots pulse; Upgrades and Staff wiggle when something can be bought | Done |
| Stations cook only while Rafi or a helper stands on their floor ring (the ring fills as each item cooks; step off and the half-cooked one waits). Cooked items go onto the tray beside the station and straight into the hands of whoever stands there. FULL tag, progress ring, batch fryer | Done: kettle, tawa, samosa fryer |
| Customers: walk-in, student (bicycle), rickshaw driver (parks at the stand) | Done |
| Patience ring, fast-serve tip, lost sale without a fail state, queue cap and roadside wait | Done |
| Unlock tiles: drain cash, ring fill, partial payments saved, camera pan. The current level's tasks decide which tiles appear | Done |
| All 11 Stage 1 unlocks, including two dining tables, the canopy (+1 star) and the expansion tile | Done |
| Dine-in tables: seats set the capacity. Guests sit and order, you walk up to drop food on their plates, and they eat, pay at the table and leave. Standing still on the table's ring for 0.6 s puts down whatever you carry that nobody there can take right now, guests or no guests (6 spots on the table); guests who sit down and order it take it themselves | Done (2 tables × 4 seats) |
| Staff: cashier (auto-collects counter cash) and waiter (Auto mode or an assigned item; stands at a station to cook and waits there when idle, so it keeps cooking ahead; also serves tables). Helpers pick a station nobody else is standing at | Done |
| Cooks: once the first helper is hired, a "Hire cook" button floats over each station without a cook (90 cash, then x1.6 per cook). The cook walks round to stand behind that station and keeps it cooking on its own, so stations automate one by one. Helpers: up to 3 waiters, the 2nd and 3rd hired from the Staff panel (260, then x1.9); they pick up from the stations and serve the counter and tables, each at a different station. The cashier collects the counter cash | Done |
| Upgrades: Rafi, Counter and Stations tabs (cost = base × growth^n × location multiplier) | Done |
| Levels (replacing missions): a HUD level card with 2–4 tasks per level, progress bars, auto-complete, a Level Up banner, and cash/leaf/cosmetic rewards | Done (10 levels for Stage 1) |
| Tea leaves: Recipe book perks and cosmetics shop (caps, kurtas, aprons, decorations) | Done |
| Stars, toasts, red-dot badges, rewarded offers (2× cash, top-up, free upgrade, leaf bundle, free cook) | Done |
| Guided FTUE: hint panel, floor arrow, camera pans, a pointing hand for UI steps (Upgrades, Staff, leaves, offers), one-time tips for each new feature | Done |
| Expansion celebration and world map | Done (stops 2–5 show "Opening soon") |
| Save (`cdt_save_v1` + `cdt_save_bak`, try/catch, versioned) and pausing while panels are open | Done |
| CrazyGames SDK bridge (`GameplayStart/Stop`, midgame and rewarded ads, Data module saves) | Done; tested in a browser on localhost, not yet on crazygames.com |
| Audio: every SFX and the tabla/rabab music loop are synthesised at startup (no audio files) | Done |

## Art

The models are made in Blender and match the concept art: warm low-poly with truck-art colours.
Every prop and the environment use Unity's standard URP Lit shader with real sun shadows, not a
custom shader. Your Tripo prop kits show their own textures (colour, normal and metal/roughness
maps); the props modelled in Blender keep their exact material colours through a small palette
texture. Rafi, the helpers and the customers are your Tripo crowd characters (see Characters). The
food items they carry still use the game's toon shader. The HUD and panels use your painted UI from the
mockups (see UI); icons it doesn't have are generated at startup or rendered from the 3D models.

- **Source**: `Art/Blender/ChaiTycoon_Stage1.blend` holds all five stages side by side (stage N at
  x = 120 × (N − 1)), each with its crowd, plus a `PROPS` library (one collection per prop, at the
  origin, front facing −Y, real size in metres) and a `CHARS` library: Rafi, the six staff roles and
  22 customer looks (walk-ins, students, rickshaw drivers, families, children, truck crew, the
  critic, tourists). Characters are about 1,060 triangles each (GDD: 600–1,200).
- **Your prop kits** (the three Tripo FBX kits, now in `D:\Downloads_from_C\DDD\Props\P`) are built by
  `Art/Blender/kit_props.py`: each kit is cut into items, levelled, scaled to metres and reduced to
  700–4,000 triangles while keeping the kit's own UVs, so the props use the kits' actual texture sets
  (copied to `Art/Blender/Textures/KitStalls`, `KitStreet` and `KitWood`). They are the `PROP_Kit*`
  collections:
  - Stations: `KitChaiStation`, `KitTawaStation`, `KitKarahiStation`, `KitMatkaStation` and
    `KitDegStation` are your stalls with a plain side table added, where the cooked stock stacks
    2 × 2. The stall tops are full of kettles and pans, so the stock needs its own spot.
  - `KitDiningSet` is your truck-art table between two charpai benches, at the seat spacing the game
    uses. `KitCounter` is the serving counter: two of your truck-art tables end to end per lane, raised
    to counter height (0.85 m); food left at the counter sits on the table tops. `KitTill` is one truck-art
    table as the cash desk, and `KitMenuBoard` is the street kit's sign board (cup and "CHAI", 1.3 × 1.6 m)
    used for every menu board. Its garbled second line of lettering is painted over with the board's own wood:
    the board face is a flat panel with a straight-on capture of the kit's board (`Textures/KitMenuBoard.png`,
    unedited capture in `KitMenuBoard_panel_raw.png`); frame, brackets and legs keep the kit texture.
  - `KitCanopy` (the "shade for all" unlock) is a row of five of your picnic-set parasols, 3 m across, behind
    the stations; `KitQueueBarrier` is the metal crowd barrier from your urban waste items set
    (`D:Downloads_from_CDDDGTA`), levelled and straightened, two 1.3 m sections either side of each
    queue lane. Both are built by `build_canopy()` and `build_queue_barrier()` in `kit_props.py`.
  - Decor: `KitStreetLamp`, `KitBarrier`, `KitTrashBin`, `KitPlanter`, `KitPlasticChair`,
    `KitPicnicUmbrella`, `KitTableStools` and `KitTruckArtTable`.
  - Changed from the kits: the charpai's thin woven strands fall apart into spikes when reduced, so
    the charpai is clean low-poly geometry with your charpai's texture baked onto it from the kit's
    texture (`Textures/KitCharpai.png`). The plastic chair's missing right side was mirrored from its
    left. The table-and-stools set has its fourth stool straightened and is scaled to the street
    tables' footprint.
  - Where they're used: every stage's kettles, tawas, fryers, handi and blender stations, Stage 1–2
    tables, Stage 4 street tables and the lamp posts. New decor was added to all five stages, for
    example charpais and a picnic umbrella at the highway dhaba, barriers along the truck bays, and
    bins and plastic chairs in the food street.
- **Stage 1 layout and the "Latest Props" kits** (from `Downloads/Latest Props`, unzipped to
  `Art/Source/LatestProps`): `Art/Blender/kit_latest.py` cuts the five kits (assorted kitchen, colourful
  kitchen, miniature kitchen + counter, stone walls, nature) into 30 items and builds the stage 1 pieces from
  them, following the reference picture: the painted stoves (kettle, tawa, fryer) are the stations, the long
  painted counter is the serving counter, the cash register sits on a short counter piece as the till, the
  teal charpai is the dine-in table (guests sit on its edges, food goes on the weave), the tuk-tuk replaces the
  rickshaw everywhere (the rickshaw stand has no building: building it opens the two parking spots at the road
  edge, and the drivers' cash drops on the ground beside them), and stone walls, the
  fridge, water drums, gas cylinders, sink, bucket, rug racks, trees, bushes, rocks and grass dress the yard.
  The yard is open sand (`Textures/GroundSand`, a generated tiling sand texture and normal map, 6 m per tile)
  instead of grass, and the queue has no barriers, as in the reference. Stations sit in one row along the
  back wall, the counter (two lanes) with the till at its right end in the middle, the charpais on the right.
  The counter stands 1 m nearer the road than the first layout (`Location.CounterZ` = -0.8; lanes, cash pile,
  queue and staff posts follow it), so the cooking row and the counter have room between them. Floor rings
  are 1.3-1.35 m across (1.2 m for staff); unlock tiles are 1.4 m squares (the expansion 2.2 m).
- **Lighting and camera (stage 1)**: real soft sun shadows from the back left (shadows fall towards the
  bottom right, as in the reference), screen-space ambient occlusion on the web renderer
  (`Settings/Mobile_Renderer`, which the Web build uses; shadows 2048 px, 2 cascades, soft) and a warm
  afternoon colour grade (`Visual/PostFX.cs`: neutral tonemapping, slight warm balance, soft bloom, light
  vignette). The camera follows Rafi at a fixed 50° angle, 18.4 m back (20% closer than before), stopping only at the yard's edges. Floor rings
  have name pills ("Serve", "Table", "Rickshaw") from `WorldUI.RingLabel`; the stations have none (they landed on the counter tops). A station's stock count (or the FULL tag) sits just over its side table and its Hire-cook button on the stove front; no world widget goes under the top HUD strip.
  The hero props keep more detail (stoves about 21k triangles, counter 12k, sink 9k); textures import at
  most 1024 px (colour, metal/smoothness) and 256 px (normal maps) with high-quality crunch, for the web download.
- **Export**: run `Art/Blender/export_art.py` in Blender. It writes `Resources/Art/art_core.bytes`
  (stage 1 props, every character, the stage 1 environment and layout; 2.9 MB raw) and
  `art_stage2..5.bytes` (0.3–0.8 MB each, loaded only when needed; currently not exported, see Stages 2–5), plus the textures in
  `Resources/Art/Tex/`: each kit's `_Base`, `_Normal` and `_MetalSmooth` maps (metallic plus smoothness
  in alpha, made from the kit's metallic and roughness maps) and `Palette.png` (one texel per plain
  colour). `Editor/ArtTexImport.cs` sets the import (colour maps up to 2048 px, the rest 1024, crunch
  compressed; the palette sharp and uncompressed), and `Editor/ArtMaterials.cs` makes the URP Lit
  materials in `Resources/Art/Mats` (also **Chai › Art › Rebuild Materials**). Keep `EXACT_BEVELS` in
  the exporter on to keep Blender's bevels, or turn it off for packs about a quarter smaller. A
  `game_meta` list on a prop collection is exported with the mesh; the stations use it for the stock
  tray and steam positions.
- **In game**: `ArtLib` reads the packs and builds meshes on first use: props and environment with
  Blender's own normals, UVs and one submesh per material; characters as flat-shaded toon meshes.
  Wherever art exists, Stage 1 uses it: environment, stations (your kit stalls), counters, till, tables
  (the kit dining set), canopy, rickshaw stand, queue posts, vehicles, decor and every character. Kit
  decor in the yard gets a collider, so Rafi walks around it. Each stage's mood sets the sun colour, a
  sky/horizon/ground ambient light and soft sun shadows. Without the packs the game falls back to the
  procedural models.
- **Characters**: your crowd (`Assets/Crowd`: Rafi, Helper, Cook, Customer1–4) replaces the low-poly
  people. The textures looked distorted because the normal maps came in as colour textures and the FBX
  made its own materials. The models are re-exported from Blender, reduced to about 4,000 triangles with
  1024 px colour and normal maps (normal maps import at 256 px), to `Assets/ChaiTycoon/Crowd/<Name>/` with only their own clips (idle,
  walk, run, plus sit for the cook). `Editor/CrowdImport.cs` imports them as Legacy animation, the normal
  map as a normal map, and builds `Resources/Crowd/<Name>.prefab` with a URP Lit material (also
  **Chai › Crowd › Rebuild Prefabs**). All seven share one Mixamo skeleton, so they share one set of clips:
  Rafi's idle, walk and run and the cook's sit. Each prefab's `CrowdRigInfo` holds the model's hip-height
  difference from the clip's source, which `CharRig` adds after the animation. Customers only walk (at
  2.1 m/s), seated customers use the sit clip with their hips set on a 0.5 m seat, and Rafi and the
  helpers run above 2.6 m/s. `Models.CrowdCharacter` uses Rafi for the player, Cook (the girl in the red apron) for cooks,
  Helper for waiters, cashiers and the other staff, and one of the four customers (picked from the look)
  for every customer. The walk and run clips play in place: their forward hip drift (Mixamo root
  motion) is taken out in Blender, keeping the bob and sway, and the game moves the character as before.
  They play idle / walk / run by speed, sped up to match the ground speed (Rafi's walk cycle covers about
  0.82 m/s at crowd scale, his run about 3 m/s); carrying masks the arms out of the clip into a fixed hold pose (upper arms forward and down, forearms level), so they no longer swing while the legs walk. Rafi's cap, kurta and
  apron cosmetics don't change the new model yet.
- **Stoves**: once built, the tawa always has a paratha on it and the fryer three samosas in its oil
  (`Station.AddPanFood`, at the kit stoves' cooking surfaces measured in Blender); they sizzle while the
  station cooks, and the steam now rises from the pan instead of the side table.
  The old procedural characters remain as the fallback.
- **UI**: `Art/UI/copy_ui_skin.ps1` copies your slices (`Assets/UI/slicing`) to
  `Resources/UISkin` under short names, and makes the few derived pieces: a blank panel (the recipe book
  frame without its title), blank yellow, cyan and red buttons, blank inventory bars, the bubble split
  into body and tail, a grey level pip, a white patience ring, and the hotspot card straightened (the
  game lays it flat on the floor, where the camera adds the perspective). `UI/Skin.cs` makes the
  sprites at 150 px per unit (the slices are drawn for 1920 × 1080, the HUD canvas is 1280 × 720) with
  9-slice borders; `Editor/UISkinImport.cs` sets their import (no mipmaps, crunch). The HUD follows
  the mockups: coin and leaf bars, the star bar, pause and settings, the level card and task rows, the
  hint bar with coloured key words, the inventory card (a slot per item, Empty hands / filling / Hands
  full), WASD keycaps, the yellow arrow, the double-cash box, the level-up ribbon, the order bubble with
  its patience ring, the FULL tag and the floor hotspot card. The top-left group (cash, leaves, boost,
  level card and tasks) is drawn at 60% of the mockup size so it covers less of the yard. The double-cash
  / cash-bag offer first appears after 2.5 minutes of play and stays 45 s (`economy.json` rules). When Rafi
  runs out of cash on an unlock tile, the same box offers exactly the missing coins for a rewarded ad (at most
  half the tile's price or 40, 90 s cooldown after one is used; `Offers.NeedCash`). Under the star bar, the
  next-unlock chip shows the cheapest open unlock with the cash counting towards it ("Ready to build!" and a
  wiggle when affordable; tap to look at the tile). Until the first customer is served, the task card, hint text
  and chip stay hidden (`Hud.Minimal`): the arrow, the pointing hand and the move hint guide the first steps.
  Items landing on Rafi's stack bounce and pop a little higher each time (a chime when it's full), and coin
  piles pull in from 0.8 m further away, streaming into him with rising ticks. Rafi starts a step from the first
  kettle; the first customer served sets off confetti and a "First sale!" toast. Station counts show only while
  Rafi works a station (or as FULL), and queued customers behind the front one show their patience only once
  it drops below half. Tapping the star bar says which unlock earns the next star. About 1 in 20 customers from
  level 2 is a VIP (gold star overhead, a little less patient, pays double). Each level-up adds a piece of decor
  to the yard (`Location.RefreshGrowth`: plants, water drum, chai cart, bucket, gas cylinder, rug rack; each on
  a spot clear of colliders and of everything the game builds later). Coming back after 2+ minutes shows
  "Welcome back!" (`UI/OfflinePanel`): a quarter of the average earning rate for the time away, capped at 2 h
  (+2 h per Long nap level), doubled by a rewarded ad. Music plays quieter, and a mute button sits beside pause. The Settings, Staff and Recipe book
  (Perks / Shop) panels use their painted frames; the other panels (Upgrades, Pause, the map) use the
  blank frame. Buttons pick the painted button of their colour (`UIKit.Paint`). The pause button
  opens the Pause panel (your painted frame with "Paused", Resume and Setting buttons from `Assets/UI/UIv2/pause`); the map button moved under the settings button.
  The second round of art in `Assets/UI/UIv2` is copied by the same script and replaces what it redraws: the
  empty star bar with separate grey and yellow stars, the painted Staff and Upgrade buttons and red
  notification dot, the pointing hand, the rickshaw icon, the staff panel pieces (square orange portrait tiles,
  new waiter and cook busts) and the hire-a-cook card. `Skin.Face` gives the painted faces (the girl in the red
  apron for cooks, the waiter for other staff) to task icons, unlock tiles and the hire card; `Skin.Icon` swaps
  in painted icons (rickshaw, counter lane, leaf) where the config names them.
- **Stages 2–5** are modelled and laid out, but their gameplay isn't built yet, so they're **out of the
  build for now** (smaller web download): their packs (`art_stage2..5.bytes`) and the textures and
  materials only they use (`KitStalls`, `KitStreet`, `KitMenuBoard`, 18 MB) are in `Art/Export/later_stages`,
  with their `.meta` files, and `export_art.py` has `EXPORT_LATER_STAGES = False`. To bring them back, set
  it to True and export, or move those files back into `Assets/ChaiTycoon/Resources/Art` (`Tex/`, `Mats/`).
  Until then **Chai › Art Preview › Stage 2–5** and the auto-check's stage shots report "no art".
- Concept renders from Blender are in `Docs/Concept/`.

## Tuning

Every price, cost, growth rate, timer, level and task is in
`Assets/ChaiTycoon/Resources/economy.json`, as the GDD specifies.

**Stage 1 loop (four queues).** A new game starts with 25 coins and an empty yard: you build the first kettle
(15) and open the chai counter (10). There are four counter queues, each opened by its own unlock: **chai**,
**paratha** and **samosa** (customers order only that item, one or two) and **mixed** (two or three different
items). Each queue has its own cash pile on its counter, collected by standing at its serve ring. Helpers are
unlocks too, one per job: a helper for each queue (fetches its item and serves only that queue), a **rickshaw
helper** (the rickshaw spots) and a **table helper** (dine-in). Cooks (hired at a stove) and the cashier (collects
every counter pile) stay as before. Food handed over at a counter is set down on the counter in front of the
customer first, and they pick it up a moment later. The shade canopy also puts parasols over the dining tables. Older saves keep
their kettle and first counter, and their old helper keeps serving everywhere.

| Level | Tasks (unlock costs) |
|---|---|
| 1 First customers | kettle (15), chai counter (10), brew 3, serve 3, collect cash |
| 2 More chai | 2nd kettle (30), deliver 8 chai, earn 60 |
| 3 Fresh parathas | tawa (50), paratha counter (60), deliver 5 parathas, earn 120 |
| 4 A cook in the kitchen | hire a cook at a stove (rewarded ad), carry upgrade, deliver 10 chai, earn 150 |
| 5 Samosas and mixed orders | fryer (100), samosa counter (120), deliver 6 samosas, 2nd tawa (130), mixed counter (160) |
| 6 A helping hand | chai helper (90), deliver 12 chai, paratha helper (120), deliver 10 parathas |
| 7 Full crew | samosa helper (150), mixed helper (190), cashier (220), buy 3 upgrades (all at once) |
| 8 Rickshaw stand | stand (180), 4 rickshaw drivers, rickshaw helper (220), 6 more rickshaws |
| 9 Dine-in | table (200), serve 4 guests, 2nd table (240), serve 8 guests |
| 10 Table service | table helper (250), 8 more guests, earn 300 |
| 11 Busy bazaar | deliver 15 chai, 10 parathas, 12 samosas, earn 300 (all at once) |
| 12 Shade for all | shade canopy (360, +1 star), serve 20 customers, earn 400 (all at once) |
| 13 The highway calls | serve 25 customers, buy 2 upgrades, expand to the Highway Dhaba (700) |

Every stove and counter is built by level 5; cooks open at level 4 (free for a rewarded ad, from the hire card at a stove; the guide points
to the nearest stove without one) and helpers from level 6, then the rickshaw stand and the tables, each followed
by its helper. Customers never leave a counter empty: besides the timed stream (stage 1 arrives every 4.6 s at one
star down to 2.0 s at five, faster with every counter, up to a quarter faster by the last level, and 15% faster per
helper), any open counter with fewer than 2–4 people in or walking to its queue (2 on level 1, 4 on the last; one
more where a helper works the queue) gets another walk-in at once, so nobody serving there stands idle. Walkers
hurry at three times the pace while off screen, so the long road in never keeps a counter waiting. Staff walk at
2.5 m/s (Rafi 4) and never nap.
Seated guests each show their own order bubble. Every task also has a `desc` line (what it is and why) that the
hint bar shows while that step is ready to do; until then it shows what to do meanwhile ("Earn 40 more for ...").

Customers walk in along the road edge outside the front wall and come into the yard through the wall's opening in
front of the queues (x -4.2 to 2.2), and leave the same way; students park their bikes at the kerb just right of
the opening. Once the rickshaw stand is built its rickshaws park on the left of the road edge, so walk-ins then come
and go on the right. Inside the yard, customers and staff walk round counters, stoves, tables and props
(`NavGrid`: a 0.25 m grid marked from the colliders, with A* only for legs that would cut through something).

Every task counts from the start of its level, even while a sequential level still hides it, so an "earn"
step can already be partly done when it appears. The task card shows 4 rows; when a level has more, the oldest
finished rows drop off so the current task stays visible. The deliver/earn tasks were added after the last
`ChaiAutoCheck` run (which reached the old level 9, *Mixed orders*, at 8:22), so level times are not measured
yet; run it again to time the longer stage (a rough guess is 15–17 minutes to the expansion).

Changes made from the GDD's literal rules to hit this curve:

- Each counter lane brings its own stream of customers, so the second lane really means "more
  customers at once".
- Walk-ins sometimes order 2 items.
- Batch items such as samosas come in pairs.
- Patience drains at 40% speed for customers who aren't at the front of the queue.
- The waiter is the first staff member (level 3, about 2 minutes in), well before the cashier.
- The expansion costs 700 instead of 1,000, so the expansion lands near 11 minutes.
- Guide text uses short sentences instead of the 3-word limit, so each step can say where to go.

## Code map (`Assets/ChaiTycoon/Scripts`)

- `Core/`: `Game` (owns the run, wallet, pause and flow), `Economy` (config), `SaveData`/`SaveSystem`,
  `Upgrades` (all derived stats), `WebBridge` (storage and CrazyGames), `Tw` (tweens), `GameEvents`.
- `Gameplay/`: `Location` (Stage 1 layout and unlock builders), `Player`, `InputReader`,
  `CarryStack`, `Station`, `ServicePoint` (counter lanes and rickshaw spots), `CashPile`, `Customer`,
  `DiningTable` (seats, plates, table cash pile), `CustomerSpawner`, `UnlockTile`, `Staff`,
  `Progression` (levels and tasks), `Guide` (FTUE hints, arrow, tips), `Offers`, `CameraRig`, `Traffic`,
  and `AutoPlayer` (test bot, editor and dev builds only).
- `Visual/`: `MeshBuilder` (flat-shaded primitives with outline normals), `ArtLib` (loads the
  Blender art packs), `Models` (characters with procedural animation, food, stations, vehicles,
  props; uses the art when it's there), `SpriteGen` (SDF icon painter), `IconBaker`, `FX`, `Pal`, `Mats`.
- `Gameplay/ArtStage.cs`: builds a whole stage from its Blender layout (used by the art preview).
- `UI/`: `Skin` (the painted UI pieces), `Hud` (with the hint panel and pointing hand), `LevelCard`,
  `InventoryBar`, `WorldUI` (order bubbles, table bubbles, seat tags, tray tags, floating numbers),
  `Panel` base, `UpgradePanel`, `HRPanel`, `RecipeBook`, `SettingsPanel`, `PausePanel`, `WorldMap`,
  `Celebration`.
- `Shaders/`: `Chai/Toon` (3-tone toon with a constant-pixel outline, for characters and food), `Chai/ToonFlat`, `Chai/UnlitAlpha`. Props use the standard URP Lit shader.
- `Editor/ChaiDevTools.cs`: sets a fixed game view size, steps frames while Unity is in the
  background, renders snapshots, and has the Chai › Art Preview menu.
- `Editor/ChaiAutoCheck.cs`: an unattended check run from the command line (with the project closed):
  `Unity.exe -projectPath D:\ChaiTycoon -executeMethod ChaiTycoon.EditorTools.ChaiAutoCheck.Run`.
  It renders the stage 2–5 previews, lets the bot play stage 1 for 9 game minutes from a fresh save,
  saves screenshots and a log to `Screenshots/autocheck/`, puts the editor save back and quits.
- `Editor/ChaiPromoVideo.cs` (Chai → Web → Render CrazyGames Videos): the two preview videos, rendered from the
  real game into `Art/Store/videos` at true 1× speed, each opening on its cover from `Art/Store/covers`. Render the
  covers first; the video needs `cover_landscape_1920x1080.png` and `cover_portrait_1080x1620.png`.
- **CrazyGames SDK**: the official Unity SDK (`sdk.crazygames.com/UnityCrazySDK.unitypackage`, version in
  `CrazySDK.Version`): `Assets/CrazySDK`, `Plugins/crazySDK.jslib` and `WebGLTemplates/CrazyGames`. To update it,
  delete `Assets/CrazySDK` first and import the new package; importing resets `CrazyGamesSettings`, and the SDK's
  sitelock only lets the build run on CrazyGames and localhost. `WebBridge` drives it: `CrazySDK.Init` at startup
  (`Game.Start` waits up to 10 s for it, then saves locally for that session), saves in `CrazySDK.Data` (so turn
  on **Progress Save** when submitting, or nothing is saved), `GameplayStart` when the boot ends and at every
  resume, `GameplayStop` on panels, pause and ads (not on a hidden tab: the site handles focus and asks games
  not to report it), midgame ads only between levels (from 5 minutes of play) and when travelling, never on
  closing a panel, rewarded ads only on the player's request, game audio muted while an ad plays, and
  `HappyTime` when a stage is finished. Unity's stripping can't see the SDK's `SendMessage` callbacks,
  so `Assets/link.xml` keeps the `CrazyGames` namespace. `Plugins/WebGL/ChaiWeb.jslib` keeps phone detection,
  tab visibility, blocking page scroll and the localStorage fallback.
- **Rewarded ads** (CrazyGames' rules shape these): 2x cash sits at the top of the Upgrades panel, for an ad or
  for 60 seconds of income in coins, because rewarded buttons aren't allowed on the active gameplay screen. The
  cook card always offers cash (`cookCost`) beside the ad, since a level can't depend on an ad. On the Welcome
  back panel, Collect and 2x Watch ad match in size, font and colour. Two ad buttons still appear during play,
  by choice: the short-of-cash top-up and the cook card, each only when Rafi stands at the spot that needs it.
- **Without ads**: ads are off during Basic Launch and with an ad blocker. `WebBridge.RewardsAvailable` then goes
  false for the session and every rewarded button hides. The game knows at startup: `prefetchAd` throws in Basic
  Launch (`Chai_AdsDisabled`), and the SDK reports ad blockers, which get a toast and a line on the 2x strip (CrazyGames wants a notice). Ad errors are matched
  by prefix because the docs say `adsDisabled` while SDK 3.8.0 sends `adsDisabledBasicLaunch`.
- **Save safety**: `SaveSystem.Load` logs what it read (`[Save] load: main 1355, backup 1355`). A run that found
  no save won't overwrite a stored save with more play on it. Twice in the editor a run started blank during a
  stalled boot, cause unknown; it never happened in a browser, and the log line is there to catch it.
- **Boot**: `Game.Awake` only loads the config and shows `UI/LoadingScreen` (cup, title, progress bar) on the
  first frame; `Game.Start` then waits for the SDK, loads the save, and builds audio, the art pack and the dhaba
  over a few frames, logging each step (`[Boot] audio: … ms`). In the editor, `Editor/FastPlayMode.cs` turns off the domain reload
  on Play (the 7–9 s grey Game view was that reload); statics that must start clean reset themselves.
- **Dynamic resolution**: `Visual/DisplayAdapt.cs` renders the 3D view near a pixel budget (1080p on
  desktop, 720p on phones) however big or high-DPI the frame is, and lowers it in 10% steps when frames run
  slow (back up with headroom). The HUD always draws at full resolution. `CameraRig` pulls back on windows
  narrower than 16:9 so the yard still fits.

## Not done yet

- **Tuk-tuk (rickshaw)**: still my Blender model. None of the downloaded kits has one (checked the three prop
  kits plus `GTA/Highway`, `roadprops2`, the metal bench and the rest). Drop a tuk-tuk model in and it can
  replace `PROP_Rickshaw` (used for the rickshaw customers, the stand and traffic).
- Fences, trees, the rickshaw stand and the cars and trucks are also still my Blender models.
- **WebGL build**: player settings are applied by `Editor/WebGLSetup.cs` each time the editor loads, per CrazyGames' optimization guide: their template, Brotli (they only accept compressed builds), file names as hashes, explicitly thrown exceptions only, data caching, engine code stripping, managed stripping High (Medium if the build breaks), IL2CPP optimised for size, WebAssembly 2023, BigInt and Table, no splash. Post-processing stays on for the look, although it adds about 1 MB. `Icons/icon512.png` is set as the game's default icon by the same script; the store images are in `Art/Store`.
**Chai → Web → Build for CrazyGames** applies those settings and runs the SDK's release build into `Builds/CrazyGamesRelease` (a DXT build plus an ASTC data file, with Disk Size LTO), then opens it on localhost. The game ships for desktop browsers, so the SDK's low-memory phone builds are off (`supportsMobile` in `WebGLSetup.cs`); a good build is 8 files. Upload every file in that folder at developer.crazygames.com. Development builds don't run on the site. From the command line: `Unity.exe -batchmode -quit -projectPath D:\ChaiTycoon -executeMethod ChaiTycoon.EditorTools.WebGLSetup.BuildForCrazyGames`.
  **Download size is the big risk** (CrazyGames allows 50 MB up to the first `GameplayStart`, 20 MB for the mobile homepage; the first release build, on 2026-10-31, is 20.6 MB, or 19.6 MiB: data 14.7, wasm 5.8, framework and loader 0.1): `Resources/Art/Tex` holds about 58 MB of source textures (stage 1 kits, after stages 2–5 were taken out; every kit's
  maps). They now import at 1024 px, normal maps at 256 px, a quarter or less of the 2048 px data; the
  painted UI keeps full size (3.8 MB of source, 9-sliced). Measure the first build to see what's left.
- **Font size**: Baloo 2 ExtraBold (titles, numbers, buttons) and Nunito ExtraBold (body text) are in
  `Resources/Fonts` under the SIL Open Font License (licence files included). Baloo 2 is 417 KB because
  it also covers Devanagari; subsetting it to Latin would save most of that in the WebGL build.
- **Stages 2–5 gameplay**: the art, layouts and crowds are done (see Art). Still to build: the
  mechanics (dirty dishes, lassi two-step, daal combo, truck rushes, supply deliveries, night critics,
  snow), the cook, cleaner, rider and manager and the Golden Kettle ending.
  Location configs are stubbed in `economy.json`. Each stage's layout in its art pack gives the
  positions for stations, counters, tables and tiles.
- **Night glow** for stage 4: the GDD calls for glow sprites on string lights and neon; the art has
  the bulbs but no glow yet.
- Station upgrade pads on the floor. Upgrades are currently reached through the panel only.
