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


# Installation

Remove any existing MIDI Listener from your scene, This package already comes with a prefab that has a `MIDI Listener` component that references all three events (Note ON, Note OFF, CC Change).

Place MIDI.prefab somewhere in your heirarchy, this prefab includes the MIDI Listener object which references the `MIDI Orchestrator` already, no modification should be done on the MIDI Listener.

For each object that should be modified by a button press, place a `MidiBehavior` script on the object. If you have an array of objects that should be modified by a button press, place the objects inside of a container and assign the container the `MidiBehavior`.

To use third party plugins, such as LTCGI or AreaLit, on the `MidiBehavior` script, select the corresponding plugin and add the required fields.

On the `MIDI Orchestrator` Prefab, modify the following settings to your liking:
- Update Rate: This controls the update rate of any material, in Hz. A value of 20Hz will update ~20 times per second.
- Pad Events: Each pad on the MIDI controller corresponds to a MidiBehavior, so the 1st pad on the controller would correspond to the first behavior in Pad Events
- Transition Time: This is the **maximum** amount of time it takes to transition from on step to another, this can be finely tuned per knob
- Sustain Level: The intensity of the material that should be maintained for the duration of **sustain**, 1.0 -> Max intensity, 0.0 -> OFF

This prefab offers a few presets for some controllers, which can be modified under the **Advanced Settings** for each controller.
If the controller you are using does not have preset values, they can be manually entered via **Custom Controller**'s **Advanced Settings** tab.

This prefab offers an optional visualizer for debugging purposes. It uses a non-synchronized visualizer so the user can see the values they are inputting in realtime, assuming their MIDI controller does not already have such capabilities.
I do not reccomend using this visualizer in the production version of your world as it is potentially expensive to render/draw on the user using the controller.
 