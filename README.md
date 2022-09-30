# Bitfish 🎣

Bitfish is a fish bot that only works for the Wotlk 3.3.5a build 12340 client. The bot is reading/writing process memory which means it can run in the background. It does not require any specific key bindings and has serveral config features such as:

* Timer
* Logout
* Hearthstone
* Nearby player tracker
  * Bot stores player id of close players and if a player id has been too close for too long it will stop the fishing.
* Inventory tracker (can cause game crash)
* Wintergrasp timer
* Auto equip fishing pole
* Interrupt if player moved
* Interrupt if player took damage

## How to

1. Start WoW and enter world, please wait for loading screen
2. Start Bitfish
3. Hit start fishing

### Options

When clicking the *Start fishing* button, the program will read current option state from UI and use that for the session. The save button saves the option state to `fish-config.json`, which lies in the same folder as the executable. This will make sure the bot remembers the option state after closing the program.

## Building

Clone the repository and open it with Visual Studio. Make sure to manually add the BlackMagic.dll and the fasm.dll. Other needed dll files can be found via NuGet packages.

## Demo

![GUI](bitfish-demo.gif "GUI")

## Disclaimer

I made this for fun and educational purposes only. I do not recommend using it because it will get you banned.
