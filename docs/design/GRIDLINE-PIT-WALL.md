# Gridline concept A · Pit Wall

[Product vision](GRIDLINE-PRODUCT-VISION.md) · [Drive Arcade](GRIDLINE-DRIVE-ARCADE.md)

Pit Wall is a workspace for the person running the rig. A stable navigation rail, compact game list, and persistent launch summary give the owner a clear view of the selected session and its companion apps. Guest Mode keeps that visual language and presents it at a larger scale.

## Visual direction

| Element | Direction |
| --- | --- |
| Palette | Graphite canvas **#11151A**, panel **#1B232D**, warm white text **#F4F2EC**, and signal amber **#F4B74D**. Use dark text on amber controls and give focus its own contrasting outline. |
| Typography | DIN-inspired headings, a neutral sans-serif for controls, and tabular numerals for laps, times, and launch order. |
| Imagery | One restrained image in the selected game's header. Keep controls and status text on solid surfaces. Give the circuit, highway, and city categories equal weight. |
| Motion | Brief focus and selection transitions. Keep panel positions stable when the selection changes or a companion status updates. |

## Five screens

| Screen | Layout and behavior |
| --- | --- |
| **Home and Library** | Persistent rail for Home, Racing, Trucking, Bus & Transit, and Settings. The middle pane lists installed games with capability labels. The right pane shows the selected title, recent preset, tool summary, and main action. Place **Guest Mode** in a consistent top-level position. |
| **Quick Race** | Put car and track selectors beside the session settings. Keep the summary visible with **GT3 at Brands Hatch**, track layout, five laps, automatic gears, and selected apps. End the focus order on the launch button. |
| **Truck and bus launch** | ATS shows **Play** and its companion summary. The standard OMSI 2 launch uses the same structure. A separately labeled experimental openOMSI preset swaps racing controls for bus, map, and line. |
| **Companion Apps** | Compact rows with a checkbox, app name, install or process status, and timing. Provide **Move Up** and **Move Down** for reordering from the wheel or keyboard. Keep **Add Custom App** and **Play Game Only** easy to find. |
| **Guest Mode** | A full-width grid of three or four owner-approved presets. Show the preset name, category, short description, and one main **Drive** action. Use **5 laps · Automatic gears** for the race example. |

## Prototype details

- Start at 1920 × 1080. Try a navigation rail around 220 px and a summary pane around 380 px. Adjust these working dimensions against the content and viewing distance.
- Give **Installed**, **Already running**, and **Setup needed** explicit labels. Show SimHub already running and a second, optional app that needs setup. The launch action remains available.
- Show saved selection and input focus as separate states. Up and Down move through the library, Right moves to game details, and Back restores the previous focus.
- Prototype **Library → Assetto Corsa → Quick Race → Launch** and **Guest Mode → GT3 at Brands Hatch → Drive → Return to Gridline**.

Judge this direction on how quickly the owner can change a session or app profile while keeping the launch summary in view. Use the same presets in Drive Arcade to compare setup speed and ease of navigation from the seat.
