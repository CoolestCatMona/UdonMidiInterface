#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.SDKBase;
using VRC.Udon;
using UdonSharp;

#if !COMPILER_UDONSHARP
using UnityEditor;
using UdonSharpEditor;
#endif
#endif

#if UNITY_EDITOR
/// <summary>
/// Acts as a custom editor for the MidiOrchestrator Behaviour, provides basic setting guidance and simplifies options for the user
/// </summary>
[CustomEditor(typeof(MidiOrchestrator))]
public class MidiOrchestratorEditor : Editor
{
    // Integers representing iterable indexes
    private int updateRateSelection;
    private int _controllerSelectionIndex = 1;
    private int _thirdPartySelectionIndex = 0;

    // Booleans representing foldouts
    private bool foldoutState = false;
    private bool debugFoldout = false;


    /// <summary>
    /// Magic Numbers
    /// </summary>
    private const int CUSTOM_CONTROLLER = 0;


    // Options for send rate
    private GUIContent[] send_rate_opts = new GUIContent[]
    {
        new GUIContent("1Hz (Slow, Reliable)"), new GUIContent("5Hz"), new GUIContent("10Hz"), new GUIContent("20Hz"), new GUIContent("25Hz"), new GUIContent("50Hz (Fast, Unreliable)"),
    };

    // Integers corresponding to index chosen for send rate
    private int[] send_rate_ints = {1, 5, 10, 20, 25, 50};

    // Options for controllers that I have personally mapped
    private GUIContent[] controller_opts = new GUIContent[]
    {
        new GUIContent("Custom Controller"), new GUIContent("Arturia Beatstep \u2215 Beatstep Pro"), new GUIContent("Novation Launchpad"),
    };

    // Options for third-party plugins
    private GUIContent[] third_party_plugins = new GUIContent[]
    {
        new GUIContent("None"), new GUIContent("LTCGI"), new GUIContent("Area Lit"),
    };

    /// <summary>
    /// Custom inspector code. Draw default UDON parameters and ensure the script has access to the latest values, then display formatted options to the user.
    /// </summary>
    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
        MidiOrchestrator inspectorBehaviour = (MidiOrchestrator)target;

        serializedObject.Update();

        EditorGUILayout.Space(); EditorGUILayout.Space();

        // Change checks will check for a change in properties between the block encompassed by BeginChangeCheck() and EndChangeCheck()
        EditorGUI.BeginChangeCheck();

        updateRateSelection = EditorGUILayout.IntPopup(new GUIContent("Update Rate", "Update rate (Hz). Higher update rates are more computationally expensive."),
                                                        inspectorBehaviour.updateRate,
                                                        send_rate_opts,
                                                        send_rate_ints);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(inspectorBehaviour, "Modify Update Rate");
            PrefabUtility.RecordPrefabInstancePropertyModifications(inspectorBehaviour);
            inspectorBehaviour.updateRate = updateRateSelection;
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonEvents"),new GUIContent("Pad Events", "Events to fire on a pad press"),  true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxTime"),new GUIContent("Transition Time", "Maximum amount of time (s) it should take for any change in material to occur"),  true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sustainLevel"),new GUIContent("Sustain Level", "Intensity of material to Sustain for the amount of time defined by the sustain CC"),  true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("MAX_STARTING_INDEX"),new GUIContent("Max Starting Offset", "Maximum starting offset for arrays of objects. If you aren't using arrays of objects, don't worry about this."),  true);
        EditorGUILayout.Space(); EditorGUILayout.Space();

        var header = new GUIStyle(EditorStyles.boldLabel);

        GUILayout.Label("Controller Specific Settings", header);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        var controllerSelection = serializedObject.FindProperty("_controllerSelectionIndex");
        controllerSelection.intValue = EditorGUILayout.Popup(new GUIContent("Controller", "Initial Controller."),
                                                            controllerSelection.intValue,
                                                            controller_opts);
        _controllerSelectionIndex = controllerSelection.intValue;

        if (EditorGUI.EndChangeCheck())
            DrawControllerSpecificSettings(true);
        else
            DrawControllerSpecificSettings(false);

        EditorGUILayout.Space(); EditorGUILayout.Space(); EditorGUILayout.Space();
        GUILayout.Label("Debug", header);
        var useVisualizer = serializedObject.FindProperty("usesVisualizer");
        useVisualizer.boolValue = EditorGUILayout.Toggle("Use Visualizer? (Resource intensive on User)", useVisualizer.boolValue);
        if (useVisualizer.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("MidiVisualizer"),new GUIContent("MIDI Visualizer"),  true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(); EditorGUILayout.Space();

        if ((debugFoldout = EditorGUILayout.Foldout(debugFoldout, "Default Inspector (Do not modify)")))
        {
            DrawDefaultInspector();
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Wrapper for interface that provides controller-specific settings
    /// </summary>
    /// <param name="useDefaults">Dictates if default values should be used</param>
    public void DrawControllerSpecificSettings(bool useDefaults)
    {
        string help_text;
        EditorGUI.indentLevel++;

        if(_controllerSelectionIndex == CUSTOM_CONTROLLER)
        {
            help_text = $@"Please define settings for your specific controller under 'Advanced Settings'";
            EditorGUILayout.HelpBox(help_text, MessageType.Info, true);
        }

        else
            help_text = $@"These are pre-defined controller specific settings for {controller_opts[_controllerSelectionIndex].text}. Only edit these if you know what you're doing! Values are reset when selecting a new preset.";

        foldoutState = EditorGUILayout.Foldout(foldoutState, "Advanced Settings");
        EditorGUI.indentLevel++;
        switch (_controllerSelectionIndex)
        {
            // Magic numbers used below, IYKYK
            case CUSTOM_CONTROLLER:
                ControllerSpecificSettings(36, 52, new int[] {10, 74, 71, 76, 114, 18, 19, 16, 77, 93, 73}, false, useDefaults, foldoutState);
                break;
            case 1:
                if (foldoutState) EditorGUILayout.HelpBox(help_text, MessageType.Info, true);
                ControllerSpecificSettings(36, 52, new int[] {10, 74, 71, 76, 114, 18, 19, 16, 77, 93, 73}, false, useDefaults, foldoutState);
                break;
            case 2:
                if (foldoutState) EditorGUILayout.HelpBox(help_text, MessageType.Info, true);
                ControllerSpecificSettings(48, 80, new int[] {65, 64, 69, 68, 73, 72, 77, 76, 67, 66, 71, 70, 75, 74, 79, 78, 80, 79, 82, 81, 84, 83}, true, useDefaults, foldoutState);
                break;
            default:
                Debug.LogError("Unrecognized Option");
                break;
        }
        EditorGUI.indentLevel--;
        serializedObject.ApplyModifiedProperties();
        EditorGUI.indentLevel--;
    }
    /// <summary>
    /// Provides an interface for controller-specific settings
    /// </summary>
    /// <param name="minNote">Minimum pad note number that should be presesed</param>
    /// <param name="maxNote">Maximum pad note number that should be presesed</param>
    /// <param name="cc_array">Array of CC values</param>
    /// <param name="usePadsAsCC">Toggle for Pads being used as CC</param>
    /// <param name="useDefaults">Should default values be used</param>
    /// <param name="draw">Draw advanced settings</param>
    private void ControllerSpecificSettings(int minNote, int maxNote, int[] cc_array, bool usePadsAsCC, bool useDefaults, bool draw)
    {
        var minNoteProperty = serializedObject.FindProperty("minNote");
        var maxNoteProperty = serializedObject.FindProperty("maxNote");
        var padsAsCC = serializedObject.FindProperty("padsAsCC");

        if(useDefaults)
        {
            minNoteProperty.intValue = minNote;
            maxNoteProperty.intValue = maxNote;
            padsAsCC.boolValue = usePadsAsCC;
        }

        if(draw)
        {
            EditorGUILayout.PropertyField(minNoteProperty,new GUIContent("Lowest Note"),  true);
            EditorGUILayout.PropertyField(maxNoteProperty,new GUIContent("Highest Note"),  true);
            EditorGUILayout.PropertyField(padsAsCC,new GUIContent("Use Pads as CC?"),  true);
        }
        EditorGUI.indentLevel++;

        if(padsAsCC.boolValue)
        {
            var red_P = serializedObject.FindProperty("RED");
            var green_P = serializedObject.FindProperty("GREEN");
            var blue_P = serializedObject.FindProperty("BLUE");
            var hue_P = serializedObject.FindProperty("HUE");
            var attack_P = serializedObject.FindProperty("ATTACK");
            var decay_P = serializedObject.FindProperty("DECAY");
            var sustain_P = serializedObject.FindProperty("SUSTAIN");
            var release_P = serializedObject.FindProperty("RELEASE");
            var intensitymult_P = serializedObject.FindProperty("INTENSITYMULT");
            var startIndex_P = serializedObject.FindProperty("START_INDEX");
            var mode_P = serializedObject.FindProperty("MODE");

            var red_M = serializedObject.FindProperty("RED_DEC");
            var green_M = serializedObject.FindProperty("GREEN_DEC");
            var blue_M = serializedObject.FindProperty("BLUE_DEC");
            var hue_M = serializedObject.FindProperty("HUE_DEC");
            var attack_M = serializedObject.FindProperty("ATTACK_DEC");
            var decay_M = serializedObject.FindProperty("DECAY_DEC");
            var sustain_M = serializedObject.FindProperty("SUSTAIN_DEC");
            var release_M = serializedObject.FindProperty("RELEASE_DEC");
            var intensitymult_M = serializedObject.FindProperty("INTENSITYMULT_DEC");
            var padChangeAmnt = serializedObject.FindProperty("padCCChangeAmnt");
            var padStop = serializedObject.FindProperty("padStop");
            var startIndex_M = serializedObject.FindProperty("START_INDEX_DEC");
            var mode_M = serializedObject.FindProperty("MODE_DEC");

            if(useDefaults)
            {
                red_P.intValue = cc_array[0];
                red_M.intValue = cc_array[1];
                green_P.intValue = cc_array[2];
                green_M.intValue = cc_array[3];
                blue_P.intValue = cc_array[4];
                blue_M.intValue = cc_array[5];
                hue_P.intValue = cc_array[6];
                hue_M.intValue = cc_array[7];
                attack_P.intValue = cc_array[8];
                attack_M.intValue = cc_array[9];
                decay_P.intValue = cc_array[10];
                decay_M.intValue = cc_array[11];
                sustain_P.intValue = cc_array[12];
                sustain_M.intValue = cc_array[13];
                release_P.intValue = cc_array[14];
                release_M.intValue = cc_array[15];
                intensitymult_P.intValue = cc_array[16];
                intensitymult_M.intValue = cc_array[17];
                startIndex_P.intValue = cc_array[18];
                startIndex_M.intValue = cc_array[19];
                mode_P.intValue = cc_array[20];
                mode_M.intValue = cc_array[21];
                padChangeAmnt.floatValue = .05f;
                padStop.intValue = 64;
            }

            if(draw)
            {
                EditorGUILayout.PropertyField(padChangeAmnt,new GUIContent("Pad Change amount", "How much the pad should increment/decrement a value by"),  true);
                EditorGUILayout.PropertyField(padStop,new GUIContent("Pad Stopping point", "When using pads as CCs, this is the value that separates pad values from CC values"),  true);
                EditorGUILayout.Space();
                GUILayout.Label("Pads to use for defined CC");
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(red_P,new GUIContent("Red+"),  true);
                EditorGUILayout.PropertyField(red_M,new GUIContent("Red-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(green_P,new GUIContent("Green+"),  true);
                EditorGUILayout.PropertyField(green_M,new GUIContent("Green-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(blue_P,new GUIContent("Blue+"),  true);
                EditorGUILayout.PropertyField(blue_M,new GUIContent("Blue-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(hue_P,new GUIContent("Hue+"),  true);
                EditorGUILayout.PropertyField(hue_M,new GUIContent("Hue-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(attack_P,new GUIContent("Attack+"),  true);
                EditorGUILayout.PropertyField(attack_M,new GUIContent("Attack-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(decay_P,new GUIContent("Decay+"),  true);
                EditorGUILayout.PropertyField(decay_M,new GUIContent("Decay-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(sustain_P,new GUIContent("Sustain+"),  true);
                EditorGUILayout.PropertyField(sustain_M,new GUIContent("Sustain-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(release_P,new GUIContent("Release+"),  true);
                EditorGUILayout.PropertyField(release_M,new GUIContent("Release-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(intensitymult_P,new GUIContent("Intensity+"),  true);
                EditorGUILayout.PropertyField(intensitymult_M,new GUIContent("Intensity-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(startIndex_P,new GUIContent("Start Index+"),  true);
                EditorGUILayout.PropertyField(startIndex_M,new GUIContent("Start Index-"),  true);
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(mode_P,new GUIContent("Mode+"),  true);
                EditorGUILayout.PropertyField(mode_M,new GUIContent("Mode-"),  true);
            }
        }
        else
        {
            var red_CC = serializedObject.FindProperty("RED");
            var green_CC = serializedObject.FindProperty("GREEN");
            var blue_CC = serializedObject.FindProperty("BLUE");
            var hue_CC = serializedObject.FindProperty("HUE");
            var attack_CC = serializedObject.FindProperty("ATTACK");
            var decay_CC = serializedObject.FindProperty("DECAY");
            var sustain_CC = serializedObject.FindProperty("SUSTAIN");
            var release_CC = serializedObject.FindProperty("RELEASE");
            var intensitymult_CC = serializedObject.FindProperty("INTENSITYMULT");
            var startIndex_CC = serializedObject.FindProperty("START_INDEX");
            var mode_CC = serializedObject.FindProperty("MODE");

            if(useDefaults)
            {
                red_CC.intValue = cc_array[0];
                green_CC.intValue = cc_array[1];
                blue_CC.intValue = cc_array[2];
                hue_CC.intValue = cc_array[3];
                attack_CC.intValue = cc_array[4];
                decay_CC.intValue = cc_array[5];
                sustain_CC.intValue = cc_array[6];
                release_CC.intValue = cc_array[7];
                intensitymult_CC.intValue = cc_array[8];
                startIndex_CC.intValue = cc_array[9];
                mode_CC.intValue = cc_array[10];
            }

            if(draw)
            {
                EditorGUILayout.PropertyField(red_CC,new GUIContent("Red CC"),  true);
                EditorGUILayout.PropertyField(green_CC,new GUIContent("Green CC"),  true);
                EditorGUILayout.PropertyField(blue_CC,new GUIContent("Blue CC"),  true);
                EditorGUILayout.PropertyField(hue_CC,new GUIContent("Hue CC"),  true);
                EditorGUILayout.PropertyField(attack_CC,new GUIContent("Attack CC"),  true);
                EditorGUILayout.PropertyField(decay_CC,new GUIContent("Decay CC"),  true);
                EditorGUILayout.PropertyField(sustain_CC,new GUIContent("Sustain CC"),  true);
                EditorGUILayout.PropertyField(release_CC,new GUIContent("Release CC"),  true);
                EditorGUILayout.PropertyField(intensitymult_CC,new GUIContent("Intensity CC"),  true);
                EditorGUILayout.PropertyField(startIndex_CC,new GUIContent("Start Index CC"),  true);
                EditorGUILayout.PropertyField(mode_CC,new GUIContent("Mode CC"),  true);
            }
        }
        EditorGUI.indentLevel--;
    }
}
#endif
