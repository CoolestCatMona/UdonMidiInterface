using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common;
using System;

#if UDONSHARP
using static VRC.SDKBase.VRCShader;
#else
using static UnityEngine.Shader;
#endif

/// <summary>
/// This Behavior acts as a master orchestrator for other events that occur on Midi note events. It synchronizes control change values across the network and handles events that should occur on button presses.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class MidiOrchestrator : UdonSharpBehaviour
{
    [Tooltip("Lowest note that can be pressed")]
    public int minNote = 0;
    [Tooltip("Highest note that can be pressed")]
    public int maxNote = 0;
    [Tooltip("When using pads as CCs, this is the value that separates pad values from CC values")]
    public int padStop = 0;
    [Tooltip("Maximum amount of time (s) it should take for any change in material to occur")]
    public int maxTime = 0;
    [Tooltip("Update rate (Hz). Higher update rates are more computationally expensive. A good value is between 10-20Hz")]
    [Range(1, 50)]
    public int updateRate = 0;
    [Tooltip("Intensity of material to Sustain for the amount of time defined by the sustain knob")]
    [Range(0.0f, 1.0f)]
    public float sustainLevel = 0.0f;
    [Tooltip("UdonBehaviors corresponding to individual button presses")]
    public UdonSharpBehaviour[] buttonEvents;
    [Tooltip("Use a visualizer to view the current MIDI settings")]
    public bool usesVisualizer = false;
    [Tooltip("Which visualizer to view current settings of MIDI")]
    public UdonSharpBehaviour MidiVisualizer;
    [Tooltip("Use Pads as CCs")]
    public bool padsAsCC = false;
    [Tooltip("How much a value should change when pressed by a pad")]
    [Range(0.0f, 1.0f)]
    public float padCCChangeAmnt = 0.05f;


    // TODO: Implementation
    public bool usesLTCGI = false;
    public bool usesAreaLit = false;

    /// <summary>
    /// Serialized fields used in editor
    /// </summary>
    [SerializeField] private int _controllerSelectionIndex = 1;
    [SerializeField] private int _thirdPartySelectionIndex = 0;

    /// <summary>
    /// Private, intermediate values used by behavior
    /// </summary>
    private float _r = 1.0f;
    private float _g = 1.0f;
    private float _b = 1.0f;
    private float _a = 1.0f;
    private float _h = 0.0f;
    private float _updateRate_s;
    private float _updateRate_Hz;
    private int _noteValue;
    private int _padIndex;

    /// <summary>
    /// Synchronized Color (RGBA) for materials.
    /// </summary>
    /// Because I have multiple synchronized variables I am unsure if I should use the [FieldChangeCallback(string)] attribute
    /// https://udonsharp.docs.vrchat.com/udonsharp/#fieldchangecallback
    [UdonSynced]
    private Color _color;

    /// <summary>
    /// Multiplier for hue of color value, a value of 0 or 1 implies no change.
    /// </summary>
    [UdonSynced]
    private float _hueShift;

    /// <summary>
    /// Amount of time (s) to reach Max value from 0
    /// </summary>
    [UdonSynced]
    private float _attack = 1.0f;

    /// <summary>
    /// Amount of time (s) to reach Sustain from Max
    /// </summary>
    [UdonSynced]
    private float _decay = 1.0f;

    /// <summary>
    /// The amount of time (s) to stay at the Sustain level
    /// </summary>
    [UdonSynced]
    private float _sustain = 1.0f;

    /// <summary>
    /// Amount of time (s) to move from sustain to 0
    /// </summary>
    [UdonSynced]
    private float _release = 1.0f;

    /// <summary>
    /// Numbers corresponding to CC on a MIDI controller.
    /// </summary>
    [SerializeField] private int RED = 10;
    [SerializeField] private int GREEN = 74;
    [SerializeField] private int BLUE = 71;
    [SerializeField] private int HUE = 76;
    [SerializeField] private int ATTACK = 114;
    [SerializeField] private int DECAY = 18;
    [SerializeField] private int SUSTAIN = 19;
    [SerializeField] private int RELEASE = 16;

    // When using pads, these values correspond to decrementing a value
    [SerializeField] private int RED_DEC = 0;
    [SerializeField] private int GREEN_DEC = 0;
    [SerializeField] private int BLUE_DEC = 0;
    [SerializeField] private int HUE_DEC = 0;
    [SerializeField] private int ATTACK_DEC = 0;
    [SerializeField] private int DECAY_DEC = 0;
    [SerializeField] private int SUSTAIN_DEC = 0;
    [SerializeField] private int RELEASE_DEC = 0;


    /// <summary>
    /// Numbers corresponding to pad button presses, these probably shouldn't change.
    /// </summary>
    private const int NOTE_0 = 0;
    private const int NOTE_1 = 1;
    private const int NOTE_2 = 2;
    private const int NOTE_3 = 3;
    private const int NOTE_4 = 4;
    private const int NOTE_5 = 5;
    private const int NOTE_6 = 6;
    private const int NOTE_7 = 7;
    private const int NOTE_8 = 8;
    private const int NOTE_9 = 9;
    private const int NOTE_10 = 10;
    private const int NOTE_11 = 11;
    private const int NOTE_12 = 12;
    private const int NOTE_13 = 13;
    private const int NOTE_14 = 14;
    private const int NOTE_15 = 15;
    private const int NOTE_16 = 16;

    /// <summary>
    /// Magic Numbers
    /// </summary>
    private const float CC_MAX = 127.0f;
    private const float MAX_COLOR_VALUE = 1.0f;
    private const float MIN_COLOR_VALUE = 0.0f;

    /// <summary>
    /// Event that is triggered when the script is intialized. Some intial variables are calculated and set once, then sychronized across relevant UdonBehaviors
    /// </summary>
    void Start()
    {
        _updateRate_s = (float)updateRate;
        _updateRate_Hz = 1.0f / _updateRate_s;
        foreach (UdonSharpBehaviour buttonEvent in buttonEvents)
        {
            buttonEvent.SetProgramVariable("_updateRate_s", _updateRate_s);
            buttonEvent.SetProgramVariable("_updateRate_Hz", _updateRate_Hz);
        }
        RequestSerialization();
    }
    /// <summary>
    /// This event triggers just before serialized data will be sent out, it's a good place to set synced variables that you want to be updated for other players.
    /// </summary>
    public override void OnPreSerialization()
    {
        foreach (UdonSharpBehaviour buttonEvent in buttonEvents)
        {
            buttonEvent.SetProgramVariable("_color", _color);
            buttonEvent.SetProgramVariable("_attack", _attack);
            buttonEvent.SetProgramVariable("_decay", _decay);
            buttonEvent.SetProgramVariable("_sustain", _sustain);
            buttonEvent.SetProgramVariable("_release", _release);
        }
    }

    /// <summary>
    /// This event triggers when sync data has been transformed from bytes back into usable variables. 
    /// It does not tell you which data has been updated, but serves as a jumping-off point to either update everything that watches synced variables, or a place to check new data against old data and make specific updates
    /// </summary>
    public override void OnDeserialization()
    {
        if (!Networking.IsOwner(gameObject))
        {
            /**
            Have tried to figure out why synced variables aren't serializing in my world, 
            and through some tests I noticed that if a player that is not the master tries
            to change a synced variable in one script through another script,
            the variables would not serialize to everyone, not even if I call
            RequestSerialization on the script

            Does this mean that the local player needs to be the owner??
            In testing with myself this is fine.
            **/
            foreach (UdonSharpBehaviour buttonEvent in buttonEvents)
            {
                buttonEvent.SetProgramVariable("_color", _color);
                buttonEvent.SetProgramVariable("_attack", _attack);
                buttonEvent.SetProgramVariable("_decay", _decay);
                buttonEvent.SetProgramVariable("_sustain", _sustain);
                buttonEvent.SetProgramVariable("_release", _release);
            }
        }
    }

    /// <summary>
    /// This event triggers just after an attempt was made to send serialized data. It returns a SerializationResult struct with a 'success' bool and 'byteCount' int with the number of bytes sent.
    /// </summary>
    /// <param name="result">Network serialization result</param>
    public override void OnPostSerialization(SerializationResult result)
    {
        if (!result.success)
        {
            if (Networking.IsClogged)
            {
                SendCustomEventDelayedSeconds("OnPostSerialization", 0.1f);
            }
            else
            {
                RequestSerialization();
            }
        }
    }

    /// <summary>
    /// Triggered when a Note On message is received, typically by pressing a key / pad on your device.
    /// Ensures that the note is valid, then will trigger a MidiOnEvent (user defined) on a corresponding UdonBehavior.
    /// </summary>
    /// <param name="channel">Midi Channel that received the event, 0-15.</param>
    /// <param name="number">Note number from 0-127 (your midi Device may not output the full range)</param>
    /// <param name="velocity">Number from 0-127 representing the speed at which the note was triggered, if supported by your midi device.</param>
    public override void MidiNoteOn(int channel, int number, int velocity)
    {
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);


        if (!IsValidNote(number))
            return;

        _noteValue = number - minNote;

        if (usesVisualizer & (number < padStop))
            MidiVisualizer.SetProgramVariable("_padIndex", _noteValue);


        if (velocity > 0)
        {
            switch (_noteValue)
            {
                case NOTE_0:
                    buttonEvents[NOTE_0].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_1:
                    buttonEvents[NOTE_1].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_2:
                    buttonEvents[NOTE_2].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_3:
                    buttonEvents[NOTE_3].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_4:
                    buttonEvents[NOTE_4].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_5:
                    buttonEvents[NOTE_5].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_6:
                    buttonEvents[NOTE_6].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_7:
                    buttonEvents[NOTE_7].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_8:
                    buttonEvents[NOTE_8].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_9:
                    buttonEvents[NOTE_9].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_10:
                    buttonEvents[NOTE_10].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_11:
                    buttonEvents[NOTE_11].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_12:
                    buttonEvents[NOTE_12].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_13:
                    buttonEvents[NOTE_13].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_14:
                    buttonEvents[NOTE_14].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                case NOTE_15:
                    buttonEvents[NOTE_15].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOnEvent");
                    break;
                default:
                    if (velocity > 0) HandlePadCChange(number);
                    break;
            }
        }
        else
        {
            switch (_noteValue)
            {
                case NOTE_0:
                    buttonEvents[NOTE_0].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_1:
                    buttonEvents[NOTE_1].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_2:
                    buttonEvents[NOTE_2].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_3:
                    buttonEvents[NOTE_3].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_4:
                    buttonEvents[NOTE_4].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_5:
                    buttonEvents[NOTE_5].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_6:
                    buttonEvents[NOTE_6].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_7:
                    buttonEvents[NOTE_7].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_8:
                    buttonEvents[NOTE_8].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_9:
                    buttonEvents[NOTE_9].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_10:
                    buttonEvents[NOTE_10].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_11:
                    buttonEvents[NOTE_11].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_12:
                    buttonEvents[NOTE_12].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_13:
                    buttonEvents[NOTE_13].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_14:
                    buttonEvents[NOTE_14].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                case NOTE_15:
                    buttonEvents[NOTE_15].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Triggered when a Note Off message is received, typically by releasing a key / pad on your device.
    /// Ensures that the note is valid, then will trigger a MidiOffEvent (user defined) on a corresponding UdonBehavior 
    /// </summary>
    /// <param name="channel">Midi Channel that received the event, 0-15.</param>
    /// <param name="number">Note number from 0-127 (your midi Device may not output the full range)</param>
    /// <param name="velocity">This value is typically 0 for Note Off events, but may vary depending on your device.</param>
    public override void MidiNoteOff(int channel, int number, int velocity)
    {
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);

        if (!IsValidNote(number))
            return;

        _noteValue = number - minNote;

        if (usesVisualizer & number < padStop)
            MidiVisualizer.SetProgramVariable("_padIndex", _noteValue);

        switch (_noteValue)
        {
            case NOTE_0:
                buttonEvents[NOTE_0].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_1:
                buttonEvents[NOTE_1].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_2:
                buttonEvents[NOTE_2].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_3:
                buttonEvents[NOTE_3].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_4:
                buttonEvents[NOTE_4].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_5:
                buttonEvents[NOTE_5].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_6:
                buttonEvents[NOTE_6].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_7:
                buttonEvents[NOTE_7].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_8:
                buttonEvents[NOTE_8].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_9:
                buttonEvents[NOTE_9].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_10:
                buttonEvents[NOTE_10].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_11:
                buttonEvents[NOTE_11].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_12:
                buttonEvents[NOTE_12].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_13:
                buttonEvents[NOTE_13].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_14:
                buttonEvents[NOTE_14].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            case NOTE_15:
                buttonEvents[NOTE_15].SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, "MidiOffEvent");
                break;
            default:
                break;
        }

    }

    /// <summary>
    /// Triggered when a control change is received. These are typically sent by knobs and sliders on your Midi device.
    /// Will normalize 'value' parameter between 0.0f - 1.0f.
    /// Manually synchronizes normalized value that is sent. 
    /// Because the user should be able to define which CCs correspond to what, we can not use a switch statement as the values are not constant.
    /// </summary>
    /// <param name="channel">Midi Channel that received the event, 0-15.</param>
    /// <param name="number" Control number from 0-127.</param>
    /// <param name="value">Number from 0-127 representing the value sent by your controller. For some knobs that can spin endlessly rather than being limited by physical start / end positions, this value might be simply 0 and 1 or some other range indicating "positive" and "negative" increments that you must manage on your own.</param>
    public override void MidiControlChange(int channel, int number, int value)
    {
        if (!Networking.IsOwner(gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        float value_nrm = (float)value / CC_MAX;

        if (number == RED)
        {
            _r = value_nrm;
            _color.r = _r;
        }
        else if (number == GREEN)
        {
            _g = value_nrm;
            _color.g = _g;
        }
        else if (number == BLUE)
        {
            _b = value_nrm;
            _color.b = _b;
        }
        else if (number == HUE)
        {
            _h = value_nrm;
            HandleHueShift(_h);
        }
        else if (number == ATTACK)
        {
            _attack = value_nrm * maxTime;
        }
        else if (number == DECAY)
        {
            _decay = value_nrm * maxTime;
        }
        else if (number == SUSTAIN)
        {
            _sustain = value_nrm * maxTime;
        }
        else if (number == RELEASE)
        {
            _release = value_nrm * maxTime;
        }
        else
        {
            Debug.Log("Invalid CC");
        }

        foreach (UdonSharpBehaviour buttonEvent in buttonEvents)
        {
            buttonEvent.SetProgramVariable("_color", _color);
            buttonEvent.SetProgramVariable("_attack", _attack);
            buttonEvent.SetProgramVariable("_decay", _decay);
            buttonEvent.SetProgramVariable("_sustain", _sustain);
            buttonEvent.SetProgramVariable("_release", _release);
        }

        if (usesVisualizer)
        {
            MidiVisualizer.SetProgramVariable("_colorValue", _color);
            MidiVisualizer.SetProgramVariable("_r", _r);
            MidiVisualizer.SetProgramVariable("_g", _g);
            MidiVisualizer.SetProgramVariable("_b", _b);
            MidiVisualizer.SetProgramVariable("_hueShift", _h);
            MidiVisualizer.SetProgramVariable("_attack", _attack);
            MidiVisualizer.SetProgramVariable("_decay", _decay);
            MidiVisualizer.SetProgramVariable("_sustain", _sustain);
            MidiVisualizer.SetProgramVariable("_release", _release);
        }

        RequestSerialization();
    }

    /// <summary>
    /// Ensures that a note given is within a given range.
    /// </summary>
    /// <param name="note">Note that was received.</param>
    /// <returns></returns>
    public bool IsValidNote(int note)
    {
        return note >= minNote & note < maxNote;
    }

    /// <summary>
    /// Handles CC change when the option for Pads as CC is set.
    /// Because the user should be able to define which CCs correspond to what, we can no longer use a switch statement as the values are not constant.
    /// </summary>
    /// <param name="note">Note that is pressed</param>
    public void HandlePadCChange(int note)
    {
        if (note == RED)
        {
            _r = _r >= MAX_COLOR_VALUE ? MAX_COLOR_VALUE : _r + padCCChangeAmnt;
            _color.r = _r;
        }
        else if (note == RED_DEC)
        {
            _r = _r <= MIN_COLOR_VALUE ? MIN_COLOR_VALUE : _r - padCCChangeAmnt;
            _color.r = _r;
        }
        else if (note == GREEN)
        {
            _g = _g >= MAX_COLOR_VALUE ? MAX_COLOR_VALUE : _g + padCCChangeAmnt;
            _color.g = _g;
        }
        else if (note == GREEN_DEC)
        {
            _g = _g <= MIN_COLOR_VALUE ? MIN_COLOR_VALUE : _g - padCCChangeAmnt;
            _color.g = _g;
        }
        else if (note == BLUE)
        {
            _b = _b >= MAX_COLOR_VALUE ? MAX_COLOR_VALUE : _b + padCCChangeAmnt;
            _color.b = _b;
        }
        else if (note == BLUE_DEC)
        {
            _b = _b <= MIN_COLOR_VALUE ? MIN_COLOR_VALUE : _b - padCCChangeAmnt;
            _color.b = _b;
        }
        else if (note == HUE)
        {
            _h = _h >= MAX_COLOR_VALUE ? MAX_COLOR_VALUE : _h + padCCChangeAmnt;
            HandleHueShift(_h);
        }
        else if (note == HUE_DEC)
        {
            _h = _h <= MIN_COLOR_VALUE ? MIN_COLOR_VALUE : _h - padCCChangeAmnt;
            HandleHueShift(_h);
        }
        else if (note == ATTACK)
        {
            _attack = _attack >= maxTime ? maxTime : (_attack + padCCChangeAmnt) * maxTime;
        }
        else if (note == ATTACK_DEC)
        {
            _attack = _attack <= 0.0f ? 0.0f : (_attack - padCCChangeAmnt) * maxTime;
        }
        else if (note == DECAY)
        {
            _decay = _decay >= maxTime ? maxTime : (_decay + padCCChangeAmnt) * maxTime;
        }
        else if (note == DECAY_DEC)
        {
            _decay = _decay <= 0.0f ? 0.0f : (_decay - padCCChangeAmnt) * maxTime;
        }
        else if (note == SUSTAIN)
        {
            _sustain = _sustain >= maxTime ? maxTime : (_sustain + padCCChangeAmnt) * maxTime;
        }
        else if (note == SUSTAIN_DEC)
        {
            _sustain = _sustain <= 0.0f ? 0.0f : (_sustain - padCCChangeAmnt) * maxTime;
        }
        else if (note == RELEASE)
        {
            _release = _release >= maxTime ? maxTime : (_release + padCCChangeAmnt) * maxTime;
        }
        else if (note == RELEASE_DEC)
        {
            _release = _release <= 0.0f ? 0.0f : (_release - padCCChangeAmnt) * maxTime;
        }
        else
        {
            Debug.Log("Invalid Pad");
        }

        foreach (UdonSharpBehaviour buttonEvent in buttonEvents)
        {
            buttonEvent.SetProgramVariable("_color", _color);
            buttonEvent.SetProgramVariable("_attack", _attack);
            buttonEvent.SetProgramVariable("_decay", _decay);
            buttonEvent.SetProgramVariable("_sustain", _sustain);
            buttonEvent.SetProgramVariable("_release", _release);
        }

        if (usesVisualizer)
        {
            MidiVisualizer.SetProgramVariable("_colorValue", _color);
            MidiVisualizer.SetProgramVariable("_r", _r);
            MidiVisualizer.SetProgramVariable("_g", _g);
            MidiVisualizer.SetProgramVariable("_b", _b);
            MidiVisualizer.SetProgramVariable("_hueShift", _h);
            MidiVisualizer.SetProgramVariable("_attack", _attack);
            MidiVisualizer.SetProgramVariable("_decay", _decay);
            MidiVisualizer.SetProgramVariable("_sustain", _sustain);
            MidiVisualizer.SetProgramVariable("_release", _release);
        }

        RequestSerialization();
    }
    /// <summary>
    /// To avoid modifying the current color, we create a new color from the knob values and hue shift that.
    /// </summary>
    /// <param name="hueShiftAmnt">Amount to shift currrent R, G, B values</param>
    public void HandleHueShift(float hueShiftAmnt)
    {
        float H, S, V;
        Color.RGBToHSV(new Color(_r, _g, _b, MAX_COLOR_VALUE), out H, out S, out V);
        float _colorChange = (float)Math.Round((double)(H + (MAX_COLOR_VALUE - hueShiftAmnt)), 2);
        if (_colorChange > MAX_COLOR_VALUE)
            _colorChange -= MAX_COLOR_VALUE;
        _color = Color.HSVToRGB(_colorChange, S, V);
    }
}
