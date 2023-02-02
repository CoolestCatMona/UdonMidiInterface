
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
    [HideInInspector] public int indexOfBehavior = -1;
    [HideInInspector] public int startingArrayIndexOffset = 0;
    [HideInInspector] public bool delaySequentialIndexes = true;
    [HideInInspector] public bool useBehaviorIndex = false;


    // AreaLit specific settings
    [SerializeField] private int _thirdPartySelectionIndex = 0;
    public bool _usesAreaLit = false;
    public float _intensityMult = 4.0f;
    private float targetIntensity;
    public GameObject _areaListMesh;
    private Renderer _AreaLitRenderer;
    private Renderer[] _AreaLitChildRenderers;

    // Not Currently Implemented
    [HideInInspector] public bool _usesLTCGI = false;


    private Vector4 _towardsColorVec4;
    private Vector4 _awayColorVec4;
    private Color _updatedColor;
    private Color _initialColor = Color.black;
    private Color[] _startColor;
    private Color _currentColor = Color.black;
    private Color[] _colorArray; // Not implemented, but can be for computational optimization
    private Renderer[] _childRenderers;
    private Renderer[] _specialRenderers;
    private Renderer _Renderer;
    private bool _isArray;
    private Vector4[] _step_towards;
    private Vector4[] _step_away;
    private bool _onEventLock = false;
    private bool _offEventLock = false;
    private int _numRenderers;
    private int _onIteration = 0;
    private int _offIteration = 0;
    private int _specialOnIteration = 0;
    private int _specialOffIteration = 0;
    private int _arrayStart;
    private int _arrayStop;
    private int _specialArrayStop;
    int finalUpdateStartIndex;


    // PropertyIDs
    private int _EmissionColor;
    private int _Color;
    private int _LightColor;

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
        _LightColor = PropertyToID("_LightColor");
    }
    /// <summary>
    /// Initializes some values on start, determines if attached object has children (is an Array of objects)
    /// </summary>
    void Start()
    {
        InitIDs();

        // Determine if Object is Array
        if (transform.childCount > 0)
        {
            _isArray = true;
            _childRenderers = transform.GetComponentsInChildren<Renderer>(true);
            _numRenderers = _childRenderers.Length;
            _colorArray = new Color[_numRenderers];
            _startColor = new Color[_numRenderers];
            _step_towards = new Vector4[_numRenderers];
            _step_away = new Vector4[_numRenderers];

            if (_usesAreaLit)
            {
                _AreaLitChildRenderers = _areaListMesh.GetComponentsInChildren<Renderer>(true);

            }
            else
            {
                _AreaLitChildRenderers = new Renderer[_numRenderers]; // Dummy Array
            }
            for (int i = 0; i < _numRenderers; i++)
            {
                var block = new MaterialPropertyBlock();
                _UpdateRendererMaterialProperties(_childRenderers[i], block, _currentColor);
                _UpdateAreaLit(_usesAreaLit, _AreaLitChildRenderers[i], _currentColor, _intensityMult);
            }
        }

        else
        {
            _isArray = false;
            _step_towards = new Vector4[1];
            _step_away = new Vector4[1];
            _Renderer = transform.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock();
            _UpdateRendererMaterialProperties(_Renderer, block, _currentColor);

            if (_usesAreaLit)
            {
                float defaultIntensityMult = (float)Math.Pow(2.0, (double)_intensityMult);
                _AreaLitRenderer = _areaListMesh.GetComponent<Renderer>();
                var areaLitBlock = new MaterialPropertyBlock();
                _UpdateAreaLit(_usesAreaLit, _AreaLitRenderer, _currentColor, defaultIntensityMult);
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
        // Target color is always the same, essentially freezes the color value at the time of the ON event.
        _towardsColorVec4 = new Vector4(_color.r, _color.g, _color.b, 1.0f);

        // Initial color -> color to step FROM - may be different for arrays, unfortunately.
        // AreaLit intensity stays the same in arrays.
        targetIntensity = (float)Math.Pow(2.0, (double)_intensityMult);

        if (_isArray)
        {
            SetStartingColor();
            SetStepSizes(true);
            _arrayStart = GetStartingIndex();
            _arrayStop = PreviousIndex(_childRenderers, _arrayStart);
            UpdateArrayTowardsColor();
        }
        else
        {
            var block = new MaterialPropertyBlock();
            _Renderer.GetPropertyBlock(block);
            _initialColor = block.GetColor(_Color);
            SetStepSizes(true);
            UpdateRendererTowardsColor();
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
        else if (_offEventLock)
        {
            return;
        }
        else
        {
            _offEventLock = true;
            _awayColorVec4 = new Vector4(Color.black.r, Color.black.g, Color.black.b, 0.0f);
            if (_isArray)
            {
                SetStartingColor();
                SetStepSizes(false);
                _arrayStart = GetStartingIndex();
                _arrayStop = PreviousIndex(_childRenderers, _arrayStart);
                UpdateArrayAwayFromColor();
            }
            else
            {
                var block = new MaterialPropertyBlock();
                _Renderer.GetPropertyBlock(block);
                _initialColor = block.GetColor(_Color);

                SetStepSizes(false);
                UpdateRendererAwayFromColor();
            }
        }
    }

    /// <summary>
    /// Updates a single game object's MaterialPropertyBlock, moving some step towards a target color
    /// </summary>
    /// <param name="renderer">Renderer to modify</param>
    /// <param name="areaLitRenderer">AreaLitRenderer, if there is one</param>
    /// <param name="targetColorVec4">Target color</param>
    /// <param name="step">Step size</param>
    /// <returns>If the renderer's color is at the target color</returns>
    public bool UpdateObject(Renderer renderer, Renderer areaLitRenderer, Vector4 targetColorVec4, Vector4 step)
    {
        bool updateCompleted = false;
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Color objectColor = block.GetColor(_Color);
        // Color objectColor = renderer.m/aterial.color;
        Vector4 _currentColorVec4 = new Vector4(objectColor.r, objectColor.g, objectColor.b, objectColor.a);
        if (Vec4AlmostEquals(_currentColorVec4, targetColorVec4))
        {
            Color _targetColor = targetColorVec4;
            _UpdateRendererMaterialProperties(renderer, block, _targetColor);
            _UpdateAreaLit(_usesAreaLit, areaLitRenderer, _targetColor, targetIntensity);
            updateCompleted = true;
        }
        else
        {
            _updatedColor = _UpdateColor(_currentColorVec4, targetColorVec4, step);
            _UpdateRendererMaterialProperties(renderer, block, _updatedColor);
            _UpdateAreaLit(_usesAreaLit, _AreaLitRenderer, _updatedColor, targetIntensity);
        }
        return updateCompleted;
    }

    /// <summary>
    /// Recursively updates a single game object's MaterialPropertyBlock until it reaches some target color.
    /// </summary>
    public void UpdateRendererTowardsColor()
    {
        // Because ON events may interrupt each other, one may release the lock early. Investigate if method should steal lock.
        if (!_onEventLock)
        {
            return;
        }

        bool objectUpdateCompleted = UpdateObject(_Renderer, _AreaLitRenderer, _towardsColorVec4, _step_towards[0]);
        if (objectUpdateCompleted)
        {
            _onEventLock = false;
        }

        else
        {
            SendCustomEventDelayedSeconds(nameof(UpdateRendererTowardsColor), _updateRate_Hz);
        }
    }
    /// <summary>
    /// Recursively updates a single game object's MaterialPropertyBlock until it reaches some target color.
    /// </summary>
    public void UpdateRendererAwayFromColor()
    {
        if (_onEventLock & _offEventLock)
        {
            _offEventLock = false;
            return;
        }

        bool objectUpdateCompleted = UpdateObject(_Renderer, _AreaLitRenderer, _awayColorVec4, _step_away[0]);
        if (objectUpdateCompleted)
        {
            _offEventLock = false;
        }

        else
        {
            SendCustomEventDelayedSeconds(nameof(UpdateRendererAwayFromColor), _updateRate_Hz);
        }
    }

    /// <summary>
    /// Recursively steps through colors in an array from a starting color towards a target color
    /// </summary>
    public void UpdateArrayTowardsColor()
    {
        // Should be able to interrupt itself if updating, one last update for current iteration, then release.
        if (!_onEventLock)
        {
            return;
        }

        for (int j = _arrayStart; j < _arrayStart + _numRenderers; j++)
        {
            // TODO: Only calculate update step for first index in array, store in array. More storage, less calculations
            int i = j % _numRenderers;

            if (delaySequentialIndexes & _onIteration - Mod(i - _arrayStart, _numRenderers) < 0)
            {
                continue;
            }

            bool objectUpdateCompleted = UpdateObject(_childRenderers[i], _AreaLitChildRenderers[i], _towardsColorVec4, _step_towards[i]);
            if (objectUpdateCompleted)
            {
                if (i == _arrayStop)
                {
                    _onEventLock = false;
                    _onIteration = 0;
                    return;
                }
            }
        }
        _onIteration++;
        SendCustomEventDelayedSeconds(nameof(UpdateArrayTowardsColor), _updateRate_Hz);
    }

    /// <summary>
    /// When an ON button press is received, the current 'Away' update should finish one round of its execution
    /// </summary>
    public void FinalArrayUpdateAway()
    {
        if (_specialOffIteration == _specialArrayStop)
        {
            _specialOffIteration = 0;
            return;
        }
        for (int j = finalUpdateStartIndex; j < finalUpdateStartIndex + _numRenderers; j++)
        {
            int i = j % _numRenderers;
            if ((_specialRenderers[i] == null) | (delaySequentialIndexes & _specialOffIteration - Mod(i - finalUpdateStartIndex, _numRenderers) < 0))
            {
                continue;
            }
            bool objectUpdateCompleted = UpdateObject(_specialRenderers[i], _AreaLitChildRenderers[i], _awayColorVec4, _step_away[i]);
        }
        _specialOffIteration++;
        SendCustomEventDelayedSeconds(nameof(FinalArrayUpdateAway), _updateRate_Hz);
    }

    /// <summary>
    /// TODO, in the case of an ON event, the current iteration(s) should finish until it reaches the end index of the array.
    /// </summary>
    public void FinalArrayUpdateTowards()
    {
        SendCustomEventDelayedSeconds(nameof(FinalArrayUpdateTowards), _updateRate_Hz);
    }

    /// <summary>
    /// Recursively steps through colors in an array away from a starting color towards a target color
    /// </summary>
    public void UpdateArrayAwayFromColor()
    {
        if (_onEventLock & _offEventLock)
        {
            _specialOffIteration = 0;
            finalUpdateStartIndex = Mod((_arrayStart + _offIteration), _numRenderers);
            _specialArrayStop = PreviousIndex(_childRenderers, _arrayStart) > finalUpdateStartIndex ? _arrayStart - finalUpdateStartIndex : (_numRenderers - finalUpdateStartIndex - 1) + _arrayStart;
            _specialRenderers = SliceArray(_childRenderers, _arrayStart, finalUpdateStartIndex);
            FinalArrayUpdateAway();
            _offEventLock = false;
            _offIteration = 0;
            return;
        }
        // Iterate through array as though it's circular if we decide to not start at 0 index
        for (int j = _arrayStart; j < _arrayStart + _numRenderers; j++)
        {
            // TODO: Only calculate update step for first index in array, store in array. More storage, less calculations
            int i = j % _numRenderers;

            if (delaySequentialIndexes & _offIteration - Mod(i - _arrayStart, _numRenderers) < 0)
            {
                continue;
            }

            bool objectUpdateCompleted = UpdateObject(_childRenderers[i], _AreaLitChildRenderers[i], _awayColorVec4, _step_away[i]);
            if (objectUpdateCompleted)
            {
                if (i == _arrayStop)
                {
                    _offEventLock = false;
                    _offIteration = 0;
                    return;
                }
            }
        }
        _offIteration++;
        SendCustomEventDelayedSeconds(nameof(UpdateArrayAwayFromColor), _updateRate_Hz);
    }

    /// <summary>
    /// Clamps a color to be within some defined boundary, also handles overshooting in either direction
    /// </summary>
    /// <param name="c">Color</param>
    /// <returns>Clamped Color</returns>
    private Color _UpdateColor(Vector4 current, Vector4 target, Vector4 step)
    {
        // Debug.Log($@"Current: {current}. Target: {target}. Step: {step}");
        if ((current.x <= target.x & step.x <= 0) || (current.x >= target.x & step.x >= 0))
            current.x = target.x;
        else
            current.x = current.x + step.x;

        if ((current.y <= target.y & step.y <= 0) || (current.y >= target.y & step.y >= 0))
            current.y = target.y;
        else
            current.y = current.y + step.y;

        if ((current.z <= target.z & step.z <= 0) || (current.z >= target.z & step.z >= 0))
            current.z = target.z;
        else
            current.z = current.z + step.z;

        if ((current.w <= target.w & step.w <= 0) || (current.w >= target.w & step.w >= 0))
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
        float _stepSize;
        // Time = 0 implies instantaneous change, so we instantly transition from start value to stop value
        if (time == 0)
        {
            _stepSize = stopValue - startValue;
        }
        // Should both values be equivlanet, no step size is needed as we are already at our target.
        else if (startValue == stopValue)
        {
            _stepSize = 0.0f;
        }
        else
        {
            // Get the interpolated value as if this were to interpolate between the two values in the full amount of time
            float _interpoloatedValue = (stopValue - startValue) / (time * (1.0f / (float)_updateRate_Hz));

            // Get a scalar multiplier based on how close we are to a target value
            float scalar = 1.0f;

            if (stopValue < startValue)
            {
                scalar = (1.0f - startValue) / (1.0f - stopValue);
            }
            else
            {
                scalar = startValue / stopValue;
            }

            scalar = 1.0f / (1.0f - scalar);

            _stepSize = (float)Math.Round((double)(_interpoloatedValue * scalar), 2);
            // Debug.Log($@"Lerping between {startValue} and {stopValue} in {time}s gives an interpolated value of {_interpoloatedValue}. We multiply this amount by {scalar} since we are already partway there.");
        }

        return _stepSize;
    }

    /// <summary>
    /// Compares two Vec4 for equality with finer control than the normal '==' functionality
    /// </summary>
    /// <param name="v1">First vector</param>
    /// <param name="v2">Second vector</param>
    /// <returns>True if vectors are almost equal</returns>
    public bool Vec4AlmostEquals(Vector4 v1, Vector4 v2)
    {
        return (AlmostEquals(v1.x, v2.x) & AlmostEquals(v1.y, v2.y) & AlmostEquals(v1.z, v2.z) & AlmostEquals(v1.w, v2.w));
    }

    /// <summary>
    /// Determines if a Vec4 is decreasing, that is all components are less than or equal to the components of a target vector.
    /// </summary>
    /// <param name="v1">Vector to compare</param>
    /// <param name="target">target vector to determine if decreasing</param>
    /// <returns>True if Vector is decreasing</returns>
    public bool Vec4IsDecreasing(Vector4 v1, Vector4 target)
    {
        return ((v1.x <= target.x) & (v1.y <= target.y) & (v1.z <= target.z) & (v1.w <= target.w));
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

    /// <summary>
    /// Updates an AreaLit renderer by modifying the _LightColor property on the AreaLit shader.
    /// </summary>
    /// <param name="useAreaLit">Boolean ot dictacte if arealit is being used</param>
    /// <param name="areaLitRenderer">Renderer for AreaLit</param>
    /// <param name="col">Color to update to</param>
    /// <param name="intensity">Intensity multiplier for AreaLit</param>
    private void _UpdateAreaLit(bool useAreaLit, Renderer areaLitRenderer, Color col, float intensity)
    {
        if (useAreaLit)
        {
            var areaLitBlock = new MaterialPropertyBlock();
            areaLitBlock.SetColor(_LightColor, new Color(col.r * intensity,
                                                           col.g * intensity,
                                                           col.b * intensity,
                                                           col.a * intensity));
            areaLitRenderer.SetPropertyBlock(areaLitBlock);
        }
    }

    /// <summary>
    /// Updates a Renderer's material properties by setting relevant properties on a property block.
    /// </summary>
    /// <param name="renderer">Renderer to update</param>
    /// <param name="block">Material Property Block</param>
    /// <param name="col">Color to update to</param>
    private void _UpdateRendererMaterialProperties(Renderer renderer, MaterialPropertyBlock block, Color col)
    {
        block.SetColor(_EmissionColor, col);
        block.SetColor(_Color, col);
        renderer.SetPropertyBlock(block);
    }

    /// <summary>
    /// Because we iterate through array in a circular manner, we may decide to not start at index 0. In such a case, we determine the starting index based off the assigned offset.
    /// Furthermode, if an option to offset by behavior index is also supplied, the starting index is also offset by that amount.
    /// </summary>
    /// <returns>Starting index of circular array for this behavior</returns>
    private int GetStartingIndex()
    {
        int startingIndex;
        if (useBehaviorIndex)
        {
            startingIndex = (indexOfBehavior + startingArrayIndexOffset + _numRenderers) % _numRenderers;
        }
        else
        {
            startingIndex = (startingArrayIndexOffset + _numRenderers) % _numRenderers;
        }
        return startingIndex;
    }

    /// <summary>
    /// Gets next index in circular array. (Last index points to first index)
    /// </summary>
    /// <param name="a">Array</param>
    /// <param name="index">Index</param>
    /// <returns>Index of next item in circular array</returns>
    private int NextIndex(Array a, int index)
    {
        return index + 1 % a.Length;
    }

    /// <summary>
    /// Gets previous index in circular array. (First index points to last index)
    /// </summary>
    /// <param name="a">Array</param>
    /// <param name="index">Index</param>
    /// <returns>Index of previous item in circular array</returns>
    private int PreviousIndex(Array a, int index)
    {
        return (index + a.Length - 1) % a.Length;
    }

    /// <summary>
    /// Custom modulo function that is able to handle negative numbers.
    /// </summary>
    /// <param name="a">an integer</param>
    /// <param name="b">an integer</param>
    /// <returns>a mod b</returns>
    private int Mod(int a, int b)
    {
        return ((a % b) + b) % b;
    }
    private void SetStartingColor()
    {
        for (int i = 0; i < _numRenderers; i++)
        {
            var block = new MaterialPropertyBlock();
            _childRenderers[i].GetPropertyBlock(block);
            _startColor[i] = block.GetColor(_Color);
        }
    }
    /// <summary>
    /// Genereates step sizes for towards and away from a color for each renderer.
    /// </summary>
    /// <param name="towards">the step size is towards a color or away from a color</param>
    private void SetStepSizes(bool towards)
    {
        if (_isArray)
        {
            if (towards)
            {
                for (int i = 0; i < _step_towards.Length; i++)
                {
                    _step_towards[i] = new Vector4(LerpStepSize(_startColor[i].r, _color.r, _attack),
                                                    LerpStepSize(_startColor[i].g, _color.g, _attack),
                                                    LerpStepSize(_startColor[i].b, _color.b, _attack),
                                                    LerpStepSize(_startColor[i].a, _color.a, _attack));
                }
            }
            else
            {
                for (int i = 0; i < _step_away.Length; i++)
                {
                    _step_away[i] = new Vector4(LerpStepSize(_startColor[i].r, Color.black.r, _release),
                                                LerpStepSize(_startColor[i].g, Color.black.g, _release),
                                                LerpStepSize(_startColor[i].b, Color.black.b, _release),
                                                LerpStepSize(_startColor[i].a, 0.0f, _release));
                }
            }

        }
        else
        {
            if (towards)
            {
                _step_towards[0] = new Vector4(LerpStepSize(_initialColor.r, _color.r, _attack),
                                                LerpStepSize(_initialColor.g, _color.g, _attack),
                                                LerpStepSize(_initialColor.b, _color.b, _attack),
                                                LerpStepSize(_initialColor.a, _color.a, _attack));
            }
            else
            {
                _step_away[0] = new Vector4(LerpStepSize(_initialColor.r, Color.black.r, _release),
                                            LerpStepSize(_initialColor.g, Color.black.g, _release),
                                            LerpStepSize(_initialColor.b, Color.black.b, _release),
                                            LerpStepSize(_initialColor.a, 0.0f, _release));
            }
        }


    }
    /// <summary>
    /// Generates a slice of an array containing the indexes from [0:i] [i+n % len]. Remaining values are filled with NULL
    /// </summary>
    /// <param name="array">An array to take a slice from</param>
    /// <param name="i"></param>
    /// <param name="n"></param>
    /// <returns></returns>
    private Renderer[] SliceArray(Renderer[] array, int i, int n)
    {
        int len = array.Length;
        Renderer[] result = new Renderer[len];
        if (n > i)
        {
            int count = Math.Min(len, n - i + 1);
            Array.Copy(array, i, result, 0, count);
            for (int j = count; j < len; j++)
            {
                result[j] = null;
            }
        }
        else
        {
            Array.Copy(array, 0, result, 0, i);
            for (int j = i; j < i + n; j++)
            {
                result[j] = null;
            }
            Array.Copy(array, i + n, result, i + n, len - (i + n));
        }

        return result;
    }
}
