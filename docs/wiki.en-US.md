# Xel Launcher User Guide

This guide is for first-time Xel Launcher users. It covers installation, first-time setup, game launch, server switching, account management, sign-in, updates, and common issues. It can be copied directly to GitHub Wiki or maintained as a local Wiki page.

## Table of Contents

- [Overview](#overview)
- [Before You Start](#before-you-start)
- [First Launch](#first-launch)
- [Main Window](#main-window)
- [Configure Game Paths](#configure-game-paths)
- [Install and Update Games](#install-and-update-games)
- [Launch Games](#launch-games)
- [Server Switching](#server-switching)
- [Account Management](#account-management)
- [Custom Launch Arguments](#custom-launch-arguments)
- [Companion Apps](#companion-apps)
- [Sign-In](#sign-in)
- [Tools and External Links](#tools-and-external-links)
- [Global Settings](#global-settings)
- [Configuration and Data Locations](#configuration-and-data-locations)
- [FAQ](#faq)

## Overview

Xel Launcher is an unofficial Windows game launcher built with AntdUI. It manages the PC clients for Arknights and Arknights: Endfield, including multiple server entries, server switching, game download/update, account switching, launch arguments, companion app launching, and sign-in.

Currently supported entries:

| Game | Servers |
| --- | --- |
| Arknights | Official, Bilibili |
| Arknights: Endfield | Official, Bilibili, Global, Google Play |

## Before You Start

Check the following before use:

- Your system is Windows.
- Microsoft Edge WebView2 Runtime is installed. The built-in browser and some web features require it.
- You have downloaded the installer or portable package from the project Releases page.
- If you need server switching, it is recommended to place Xel Launcher on the same disk partition as the game so hard links can be used for faster switching.

Notes:

- Do not place the game root directory in a location that conflicts with anti-cheat component restrictions. Otherwise, the launcher may be unable to read or replace game files.
- Server switching overwrites part of the server-specific files in the target game directory. Always confirm that the selected directory is the correct game root before running it.
- The launcher does not actively upload account files or login credentials. Account switching and backups are completed locally.

## First Launch

1. Open Xel Launcher.
2. Select a game or server entry from the sidebar.
3. Click the main button. If no local game path has been configured, the launcher will ask you to select the game root directory.
4. Select the directory that contains the game executable:
   - Arknights: the directory should contain `Arknights.exe`.
   - Endfield: the directory should contain `Endfield.exe`.
5. After the path is validated, the launcher saves the configuration. You do not need to select it again later.

If the main button shows `Install Game`, the launcher did not detect a complete local game installation at the current path. You can download and install the game directly through the launcher.

## Main Window

The main window has four primary areas:

- Sidebar: switch between game and server entries, and add or manage entries.
- Top bar: language switch, theme switch, settings, help, about, GitHub, and Bilibili links.
- Center area: game cover, announcements, and related information.
- Bottom action area: account management, account selector, start/install/update button, and more menu.

The bottom main button changes automatically based on state:

- `Install Game`: no local game was detected.
- `Update Game`: an update is available.
- `Start Game`: the game is installed and no update is required.
- `Pause` / `Resume`: a download or update task is in progress.

## Configure Game Paths

### Configure During First Launch

1. Select a game entry.
2. Click `Start Game` or `Install Game`.
3. Select the game root directory when prompted.
4. The launcher checks whether the corresponding executable exists in the directory.

### Change Path in Game Settings

1. Open the target game page.
2. Click the more menu in the lower-right corner.
3. Select `Game Settings`.
4. In `Game Install Path`, click `Change Path`.
5. Select the correct game root directory.

`Game Settings` also provides `Open Folder`, which opens the currently configured path.

## Install and Update Games

Xel Launcher checks the local version and remote version, then updates the main button state based on the result.

Install or update flow:

1. Select a game entry.
2. Click the main button when it shows `Install Game` or `Update Game`.
3. Select the install directory for first-time installation. If a path already exists, the launcher uses the current configured path.
4. Wait for download, verification, extraction, installation, or cleanup to complete.
5. After completion, the main button returns to `Start Game`.

If a download or update is interrupted, click `Resume` to continue. If the cache is abnormal, use `Clear Download Cache` from the more menu and try again.

## Launch Games

1. Select the game or server entry to launch.
2. If account switching is enabled, select an account from the account dropdown.
3. Click `Start Game`.
4. The launcher performs the following steps as needed:
   - Ends game processes that may occupy files.
   - Switches server-specific files.
   - Restores the selected account data.
   - Appends custom launch arguments.
   - Launches companion apps.
   - Starts the game executable.

After a successful launch, a notification is shown. If `Close launcher after starting game` or `Hide to tray after starting game` is enabled in global settings, the launcher closes or hides after it confirms that the game has started.

## Server Switching

When multiple server entries share the same game directory, the launcher automatically switches the corresponding server files before launch.

Supported switching directions:

- Arknights Official and Bilibili.
- Endfield Official, Bilibili, Global, and Google Play.

Recommended setup:

1. Configure the correct game path for the official server entry first.
2. In the official server `Game Settings`, use `Sync Path to Bilibili / Global / Google Play`.
3. Confirm that multiple entries point to the same game root directory.
4. When launching different entries, the launcher automatically deploys the corresponding server files.

Manual replacement:

- The Arknights Bilibili entry provides `Replace files with Bilibili`.
- The Endfield Bilibili entry provides `Replace files with Bilibili`.
- The Endfield Global entry provides `Replace files with Global`.
- The Endfield Google Play entry provides its corresponding replacement action.

Manual replacement overwrites related files in the current directory. Confirm that the path is correct and the game has exited before running it.

## Account Management

Account switching applies to:

- Arknights Official
- Endfield Official
- Endfield Global

Bilibili and Google Play entries do not show the account switching option.

### Enable Account Switching

1. Open the target game page.
2. Open the more menu in the lower-right corner.
3. Select `Game Settings`.
4. Enable `Enable Account Switching`.
5. Return to the game page. The account button and account dropdown are shown at the bottom.

### Add and Save an Account

1. Click the account button at the bottom to open account management.
2. Click `+ Add Account`.
3. Enter an account name.
4. Open the game and log in to this account.
5. Return to the account management window and click `Save Account` for that account.

After saving, the launcher backs up the current account data to the local account directory.

### Switch Accounts

1. Select an account from the account dropdown on the game page.
2. Click `Start Game`.
3. The launcher restores that account data before launch.

The account management window supports `Save Account`, `Set as Default`, `Rename`, `Enable/Disable`, and `Delete`. Account deletion requires confirmation.

## Custom Launch Arguments

Each game entry can configure its own launch arguments.

1. Open `Game Settings` for the target entry.
2. Enable `Custom Launch Arguments`.
3. Enter arguments in the input box.
4. The arguments are appended automatically the next time this entry is launched.

The Endfield Google Play entry also provides a `Session Token` setting. You can enter it manually or use `Auto Get Token`. Before automatic retrieval, make sure the game process is already running.

## Companion Apps

Companion apps are programs that start automatically when launching a game, such as assistant tools, statistics tools, or scripts.

1. Open `Game Settings` for the target entry.
2. Enable `Custom Companion Apps`.
3. Click `Manage Companion Apps`.
4. Click `+ Add` and select the program to launch.
5. Add launch arguments for the program if needed.

After this is enabled, the launcher starts the configured companion apps whenever you launch the game through that entry.

## Sign-In

The lower-right more menu provides a `Sign-In` entry. It currently includes:

- Skyland Sign-In
- SKPORT Sign-In

### Skyland Sign-In

Skyland Sign-In supports multiple ways to obtain a token:

- QR code login
- Phone verification code login
- Account/password login
- Manual token input

Use an English semicolon `;` to separate multiple tokens for multi-account sign-in. After configuration, click `Sign In Now`. You can also enable:

- `Sign in while launcher is running`: automatically signs in once per day while the launcher is running and shows the result in the lower-right corner.
- `Auto sign in on startup`: runs sign-in once in the background after Windows starts, without showing the main window, and reports the result through a system notification.

### SKPORT Sign-In

SKPORT Sign-In supports:

- Account/password login to obtain a token
- Manual token input
- Multiple account tokens separated by an English semicolon `;`
- Sign in now
- Sign in while launcher is running
- Auto sign in on startup

If sign-in fails, check the log in the sign-in window first, then confirm that the network, account password, and token are valid.

## Tools and External Links

The tools button in the lower-left corner of the game page provides common links based on the current game:

- PRTS Wiki
- Arknights Toolbox
- Arknights Yituliu
- Endfield Yituliu
- Warfarin Wiki
- Endfield Tools

By default, links open in the built-in browser. If `Use external browser` is enabled in settings, links open in the system default browser.

## Global Settings

Click the settings button in the top bar to open global settings. Common options include:

- `Minimize to tray`: minimize the main window to the system tray when closing it.
- `Start with Windows`: launch with Windows.
- `Close launcher after starting game`: exit the launcher after the game starts successfully.
- `Hide to tray after starting game`: hide to tray after the game starts successfully, then restore after the game exits.
- `Use external browser`: open web links in the system default browser.
- `Use hard links for server switching`: prefer hard links when replacing server files to improve switching speed.

The settings window also includes:

- `Logs`: view launcher runtime logs.
- `Software Update`: check for new versions and download the update installer or portable package.

The top bar also provides:

- Language switch: Chinese / English.
- Theme switch: follow system / light / dark.

## Configuration and Data Locations

Launcher configuration is saved by default at:

```text
%LocalAppData%\XelLauncher\config.json
```

The equivalent path is usually:

```text
C:\Users\<username>\AppData\Local\XelLauncher\config.json
```

Account backup directories:

```text
%LocalAppData%\XelLauncher\AccountBackups
%LocalAppData%\XelLauncher\EndAccountBackups
%LocalAppData%\XelLauncher\GlobalEndAccountBackups
```

To reset launcher configuration, close the app first and then delete `config.json`. To reset account backups, delete the corresponding backup directories.

## FAQ

### The launcher says the exe was not found when I click Start Game

The selected directory is usually not the game root directory. Select the directory that contains `Arknights.exe` or `Endfield.exe`.

### Server switching is slow

Hard links are usually unavailable, so the launcher falls back to file copy. Recommended checks:

- Put Xel Launcher on the same disk partition as the game.
- Enable `Use hard links for server switching` in settings.
- Confirm that the disk file system supports hard links.

### Server switching fails or reports that files are occupied

Exit the game and related processes first, then try again. Restart Windows if needed and run server switching again.

### Account switching fails

Confirm that:

- Account switching is enabled in game settings.
- You have clicked `Save Account` for that account.
- The launcher has permission to read and write account files.
- The game is not currently running.

### Google Play server cannot start correctly with Token

Open `Game Settings` for the Endfield Google Play entry and confirm that `Session Token` has been filled in. If using `Auto Get Token`, start the game first and keep the game process running.

### Built-in web pages do not open

Confirm that WebView2 Runtime is installed. You can also enable `Use external browser` in global settings to open web pages with the system default browser.

### Download or update fails

Try the following:

- Check the network connection.
- Disable proxy or switch networks and try again.
- Use `Clear Download Cache` from the more menu.
- Download the installer or portable package manually from the project Releases page.

### The launcher does not hide or close automatically after launch

Check `Close launcher after starting game` and `Hide to tray after starting game` in global settings. These actions only run after the launcher detects that the game process has started successfully.

## Tips

- After first-time setup, launch each server entry once to confirm that paths, server switching, and account status are correct.
- Before manually replacing server files, confirm the target path and close the game.
- Before account switching, save the current account once with `Save Account`.
- Back up `%LocalAppData%\XelLauncher` regularly if the account data is important.

## Disclaimer

Xel Launcher is an unofficial tool and has no affiliation, ownership, or partnership relationship with Hypergryph or its affiliated organizations. Game images, names, and data copyrights belong to their respective owners.

This tool does not actively upload account files or login credentials. Account switching only reads and writes related data locally. Users are solely responsible for any risks and consequences arising from the use of this tool.
