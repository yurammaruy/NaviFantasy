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
- Glowing fairy with translucent wings, a fast flap animation, a halo and a trail of sparkles.
- She hovers next to your character's shoulder (left or right, switching from time to time), never in front of it.
  While she is saying something she moves much less, so the message is easy to read.
- She dodges your mouse cursor: when it gets close she quickly moves away, around it, without leaving the screen,
  so she never gets in the way when you click on something. Her speech bubble is rigidly attached to her: it never
  moves on its own, only together with her.
- She reads your **character name and job**, and calls you by your first name.
- Every message starts with a "Hey!" (sound + bubble) followed by the actual tip.
- **PvE, any role:** warns you when your **HP** or **mana** drops below a configurable percentage, and flies to the
  HP / MP numbers on your HUD to tell you.
- **In a party (dungeons etc.):**
  - **Tank:** tells you when your allies are taking damage.
  - **Healer:** tells you which ally needs healing, and flies to that ally's name in the party list.
  - **DPS:** only your own HP and mana.
- **Any role:** warns you when an enemy is preparing a powerful attack (casts of 3 s or more).
- **Curiosities:** for every hour spent in a safe area (the main cities and residential districts, out of combat)
  she tells a random "Did you know?" fact (100 facts, none repeats until all were told). The hour counter pauses
  outside those areas and resumes when you come back; it only starts over after she told a curiosity or when the
  game is closed.
- **Daily roulettes:** after login (and every 2 hours) she reminds you of the Duty Roulettes whose daily bonus is
  still pending. Which roulettes count can be chosen in the settings.
- **Unread mail:** she tells you when you have unread Moogle letters (best effort: it watches the HUD letter icon).
- **Eat and drink:** every 1h11 she reminds you that your character does not need food or water, but you do.
- **PvP:** the fairy is switched off (PvP features will come later).
- **Languages:** everything she says and shows (messages, settings, commands, curiosities) is available in
  Brazilian Portuguese, English, Japanese, German and French. Pick one in the settings (`/navi`).

## Why this project exists
Navi Fantasy is meant to help players with autism and ADHD to play FINAL FANTASY XIV: gentle reminders for the things
that are easy to forget (dailies, mail, eating and drinking, low HP), kept short and readable, in the player's language.
It is also a love letter to a certain annoying and very useful fairy.

## Commands
- `/navi` opens the settings (language, size, HP %, mana %, healer ally %, reminders).
- `/navi status` shows what she detected: name, job, role, HP/MP, group, PvP.
- `/navi test <hp|mp|heal|tank|boss|fact|daily|mail|water>` plays a sample alert so you can see it without waiting for the real thing.
- `/navi roulettes` lists the roulettes and whether today's bonus is done; `/navi mail` shows what the mail detector sees.
- `/ykff14` ("You Know FF14") makes her tell a random curiosity right now.

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
