# AutoBS — Beat Saber Automation Mod
# Featuring 360fyer, Arcitect Arc + Chain Maker, Auto Lights, Auto Walls, and Auto NJS Fixer

## For Beat Saber PC Version v1.42


A Beat Saber mod to automatically generate 360 degree maps from standard maps, and automatically add arcs, chains, lights and walls, and set note speed and spawn distance. Most features fully work on non-generated maps as well. So you can add arcs and walls etc to standard maps too.

NOTE: This mod can be used to output and convert to v2, v3, and v4 JSON beatmap `.dat` files containing all generated features. See below.

The original 360fyer mod was created by the genius CodeStix. https://github.com/CodeStix/
I have updated the mod since it has been dormant for a long time.

This version has lots of customization and supports v3 and v4 maps. Much of the customization in this update is centered around rotation updates and flexibility, automatic arcs and chains, and attempted visual improvements for 360 maps. As you probably know, 360 maps have had the same environment since they first came out in 2019. The 360 environment is very low-key with dim, narrow lasers compared to modern environments. Since the 360 environment doesn't work with v3 `GLS` lights (Group Lighting System), new `OST maps` converted to 360 have no lights in the 360 environment without `Auto Lights` lights. So I attempted to make 360 a bit flashier. `Boost lighting events` add more color to maps that don't have them. `Auto Lights` lights power larger and brighter lasers and optional strobe events. If you hate it, disable it :) I've added auto `Mapping Extensions` Walls to make the environment more intereseting as well. As an automation tool, this mod can also clean up small problems with `Beat Sage`-generated maps.

Note: Vivify maps and many complex Noodle maps are currently incompatible and disabled for 360fyer.

[![showcase video](https://github.com/procedure1/AutoBS/blob/master/AutoBS-Big-Lasers-Big-Walls.gif)](https://www.youtube.com/watch?v=xUDdStGQwq0)

## Installation

- You can install this mod using BSManager or ModAssistant (coming soon).
- Or install this mod manually by downloading the `AutoBS.dll` from the Releases tab: https://github.com/procedure1/autobs/releases and placing it in the `Plugins/` directory of your modded Beat Saber installation.
- Requires `CustomJSONData` mod (also via BSManager or ModAssistant)
- Recommended: install Kylemc's `Mapping Extensions` mod if you want full wall customization.
- Aeroluna's `Technicolor` mod is awesome with 360fyer

![Technicolor with 360fyer](https://github.com/procedure1/AutoBS/blob/master/AutoBS-Big-Lasers-Big-Walls-Technicolor.gif)

## 360fyer

`360fyer` will take a standard map and create a new map with rotation events. After installation, every beatmap will have the 360-degree game mode enabled. Just choose 360 when you select a song. The level will be generated once you start the level.
The algorithm is completely deterministic and does not use random chance; it generates rotation events based on the notes in the Standard beatmap (the base map can be changed in the menus from "Standard" to "OneSaber", "NoArrows", "90Degree", or even "360Degree" as well).

Wireless headset users can use the `Wireless 360` menu setting, which has no rotation limits and fewer tendencies to reverse direction. Tethered headset users have rotation-limiting settings to make sure they don’t ruin the cable by rotating too much. You can also use these settings if your play space is limited (for example, you could limit rotations to 150° or 180° if you want to face forward only).

Rotation size and frequency can be adjusted in the menu, and headset FOV limits can be set so that rotations don't move outside your peripheral vision.

***HINT: For challenging rapid, large-angle rotations, go to the `Rotation` settings section and crank up `Rot Speed Multiplier`, `Min Rotation Size` and/or `Max Rotation Size`. If you set `Max Rotation Size` > 30, then your must set `FOV` to 90 or greater (otherwise those rotations will be removed. Higher than 90 on a Quest will allow rotations you can't see most likely). I like to use Mult = 1.6x, Min Rotation = 15, Max Rotation = 45 and FOV = 90 for my Quest3 headset. To go even bigger set Min Rotation = 30. You can start trimming around the edges of your peripheral vision with `FOV Time Window`. Increase it a bit to reduce a little of the large jumps at the periphery.***

## Arcitect Arc + Chain Maker

`Arcitect` automatically adds arcs and chains to maps that don't have them. Not as good as a human, of course! But better than nothing. Long-duration chains are available, but the segments can become difficult to hit when chains get too long.

NOTE: Chains added to a map would change the scoring, so I have disabled score submission for maps with generated chains.

## Auto Lighting

`Auto Lights` automatically adds basic lighting events to maps that don't have them. This only works for 360 and the older environments before Weave. Modern GLS environments are not supported. Thanks to Loloppe (based on their ChroMapper-AutoMapper)! I made many changes so anything crappy is my fault :) You can choose to add `boost` lighting events as well. And 360-environment lasers are fat and bright to enliven the boring 360 environment. Human-crafted lights are best, machine-made lights are OK, no lights suck! (I am considering adding automatic lighting for GLS environments as well in the future.)

Note: Aeroluna's `Technicolor` mod is awesome with 360fyer if you play a lot of 360 maps.

## Auto Wall Generator

The original 360fyer generated awesome walls. This version adds `Generated Extension Walls` walls and creates tons of walls and particle walls to help enliven the boring 360 environment. But you can add this to any environment. This is designed to work with `Mapping Extensions` mod. Many of the walls types will still work in a modified and reduced way without the mod.

NOTE: Dense walls can be claustrophobic and distracting, but you can disable them, reduce them, or move them away from your play space if you like (using `Min Distance` for each wall type) .

## Auto NJS Fixer

Thanks to Kylemc for allowing me to work from their original code! The original `NJS Fixer` is designed to be used on a per-song basis more or less (IMHO). `Auto NJS Fixer` is designed to “set it and forget it.” 360 maps with rapid turns prefer a long note spawn distance, hence the need for this. You can choose `Preserve Travel Time` if you want to keep the mapper’s intended duration of travel and perceived note speed while letting you change the note spawn distance. Or you can use `Set Note Speed` to set your favorite speed and spawn distance; this works well for most songs until note density gets very tight. Overrides Beat Saber PLAYER OPTIONS > JUMP DURATION TYPE and OFFSET.

NOTE: If settings cause the note speed to be slower than the mapper intended, score submission will be disabled. Also, `Auto NJS Fixer` is disabled by the original `NJS Fixer` and `JDFixer` if they are installed (enabled or not).

## Beat Sage Cleaner

Can remove some impossible note combinations common with `Beat Sage`-generated maps. It shortens long crouch walls and removes stray notes (notes many seconds away from the main body of song notes) at the start or end of maps.

## Menu Settings and Config file

There is a settings menu in-game. Or you can tweak settings in the `Beat Saber/UserData/AutoBS.json` config file. (You can open this file with notepad or another text editor.)


| Option                        | Description                                                                                                                                                                                                                                                                                                                                                  |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **AutoBS**                    | Main toggle and toolset that powers all automatic generation and modification: 360° maps, arcs, chains, NJS fixes, lighting, and walls, etc. Requires a restart for maps that have already been selected in the menu. ***Disable this to disable everything.*** |
| **360fyer**                   | 360° rotation engine. Generates 360° maps from standard maps. Unlikely to work well with Vivify, Noodle, Mapping Extensions, or maps that contain thousands of walls. ***Requires a restart for maps that have already been selected in the menu.***|


***360fyer – Rotation Settings***

| Option                   | Description                                                                                                                                                                                                                                                                                                      |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Wireless 360**         | Default: **True**. For wireless headsets. When enabled, generated maps have no rotation restrictions and are less likely to repeatedly reverse direction.|
| **Limit Rotations 360**  | Default: **360°**. Limits maximum allowed accumulated rotation. Disabled if **Wireless 360** is enabled. For wired headsets, use 360° or less to avoid twisting your cable (“cable rip”). You can lower this in tight spaces or rooms with furniture. **Score submission is disabled** if this is set below 90°. |
| **Rot Speed Multiplier*** | Default: **1.0**. Scales how often rotations occur. Less than 1 slows rotations; greater than 1 increases them. **Score submission is disabled** if set below 0.3. Very high values are still constrained by `FOV` settings. * ***Bump this up for more rotations!***               |
| **Add Extra Rotations**  | Default: **True**. Adds extra rotations in a consistent direction to maps that would otherwise have low overall rotation. |
| **Min Rotation Size***    | Default: **15°**. Minimum rotation step size. Rotations are typically 15° or 30°. `FOV` rules may force this down to 15° sometimes. * ***Bump this up for larger rotations angles!***|
| **Max Rotation Size**    | Default: **30°**. Maximum rotation step size. 30° is typical while 45° is more challenging. 60° rotations may fall outside peripheral vision. `FOV` rules may also force this value lower.|
| **FOV**                  | Default: **80°**. Set to slightly below your headset’s actual field of view. This is used to keep rotations within your peripheral vision over time. 80° is recommended for Quest 2 and 3. 90° and higher is required if you allow 45° rotations. This still works well for Quest 3.|
| **FOV Time Window**      | Default: **0.36 s**. Time window used to evaluate cumulative rotations against your FOV. Prevents multiple small rotations from stacking into a large FOV-breaking turns within this time span. Lower values allow more rapid rotations that can edge toward or outside the periphery. ***Bump this down for more and larger rotation angles!***|
| **Wall Removal Mult**    | Default: **1.0**. Controls how aggressively vision-blocking walls are removed during rotation sequences. Higher values remove more chaotic walls that may pass in front of the player and block visibility.|
| **Wall Note Dist**       | Default: **0.2 s**. This is the minimum distance allowed between notes and walls. Rotation events can cause notes and walls to appear closer together. Increase this to allow more space between them.|
| **Base Map**             | Default: **Standard**. Chooses which map type is used as the base for 360fyer-generated maps (e.g., Standard vs. other modes). **Score submission is disabled** if this is not set to Standard, and a Beat Saber restart is required for changes to take effect for maps that have already been selected in the menu.|

***Architect – Arcs***

| Option                        | Description                                                                                                                                                                                                                                                                                |
| ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Arc Rotation Mode**         | Default: **Net Zero**. Controls how arcs behave during rotations. **Force Zero** disables rotations during arcs. **Net Zero** allows rotations that sum to net 0° over the arc duration. **No Restrictions** allows full rotations during arcs, which can be more challenging. |
| **Force Natural Arc Swings**  | Default: **True**. Forces 180° relationships between head and tail cut directions so arcs feel more natural. Turning this off allows more variety in arc shapes (135° and 180°) but can produce some less natural-feeling motions.                                                 |
| **Pref Count per Minute** | Default: **12**. Attempts to create this many arcs per minute. This is an asperation only and could create many more or less than this number.                                                                                                     |
| **Min Duration**       | Default: **0.9 s**. Shortest allowed arc duration. This value may be relaxed internally if needed to better match your preferred arc count.                                                                                                                                                |
| **Max Duration**       | Default: **2.5 s**. Longest allowed arc duration. Longer arcs than this will not be generated.                                                                                                                                                                                             |

NOTE: The JSON config file has an `AllowArcHeadDotNotes` and `AllowArcTailDotNotes` setting not available in the menus if you want dot notes to produce arcs.

***Architect – Chains***

| Option                          | Description                                                                                                                                        |
| ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Pref Count per Minute** | Default: **10**. Attempts to create this many chains per minute. This is an asperation only and could create many more or less than this number. To create more chains, reduce `Chain Time Bumper` and reduce arcs or turn them off.     |
| **Chain Time Bumper**           | Default: **0.2 s**. Minimum allowed time between a chain and surrounding notes. |
| **Enable Long Chains**          | Default: **True**. Enables long chains that behave similarly to arcs in terms of duration and feel.
| **Long Chain Max Duration**     | Default: **0.425 s**. Maximum allowed duration for a chain. Chain segments become awkward to hit on longer chains.        |

***Auto NJS Fixer***

| Option                       | Description |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enable for Practice Mode** | When enabled, Auto NJS Fixer attempts to apply its adjustments in Practice Mode. It will be overridden if `PracticePlugin` is installed (activated or not). |
| **Mode**                     | Default: **Preserve Travel Time**. Use Preserve Travel Time to keep a similar perceived note speed and keep the same duration of travel intended by the map while letting you change the note spawn distance. Use **Set Note Speed** to set your desired note speed. **Score submission is disabled** if the speed ends up lower than the map’s original speed. (This can also happen with **Preserve Travel Time** when adjusting for note spawn distance.) |
| **Note Speed**               | Default: **10 m/s**. Overrides the map’s note speed (NJS). Setting this to **0** reverts to the map’s original NJS. **Score submission is disabled** if you set this below the map’s original speed. I use this to set my favorite speed for all maps. Doesn't work great on high note density maps sometimes. |
| **Note Spawn Distance**      | Default: **30 m**. Overrides the map’s note spawn distance (JD). Setting this to **0** uses the map’s original spawn distance. It's difficult to see notes coming in 360 with short note spawn distances. |

***Auto Lighting***

| Option                       | Description                                                                                                                                                                                      |
| ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| **Big 360 Lasers**           | Default: **True**. Scales up laser sizes in 360 environments. The stock 360 environment has small, weak lasers without this.                                                                |
| **Bright 360 Lights**        | Default: **True**. Brightens low-key lights in 360 environments. The base 360 environment laser set is fairly dim without this.                                                                |
| **Boost Lighting Events**    | Default: **True**. Adds “boost” lighting events to maps that don’t have them, giving lights another set of colors. Use Beat Saber **COLORS → OVERRIDE DEFAULT COLORS** to set boost colors. |
| **Enable Auto Lights** | Uses note-based automatic lighting for maps without lights. Based on ChroMapper-AutoMapper by Loloppe (tweaked for AutoBS; any issues are my fault!).  This only works for 360 and the older environments before Weave.  ***Modern GLS environments are not supported.***                |
| **Strobe Multiplier**     | Default: **1**. Lower this to reduce the number of strobe sessions. 0 is OFF. |
| **Strobe Max Duration**     | Default: **5s**. Lower this for shorter strobe sessions. |
| **Strobe Brightness Mult**     | Default: **1**. Controls the brightness of strobe lights. |
| **All Lights Frequency Multiplier**     | Default: **1.0**. Scales how frequently auto light events are placed. Lower this to reduce the number of events. |
| **All Lights Brightness Multiplier**    | Default: **1.0**. Scales the brightness of auto lights. Increase to make the overall light show brighter.   |
| **Light Style**              | Default: **Med Flash**. Chooses the auto lighting style. Styles lower in the list are less and less strobe-like. This is not related to 'strobe lights' which set above separately. |

***Beat Sage Cleaner***

| Option                           | Description                                                                                                                                                                                                                         |
| -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Set Crouch Wall Max Duration** | Default: **0.75s**. Shortens long crouch walls created by Beat Sage to this maximum duration. |
| **Stray Notes Remover**          | Default: **6s**. 0 is OFF. Removes stray notes at the start or end of a map if they are this far away in time from the main note stream. Use with `Intro Skip` mod. |
| **Max Strays Allowed**          | Default: **5 notes**. 0 is OFF. Removes up to this many stray notes at the start or end of a song. NOTE: All bombs will be removed where clean up occurs. Use with `Intro Skip` mod. |

***Existing Wall Manipulation***

| Option                 | Description                                                                                                                                                 |
| ---------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Allow Crouch Walls** | Allows **crouch walls** in the final output. These can be difficult to see and react to in fast 360 maps.     |
| **Allow Lean Walls**   | Allows **lean walls** in the final output. These can be difficult to see and react to in fast 360 maps. |

***Auto Walls – Generated Walls (Standard & Big)***

No crouch are generated by this system. Walls are primarily for visual effect only but lean walls can be added by setting `Standard and Big Walls Min Dis` to 0.

| Option                             | Description                                                                                                                                                               |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Standard Walls**                 | Enables generation of standard-size walls |
| **Big Walls**                      | Enables generation of very wide walls   |
| **Standard and Big Walls Mult**    | Default: **100%**. Percentage multiplier controlling how many Standard and Big walls are generated. Decrease for fewer walls.                          |
| **Standard and Big Walls Min Dis** | Default: **1**. Minimum distance from the player (by line index) for Standard and Big walls. 0 will make lean walls. Increase this to move generated walls further out from the center playspace. |

***Auto Walls – Generated Extension Walls***

Designed to work with `Mapping Extensions` mod installed. Many of the following will work in a modified and reduced way without the mod. (I use the word `Ext` below to denote Generated Extension walls.)

***Distant Walls***

| Option                       | Description                                                                                                     |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------- |
| **Enable Ext Distant Walls** | Enables mostly large, distant walls that appear far from the player or high above. Best with `Mapping Extensions` mod installed.                 |
| **Distant Walls Mult**       | Default: **2**. Multiplier controlling how many distant walls are generated. Higher values generate more walls. |

***Column Walls***

| Option                      | Description                                                                        |
| --------------------------- | ---------------------------------------------------------------------------------- |
| **Enable Ext Column Walls** | Enables tall, narrow column walls. Best with `Mapping Extensions` mod installed.                                                |
| **Column Walls Mult**       | Default: **1**. Controls how many column walls are generated.                      |
| **Column Walls Min Dis**    | Default: **2**. Minimum distance from the player (by line index) for column walls. |
		

***Row Walls***

| Option                   | Description                                                                     |
| ------------------------ | ------------------------------------------------------------------------------- |
| **Enable Ext Row Walls** | Enables long, wide rows of stacked walls. Best with `Mapping Extensions` mod installed.                |
| **Row Walls Mult**       | Default: **1**. Controls how many row walls are generated.                      |
| **Row Walls Min Dis**    | Default: **3**. Minimum distance from the player (by line index) for row walls. |


***Grid Walls***

| Option                    | Description                                                                                                                                       |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enable Ext Grid Walls** | Enables grids of small walls. Depending on frequency, these can be huge, sparse grids or smaller, dense grids. Best with `Mapping Extensions` mod installed.                                    |
| **Grid Walls Mult**       | Default: **1**. Frequency of grid wall generation. |
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
| **Enable Ext Particle Walls**   | Enables a burst of tiny particle-like walls with `Mapping Extensions` mod. Without the mod, it produces random blocks. Best with `Mapping Extensions` mod installed.                                                                                                                                                                                                                   |
| **Enable Large Particle Walls** | Default: **True**. Enables larger, note-sized particle walls. These can sometimes be confused with actual notes.|
| **Particle Walls Mult**         | Default: **4**. Controls how frequently particle wall batches are generated. More is better! Calculations on this can slow map generation time on some maps.   |
| **Particle Walls Batch Size**   | Default: **20**. Maximum number of walls per particle batch. Higher values create denser particle bursts. More is better! Calculations on this can slow map generation time on some maps.  |
| **Particle Walls Min Dis**      | Default: **0**. Minimum distance from the player (by line index and line layer). |
| **Max Wait Time**               | Default: **6 s**. Particle and floor wall calculations can slow map generation and even cause Beat Saber to appear frozen on heavy maps. This setting automatically reduces the number of particle and floor walls when generation time reaches this limit. |


***Floor Walls (and sky tiles and miniature cityscapes)***

| Option                     | Description                                                                                                                                                                 |
| -------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Enable Ext Floor Walls** | Enables bursts of thin, flat walls like floor tiles under (and sometimes sky tiles above) the player. This also forms miniature “cityscape” patterns of walls around you. Best with `Mapping Extensions` mod installed. |
| **Floor Walls Mult**       | Default: **4**. Controls how frequently floor wall batches are generated. Higher values mean more batches. More is better! Calculations on this can slow map generation time on some maps.                                        |
| **Floor Walls Batch Size** | Default: **20**. Maximum number of floors walls per batch. More is better! Calculations on this can slow map generation time on some maps..                                                               |
| **Floor Walls Min Dis**    | Default: **0**. Minimum distance from the player (by line index and line layer).                                                                                           |

***Tunnel Walls (Mapping Extensions only)***

| Option                      | Description                                                                                    |
| --------------------------- | ---------------------------------------------------------------------------------------------- |
| **Enable Ext Tunnel Walls** | Enables thin side and overhead wall groups that form tunnel-like structures around the player. Requires `Mapping Extensions` mod installed. |
| **Tunnel Walls Mult**       | Default: **1**. Controls how many tunnel wall groups are generated.                            |
| **Tunnel Walls Min Dis**    | Default: **0**. Minimum distance from the player (by line index) for tunnel walls.             |


NOTE: The JSON config file has a `StandardLevelWallMultiplier` setting not available in the menus that reduces walls for standard maps. If you like the number of walls in 360 but find that there are too many or few in a standard map, then change this config item.
***

## JSON Beatmap File Output

You can output a generated map to a JSON beatmap file. This file will contain all standard and all generated features including 360 rotation events, arcs, chains, lighting events, and walls (and attempts customData but not really tested). The output file can be in v2, v3, or v4 format (no matter what the starting format was). The output file(s) will be placed in the same folder as the original beatmap. 

NOTE: v2 maps will not have arcs, chains or `Mapping Extensions` walls added.

NOTE: v3 maps may need some cleanup due to some vision blocking walls around arcs if the arc mode is NOT set to `Force Zero`.

NOTE: v4 will be the most exact match to the in-game generated map. 

NOTE: v4 doesn't really support customData so all customData will be lost (for Noodle etc). `Mapping Extensions` precision placement does still function.

NOTE: This generator does not output an `info.dat` file. So you will need to make your own. FYI, `info` v2.1.0 handles v2 and v3 maps. `info` v4.1.0 files can handle v2, v3, and v4 maps. You can look at `PnfrlEnm`'s `Ascension to Heaven` to see a v4.1.0 info file.

To enable JSON file output, edit the `Beat Saber/UserData/AutoBS.json` config file in Notepad or other text editor. The last 5 settings in the config file are the ones to edit. 

Set:

`OutputV2JsonToSongFolder_NoArcsNoChainsNoMapExtWalls` and/or 

`OutputV3JsonToSongFolder` and/or 

`OutputV4JsonToSongFolder` 

to `true`.

Start Beat Saber and simply begin to play any map difficulty and the file(s) will be generated automatically. No need to finish playing the map.

By default, the `TurnOffJSONDatOutputAfterOneMapPlay` config setting is set to `true`. This means all the output settings will revert to `false` after one play. You can set this to `false` if you want to generate multiple files in one session every time you play a difficulty. After quitting Beat Saber, this will revert back to `true` and all outputs to `false`. This makes sure you don't accidentally leave this on.

v4 output will create beatmap, lightshow, and audioData files.

For v4 output, you must also set the `OutputV4JsonSongSampleRate` config setting. This is the sample rate of the audio file for the song. It will default to `44100`. The map will be out-of-sync if this is not correct. Most songs are `44100` or `48000`. (In Windows, right click the song, choose PROPERTIES>DETAILS.)

***

## How to build AutoBS Mod

To test and build this project locally, do the following:
1. Use `Beat Saber Modding Tools` extension for Visual Studio to make things easier.
1. Clone this repo & open the project in Visual Studio
2. Make sure to add the right dll references. The needed dlls are located in your modded Beat Saber installation folder.
3. Build. Visual Studio should copy the plugin to your Beat Saber installation automatically.

## Todo

Beat Saber Mixed Reality Mode support

Modern GLS lighting and environment support