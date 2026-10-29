# Chai Dhaba Tycoon: prop generation prompts

Prompts for AI 3D generators (Meshy, Tripo, Rodin, Unity AI, Blender MCP `generate_3d`) and for
text-to-image concept sheets you can feed into image-to-3D. Everything follows the GDD's art
direction: chunky low-poly toon, flat colours, Pakistani truck-art accents, warm muted surroundings.

## How to use

1. **Paste the style block** in front of every prompt (or into the generator's "style" field).
2. **Add the negative block** wherever the tool takes a negative prompt.
3. **Image-to-3D gives the cleanest results.** First generate a concept image with the same prompt
   plus the "concept sheet" line, then feed that image into image-to-3D.
4. **After generating**, in Blender: decimate to the triangle budget, merge to as few materials as
   possible, set the pivot at the bottom centre, apply scale (1 unit = 1 m), face +Z forward
   (the side the player or customer stands on), export FBX or GLB. Drop PBR maps; the game's toon
   shader only needs base colour (vertex colours or the shared palette texture).
5. **Check it in a phone-sized screenshot** before approving (GDD rule): anything thinner than
   about 0.1 m disappears.

### Style block (prefix every prompt)

```
Stylized low-poly 3D game asset, chunky rounded shapes with bevelled edges, flat colours with
simple 3-tone toon shading, no textures except bold painted patterns, cute casual mobile-game
style like Monkey Mart, slightly oversized proportions, clean silhouette readable from a
50-degree top-down camera, single object centred on a plain white background, soft even
lighting, game-ready
```

### Colour line (add when the prop should use the palette)

```
Colour palette: sand #E8D2A6, clay #C98B5E, dark wood #6B4A33, leaf green #7FA85A,
truck pink #F2459A, marigold #FFC21A, parrot green #2FBF5B, chili red #E8382F,
peacock teal #14A8B5, royal blue #2E5BD8, steel grey #C9CDD3
```

### Negative block

```
realistic, photorealistic, PBR, photo textures, normal map detail, shiny reflections, metallic
gloss, thin wires, tiny details, fine text, letters, logos, words, noise, grime, dirt, scratches,
high poly, sculpted wrinkles, multiple objects, background scene, ground plane, people
```

### Concept-sheet line (for text-to-image before image-to-3D)

```
orthographic three-quarter view and front view on one sheet, flat colours, thick dark outline,
no shadows on the background
```

> **No text on models.** The GDD forbids baked-in words (it blocks translation), so signs and
> boards show icons and patterns only. The game adds any text as UI.

---

## Stage 1: Roadside stall (in the game now)

| # | Prop | Size in game (W × D × H) | Triangle budget |
|---|---|---|---|
| 1 | Chai kettle station | 1.7 × 1.0 × 1.4 m | 400–600 |
| 2 | Paratha tawa station | 1.7 × 1.0 × 1.1 m | 400–600 |
| 3 | Samosa karahi fryer | 1.7 × 1.0 × 1.2 m | 400–600 |
| 4 | Serving counter segment | 3.0 × 0.9 × 0.85 m | 200–400 |
| 5 | Cash till table | 1.1 × 0.9 × 1.1 m | 200–350 |
| 6 | Charpai dining table set | 2.5 × 2.4 × 0.75 m | 400–600 |
| 7 | Rickshaw stand | 1.2 × 1.2 × 2.4 m | 200–400 |
| 8 | Shade canopy | 14 × 1.4 × 2.6 m | 300–600 |
| 9 | Painted kettle sign | 1.4 × 0.2 × 2.4 m | 150–300 |
| 10 | Highway road sign | 1.6 × 0.2 × 2.4 m | 150–250 |

**1. Chai kettle station**
```
A roadside Pakistani chai station: a sturdy dark wooden table with a truck-pink painted front
panel and small marigold stars, a big dented aluminium chai kettle with a curved spout on a
round black gas burner, two tall steel milk cans beside it, and a small tray area on the right
for tea glasses. Kettle oversized and friendly, warm brown chai colour #B0703C showing at the
spout, a tiny white sparkle on the kettle
```

**2. Paratha tawa station**
```
A roadside paratha station: dark wooden table with a teal and pink truck-art painted front
panel, a large round flat black iron tawa griddle on a low burner, one golden paratha
#E9B45C cooking on it, a small wooden board with a rolling pin, a steel bowl of dough
```

**3. Samosa karahi fryer**
```
A roadside samosa fryer station: dark wooden table with a painted truck-art front panel, a wide
deep black iron karahi wok on a burner filled with golden oil, a slotted steel spoon resting on
the rim, a steel tray beside it holding four triangular golden-brown samosas #D9922E
```

**4. Serving counter segment**
```
A long low wooden food stall counter, dark wood #6B4A33 top with rounded bevelled edges,
front panel painted in Pakistani truck art: truck pink #F2459A base, teal border stripes top and
bottom, a row of alternating marigold and green five-pointed stars with small red dots;
two flat steel serving trays sunk into the top. Designed to tile side by side
```

**5. Cash till table**
```
A small square wooden side table with a painted pink front and one marigold star, an old
chunky teal cash register on top with big square keys and an open coin drawer, a small
steel bowl of gold coins
```

**6. Charpai dining table set**
```
A rustic dhaba dining set: one low rectangular wooden table with a pink and marigold truck-art
runner painted down the middle, and two long charpai benches on either side, each with a
dark wood frame, short round legs and a cream woven rope seat with red cross-stripes. Seats four
```

**7. Rickshaw stand**
```
A small roadside rickshaw stop: a short wooden stool with a round tin cash box on top, and a
wooden post beside it holding a square sign board painted parrot green with a cream inner panel
and a simple marigold auto-rickshaw icon (no text)
```

**8. Shade canopy**
```
A long colourful roadside stall canopy: a cloth awning stretched over thin dark wood poles,
alternating triangle flags in truck pink, marigold, teal and parrot green along the front edge,
a scalloped trim, gently sagging fabric. Very wide and shallow, designed to run along the back
of a food stall
```

**9. Painted kettle sign**
```
A hand-painted roadside dhaba sign on two wooden posts: a rounded rectangular board with a thick
truck-art border of pink and teal flowers, a big marigold chai kettle with steam curls painted
in the centre, a cup of chai beside it (no letters or words)
```

**10. Highway road sign**
```
A bright green highway direction sign shaped like an arrow pointing right, on a single grey
steel post, a simple white road icon on the arrow (no letters)
```

### Food, cups and coins (shown at 1.5× real size, 80–200 triangles each)

**11. Chai glass**
```
A small clear-ish cream ceramic chai cup filled with milky brown tea #B0703C, a thin teal band
around the rim, chunky and oversized, readable from far away
```

**12. Paratha**
```
A single round golden paratha flatbread #E9B45C, slightly puffed with a few darker brown
toasted spots and a small pat of butter on top, thick and chunky
```

**13. Samosa**
```
One triangular golden-brown samosa #D9922E, puffy pastry with crimped edges, chunky and cute
```

**14. Gold coin**
```
A thick chunky gold coin #FFD23F with a raised rim and a simple embossed star in the middle,
small white sparkle highlight
```

**15. Tea leaf (currency)**
```
A single plump stylised tea leaf in parrot green #2FBF5B with a lighter central vein, thick and
chunky like a game currency pickup
```

**16. Plate stack (dirty dishes)**
```
A small wobbly stack of four steel thalis and a cup, with cartoon smudges of curry on the top
plate, chunky and readable
```

### Vehicles (800–1,500 triangles, wheels 1.3× oversized)

**17. Auto-rickshaw**
```
A chunky cartoon Pakistani auto-rickshaw, squashed proportions, three oversized wheels, body
fully painted in truck art: marigold body, pink and teal floral border panels, a fringe of
small dangling tassels along the roof edge, round headlight with painted eyes, dark canopy roof
```

**18. Bicycle**
```
A chunky cartoon roadside bicycle with oversized tyres, thick frame (no thin tubes), a small
basket on the front, a bell; frame in one bright colour
```

**19. Painted truck: style A (pink and teal)**
```
A chunky cartoon Pakistani Bedford truck, squashed proportions, huge oversized wheels, tall
decorated crown above the cab, every panel covered in truck art: pink base, teal borders,
marigold flowers, peacock motifs, a painted eye on the front bumper, chains hanging along the
bumper as thick chunky links
```

**20. Painted truck: style B (green and marigold)**
```
Same chunky cartoon Pakistani truck, parrot green base with marigold and red flower borders,
painted mountains and a sunrise on the side panels, decorated wooden doors on the cargo bed
```

**21. Painted truck: style C (royal blue and red)**
```
Same chunky cartoon Pakistani truck, royal blue base with chili red and white geometric
borders, painted roses and a star on the cab, mirrored discs along the crown
```

**22. Family car**
```
A chunky cartoon small hatchback car, squashed rounded proportions, oversized wheels, pastel
body colour, roof rack with a tied luggage bundle
```

**23. Scooter (food critic)**
```
A chunky cartoon Vespa-style scooter with rounded body, oversized wheels, purple accent stripe,
small round mirrors
```

### Background props (150–400 triangles; no outline in game)

**24. Steel milk cans** — `Two tall rounded steel milk cans with lids and handles, chunky, matte grey`
**25. Wooden crates** — `A stack of two chunky wooden fruit crates, slatted sides, one tilted on top`
**26. Potted plant** — `A round truck-pink clay pot with a puffy leaf-green bush, one marigold flower`
**27. Roadside tree** — `A chunky low-poly roadside tree, short brown trunk, three puffy faceted leaf-green clumps`
**28. Gas cylinder** — `A short fat red gas cylinder with a steel valve on top, chunky`
**29. Water cooler** — `A chunky steel water cooler on a stand with a tap and a hanging chained cup`
**30. Trash bin** — `A round painted oil-drum trash bin in parrot green with a marigold stripe`

---

## Stage 2: Highway dhaba

**31. Fridge (lassi step 1)**
```
A chunky retro upright fridge, cream body with rounded corners, a big chrome handle, a pink and
teal truck-art strip down the door, a yogurt pot icon painted on the door (no text)
```

**32. Blender (lassi step 2)**
```
A wooden table with an oversized countertop blender full of white frothy lassi #FFF6E0, a stack
of tall steel lassi tumblers beside it, painted truck-art front panel
```

**33. Daal handi station**
```
A wooden table with a round black clay handi pot of golden daal on a low burner, a ladle resting
inside, a small steel bowl stack, truck-art painted front panel
```

**34. Tandoor oven**
```
A chunky round clay tandoor oven #C98B5E, wide body narrowing at the top, glowing orange opening,
a wooden paddle leaning against it, two rotis stuck to a steel rack beside it
```

**35. Wash tub**
```
A big round steel wash tub on a low wooden stand, soapy water with bubbles, a small stack of
clean thalis drying beside it
```

**36. Charpai bed seating (4 seats)**
```
A traditional Pakistani charpai daybed used as dhaba seating, dark wood frame with chunky turned
legs, woven rope top in cream with red and teal cross-pattern, a low wooden table beside it
```

**37. Roadside dhaba sign (+1 star)**
```
A big roadside dhaba billboard on two posts, rounded board with a truck-art border of pink, teal
and marigold flowers, a painted plate of food and a steaming chai cup in the middle, small
light bulbs around the edge (no letters)
```

**38. Hand-painted menu board (cosmetic)**
```
A wooden A-frame menu board painted with food icons only (chai cup, paratha, samosa, lassi)
inside truck-art flower borders, no letters
```

**39. Marigold garlands (cosmetic)**
```
A swag of chunky marigold flower garlands strung between two short posts, orange and yellow
pom-pom flowers, a few hanging tassels
```

## Stage 3: GT Road truck stop

**40. Karahi stove**
```
A heavy double gas stove on a brick base, a big black iron karahi wok of red chicken karahi on
top, green chillies and ginger on the rim, a steel ladle
```

**41. Biryani deg**
```
A huge round copper biryani deg pot with a lid sealed by dough around the edge, sitting on a
brick fire pit with glowing coals, a long ladle leaning on it
```

**42. Rice sack**
```
A plump burlap rice sack tied at the top, a simple green leaf stamp printed on it (no text)
```

**43. Long table, 6 seats**
```
A long wooden truck-stop table with three seats on each side made from charpai benches,
truck-art runner down the middle, steel water jug in the centre
```

**44. Truck bay marking**
```
A low concrete truck parking pad with chunky painted yellow and black kerb blocks at the edges,
a tyre stop at the back
```

**45. Neon sign (+1 star)**
```
A chunky dhaba neon sign shaped like a steaming chai cup and a star, glowing pink and teal tubes
on a dark frame, thick tubes (no letters)
```

**46. Supply truck**
```
A small chunky cartoon delivery pickup truck with a covered cargo bed full of burlap rice sacks,
plain marigold paint with one teal stripe, oversized wheels
```

## Stage 4: City food street (night)

**47. Seekh kebab grill**
```
A long black charcoal grill on legs, glowing orange coals, a row of skewered brown seekh kebabs,
a hand fan resting on the side, small smoke puffs
```

**48. Kulfi cart**
```
A chunky street kulfi cart with two big wheels, a red and white striped box body with a round
lid, cone-shaped kulfi on sticks in a rack, a small parasol on top
```

**49. Lassi stall**
```
A small street stall with a white tiled counter, giant clay matka pots, tall glasses of white
lassi with a pink straw, a teal awning
```

**50. Street table**
```
A round steel street-food table with two plastic stools in bright red, a napkin holder and a
water jug
```

**51. String lights (+1 star)**
```
A long sagging string of chunky round bulbs in warm yellow, pink and teal between two poles,
bulbs big enough to read from far away
```

**52. Fairy lights (cosmetic)**
```
A short swag of chunky multicolour fairy light bulbs on a thick dark cable, hanging in three loops
```

## Stage 5: Mountain tourist cafe (snow)

**53. Soup pot**
```
A big steel soup pot of creamy yellow corn soup on a gas ring, steam curls, a ladle and a stack
of cups beside it, snow on the table edge
```

**54. Chapli kebab griddle**
```
A wide round flat iron griddle on a brick stand frying two big round chapli kebabs, sliced tomato
rings on top, a spatula resting on the edge
```

**55. Heated table**
```
A wooden cafe table with a small round charcoal brazier in the middle glowing orange, two
benches with folded woollen blankets in red and teal
```

**56. Tourist bus**
```
A chunky cartoon tour bus, rounded squashed body, oversized wheels, big windows, cream and teal
paint with a pink truck-art stripe, luggage rack on the roof with suitcases
```

**57. Pine tree with snow**
```
A chunky low-poly pine tree with three stacked cone layers in dark green, thick white snow caps
on each layer, short brown trunk
```

**58. Snow pile** — `A soft rounded heap of snow with a few chunky lumps, pure white with pale blue shading`

**59. Viewpoint deck rail** — `A short wooden deck railing section with thick posts and a top rail, a small brass telescope on a stand`

**60. Golden Kettle (ending goal)**
```
A giant shining golden chai kettle trophy on a round marigold plinth, curved spout, ornate
truck-art floral engraving, small star sparkles around it, heroic and celebratory
```

---

## Batch tips

- **Matching sets:** generate one station first, then add "same style and table as the previous
  chai station" to the next prompt (or use it as the reference image) so the whole kitchen
  matches.
- **Truck-art patterns:** if a generator makes the patterns too fine, add "big bold simple
  shapes, at most 3 colours per panel". The GDD allows only two 512 × 512 pattern textures in the
  whole game.
- **Size budget:** the whole stage 1 art budget is 2 MB compressed. Keep props under the triangle
  budgets above and reuse one material.
