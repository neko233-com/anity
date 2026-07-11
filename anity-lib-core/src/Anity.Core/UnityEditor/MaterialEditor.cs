using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEditor;

public class MaterialEditor : Editor
{
    private Material[] _targets;
    private Shader _shader;
    private int _firstInspectedEditorIndex;
    private bool _isVisible = true;
    private MaterialProperty[] _properties;
    private static readonly Dictionary<int, MaterialEditor> _editors = new();

    public object customShaderGUI { get; set; }
    public bool isVisible { get => _isVisible; set => _isVisible = value; }
    public MaterialEditor firstInspectedEditor { get; private set; }

    public Material[] targets => _targets ?? Array.Empty<Material>();

    private void Awake()
    {
    }

    protected override void OnEnable()
    {
        if (target is Material mat)
        {
            _targets = new[] { mat };
            _shader = mat.shader;
            _properties = ShaderUtil.GetMaterialProperties(_targets);
        }
        else if (targets != null)
        {
            var mats = new List<Material>();
            foreach (var t in targets)
            {
                if (t is Material m) mats.Add(m);
            }
            _targets = mats.ToArray();
            if (_targets.Length > 0)
            {
                _shader = _targets[0].shader;
                _properties = ShaderUtil.GetMaterialProperties(_targets);
            }
        }
    }

    protected override void OnDisable()
    {
    }

    protected override void OnHeaderGUI()
    {
        DrawHeader();
    }

    public override void OnInspectorGUI()
    {
        serializedObject?.Update();
        DrawShaderProperties();
        RenderQueueField();
        EnableInstancingField();
        DoubleSidedGIField();
        LightmapEmissionFlagsProperty();
        serializedObject?.ApplyModifiedProperties();
    }

    public new void DrawHeader()
    {
        if (_targets != null && _targets.Length > 0)
        {
            GUILayout.Label(_targets[0].name ?? "Material", EditorStyles.boldLabel);
            if (_shader != null)
            {
                GUILayout.Label($"Shader: {_shader.name}");
            }
        }
    }

    public bool DrawPropertiesExcluding(SerializedProperty property, params string[] propertiesToExclude)
    {
        _ = property;
        _ = propertiesToExclude;
        return true;
    }

    public float DefaultShaderProperty(MaterialProperty prop)
    {
        return ShaderProperty(prop, prop.name);
    }

    public Rect TexturePropertySingleLine(GUIContent label, MaterialProperty textureProp)
    {
        MaterialProperty? extra = null;
        return TexturePropertySingleLine(label, textureProp, extra);
    }

    public Rect TexturePropertySingleLine(GUIContent label, MaterialProperty textureProp, MaterialProperty? extraProperty)
    {
        var rect = EditorGUILayout.GetControlRect();
        if (label != null) EditorGUI.LabelField(rect, label);
        return rect;
    }

    public Rect TexturePropertyTwoLine(GUIContent label, MaterialProperty textureProp, MaterialProperty? extraProperty, bool showTilingOffset, float height)
    {
        var rect = EditorGUILayout.GetControlRect();
        if (label != null) EditorGUI.LabelField(rect, label);
        return rect;
    }

    public Color ColorProperty(MaterialProperty prop, GUIContent label)
    {
        return ColorProperty(prop, label?.text ?? prop.name);
    }

    public Color ColorProperty(MaterialProperty prop, string label)
    {
        var color = EditorGUILayout.ColorField(label ?? prop.name, prop.colorValue);
        prop.colorValue = color;
        if (_targets != null)
        {
            foreach (var mat in _targets)
            {
                mat.SetColor(prop.nameID, color);
            }
        }
        return color;
    }

    public float FloatProperty(MaterialProperty prop, GUIContent label)
    {
        return FloatProperty(prop, label?.text ?? prop.name);
    }

    public float FloatProperty(MaterialProperty prop, string label)
    {
        var val = EditorGUILayout.FloatField(label ?? prop.name, prop.floatValue);
        prop.floatValue = val;
        if (_targets != null)
        {
            foreach (var mat in _targets)
            {
                mat.SetFloat(prop.nameID, val);
            }
        }
        return val;
    }

    public float RangeProperty(MaterialProperty prop, GUIContent label)
    {
        return RangeProperty(prop, label?.text ?? prop.name);
    }

    public float RangeProperty(MaterialProperty prop, string label)
    {
        var val = EditorGUILayout.Slider(label ?? prop.name, prop.floatValue, prop.rangeLimits.x, prop.rangeLimits.y);
        prop.floatValue = val;
        if (_targets != null)
        {
            foreach (var mat in _targets)
            {
                mat.SetFloat(prop.nameID, val);
            }
        }
        return val;
    }

    public Vector4 VectorProperty(MaterialProperty prop, GUIContent label)
    {
        return VectorProperty(prop, label?.text ?? prop.name);
    }

    public Vector4 VectorProperty(MaterialProperty prop, string label)
    {
        var val = EditorGUILayout.Vector4Field(label ?? prop.name, prop.vectorValue);
        prop.vectorValue = val;
        if (_targets != null)
        {
            foreach (var mat in _targets)
            {
                mat.SetVector(prop.nameID, val);
            }
        }
        return val;
    }

    public float ShaderProperty(MaterialProperty prop, string label)
    {
        switch (prop.type)
        {
            case MaterialPropertyType.Range:
                return RangeProperty(prop, label);
            case MaterialPropertyType.Float:
            case MaterialPropertyType.Int:
                return FloatProperty(prop, label);
            case MaterialPropertyType.Color:
                ColorProperty(prop, label);
                return 0f;
            case MaterialPropertyType.Vector:
                VectorProperty(prop, label);
                return 0f;
            case MaterialPropertyType.Texture:
                TexturePropertySingleLine(GUIContent.Temp(label), prop);
                return 0f;
            default:
                return FloatProperty(prop, label);
        }
    }

    public new MaterialProperty[] GetMaterialProperties(UnityEngine.Object[] mats)
    {
        var materials = new List<Material>();
        foreach (var o in mats)
        {
            if (o is Material m) materials.Add(m);
        }
        return ShaderUtil.GetMaterialProperties(materials.ToArray());
    }

    public void RenderQueueField()
    {
        if (_targets == null || _targets.Length == 0) return;
        var queue = EditorGUILayout.IntField("Render Queue", _targets[0].renderQueue);
        foreach (var mat in _targets)
        {
            mat.renderQueue = queue;
        }
    }

    public void EnableInstancingField()
    {
        if (_targets == null || _targets.Length == 0) return;
        var inst = EditorGUILayout.Toggle("Enable GPU Instancing", _targets[0].enableInstancing);
        foreach (var mat in _targets)
        {
            mat.enableInstancing = inst;
        }
    }

    public void DoubleSidedGIField()
    {
        if (_targets == null || _targets.Length == 0) return;
        var dsgi = EditorGUILayout.Toggle("Double Sided Global Illumination", _targets[0].doubleSidedGI);
        foreach (var mat in _targets)
        {
            mat.doubleSidedGI = dsgi;
        }
    }

    public void LightmapEmissionFlagsProperty()
    {
        if (_targets == null || _targets.Length == 0) return;
        EditorGUILayout.LabelField("Global Illumination", EditorStyles.boldLabel);
        var flags = _targets[0].globalIlluminationFlags;
        var realtimeEmissive = EditorGUILayout.Toggle("Realtime Emissive", flags.HasFlag(MaterialGlobalIlluminationFlags.RealtimeEmissive));
        var bakedEmissive = EditorGUILayout.Toggle("Baked Emissive", flags.HasFlag(MaterialGlobalIlluminationFlags.BakedEmissive));
        var newFlags = MaterialGlobalIlluminationFlags.None;
        if (realtimeEmissive) newFlags |= MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (bakedEmissive) newFlags |= MaterialGlobalIlluminationFlags.BakedEmissive;
        foreach (var mat in _targets)
        {
            mat.globalIlluminationFlags = newFlags;
        }
    }

    public bool EmissionEnabledProperty()
    {
        if (_targets == null || _targets.Length == 0) return false;
        return _targets[0].globalIlluminationFlags.HasFlag(MaterialGlobalIlluminationFlags.AnyEmissive);
    }

    public bool BumpScaleNotSupported()
    {
        return false;
    }

    public bool TextureCompatibilityWarning(Texture texture)
    {
        return texture != null;
    }

    public void DrawShaderProperties()
    {
        if (_properties == null || _properties.Length == 0) return;
        foreach (var prop in _properties)
        {
            if (prop.flags.HasFlag((MaterialPropertyFlags)ShaderPropertyFlags.HideInInspector)) continue;
            ShaderProperty(prop, prop.name);
        }
    }

    public void DrawFoldoutColorReference(ref bool folded, GUIContent label, MaterialProperty prop)
    {
        folded = EditorGUILayout.Foldout(folded, label ?? GUIContent.Temp(prop.name));
        if (folded)
        {
            ColorProperty(prop, (string)null);
        }
    }

    public void DrawMaterialToScene(Material mat)
    {
        _ = mat;
    }

    public void DrawPreview(Rect previewArea)
    {
        EditorGUI.DrawRect(previewArea, new Color(0.2f, 0.2f, 0.2f, 1f));
        EditorGUI.LabelField(previewArea, "Material Preview", EditorStyles.centeredGreyMiniLabel);
    }

    public override GUIContent GetPreviewTitle()
    {
        return GUIContent.Temp(_targets != null && _targets.Length > 0 ? _targets[0].name : "Material");
    }

    public void OnPreviewSettings()
    {
    }

    public void ReloadPreviewInstances()
    {
    }

    public static void RegisterPropertyChangeUndo(string name)
    {
        Undo.SetCurrentGroupName(name);
    }

    public static void FixupEmissiveFlag(Material mat, Color color)
    {
        if (mat == null) return;
        var flags = mat.globalIlluminationFlags;
        var isBlack = color.r <= 0.001f && color.g <= 0.001f && color.b <= 0.001f;
        if (isBlack)
        {
            flags |= MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        else
        {
            flags &= ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        mat.globalIlluminationFlags = flags;
    }

    public static float GetDefaultShaderProperty(MaterialProperty prop)
    {
        return prop.floatValue;
    }

    public static void ApplyMaterialPropertyDrawers()
    {
    }

    internal void SetTargetMaterials(Material[] materials)
    {
        _targets = materials;
        if (materials != null && materials.Length > 0)
        {
            _shader = materials[0].shader;
            _properties = ShaderUtil.GetMaterialProperties(materials);
        }
    }
}
