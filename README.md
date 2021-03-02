# Beat-360fyer-Plugin
A Beat Saber plugin to play any beatmap in 360 degree mode. This is the successor to [Beat-360fyer](https://github.com/CodeStix/Beat-360fyer), the desktop application version.


![showcase](https://github.com/CodeStix/Beat-360fyer/raw/master/Build/Demos/beatsaber.gif)

*(Jaroslav Beck - Beat Saber) Left: Standard; Right: Generated 360 mode*

## Installation

- ~~You can install this mod using ModAssistant~~. (not yet)
- Or install this mod manually by downloading a release from the [releases](https://github.com/CodeStix/Beat-360fyer-Plugin/releases) tab and placing it in the `Plugins/` directory of your modded Beat Saber installation.

After doing this, **every** beatmap will have the 360/90 degree gamemode enabled.

## Algorithm

The algorithm is completely deterministic and does not use random chance, it generates rotation events based on the notes in the *Standard* beatmap. 

**It also makes sure to not ruin your cable by rotating too mush!**

## Todo

- Wall generator :open_mouth:
