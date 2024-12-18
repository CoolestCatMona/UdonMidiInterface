﻿#if UNITY_EDITOR
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
/// Acts as a custom editor for a MidiBehaviour, provides basic setting guidance and simplifies options for the user
/// </summary>
[CustomEditor(typeof(MidiBehavior))]
public class MidiBehaviorEditor : Editor
{
    private int _thirdPartySelectionIndex = 0;
    // Options for third-party plugins
    private GUIContent[] third_party_plugins = new GUIContent[]
    {
        new GUIContent("None"), new GUIContent("LTCGI"), new GUIContent("Area Lit"),
    };
    public override void OnInspectorGUI()
    {
        if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
        MidiBehavior inspectorBehaviour = (MidiBehavior)target;

        var thirdPartyPluginSelection = serializedObject.FindProperty("_thirdPartySelectionIndex");
        thirdPartyPluginSelection.intValue = EditorGUILayout.Popup(new GUIContent("Plugin", "Which plugin to use, if any"),
                                                thirdPartyPluginSelection.intValue,
                                                third_party_plugins);
        _thirdPartySelectionIndex = thirdPartyPluginSelection.intValue;
        DrawThirdPartyPluginSettings();
        serializedObject.ApplyModifiedProperties();  
    }
    /// <summary>
    /// Draws inspector for third party plugins, if used.
    /// </summary>
    public void DrawThirdPartyPluginSettings()
    {
        EditorGUI.indentLevel++;
        var useLTCGI = serializedObject.FindProperty("_usesLTCGI");
        var useAreaLit = serializedObject.FindProperty("usesAreaLit");

        switch (_thirdPartySelectionIndex)
        {
            case 0:
                useAreaLit.boolValue = false;
                useLTCGI.boolValue = false;
                break;
            case 1:
                EditorGUILayout.HelpBox("Eventual Hook-ins for LTCGI Here", MessageType.Info, true);
                useLTCGI.boolValue = true;
                break;
            case 2:
                useAreaLit.boolValue = true;
                EditorGUILayout.PropertyField(serializedObject.FindProperty("areaLitMesh"), new GUIContent("Area Lit Meshe(s)", "Meshes / continaer of objects with the 'AreaLit / LightMesh' Shader that will be modified with this object"));
                EditorGUILayout.HelpBox("For single game objects, the Area Lit Mesh should be a single game object, for arrays of objects, the Area Lit mesh should also be an array of objects.", MessageType.Info, true);
                break;
            default:
                Debug.LogError("Unrecognized Option");
                break;
        }
        EditorGUI.indentLevel--;
        serializedObject.ApplyModifiedProperties();  
    }
}
#endif