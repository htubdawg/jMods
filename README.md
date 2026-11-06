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

A dashboard GPS and optional on-screen minimap showing the track, your position, and other racers. Supports the base game, procedural maps, and Open World.

![MiniMap preview](https://raw.githubusercontent.com/htubdawg/HalfGripMiniMap/main/docs/preview.png)

### [Helicopter](https://github.com/htubdawg/HalfGripHelicopter)

A chassis item that replaces your car with a flyable helicopter. Includes a custom cockpit, flight controls, and working rotors. Designed for Open World but usable in regular races.

![Helicopter](https://raw.githubusercontent.com/htubdawg/HalfGripHelicopter/main/docs/model.png)

### [Car Stereo](https://github.com/htubdawg/HalfGripStereo)

An interactive CD player mounted in the cockpit, with a CD wallet on the sun visor. Play the game's soundtrack or load your own music files.

![Car Stereo](https://raw.githubusercontent.com/htubdawg/HalfGripStereo/main/docs/screenshot.jpg)

### [SlapPack](https://github.com/htubdawg/HalfGripSlapPack)

More things to grab in your car: an adjustable rear-view mirror, working window cranks, a turn-signal stalk, and three-speed windshield wipers with a washer. Rain and slush build up on the glass and distort the view until you wipe them away. Works with the base game's weather and with Open World's variable rain, snow, and wind.

| Control | How to use it |
|---|---|
| **Rear-view mirror** | Grab and drag to aim; right-click to reset. The position is saved between races |
| **Window cranks** | Grab a door's crank and circle your hand: clockwise lowers the window, counterclockwise raises it |
| **Turn-signal stalk** (left) | Down for left, up for right. A half tap flashes three times; full travel latches |
| **Wiper sleeve** (right) | Hold right-click and drag through **0 / I / II / III**: off, intermittent, continuous, fast |
| **Washer stalk** (right) | Pull down to spray and wipe |

## Updates

This repository checks the individual projects for new releases every six hours. Only published releases are included; unreleased changes on their development branches are not copied.

For configuration, compatibility information, and release notes, see the individual project repositories.

## Dependencies

These mods use [HalfGripTools](https://github.com/4l3x4nd3r0/HalfGripTools) and BepInEx. Both can be installed through the [HalfGripMods Installer](https://github.com/4l3x4nd3r0/HalfGripMods).

Some mods have additional requirements or work best together. Check their individual READMEs for details.
