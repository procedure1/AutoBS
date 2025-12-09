# AutoBS Beat Saber Automation Mod 
# Featuring 360fyer, Arcitect Arc and Chain Maker, and Auto NJS

---

A Beat Saber mod to automatically generate 360 degree maps from standard maps, and automatically add arcs, chains, lights and walls. Auto NJS Fixer is included primarily to ensure long note spawn distances for 360 maps (since short distances with rapid turning can make it hard to see notes coming). However, it can be used to automatically set your favorite note speed and spawn distance and works across most songs unless note density gets too high. Most features fully work on non-generated maps as well.

NOTE: This mod can be used to output and convert to v2, v3, and v4 JSON dat files containing all generated features. See below.

The orginal 360fyer mod was created by the genius CodeStix. https://github.com/CodeStix/
I have updated the mod since it has been dormant for a long time.

This version has lots of customization and supports v3 and v4 maps. Much of the customization in this update is centered around rotation updates and flexibility, automatic arcs and chains, and attempted visual improvements for 360 maps. As you probably know, 360 maps have had the same environment since they first came out in 2019. The 360 environment is very low key with dim narrow lasers compared to modern environments. Since the 360 environment doesn't work with v3 GLS lights (Group Lighting System), new OST maps have no lights in the 360 environment. So I attempted to make 360 a bit flashier. Boost lighting events add more color to maps that don't have them. Automapper lights power larger and brighter lasers. If you hate it, disable it :) As an automation tool, this tool can cleanup small problems with `Beat Sage` generated maps.

Note: Vivify maps and many complex Noodle maps are currently incompatible and disabled for 360fyer.

[![showcase video](https://github.com/procedure1/AutoBS/blob/master/AutoBS-Big-Lasers-Big-Walls.gif)](https://www.youtube.com/watch?v=xUDdStGQwq0)
## Beat Saber PC Version
v1.40.0

## Installation

- You can install this mod using ModAssistant.
- Or install this mod manually by downloading a release from the Releases tab: https://github.com/procedure1/autobs/releases and placing it in the `Plugins/` directory of your modded Beat Saber installation.
- Requires CustomJSONData Mod (also with ModAssistant)
- Recommended to install Mapping Extensions Mod (also with ModAssistant) if you want full wall custimization
- Aeroluna's 'Technicolor' dod is awesome with 360fyer

- ![Technicolor with 360fyer](https://github.com/procedure1/AutoBS/blob/master/AutoBS-Big-Lasers-Big-Walls-Technicolor.gif)
## 360fyer

`360fyer` will take a standard map and create a new map with rotation events. After installation, **every** beatmap will have the 360 degree gamemode enabled. Just choose `360` when you select a song. The level will be generated once you start the level.
The algorithm is completely deterministic and does not use random chance, it generates rotation events based on the notes in the *Standard* beatmap (the base map can be changed in the menus from "Standard" to "OneSaber", "NoArrows", "90Degree", or even "360Degree" as well).

Wireless headset users can use the `Wireless 360` menu setting which has no rotation limits and less tendencies to reverse direction. Tethered headset users have rotation limiting settings to make sure to not ruin the cable by rotating too much. You can also use these settings if your play space is limited (for example you could limit rotations to 150° or 180° if you want to face forward only).

Rotations size and frequency can be adjusted in the menu and headset FOV limits can be set so that rotations don't move outside your peripheral vision.

## Arcitect Arc and Chain Maker

`Arcitect` automatically adds arcs and chains to maps that don't have them. Not as good as a human, of course! But better than nothing. Long duration chains are available but get impossible to hit the segments when they get too long.

NOTE: Chains added to a map will change the scoring so I have disabled score submission for maps that get chains added.

## Auto Lighting

`Auto Lighting` automatically adds lighting events to maps that don't have them. Thanks to Loloppe (based on their ChroMapper-AutoMapper)! I made many changes so anything crappy is my fault :) You can choose to add 'boost' lighting events as well. And 360 environment lasers are fat and bright to enliven the boring 360 environment. Human crafted lights are best, machine made lights are ok, no lights suck! (I was considering adding automatic lighting for GLS environments as well in the future.)

Note: Aeroluna's `Technicolor` mod is awesome with 360fyer if you play a lot of 360 maps.

## Auto NJS Fixer

Thanks to Kylemc for allowing me to work from their original code! The orginal NJS Fixer is designed to be used on a per song basis more or less. `Auto NJS Fixer` is designed to set it and forget it. 360 maps with rapid turns prefer a long note spawn distance hence the need for this. You can choose `Maintain Speed` if you want to keep the mappers intended perceived speed but increase or change the spawn distance. Or you can use `Set Note Speed` to set your favorite speed and spawn distance and this works well for most songs until note density gets very tight.

NOTE: If settings cause the note speed to be slower than the mapper intended, score submission will be disabled.

## Auto Wall Generator

The original 360fyer generated awesome walls. This version adds Mapping Extension walls (if you have it installed) and creates tons of walls to help enliven the boring 360 enviroment. But you can add this to any environnment. 

NOTE: Dense walls in 360 can be claustophobic and distracting but you can disable them, reduce them, or move them away from your play space if you like.

## Beat Sage Cleaner

Can remove some impossible note combinations common with Beat Sage generated maps. Shortens long crouch walls and removes stray notes (notes far from the main body of song notes) at the start or end of maps.

## Menu Settings and Config file

There is a settings menu in-game. Or you can tweak settings in the `Beat Saber/UserData/AutoBS.json` config file. (You can open this file with notepad or another text editor.)


| Option                        | Description                                                                                                                                                                                                                                                                                                                                                  |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **AutoBS**                    | Main toggle and toolset that powers all automatic generation and modification: 360° maps, arcs, chains, NJS fixes, lighting, and walls. Requires a restart (or **OPTIONS → SETTINGS → OK**) for maps that have already been selected in the menu. Disable this to disable everything.                                                                                                                                                                                                     |
| **360fyer**                   | 360° rotation engine. Generates 360° maps from Standard maps. Unlikely to work well with Vivify, Noodle, Mapping Extensions, or maps that contain thousands of walls. Requires a restart (or **OPTIONS → SETTINGS → OK**) for maps that have already been selected in the menu.                                                                         |


***360fyer – Rotation Settings***

| Option                   | Description                                                                                                                                                                                                                                                                                                      |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Wireless 360**         | Default: **True**. For wireless headsets. When enabled, generated maps have no rotation restrictions and are less likely to repeatedly reverse direction, producing smoother rotational flow.                                                                                                                    |
| **Limit Rotations 360**  | Default: **360°**. Limits maximum allowed accumulated rotation. Disabled if **Wireless 360** is enabled. For wired headsets, use 360° or less to avoid twisting your cable (“cable rip”). You can lower this in tight spaces or rooms with furniture. **Score submission is disabled** if this is set below 90°. |
| **Rot Speed Multiplier*** | Default: **1.0**. Scales how often rotations occur (frequency). Less than 1 slows rotations; greater than 1 increases them. **Score submission is disabled** if set below 0.3. Very high values are still constrained by FOV settings and can push rotations closer to your peripheral boundaries. * ***Bump this up for more rotations!***               |
| **Add Extra Rotations**  | Default: **True**. Adds extra rotations in a consistent direction to maps that would otherwise have low overall rotation, making them feel more “360-like.”                                                                                                                                                      |
| **Min Rotation Size***    | Default: **15°**. Minimum rotation step size. Rotations are typically 15° or 30°. FOV rules may force this down to 15°. Larger minimum steps increase the intensity of each rotation. * ***Bump this up for larger rotations angles!***                                                                                                                           |
| **Max Rotation Size**    | Default: **30°**. Maximum rotation step size. While 15° and 30° are typical, 45° rotations may fall outside peripheral vision on smaller-FOV headsets. FOV rules may also force this value lower.                                                                                                                |
| **FOV**                  | Default: **80°**. Set to slightly below your headset’s actual field of view. This is used to keep rotations within your peripheral vision over time. 80° is recommended for Quest 2 and 3.                                                                                                                       |
| **FOV Time Window**      | Default: **0.35 s**. Time window used to evaluate cumulative rotations against your FOV. Prevents multiple small rotations from stacking into a large FOV-breaking turn within this time span. Lower values allow more rapid rotations that can edge toward or outside the periphery.                            |
| **Wall Removal Mult**    | Default: **1.0**. Controls how aggressively vision-blocking walls are removed during rotation sequences. Higher values remove more chaotic walls that pass in front of the player and block visibility.                                                                                                          |
| **Wall Note Dist**       | Default: **0.2 s**. Minimum allowed time distance between a note and nearby walls, accounting for rotation events that may bring notes and walls closer together. Increase this if you want more breathing room between notes and walls.                                                                         |
| **Base Map**             | Default: **Standard**. Chooses which map type is used as the base for 360fyer-generated maps (e.g., Standard vs. other modes). **Score submission is disabled** if this is not set to Standard, and a restart may be required for changes to take effect.                                                        |

***Architect – Arcs***

| Option                        | Description                                                                                                                                                                                                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Arc Rotation Mode**         | Default: **Net Zero**. Controls how arcs behave during rotations. **Force Zero** disables rotations during arcs. **Net Zero** allows rotations that sum to a net zero degrees over the arc duration. **No Restrictions** allows full rotations during arcs, which can be more challenging. |
| **Force Natural Arc Swings**  | Default: **True**. Encourages more 180-degree relationships between head and tail cut directions so arcs feel more natural to swing. Turning this off allows more variety in arc shapes but can produce some less natural-feeling motions.                                                 |
| **Pref Count per Min (Arcs)** | Default: **12**. The target number of arcs per minute of music. The system attempts (but does not guarantee) to reach this count while respecting other constraints.                                                                                                                       |
| **Min Duration (Arcs)**       | Default: **0.9 s**. Shortest allowed arc duration. This value may be relaxed internally if needed to better match your preferred arc count.                                                                                                                                                |
| **Max Duration (Arcs)**       | Default: **2.5 s**. Longest allowed arc duration. Longer arcs than this will not be generated.                                                                                                                                                                                             |

***Architect – Chains***

| Option                          | Description                                                                                                                                        |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Pref Count per Min (Chains)** | Default: **10**. Target number of chains per minute. The system attempts to reach this count while balancing musical and gameplay constraints.     |
| **Chain Time Bumper**           | Default: **0.2 s**. Minimum allowed time between a chain and surrounding notes. Helps prevent chains from colliding with other notes rhythmically. |
| **Enable Long Chains**          | Default: **True**. Enables long chains that behave similarly to arcs in terms of length/feel, adding more continuous chain segments.               |
| **Long Chain Max Duration**     | Default: **0.425 s**. Maximum allowed duration for a chain. Longer chains can become awkward to hit, so they are limited to this threshold.        |

***Auto NJS Fixer***

| Option                       | Description                                                                                                                                                                                                                                                                                                   |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enable for Practice Mode** | When enabled, Auto NJS Fixer attempts to apply its adjustments in Practice Mode. However, it will be overridden if PracticePlugin is installed.                                                                                                                                                               |
| **Mode**                     | Default: **Maintain Speed**. **Maintain Speed** keeps the perceived note speed intended by the map while allowing you to change note spawn distance. **Set Speed** lets you force a specific note speed, which can improve readability or comfort. Short spawn distances are particularly hard to see in 360. |
| **Note Speed**               | Default: **10**. Overrides the map’s note speed (NJS). Setting this to **0** reverts to the map’s original NJS. **Score submission is disabled** if you set this below the map’s original speed.                                                                                                              |
| **Note Spawn Distance**      | Default: **30**. Overrides the map’s note spawn distance (JD). Setting this to **0** uses the map’s original spawn distance. Larger values push notes further out, giving more time to read patterns—especially important in 360 maps.                                                                        |

***Auto Lighting***

| Option                       | Description                                                                                                                                                                                      |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Big 360 Lasers**           | Default: **True**. Scales up laser sizes in 360°/90° environments. The stock 360 environment has small, weak lasers without this.                                                                |
| **Bright 360 Lights**        | Default: **True**. Brightens low-key lights in 360°/90° environments. Again, the base 360 environment is fairly dim without this.                                                                |
| **Boost Lighting Events**    | Default: **True**. Adds “boost” lighting events to maps that don’t have them, making colors and lighting more dramatic. Works best when Beat Saber **COLORS → OVERRIDE DEFAULT COLORS** is used. |
| **Enable Automapper Lights** | Uses note-based automatic lighting for maps that lack lights. Based on ChroMapper-AutoMapper by Loloppe (heavily tweaked for AutoBS; any issues are likely from these tweaks).                   |
| **Frequency Multiplier**     | Default: **1.0**. Scales how frequently automapped light events are placed. Lower values reduce the number of events for a less busy light show.                                                 |
| **Brightness Multiplier**    | Default: **1.0**. Scales the brightness of automapped lights. Increase to make the overall light show brighter and more vivid.                                                                   |
| **Light Style**              | Default: **Med Flash**. Chooses the automapper lighting style. Styles lower in the list tend to be less strobe-like and more relaxed.                                                            |

***Beat Sage Cleaner***

| Option                           | Description                                                                                                                                                                                                                         |
| -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Set Crouch Wall Max Duration** | Default: **0.75 s**. Shortens long crouch walls created by Beat Sage to this maximum duration, making them more playable and less punishing.                                                                                        |
| **Stray Notes Remover**          | Default: **6 s**. Removes up to four stray notes at the start or end of a map if they are further than this time from the main note stream. Setting this to **0** turns the feature off. Works nicely with “Intro Skip” style mods. |

***Existing Wall Manipulation***

| Option                 | Description                                                                                                                                                 |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Allow Crouch Walls** | Allows crouch walls in the final output. These can be difficult to see and react to in fast 360 maps, so disabling this can make maps more comfortable.     |
| **Allow Lean Walls**   | Allows lean walls in the final output. Like crouch walls, lean walls are harder to anticipate on fast 360 maps, so some players may prefer to disable them. |

***Auto Walls – Generated Walls (Standard & Big)***

| Option                             | Description                                                                                                                                                               |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Standard Walls**                 | Enables generation of standard-size walls for added visual interest.                                                                                                      |
| **Big Walls**                      | Enables generation of very wide walls for dramatic visual effects.                                                                                                        |
| **Standard and Big Walls Mult**    | Default: **100%**. Percentage multiplier controlling how many Standard and Big walls are generated. Increase for more walls; decrease for fewer.                          |
| **Standard and Big Walls Min Dis** | Default: **0**. Minimum distance from the player (by line index) for Standard and Big walls. Increase this to move generated walls further out from the center playspace. |

***Auto Walls – Generated Walls (Mapping Extensions)***

All options below require Mapping Extensions to be installed.

***Distant Walls***

| Option                       | Description                                                                                                     |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------- |
| **Enable Ext Distant Walls** | Enables mostly large, distant walls that appear far from the player or high above.                 |
| **Distant Walls Mult**       | Default: **2**. Multiplier controlling how many distant walls are generated. Higher values generate more walls. |

***Column Walls***

| Option                      | Description                                                                        |
| --------------------------- | ---------------------------------------------------------------------------------- |
| **Enable Ext Column Walls** | Enables tall, narrow column walls.                                                 |
| **Column Walls Mult**       | Default: **1**. Controls how many column walls are generated.                      |
| **Column Walls Min Dis**    | Default: **2**. Minimum distance from the player (by line index) for column walls. |
		

***Row Walls***

| Option                   | Description                                                                     |
| ------------------------ | ------------------------------------------------------------------------------- |
| **Enable Ext Row Walls** | Enables long, wide rows of walls.                |
| **Row Walls Mult**       | Default: **1**. Controls how many row walls are generated.                      |
| **Row Walls Min Dis**    | Default: **3**. Minimum distance from the player (by line index) for row walls. |


***Tunnel Walls***

| Option                      | Description                                                                                    |
| --------------------------- | ---------------------------------------------------------------------------------------------- |
| **Enable Ext Tunnel Walls** | Enables thin side and overhead wall groups that form tunnel-like structures around the player. |
| **Tunnel Walls Mult**       | Default: **1**. Controls how many tunnel wall groups are generated.                            |
| **Tunnel Walls Min Dis**    | Default: **0**. Minimum distance from the player (by line index) for tunnel walls.             |

***Grid Walls***

| Option                    | Description                                                                                                                                       |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enable Ext Grid Walls** | Enables grids of small walls. Depending on frequency, these can be huge, sparse grids or smaller, dense grids.                                    |
| **Grid Walls Mult**       | Default: **1**. Frequency of grid generation. At lower values, grids are rarer but larger; at higher values, grids are more frequent and smaller. |
| **Grid Walls Min Dis**    | Default: **2**. Minimum distance from the player (by line index) for grid walls.                                                                  |


***Window Pane Walls***

| Option                           | Description                                                                             |
| -------------------------------- | --------------------------------------------------------------------------------------- |
| **Enable Ext Window Pane Walls** | Enables flat, thin walls like window panes.                                             |
| **Pane Walls Mult**              | Default: **1**. Controls how many window-pane walls are generated.                      |
| **Pane Walls Min Dis**           | Default: **0**. Minimum distance from the player (by line index) for window-pane walls. |


***Particle Walls***

| Option                          | Description                                                                                                                                                                                                                                                                      |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enable Ext Particle Walls**   | Enables bursts of tiny particle-like walls.                                                                                                                                                                                                                   |
| **Enable Large Particle Walls** | Default: **True**. Enables larger, note-sized particle walls. These can sometimes be confused with actual notes, so use with care.                                                                                                                                               |
| **Particle Walls Mult**         | Default: **4**. Controls how frequently particle wall batches are generated. Higher values mean more batches.                                                                                                                                                                    |
| **Particle Walls Batch Size**   | Default: **20**. Maximum number of walls per particle batch. Higher values create denser particle bursts.                                                                                                                                                                        |
| **Particle Walls Min Dis**      | Default: **0**. Minimum distance from the player (by line index) for particle walls.                                                                                                                                                                                             |
| **Max Wait Time**               | Default: **6 s**. Particle and floor wall calculations can slow map generation and even cause Beat Saber to appear frozen on heavy maps. This setting automatically reduces the number of particle and floor walls when generation time reaches this limit, improving stability. |


***Floor Walls (and ceiling tiles and miniture cityscapes)***

| Option                     | Description                                                                                                                                                                 |
| -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enable Ext Floor Walls** | Enables bursts of thin, flat walls like floor tiles under (and sometimes above) the player. These can occasionally form miniature “cityscape” patterns of walls around you. |
| **Floor Walls Mult**       | Default: **4**. Controls how frequently floor wall batches are generated. Higher values mean more batches (and heavier computation).                                        |
| **Floor Walls Batch Size** | Default: **20**. Maximum number of floors in each batch. Larger batches create denser carpets of floor walls.                                                               |
| **Floor Walls Min Dis**    | Default: **0**. Minimum distance from the player (by line index) for floor walls.                                                                                           |


***

## How to build

To test and build this project locally, do the following:
1. Clone this repo & open the project in Visual Studio
2. Make sure to add the right dll references. The needed dlls are located in your modded Beat Saber installation folder.
3. Build. Visual Studio should copy the plugin to your Beat Saber installation automatically.

## Todo

Beat Saber Mixed Reality Mode support
Modern GLS lighting and evironment support


