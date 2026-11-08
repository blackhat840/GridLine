# GridLine

GridLine Sim Launcher is a fullscreen Windows launcher for home driving rigs. Browse games in Play and Library, then launch through Steam or a local executable.

## Build and run

On 64-bit Windows 10 or 11, run the following in PowerShell. The build uses the Windows .NET Framework desktop compiler.

```powershell
.\build.ps1
.\build\GridLine.exe
```

GridLine scans Steam's configured libraries across drives and checks for standalone iRacing. In **Settings**, choose **Add Game** for a local executable. In **Game Details**, choose **Locate Game** to link an executable to a catalog entry. The **Installed** filter shows games with an available install path.

The built-in catalog lists selected driving and racing games by Steam App ID. In **Game Details**, choose **Hide Game** to remove a title from Play and Library. To bring it back, open the top-right cog and choose **Hidden Games**, then **Restore**. GridLine remembers these choices across rescans and restarts.

Use the mouse, arrow keys, or an XInput controller to select a game. **Enter / A** launches the selection. **Esc / B** goes back. **Page Up / Page Down / LB / RB** changes the Library filter or opens the Library from Play. **Y** opens Game Details. The top-right cog, **F1**, or **Start** opens Settings. The three color schemes share one header and the same controls. A brighter shade of the theme color sweeps across the header in 1.45 seconds. It runs when GridLine becomes active and every five seconds afterward. GridLine follows Windows' animation preference.

Use **Add Favorite** or **Remove Favorite** in **Game Details** to manage the Favorites strip on Play. When no favorites are visible, the featured game expands to fill the Play screen. A subtle circuit-line sweep runs behind Play artwork and follows the Windows animation setting. Hidden games and the **Installed Only** setting still control which titles appear.

The action controls in the bottom bar work with a mouse. In Library, click **LB** for the previous filter or **RB** for the next one.

## Rig launch setup

Open **Game Details → Launch Setup** to pick the companion executables you want to start before that game. GridLine skips a companion if its selected executable is already running. An unavailable companion is reported and the game can still start.

For Assetto Corsa, choose **Set Up No Hesi** or **Content Manager** and select its installed launcher executable. Use **Start** beside the saved launcher in Launch Setup to run it with the same companions. Use **Add Launcher** for other installed alternate launchers. The alternate launcher handles its own sign-in, mods, and server selection. [No Hesi Launcher](https://nohesi.gg/launcher)

Turn on **Start at Login** in Settings to start GridLine for your current Windows account at sign-in. Enable it from the build and location you plan to keep on the rig. The intro starts when GridLine is active and pauses if focus moves away.

Enable **Kiosk Handoff** in Settings to keep GridLine above the desktop while a game or alternate launcher opens. GridLine follows the launched process and its child processes, plus anything running from the game's install folder. When a launcher or game window appears, GridLine hands it the foreground. When the session ends, GridLine returns to the front if the desktop, Steam, or the window left behind by the closed session is in front. If you switch to another app yourself, GridLine leaves it there. A launcher that stays in the tray after the game closes keeps the session open for up to 20 seconds. **Esc** releases a pending handoff. Windows still manages focus and the external game windows.

## Steam ownership

Open **Settings → Steam Library → Connect Steam** and enter your SteamID64 and a Steam Web API user key. GridLine shows a preview before changing the library. **Matched** lists titles from the driving catalog. Use **Review Unmatched** to classify a driving game that GridLine does not know, then select **Apply Import**. Steam Game Details visibility can affect what the API returns. An empty or failed response keeps the last import.

The **Owned** filter shows matched owned titles. **Catalog** lets you browse all approved titles. **Installed Only** in Settings filters both views for a ready-to-drive rig. Hidden titles remain hidden across imports. Steam sync includes free titles you have played when Steam returns them. Known free-to-play titles such as RaceRoom can also be installed from GridLine. **Rescan Games** checks for completed installations.

Steam sync is optional. GridLine stores its last ownership snapshot locally and protects the key using Windows DPAPI for the current Windows user. **Disconnect Steam** can keep or clear the snapshot. The independent SteamGridDB key is managed in the artwork picker.

## Game details and artwork

GridLine looks for Steam library artwork on the PC. Library prefers a portrait cover for the selected game when one is available and fits wide images inside the panel. Games without artwork get a themed placeholder. To override artwork, put a PNG or JPEG named hero or tile in the game's folder under %LOCALAPPDATA%\GridLine\artwork. For example, place Assetto Corsa's hero image in %LOCALAPPDATA%\GridLine\artwork\244210\hero.jpg. Then choose **Rescan Games** in Settings.

Open **Game Details** for game facts, Steam reviews, and source links. **View on Steam** opens the game page on the Steam Store in your browser. **Watch Trailer** appears when a direct trailer link is available. Add PNG or JPEG files under the game's `artwork/<id>/screenshots` folder to show them in the fullscreen gallery. A `game.json` in the game's artwork folder can override the fields `Summary`, `Developer`, `Publisher`, `Release`, `OfficialSite`, `Trailer`, `RigGuide`, `Wheel`, `VR`, and `Telemetry`.

Under **Game Details → Artwork**, use **Find Heroes** or **Find Tiles** to preview SteamGridDB choices with your own provider key. GridLine stores only the selected assets, with creator credit in the picker. These assets are for your local library and are not bundled in the launcher. Use **Reset Artwork** to return to local Steam artwork or a placeholder. [SteamGridDB API](https://www.steamgriddb.com/api/v2)

For richer facts, run the optional Python 3.8+ [IGDB bridge](tools/igdb_provider.py) with `GRIDLINE_IGDB_CLIENT_ID` and `GRIDLINE_IGDB_CLIENT_SECRET` set in its environment, then enable **IGDB Metadata** in Settings. The bridge listens at `127.0.0.1:8766` and the Twitch application secret stays in the bridge process. See IGDB for account, attribution, and commercial-use terms. [IGDB API](https://api-docs.igdb.com/)

The supplied design mockups can be kept locally under docs\design\mockups for preview artwork. The app builds with source files alone. Themes, hidden games, and manual game links are saved in %LOCALAPPDATA%\GridLine\settings.json.

[Product and design briefs](docs/design) · [Steam integration plan](docs/design/GRIDLINE-STEAM-INTEGRATION-PLAN.md)
