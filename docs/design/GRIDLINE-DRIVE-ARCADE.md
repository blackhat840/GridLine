# Gridline concept B · Drive Arcade

[Product vision](GRIDLINE-PRODUCT-VISION.md) · [Pit Wall](GRIDLINE-PIT-WALL.md)

Drive Arcade is a fullscreen interface for the driving seat. One game or preset gets the focus of the screen. Large artwork and short labels help the driver recognize their choices from a distance, and Guest Mode uses the same controls to get someone driving quickly.

## Visual direction

| Element | Direction |
| --- | --- |
| Palette | Midnight canvas **#101927**, elevated panel **#1D2C3C**, soft white text **#F6F1E7**, and mint **#59E6CF** for focus and the main action. Use coral **#FF927A** as a secondary accent and dark text on bright fills. |
| Typography | Broad, humanist display type for titles and a readable sans-serif for instructions, categories, and control hints. |
| Imagery | One dominant image for the focused experience, with quieter supporting cards. Use circuit, highway, and city scenes to establish the categories. Use placeholder or cleared artwork in mockups. |
| Motion | Short crossfades when artwork changes. With reduced motion, switch the artwork immediately and retain the same focus indicator. |

## Five screens

| Screen | Layout and behavior |
| --- | --- |
| **Home and Library** | A horizontal category bar and a large featured area above a row of installed games. Give Trucking and Bus & Transit their own browse views. The focused tile updates the title, launch capability, and main action. Place **Guest Mode** in the header. |
| **Quick Race** | A fullscreen picker with large **Car**, **Track**, and **Session** cards. Each opens a focused list or settings panel. Keep the launch summary and input hints in a fixed footer. Use the same GT3 and Brands Hatch example as Pit Wall. |
| **Truck and bus launch** | Give ATS a highway image, **Play** action, and tool summary. Give the experimental openOMSI preset a city route image and a details sheet for bus, map, and line. Clearly label where setup takes place. |
| **Companion Apps** | Open an owner-only settings sheet from the game page. Use app cards with checkboxes and short purpose labels. Keep launch timing, order, and exit behavior in an expandable section. Leave **Play Game Only** on the game page. |
| **Guest Mode** | Center the focused experience in one row of large preset cards. Show a race, a truck drive, and a bus option when configured. Put its description and **Drive** directly below. Restore the same focused card on return. |

## Prototype details

- Start at 1920 × 1080. Keep the active card and primary action in the central viewing area, with space for visible focus and readable text.
- Use a strong outline for input focus and a separate indicator for the saved selection. Left and Right browse choices, Down moves to the main action, and Back returns to the previous view.
- Use short game-launch copy such as **Opens American Truck Simulator. Choose your job in the game.** For the race preset, use **Five laps in a GT3 car with automatic gears.**
- Prototype **Home → Assetto Corsa → Quick Race → Launch** and **Guest Mode → GT3 at Brands Hatch → Drive → Return to Gridline**. Show the owner view when an optional app is missing and keep the game launch available.

Judge this direction on whether a first-time guest can identify the focused experience, start it, and find their way back without instruction. Use the same presets in Pit Wall to compare ease of use and owner setup speed.
