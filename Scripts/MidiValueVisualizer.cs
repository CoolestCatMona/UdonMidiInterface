
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
    [HideInInspector, FieldChangeCallback(nameof(Attack))] public float _attack = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Delay))] public float _delay = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Sustain))] public float _sustain = 1.0f;
    [HideInInspector, FieldChangeCallback(nameof(Release))] public float _release = 1.0f;
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
    public Text delayText;
    public Text releaseText;

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
        delayText.text = "1.000";
        sustainText.text = "1.000";
        releaseText.text = "1.000";
    }
    public Color ColorValue
    {
        set
        {
            _colorValue = value;
            rText.text = _colorValue.r.ToString("0.000");
            gText.text = _colorValue.g.ToString("0.000");
            bText.text = _colorValue.b.ToString("0.000");
            Renderer _Renderer = previewObject.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            block.SetColor(_Color, _colorValue);
            _Renderer.SetPropertyBlock(block);
        }
        get => _colorValue;
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
            _delay = value;
            delayText.text = _delay.ToString("0.000");
        }
        get => _delay;
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
