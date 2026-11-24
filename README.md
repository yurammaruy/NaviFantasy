# Navi Fantasy

A Dalamud plugin for FINAL FANTASY XIV: a Navi-inspired fairy flutters around your character
and plays a "Hey! Listen!" sound every 30 seconds.

**Status:** v0.1 (early test build)

## Features
- Fairy with a fast wing-flap animation, flying around your character on screen.
- `/navi` opens the settings window, which has a single option: **Size**.
- Plays the sound once every 30 seconds while a character is in the world.

## Requirements
- XIVLauncher + Dalamud (API 15)
- To build: .NET 10 SDK (see https://dalamud.dev/plugin-development)

## Audio file (not included in this repository)
The "Hey! Listen!" clip belongs to Nintendo, so it is not committed here. Put your own copy at:

    NaviFantasy/Assets/hey_listen.mp3

If the file is missing the fairy still appears, but stays silent (a warning is written to `/xllog`).

## Build and test
1. `dotnet build -c Release`
2. In game: `/xlsettings` > **Experimental** > **Dev Plugin Locations**, add the folder
   `NaviFantasy/bin/x64/Release/` (or wherever the built `NaviFantasy.dll` ends up).
3. `/xlplugins` > **Dev Tools** > enable **Navi Fantasy**.
4. Type `/navi` to change her size.

## AI disclosure
This plugin was written with heavy AI assistance (Claude, by Anthropic) at the "Copilot" level, and the
wing animation was generated with scripts. If you ever submit it to the official Dalamud plugin repository,
read https://dalamud.dev/plugin-publishing/ai-policy first and disclose this in your pull request.

## Disclaimer
Unofficial fan project, not affiliated with Nintendo or Square Enix. Third-party plugins are not supported
by Square Enix; use at your own risk.
