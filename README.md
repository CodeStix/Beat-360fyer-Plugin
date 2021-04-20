# Beat-360fyer-Plugin
A Beat Saber plugin to play any beatmap in 360 or 90 degree mode. 

#### Showcase video

[![showcase video](https://img.youtube.com/vi/xUDdStGQwq0/0.jpg)](https://www.youtube.com/watch?v=xUDdStGQwq0)

## Installation

- ~~You can install this mod using ModAssistant~~. (not yet)
- Or install this mod manually by downloading a release from the [releases](https://github.com/CodeStix/Beat-360fyer-Plugin/releases) tab and placing it in the `Plugins/` directory of your modded Beat Saber installation.

After doing this, **every** beatmap will have the 360/90 degree gamemode enabled. Just choose `360` when you select a song. The level will be generated once you start the level.

## Algorithm

The algorithm is completely deterministic and does not use random chance, it generates rotation events based on the notes in the *Standard* beatmap. 

**It also makes sure to not ruin your cable by rotating too mush!**

## Config file

Currently, there is no settings menu. You can tweak some settings in the `UserData/Beat-360fyer-Plugin.json` config file.

```js
{
  // True if you want to enable 360 degree mode generation (default = true)
  "ShowGenerated360": true,
  // True if you want to enable 90 degree mode generation (default = true)
  "ShowGenerated90": true,
  // Set to false to disable wall generation (default = true)
  "EnableWallGenerator": true
}
```
## How to build

To test and build this project locally, do the following:
1. Clone this repo & open the project in Visual Studio
2. Make sure to add the right dll references. The needed dlls are located in your modded Beat Saber installation folder.
3. Build. Visual Studio should copy the plugin to your Beat Saber installation automatically.

## Todo

- Settings menu
