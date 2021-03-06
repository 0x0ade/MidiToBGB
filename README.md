# MidiToMGBA - MIDI input -> mGBA link

### License: MIT

----

MidiToMGBA acts as a simple MIDI message forwarder, listening to a MIDI input device and passing on all MIDI messages to mGBA's internal link interface.

This previously was MidiToBGB, but BGB's TCP link is suboptimal for this task. Development thus moved on to a mGBA-centric solution.

### Setup:
- If you want to use MidiToBGB with your DAW: Download and install a MIDI "loopback" driver, f.e. [loopmidi by Tobias Erichsen](http://www.tobias-erichsen.de/software/loopmidi.html)
- [**Download MidiToMGBA**](https://github.com/0x0ade/MidiToMGBA/releases) from the releases tab.
- **Run `MidiToMGBA.bat`**
    - If you want to change the settings, open the .bat in any text editor.

**TODO:**
- Drop dependency on `libmgba-sdl.dll` exposing `mSDLMain` - instead, use custom SDL2-CS "platform."
- Find a good cross-platform MIDI library ([atsushieno/managed-midi](https://github.com/atsushieno/managed-midi) lacks good input recording).
- If possible, improve timing.

**Note:**  
It ships with a devbuild of mGBA 0.7, split into multiple libraries, with the following changes:
- SDL platform's "executable" built as `libmgba-sdl.dll` instead of `mgba-sdl.exe`
- Entry point (function `main`) renamed to `mSDLMain`
