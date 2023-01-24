# MIDI Interface

**This is still a work-in-progress project**

The goal of this project is to provide an extendible interface for MIDI Controllers to VRChat utilizing Udon in a computationally efficient way.

## Note on MIDI Controllers

This project is being designed and tested with the [Arturia Beatstep Pro](https://www.arturia.com/products/hybrid-synths/beatstep-pro/overview) and as such the default mapping assumes the user is using this controller.

This project should theoretically work with any MIDI device by modifying the `minNote` and `maxNote` parameters on the `MidiOrchestrator` behavior.

To change the value of the CCs, the [MidiOrchestrator.cs](https://github.com/SeanmanX/UdonMidiInterface/blob/main/Scripts/MidiOrchestrator.cs) script itself needs to be modified

One can utilize the [Udon Midi Test](https://vrchat.com/home/world/wrld_f8bc6485-dcdf-4646-89d8-14e4772561ee) world or a similar third party program to find out their control/note numbers mapping.

**[Some controllers](https://novationmusic.com/en/launch/launchpad-x) do not register a `MidiNoteOff` event**, instead they send a second `MidiNoteOn` event with Velocity 0.

## Requirements
Unity 2019.4.31f1

[Udon Sharp >= 1.x](https://github.com/vrchat-community/UdonSharp)
