# WildReach

**An open-world survival adventure about leaving home.**

You start on the island you're from. A cold sacred peak at one end, a warm departure beach at the other, a forest and a wooden town in between. Somewhere up in the snow there's a broken rowboat beside a glowing tree. The whole island is a lesson in how to survive long enough to reach it, repair it, and carry it down to the water.

Then you push off, and the ocean is the rest of the game.

![WildReach — the forest, and something large in it](docs/screenshots/wildreach-1-forest-beast.jpg)

<table>
<tr>
<td width="50%"><img src="docs/screenshots/wildreach-2-forest-vista.jpg" alt="The forest, with the mountain beyond"></td>
<td width="50%"><img src="docs/screenshots/wildreach-3-sacred-canopy.jpg" alt="Up in the canopy of the home tree"></td>
</tr>
<tr>
<td width="50%"><img src="docs/screenshots/wildreach-4-leopard-dusk.jpg" alt="A snow leopard at dusk, torch lit"></td>
<td width="50%"><img src="docs/screenshots/wildreach-5-climbing.jpg" alt="Climbing a cliff face"></td>
</tr>
</table>

---

## The island teaches you by making you live it

There are no tutorial popups. The island is laid out cold-at-the-top to warm-at-the-bottom, and the survival loop is chained so each step earns the next:

**hunt** → eat for the first time in the wild woods → **the pelt** → becomes warm clothes → **the warmth** → unlocks the climb into the cold mountains → **the home tree**, and the broken boat beside it.

Every verb in the game is taught by needing it. The reward for finishing the island isn't a movement skill, it's the boat itself, and the open sea it gets you to.

---

## What's actually built

Around 120 C# scripts and 12,000 lines of gameplay code. Not a prototype.

**Traversal.** Surface-conforming climbing that samples a fan of rays around each grip, averages the normals and orients the whole body to the wall plane, so you hug slabs, overhangs and curves like a climber instead of staying bolt upright. Hands and feet are pinned to the rock by IK on top of that. Movement is driven by root motion rather than a constant, so the climb doesn't slide. Swimming, sailing with a controllable ship and wake, physics-synced jumping.

**Survival.** Health and hearts, stamina, an inventory with a pouch UI and mesh-rendered item icons, a wield system with sheathe-to-back and draw, a lit torch with a flickering flame and warm point light.

**Combat that reads honestly.** A swing sweeps the weapon's *actual* arc — a per-frame overlap capsule along the blade, gated by the swing animation's normalized time — so a hit lands on real contact mid-swing rather than the instant you click. Every creature carries health, hurt flash, knockback with a brief AI pause, and a sideways topple-and-shrink death. An over-the-shoulder camera with a fixed crosshair, and spine aiming that pitches the upper body toward the cursor so small critters are actually hittable.

**A living world.** A day/night cycle with sun, moon and a camera-following star dome. Global wind that bends grass blades at the tip while the base stays planted. Fireflies on a real per-mote blink cycle, dark for most of it, rising and fading softly. Predators that stalk, wind up and pounce. Passive creatures bound to painted territories that wander the whole zone with capped-rate turning, so they bank through corners instead of whipping around. Pets that follow. Save and resume.

**A world-building toolchain.** Because hand-placing an island doesn't scale, the repo ships its own Unity editor tooling: an Island Builder with per-zone definitions and painted zone maps, GPU-instanced scatter fields for forests and blossom, terrain sculpting and texture setup, and around fifty one-click setup commands that assemble a system into the scene from scratch.

**A cast with a design behind it.** The forest is built as a layered ecology rather than a bag of models — ambient charm at your feet, game worth hunting in the middle, and a real apex predator in the deep woods that mirrors the snow leopard owning the peak. Mossback Fawn, Bramble Boar, Thornstalker, Spritehare, Tuftling, Glimmer Moth, Stoneshell Plodder.

---

## Running it

Unity 6.4 or newer. Clone, open the project folder in Unity Hub, and load `Assets/Scenes/SampleScene.unity`.

Editor tooling lives under the **WildReach** menu in the Unity menu bar — island building, scatter, lighting, weather and the per-system setup commands.

Large binaries (Blender working files, build output, `Library/`) are gitignored. Source meshes and prefabs are committed.

---

## Status

Island 1 is playable end to end as a world: terrain, zones, forest, town site, creatures, combat, climbing, sailing, day/night, save and resume.

Still ahead: the survival meters themselves (hunger and the cold gate), crafting, NPCs, and the boat repair that closes the island. Weather is partly built and parked for a refinement pass. Creature behaviour is currently solo zone-roaming, with herding and species-specific temperament designed but not yet built.

Built solo, as an exploration of how much world one person can actually make.

## License

MIT — see [LICENSE](LICENSE).
