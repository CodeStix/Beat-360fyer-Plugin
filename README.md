# Beat-360fyer-Plugin
A Beat Saber plugin to play any beatmap in 360 or 90 degree mode. 

[![showcase video](https://github.com/CodeStix/Beat-360fyer-Plugin/raw/master/preview.gif)](https://www.youtube.com/watch?v=xUDdStGQwq0)

## Installation

- You can install this mod using ModAssistant.
- Or install this mod manually by downloading a release from the [releases](https://github.com/CodeStix/Beat-360fyer-Plugin/releases) tab and placing it in the `Plugins/` directory of your modded Beat Saber installation.

After doing this, **every** beatmap will have the 360/90 degree gamemode enabled. Just choose `360` when you select a song. The level will be generated once you start the level.

## Algorithm

The algorithm is completely deterministic and does not use random chance, it generates rotation events based on the notes in the *Standard* beatmap. 

**It also makes sure to not ruin your cable by rotating too much!**

## Config file

Currently, there is no settings menu. You can tweak some settings in the `Beat Saber/UserData/Beat-360fyer-Plugin.json` config file. You should open this file with notepad or another text editor.

```js
{
  "ShowGenerated360": true,
  "ShowGenerated90": true,
  "AlwaysShowGenerated": false,
  "EnableWallGenerator": true,
  "LimitRotations360": 28,
  "LimitRotations90": 2,
  "BasedOn": "Standard",
  "OnlyOneSaber": false
}
```
|Option|Description|
|---|---|
|`ShowGenerated360`| `true` if you want to enable 360 degree mode generation (default = `true`)|
|`ShowGenerated90`| `true` if you want to enable 90 degree mode generation (default = `true`)|
|`AlwaysShowGenerated`| `true` if you always want to show the generated 360 gamemode, also if there is already a 360 mode available. (default = `false`)|
|`EnableWallGenerator`| Set to `false` to disable wall generation (default = `true`). Walls are not generated for NoodleExtension levels by default.|
|`LimitRotations360`| The max amount of rotation steps to the left or right in 360 degree mode. (default = `28`)|
|`LimitRotations90`|The max amount of rotation steps to the left or right in 90 degree mode. (default = `2`)|
|`BasedOn`|Which game mode to generate the 360 mode from, can be `"Standard"` (default), `"OneSaber"` or `"NoArrows"`|
|`OnlyOneSaber`|`true` if you want to only keep one color during generation, this allows you to play `OneSaber` in 360 degree on any level, also the ones that don't have a OneSaber gamemode. (default = `false`, caution: experimental)|

**Not working? Config not doing anything?** Make sure you saved the file in the original location, and make sure you didn't place a comma after the last option (see working example config above).


## How to build

To test and build this project locally, do the following:
1. Clone this repo & open the project in Visual Studio
2. Make sure to add the right dll references. The needed dlls are located in your modded Beat Saber installation folder.
3. Build. Visual Studio should copy the plugin to your Beat Saber installation automatically.

## Todo

- Settings menu
