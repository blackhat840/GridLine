# Gridline product vision

Gridline is a free, open-source Windows launcher for home driving rigs. It brings racing, trucking, and bus simulators into one library. The rig owner chooses which companion apps run with each game and can give a guest a short menu of experiences ready to drive.

| Driver | Experience |
| --- | --- |
| Rig owner | Browse installed games, prepare a session where supported, choose companion apps, and publish presets to Guest Mode. |
| Guest | Pick an owner-approved experience using the wheel D-pad or controller, drive, and return to the preset menu when the game exits. |

## Game and session scope

The catalog below is a first-release target. Verify each title and build on Windows before marking a launch feature supported. Track ordinary launch, prepared menus, and direct sessions as separate capabilities.

| Category | Starter catalog |
| --- | --- |
| Circuit racing | Assetto Corsa, Assetto Corsa Competizione, Assetto Corsa EVO, iRacing, Automobilista 2, Le Mans Ultimate, RaceRoom, rFactor 2, and F1 25. |
| Rally and open-world driving | Assetto Corsa Rally, EA SPORTS WRC, DiRT Rally 2.0, Richard Burns Rally, BeamNG.drive, Forza Horizon 5 and 6, and SuperTuxKart. |
| Trucking | American Truck Simulator and Euro Truck Simulator 2. |
| Bus & Transit | The Bus, OMSI 2, Bus Simulator 27, Bus Simulator 21 Next Stop, Fernbus Simulator, Tourist Bus Simulator, and City Transport Simulator 2026 – Bus & Tram. |

| Label | Launch behavior | Examples |
| --- | --- | --- |
| **Quick Race** | Gridline sends the car, track, and supported settings to the game and starts the session. | Original Assetto Corsa is the lead integration. SuperTuxKart has documented direct race options. |
| **Open Setup** | Gridline prepares available selections. The driver starts the session in the game's own interface. | Assetto Corsa Competizione, EVO, and Rally are candidates. Selection support varies by title and version. |
| **Play** | Opens the title with the apps the owner selected. The driver chooses the session in the game. | ATS, ETS2, the initial bus library, and racing titles awaiting deeper integration. |
| **Quick Route** | Starts a configured vehicle and route where the integration supports it. | Experimental openOMSI candidate. It requires content from an existing OMSI 2 install. |

Use **GT3 at Brands Hatch** as the reference direct session, with an installed car and layout, five laps, and automatic gears. Use ATS to show an ordinary launch. Prototype an openOMSI route as the bus counterpart. Keep deeper launch paths for rFactor 2, Richard Burns Rally, BeamNG.drive, and Live for Speed in the verification backlog.

## Companion apps and profiles

The owner opts in per game or profile. Suggestions start unchecked. Show whether an app is installed, configured, and currently running as separate facts when those states are known. Always offer **Play Game Only**.

| Group | Initial catalog |
| --- | --- |
| Dashboards and rig feedback | SimHub and SIM Dashboard. |
| Racing | Crew Chief, RaceLab, Trading Paints, Garage 61, and Track Titan. |
| Trucking | Trucky and TrucksBook. |
| Custom apps | Owner-selected Windows executable and optional arguments for a wheelbase utility, overlay, button box, or other tool. |

Support ordered startup before or after the game. Reuse an already-running tool. If the owner opts into closing a tool on exit, close only the instance Gridline started for that launch. Continue when an optional tool fails and show its status in the owner view.

Personal and guest profiles are independent. A personal ATS profile could launch SimHub and Trucky. Its guest counterpart could launch only SimHub for the physical dash. Keep account-linked tools opt-in in Guest Mode and surface already-running telemetry tools during owner setup.

## Shared interaction requirements

- Design first for a 1920 × 1080 fullscreen Windows display. Make the primary actions readable from the driving seat.
- Support mouse, keyboard, gamepad, and wheel D-pad navigation. Show a visible focus state and Select and Back hints for the active input. Restore focus when the driver goes back.
- Use plain-language preset names and a short description. Show an estimated time only when the preset provides one.
- Keep the core library, launch profiles, and guest presets local. Companion apps handle their own accounts and service requirements.
- Return to the launcher when the game exits. In Guest Mode, restore focus to the preset the guest last chose.

## What the designs should prove

| Scenario | Pass condition |
| --- | --- |
| Owner starts a Quick Race | The car, track, session settings, and tools to be started are clear before launch. |
| Owner starts a truck or bus sim | The screen explains where route or job selection happens and only shows supported controls. |
| Guest takes a turn | A first-time guest can choose a preset and start it from the wheel, then return to the same menu after driving. |
| An optional app fails | The game still opens and the owner can identify which tool failed. |

## Design directions

- Compact library and session workspace. [Pit Wall](GRIDLINE-PIT-WALL.md)
- Fullscreen, image-led game and preset browsing. [Drive Arcade](GRIDLINE-DRIVE-ARCADE.md)

## Technical references

- Direct launch and prepared-menu behavior for the Assetto Corsa family. [AssettoLauncher](https://github.com/Zelbrad/AssettoLauncher)
- Command-line selection for bus, map, and route. [openOMSI user guide](https://github.com/openOMSI-org/openOMSI/blob/main/docs/USER_GUIDE.md)
- Companion app compatibility and setup. [SimHub](https://www.simhubdash.com/supported-games/), [SIM Dashboard](https://www.stryder-it.de/simdashboard/help/en/For_PC_Gamers/Game_Configuration/index), and [Trucky](https://truckyapp.com/client/)
