# Bitfish - a memory reading fishbot

## Brief overview of how it works
This fish bot is built for the Wotlk 3.3.5 build 12340 client. It reads the object manager list and finds the bobber object. The object contains information about when to click, `bobbing = true/false`, and the object GUID, among other things. We are hooked into the Wow's process which enables us to do protected Lua commands, such as `InteractUnit()`. We set the Mouseover memory address to bobber GUID and call with `InteractUnit('mouseover')`. This will will click the bobber without using the mouse.
