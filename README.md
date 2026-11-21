# jMods

A collection of mods for [Half Grip](https://store.steampowered.com/app/4163270/), adding an open world, procedural race tracks, cockpit equipment, working car controls and windshield weather, and new ways to play.

Each mod is maintained in its own repository. This collection automatically pulls the latest published releases into one place for easy installation.

## Install

The [HalfGripMods Installer](https://github.com/4l3x4nd3r0/HalfGripMods) handles installation, dependencies, and updates.

1. Download `HalfGrip-Mods-Installer.exe` from the [installer releases](https://github.com/4l3x4nd3r0/HalfGripMods/releases).
2. Close Half Grip and run the installer.
3. Install **BepInEx** and **HalfGripTools** if they aren't already installed.
4. Click **+ ADD** and enter:

   `https://github.com/htubdawg/jMods/tree/main`

5. Click **INSTALL** on the mods you want, then start the game through Steam.

The first launch may take a little longer while the mods compile. To check for updates, reopen the installer and click **CHECK NOW**.

## Mods

### [Open World](https://github.com/htubdawg/HalfGripOpenWorld)

An open world about 9.7 km (6 miles) square, with towns, villages, mountains, rivers, highways, and country roads. Includes garages, timed race events (a circuit, a rally stage, a hill climb, and the highway), a day/night cycle, storms that drift across the country, and multiplayer support.

![Open World map](https://raw.githubusercontent.com/htubdawg/HalfGripOpenWorld/main/docs/world-labels.png)

### [Procedural Maps (ProcTrack)](https://github.com/htubdawg/ProcTrack)

Five procedural race maps built from a new seed each race. Stadium, Forest, Mountain, Factory, and Tokyo each get their own generated tracks, with the original environments and hazards.

![Procedural track layouts](https://raw.githubusercontent.com/htubdawg/ProcTrack/main/docs/tracks.png)

### [MiniMap](https://github.com/htubdawg/HalfGripMiniMap)

A pixel-art map of the whole lap that turns with your car and shows every racer: you as a white arrow, bots as red dots, other players as blue dots. Buy the **GPS** in the shop ($25, one dash module slot), or switch on the **screen minimap** in the config. Works on the base game's maps, ProcTrack's procedural maps, and other modded maps, coloured to match the map and weather. In Open World free roam it shows the country round your car instead: roads by class, rivers, towns, and a north marker.

| Control | How to use it |
|---|---|
| **GPS screen** | Left-click to tap it; right-click to zoom (middle, out, in) |
| **Screen minimap** | Off by default: set `Display.Enabled = true` in the config. **K** hides or shows it |

![MiniMap preview](https://raw.githubusercontent.com/htubdawg/HalfGripMiniMap/main/docs/preview.png)

### [Helicopter](https://github.com/htubdawg/HalfGripHelicopter)

A chassis item that replaces your car with a flyable helicopter. Includes a custom cockpit, flight controls, and working rotors. Designed for Open World but usable in regular races.

![Helicopter](https://raw.githubusercontent.com/htubdawg/HalfGripHelicopter/main/docs/model.png)

### [Car Stereo](https://github.com/htubdawg/HalfGripStereo)

A CD stereo built into the cockpit, with a CD wallet on the sun visor: one disc for each song of the game's soundtrack, plus **MY**, which plays your own music. Until you pick a disc it plays the race music the game would have played. Mouse only for now: it can't be worked with a gamepad yet.

| Control | How to use it |
|---|---|
| **Put a disc in** | Grab a disc from the wallet on the visor and drop it on the stereo. Dropping a new disc swaps it for the one playing |
| **Left knob** | Mouse wheel for the volume; click to pause or play |
| **Right knob** | Mouse wheel for the next or previous disc (on **MY**, the next or previous song); click to eject the disc |

For **MY**, put `.ogg`, `.mp3`, `.wav` or `.aiff` files in `Music\Half Grip` in your user folder, or point `Music.Folder` in the config at any folder.

![Car Stereo](https://raw.githubusercontent.com/htubdawg/HalfGripStereo/main/docs/screenshot.jpg)

### [SlapPack](https://github.com/htubdawg/HalfGripSlapPack)

More things to grab in your car: an adjustable rear-view mirror, working window cranks, a turn-signal stalk, and three-speed windshield wipers (an arm and angled blade each, sweeping in tandem) with a washer. Rain and slush build up on the windshield and distort the view until you wipe them away; the side and rear windows get lighter rain and snow that clears by itself. Works with the base game's weather and with Open World's variable rain, snow, and wind (Open World 0.24.0 or newer). Only the game's own cars get the additions: custom chassis such as Helicopter turn them off.

| Control | How to use it |
|---|---|
| **Rear-view mirror** | Grab and drag to aim; right-click to reset. The position is saved between races |
| **Window cranks** | Grab a door's crank and circle your hand: clockwise lowers the window, counterclockwise raises it |
| **Turn-signal stalk** (left) | Down for left, up for right. A half tap flashes three times; full travel latches and cancels after the turn |
| **Wiper sleeve** (right) | Point the hand at it and turn the mouse wheel through **0 / I / II / III**: off, intermittent, continuous, fast. Holding right-click and dragging also works, including on a controller |
| **Washer stalk** (right) | Left-click and pull down to spray and wipe |

| Wipers parked | Mid-sweep, clearing the rain |
|---|---|
| ![Wipers parked](https://raw.githubusercontent.com/htubdawg/HalfGripSlapPack/main/docs/images/wipers-stopped.png) | ![Wipers sweeping](https://raw.githubusercontent.com/htubdawg/HalfGripSlapPack/main/docs/images/wipers-moving.png) |

## Updates

This repository checks the individual projects for new releases every six hours. Only published releases are included; unreleased changes on their development branches are not copied.

For configuration, compatibility information, and release notes, see the individual project repositories.

## Dependencies

These mods use [HalfGripTools](https://github.com/4l3x4nd3r0/HalfGripTools) and BepInEx. Both can be installed through the [HalfGripMods Installer](https://github.com/4l3x4nd3r0/HalfGripMods).

Some mods have additional requirements or work best together. Check their individual READMEs for details.
