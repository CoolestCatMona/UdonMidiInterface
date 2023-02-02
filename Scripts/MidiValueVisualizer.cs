
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using System;

#if UDONSHARP
using static VRC.SDKBase.VRCShader;
#else
using static UnityEngine.Shader;
#endif

/// <summary>
/// Value Viewer for variables set by MIDI controller, for debugging.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class MidiValueVisualizer : UdonSharpBehaviour
{
    /// <summary>
    /// Synchronized variables that are set via the MidiOrchestrator behavior.
    /// </summary>
    [HideInInspector, FieldChangeCallback(nameof(ColorValue))] public Color _colorValue = Color.black;
    [HideInInspector, FieldChangeCallback(nameof(Red))] public float _r = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Green))] public float _g = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Blue))] public float _b = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Attack))] public float _attack = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Delay))] public float _decay = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Sustain))] public float _sustain = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Release))] public float _release = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(HueShift))] public float _hueShift = 0.0f;
    [HideInInspector, FieldChangeCallback(nameof(IntensityMult))] public float _intensityMult = 0.0f;
    [HideInInspector, FieldChangeCallback(nameof(StartIndex))] public int startingArrayIndexOffset = -1;
    [HideInInspector, FieldChangeCallback(nameof(ModeSelect))] public string modeSelect = "NONE";
    [HideInInspector] public float _sendRate_s = 1.0f;
    [HideInInspector] public float _sendRate_Hz = 1.0f;
    public GameObject previewObject;
    public GameObject[] pads;

    public Text rText;
    public Text gText;
    public Text bText;
    public Text hText;
    public Text attackText;
    public Text sustainText;
    public Text decayText;
    public Text releaseText;
    public Text intensityText;
    public Text startIndexText;
    public Text modeText;

    // Private Variables
    [HideInInspector, FieldChangeCallback(nameof(PadIndex))] public int _padIndex = -1;

    // Property ID
    private int _Color;


    private void InitIDs()
    {
        _Color = PropertyToID("_Color");
    }
    void Start()
    {
        InitIDs();
        attackText.text = "1.000";
        decayText.text = "1.000";
        sustainText.text = "1.000";
        releaseText.text = "1.000";
    }
    public Color ColorValue
    {
        set
        {
            _colorValue = value;
            Renderer _Renderer = previewObject.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor(_Color, _colorValue);
            _Renderer.SetPropertyBlock(block);
        }
        get => _colorValue;
    }
    public float Red
    {
        set
        {
            _r = value;
            rText.text = _r.ToString("0.000");
        }
        get => _r;
    }
    public float Green
    {
        set
        {
            _g = value;
            gText.text = _g.ToString("0.000");
        }
        get => _g;
    }
    public float Blue
    {
        set
        {
            _b = value;
            bText.text = _b.ToString("0.000");
        }
        get => _b;
    }
    public float HueShift
    {
        set
        {
            _hueShift = value;
            hText.text = _hueShift.ToString("0.000");
        }
        get => _hueShift;
    }
    public float Attack
    {
        set
        {
            _attack = value;
            attackText.text = _attack.ToString("0.000");
        }
        get => _attack;
    }
    public float Delay
    {
        set
        {
            _decay = value;
            decayText.text = _decay.ToString("0.000");
        }
        get => _decay;
    }
    public float Sustain
    {
        set
        {
            _sustain = value;
            sustainText.text = _sustain.ToString("0.000");
        }
        get => _attack;
    }
    public float Release
    {
        set
        {
            _release = value;
            releaseText.text = _release.ToString("0.000");
        }
        get => _release;
    }
    public float IntensityMult
    {
        set
        {
            _intensityMult = value;
            intensityText.text = _intensityMult.ToString("0.000");
        }
        get => _intensityMult;
    }
    public int StartIndex
    {
        set
        {
            startingArrayIndexOffset = value;
            startIndexText.text = startingArrayIndexOffset == -1 ? "NODEL" : startingArrayIndexOffset.ToString();
        }
        get => startingArrayIndexOffset;
    }
    public string ModeSelect
    {
        set
        {
            modeSelect = value;
            modeText.text = modeSelect;
        }
        get => modeSelect;
    }
    public int PadIndex
    {
        set
        {
            _padIndex = value;
            Transform pad_transform = pads[_padIndex].transform;
            GameObject _off = pad_transform.Find("Pad OFF").gameObject;
            GameObject _on = pad_transform.Find("Pad ON").gameObject;
            _off.SetActive(!_off.activeSelf);
            _on.SetActive(!_on.activeSelf);
            _padIndex = -1;
        }
        get => _padIndex;
    }
}
