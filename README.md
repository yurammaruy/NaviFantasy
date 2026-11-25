# Navi Fantasy

A Dalamud plugin for FINAL FANTASY XIV: a Navi-inspired fairy flutters around your character
and plays a short "Hey!" sound every 30 seconds, with a speech bubble.

**Status:** v0.1 (early test build)

## Install (players)
1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this link in the empty box, then click the **+** button:

       https://github.com/yurammaruy/NaviFantasy/releases/download/latest/repo.json

3. Click the **Save** button (bottom right).
4. Type `/xlplugins`, search for **Navi Fantasy** in *All Plugins* and click **Install**.

## Features
- Fairy with a fast wing-flap animation, flying around your character on screen.
- `/navi` opens the settings window, which has a single option: **Size**.
- Plays a sound once every 30 seconds while a character is in the world.

## Build (developers)
Requires the .NET 10 SDK.

1. `dotnet build -c Release`
2. In game: `/xlsettings` > **Experimental** > **Dev Plugin Locations**, add the folder with the built `NaviFantasy.dll`.
3. `/xlplugins` > **Dev Tools** > enable **Navi Fantasy**.

Every push to `main` is built automatically by GitHub Actions and published as the **latest** release together with
a fresh `repo.json`. The version number goes up on every build (0.2.<build number>.0), so Dalamud offers the update
by itself. The `repo.json` in the repository is only the template for that file.

## Custom sound
The sound is `NaviFantasy/Assets/hey_listen.mp3` (made by the author as a sound-alike, not an official recording).
To use another sound, replace that file with your own MP3 and rebuild.

## AI disclosure
This plugin was written with heavy AI assistance (Claude, by Anthropic) at the "Copilot" level, and the
wing animation was generated with scripts. If you ever submit it to the official Dalamud plugin repository,
read https://dalamud.dev/plugin-publishing/ai-policy first and disclose this in your pull request.

## Disclaimer
Unofficial fan project, not affiliated with Nintendo or Square Enix. Third-party plugins are not supported
by Square Enix; use at your own risk.
