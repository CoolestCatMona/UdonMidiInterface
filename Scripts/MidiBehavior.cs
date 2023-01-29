
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;
#if UDONSHARP
using static VRC.SDKBase.VRCShader;
#else
using static UnityEngine.Shader;
#endif
/// <summary>
/// This class represents a behaviour that should be enacted based on the press of a MIDI note. This does not use any synchronized variables, rather it has its variables set by a master orchestrator and will expose events that can be called.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class MidiBehavior : UdonSharpBehaviour
{

    /// <summary>
    /// Synchronized variables that are set via the MidiOrchestrator behavior
    /// </summary>
    [HideInInspector] public Color _color = Color.black;
    [HideInInspector] public float _attack = 1.0f;
    [HideInInspector] public float _decay = 1.0f;
    [HideInInspector] public float _sustain = 1.0f;
    [HideInInspector] public float _release = 1.0f;
    [HideInInspector] public float _updateRate_s = 1.0f;
    [HideInInspector] public float _updateRate_Hz = 1.0f;

    // AreaLit specific settings
    [HideInInspector] public bool _usesAreaLit = false;
    [HideInInspector] public float _intensityMult = 4.0f; // Sync this value
    private float targetIntensity;
    [HideInInspector] public GameObject _areaListMesh;
    private Renderer _AreaLitRenderer;
    private Renderer[] _AreaLitChildRenderers;

    // Not Currently Implemented
    [HideInInspector] public bool _usesLTCGI = false;


    private Vector4 _targetColorVec4;
    private Color _updatedColor;
    private Color _initialColor = Color.black;
    private Color _currentColor = Color.black;

    private Renderer[] _childRenderers;
    private Renderer _Renderer;
    private bool _isArray;
    private Vector4 _rgba_step;
    private bool _onEventLock = false;
    private bool _offEventLock = false;
    private int _numChildren;
    private int _iteration = 0;

    // PropertyIDs
    private int _EmissionColor;
    private int _Color;

    // Magic Numbers
    private float MAX_COLOR_VALUE = 1.0f;
    private float MIN_COLOR_VALUE = 0.0f;

    /// <summary>
    /// https://docs.unity3d.com/ScriptReference/Shader.PropertyToID.html
    /// </summary>
    private void InitIDs()
    {
        _EmissionColor = PropertyToID("_EmissionColor");
        _Color = PropertyToID("_Color");
        // _LightColor = PropertyToID("_LightColor");
    }
    /// <summary>
    /// Initializes some values on start, determines if attached object has children (is an Array of objects)
    /// </summary>
    void Start()
    {
        InitIDs();
        _numChildren = transform.childCount;

        // Determine if Object is Array
        if (_numChildren > 0)
        {
            _isArray = true;
            _childRenderers = transform.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in _childRenderers)
            {
                var block = new MaterialPropertyBlock();
                _UpdateRendererMaterialProperties(renderer, block, _currentColor)
            }
        }

        else
        {
            _isArray = false;
            _Renderer = transform.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            _UpdateRendererMaterialProperties(_Renderer, block, _currentColor)

            if(_usesAreaLit)
            {
                float defaultIntensityMult = (float)Math.Pow(2.0, (double)_intensityMult);
                _AreaLitRenderer = _areaListMesh.GetComponent<Renderer>();
                var areaLitBlock = new MaterialPropertyBlock();
                block.SetColor("_LightColor", new Color(_currentColor.r * defaultIntensityMult,
                                                        _currentColor.g * defaultIntensityMul,
                                                        _currentColor.b * defaultIntensityMult,
                                                        _currentColor.a * defaultIntensityMult;
                                                        _currentColor.a * (float)Math.Pow(2.0, (double)_intensityMult)));
                _AreaLitRenderer.SetPropertyBlock(areaLitBlock);
            }
        }
    }
    /// <summary>
    /// Calculates linearly interpolated step size for reaching synchronized _color value from some starting color
    /// Recursively increases the material's color values by the calculated step size at some update rate until the target color is reached.
    /// Sets a lock such that an OFF event must wait to execute as long as this method holds the lock
    /// </summary>
    public void MidiOnEvent()
    {
        _onEventLock = true;
        _targetColorVec4 = new Vector4(_color.r, _color.g, _color.b, _color.a);
        _rgba_step = new Vector4(LerpStepSize(_initialColor.r, _color.r, _attack), LerpStepSize(_initialColor.g, _color.g, _attack), LerpStepSize(_initialColor.b, _color.b, _attack), LerpStepSize(_initialColor.a, _color.a, _attack));
        targetIntensity = (float)Math.Pow(2.0, (double)_intensityMult);

        if (_isArray)
        {
            UpdateArray();
        }
        else
        {
            UpdateSingle();
        }
    }

    /// <summary>
    /// Calculates linearly interpolated step size for reaching Color(0,0,0,0) from synchronized _color value
    /// Recursively increases the material's color values by the calculated step size at some update rate until the target color is reached.
    /// This event will wait to fire so long as an ON event is executing
    /// TODO: Currently does MAX -> 0 in _release, SHOULD do MAX -> Sustain in _decay, then do Sustain -> 0 in _release
    /// </summary>
    public void MidiOffEvent()
    {
        if (_onEventLock)
        {
            SendCustomEventDelayedSeconds(nameof(MidiOffEvent), _updateRate_Hz);
        }
        else
        {
            _offEventLock = true;
            _targetColorVec4 = new Vector4(Color.black.r, Color.black.g, Color.black.b, 0.0f);
            _rgba_step = new Vector4(LerpStepSize(_color.r, Color.black.r, _release), LerpStepSize(_color.g, Color.black.g, _release), LerpStepSize(_color.b, Color.black.b, _release), LerpStepSize(_color.a, 0.0f, _release));
            if (_isArray)
            {
                UpdateArray();
            }
            else
            {
                UpdateSingle();
            }
        }
    }
    /// <summary>
    /// Recursively updates a single game object's MaterialPropertyBlock until it reaches some target color.
    /// </summary>
    public void UpdateSingle()
    {
        // Both locks existing implies that an ON event was received while an OFF event is executing
        // Release the OFF event lock and 'convert' the OFF event to an ON event
        if (_onEventLock & _offEventLock)
        {
            _offEventLock = false;
            _targetColorVec4 = new Vector4(_color.r, _color.g, _color.b, _color.a);
            _rgba_step = new Vector4(LerpStepSize(_currentColor.r, _color.r, _attack), LerpStepSize(_currentColor.g, _color.g, _attack), LerpStepSize(_currentColor.b, _color.b, _attack), LerpStepSize(_currentColor.a, _color.a, _attack));
        }

        var block = new MaterialPropertyBlock();
        _Renderer.GetPropertyBlock(block);
        _currentColor = block.GetColor(_Color);
        Vector4 _currentColorVec4 = new Vector4(_currentColor.r, _currentColor.g, _currentColor.b, _currentColor.a);

        if (RoughlyEquivalent(_currentColorVec4, _targetColorVec4))
        {
            Color _targetColor = new Color(_targetColorVec4.x, _targetColorVec4.y, _targetColorVec4.z, _targetColorVec4.w);

            _UpdateRendererMaterialProperties(_Renderer, block, _targetColor)
            _UpdateAreaLit(_usesAreaLit, _AreaLitRenderer, _targetColor, targetIntensity);

            _initialColor = block.GetColor(_Color);
            _onEventLock = false;
            _offEventLock = false;
        }
        else
        {
            _updatedColor = _UpdateColor(_currentColorVec4, _targetColorVec4, _rgba_step);
            _UpdateRendererMaterialProperties(_Renderer, block, _updatedColor);
            _UpdateAreaLit(_usesAreaLit, _AreaLitRenderer, _updatedColor, targetIntensity);

            SendCustomEventDelayedSeconds(nameof(UpdateSingle), _updateRate_Hz);
        }
    }

    /// <summary>
    /// Recursively updates an array of GameObject's MaterialPropertyBlocks until they all reach some target color.
    /// </summary>
    public void UpdateArray()
    {
        // Both locks existing implies that an ON event was received while an OFF event is executing
        // Release the OFF event lock and 'convert' the OFF event to an ON event
        if (_onEventLock & _offEventLock)
        {
            _offEventLock = false;
            _targetColorVec4 = new Vector4(_color.r, _color.g, _color.b, _color.a);
            _rgba_step = new Vector4(LerpStepSize(_currentColor.r, _color.r, _attack), LerpStepSize(_currentColor.g, _color.g, _attack), LerpStepSize(_currentColor.b, _color.b, _attack), LerpStepSize(_currentColor.a, _color.a, _attack));
        }
        // Iterate through each child renderer in the GameObject array
        foreach (Renderer renderer in _childRenderers)
        {
            Transform child = renderer.transform;
            int index = child.GetSiblingIndex();

            // Get current renderer's MaterialPropertyBlock and ensures the color is within some defined bounds
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            _currentColor = block.GetColor(_Color);
            Vector4 _currentColorVec4 = new Vector4((float)Math.Round((double)_currentColor.r, 3), (float)Math.Round((double)_currentColor.g, 3), (float)Math.Round((double)_currentColor.b, 3), (float)Math.Round((double)_currentColor.a, 3));

            // Do not update each renderer at the same time, updates should be offset by at least one iteration
            if (_iteration - index < 0)
            {
                continue;
            }
            // Checks to see if the current color is roughly equivalent to the target color
            // If they are roughtly equal, set current material to target material (avoids floating point inconsistencies)
            // If this is the final element in the array, release any locks, reset iteration, and exit
            if (RoughlyEquivalent(_currentColorVec4, _targetColorVec4))
            {
                Color _targetColor = new Color(_targetColorVec4.x, _targetColorVec4.y, _targetColorVec4.z, _targetColorVec4.w);
                _UpdateRendererMaterialProperties(renderer, block, _targetColor);
                // _UpdateAreaLit(_usesAreaLit, renderer, _targetColor, targetIntensity);


                if (index == _numChildren - 1)
                {
                    _onEventLock = false;
                    _offEventLock = false;
                    _iteration = 0;
                    _initialColor = block.GetColor(_Color);
                    return;
                }
            }
            // Otherwise, update the current material by the calculated rgba step size
            else
            {
                _updatedColor = _UpdateColor(_currentColorVec4, _targetColorVec4, _rgba_step);
                _UpdateRendererMaterialProperties(renderer, block, _updatedColor);
                // _UpdateAreaLit(_usesAreaLit, renderer, _targetColor, targetIntensity);
            }
        }
        // After each renderer has been updated, increase iteration and recurse
        _iteration++;
        SendCustomEventDelayedSeconds(nameof(UpdateArray), _updateRate_Hz);
    }

    /// <summary>
    /// Clamps a color to be within some defined boundary, also handles overshooting in either direction
    /// </summary>
    /// <param name="c">Color</param>
    /// <returns>Clamped Color</returns>
    private Color _UpdateColor(Vector4 current, Vector4 target, Vector4 step)
    {
        if ((current.x <= target.x & step.x <= 0) | (current.x >= target.x & step.x >= 0))
            current.x = target.x;
        else
            current.x = current.x + step.x;

        if ((current.y <= target.y & step.y <= 0) | (current.y >= target.y & step.y >= 0))
            current.y = target.y;
        else
            current.y = current.y + step.y;

        if ((current.z <= target.z & step.z <= 0) | (current.z >= target.z & step.z >= 0))
            current.z = target.z;
        else
            current.z = current.z + step.z;

        if ((current.w <= target.w & step.w <= 0) | (current.w >= target.w & step.w >= 0))
            current.w = target.w;
        else
            current.w = current.w + step.w;

        Color safeColor = new Color(Mathf.Clamp(current.x, MIN_COLOR_VALUE, MAX_COLOR_VALUE),
                                    Mathf.Clamp(current.y, MIN_COLOR_VALUE, MAX_COLOR_VALUE),
                                    Mathf.Clamp(current.z, MIN_COLOR_VALUE, MAX_COLOR_VALUE),
                                    Mathf.Clamp(current.w, MIN_COLOR_VALUE, MAX_COLOR_VALUE));
        return safeColor;
    }

    /// <summary>
    /// Determines the step size for reaching a stopping value from a start value in some amount of time.
    /// </summary>
    /// <param name="startValue">Starting value</param>
    /// <param name="stopValue">Stopping value</param>
    /// <param name="time">Amount of time (s) to reach stopValue from startValue assuming some update rate</param>
    /// <returns></returns>
    public float LerpStepSize(float startValue, float stopValue, float time)
    {
        // Debug.Log("Start Value: " + startValue + " Stop Value: " + stopValue + " Time: " + time);
        // Time = 0 implies instantaneous change, so we instantly transition from start value to stop value
        if (time == 0)
        {
            return stopValue - startValue;
        }

        // Because one event may interrupt another, a scalar multiplier is applied to the step size.
        float scalar;
        float slope = (stopValue - startValue) / time;

        // When LERPing to/from 0 scalar should be 1
        if (AlmostEquals(startValue, 0.0f) | AlmostEquals(stopValue, 0.0f))
        {
            scalar = 1.0f;
        }
        else
        {
            // Handle remaining time scale to account for (re)starting event partway through.
            if (stopValue > startValue)
            {
                scalar = (float)Math.Round((double)(1.0f / (1.0f - (startValue / stopValue))), 2);
            }
            else
            {
                scalar = (float)Math.Round((double)(1.0f / (1.0f - (stopValue / startValue))), 2);
            }
        }
        // Should both values be equivlanet, no step size is needed as we are already at our target.
        if (AlmostEquals(startValue, stopValue))
        {
            return 0.0f;
        }

        float interpoloatedValue;
        interpoloatedValue = startValue + _updateRate_Hz * ((stopValue - startValue) / (time));
        interpoloatedValue = Math.Abs(interpoloatedValue - startValue);
        float _stepSize = interpoloatedValue * slope;

        // Should NEVER increase or decrease more than the actual difference between the two values in a single step
        _stepSize = Mathf.Clamp((float)Math.Round((double)(_stepSize * scalar), 2), -1 * Math.Abs(stopValue - startValue), Math.Abs(stopValue - startValue));
        return _stepSize;
    }
    /// <summary>
    /// Compares two Vec4 for equality with finer control than the normal '==' functionality
    /// </summary>
    /// <param name="v1">First vector</param>
    /// <param name="v2">Second vector</param>
    /// <returns></returns>
    public bool RoughlyEquivalent(Vector4 v1, Vector4 v2)
    {
        // TODO: Should be able to handle >, >=, <, <= or !=
        // switch (pred)
        // {
        //     case "gt":
        //         break;
        //     case "lt":
        //         break;
        //     case "gte":
        //         break;
        //     case "lte":
        //         break;
        //     case "eq":
        //         break;
        //     case "neq":
        //         break;
        // }
        return (AlmostEquals(v1.x, v2.x) & AlmostEquals(v1.y, v2.y) & AlmostEquals(v1.z, v2.z) & AlmostEquals(v1.w, v2.w));
    }
    /// <summary>
    /// Checks to see if one float is almost equal to another float up to some precision value
    /// </summary>
    /// <param name="float1">First float</param>
    /// <param name="float2">Second float</param>
    /// <param name="precision">Precision, a precision of 2 checks up to two decimal places. Default 2</param>
    /// <returns></returns>
    public bool AlmostEquals(float float1, float float2, int precision = 2)
    {
        float epsilon = (float)Math.Pow(10.0f, -precision);
        return (Math.Abs(float1 - float2) <= epsilon);
    }

    private void _UpdateAreaLit(bool useAreaLit, Renderer areaLitRenderer, Color col, float intensity)
    {
            if(useAreaLit)
            {
                var areaLitBlock = new MaterialPropertyBlock();
                areaLitBlock.SetColor("_LightColor", new Color(col.r * intensity,
                                                               col.g * intensity,
                                                               col.b * intensity,
                                                               col.a * intensity));
                areaLitRenderer.SetPropertyBlock(areaLitBlock);
            }
    }
    private void _UpdateRendererMaterialProperties(Renderer renderer, MaterialPropertyBlock block, Color col)
    {
        block.SetColor(_EmissionColor, col);
        block.SetColor(_Color, col);
        renderer.SetPropertyBlock(block);
    }
}
