namespace UnityEngine;

public enum PhysicMaterialCombine
{
    Average = 0,
    Minimum = 1,
    Multiply = 2,
    Maximum = 3
}

public class PhysicMaterial
{
    private string _name = string.Empty;
    private float _dynamicFriction = 0.6f;
    private float _staticFriction = 0.6f;
    private float _bounciness;
    private Vector3 _frictionDirection2;
    private PhysicMaterialCombine _frictionCombineMode = PhysicMaterialCombine.Average;
    private PhysicMaterialCombine _bounceCombine = PhysicMaterialCombine.Average;

    public PhysicMaterial()
    {
    }

    public PhysicMaterial(string name)
    {
        _name = name;
    }

    public string name
    {
        get => _name;
        set => _name = value;
    }

    public float bounciness
    {
        get => _bounciness;
        set => _bounciness = Math.Clamp(value, 0f, 1f);
    }

    public float dynamicFriction
    {
        get => _dynamicFriction;
        set => _dynamicFriction = Math.Clamp(value, 0f, 1f);
    }

    public float staticFriction
    {
        get => _staticFriction;
        set => _staticFriction = Math.Clamp(value, 0f, float.MaxValue);
    }

    public PhysicMaterialCombine frictionCombine
    {
        get => _frictionCombineMode;
        set => _frictionCombineMode = value;
    }

    public PhysicMaterialCombine bounceCombine
    {
        get => _bounceCombine;
        set => _bounceCombine = value;
    }

    public Vector3 frictionDirection2
    {
        get => _frictionDirection2;
        set => _frictionDirection2 = value;
    }

    public float friction
    {
        get => (_dynamicFriction + _staticFriction) * 0.5f;
        set
        {
            _dynamicFriction = value;
            _staticFriction = value;
        }
    }

    internal static float CombineBounciness(float a, float b, PhysicMaterialCombine ca, PhysicMaterialCombine cb)
    {
        var mode = ca > cb ? ca : cb;
        return mode switch
        {
            PhysicMaterialCombine.Average => (a + b) * 0.5f,
            PhysicMaterialCombine.Minimum => MathF.Min(a, b),
            PhysicMaterialCombine.Maximum => MathF.Max(a, b),
            PhysicMaterialCombine.Multiply => a * b,
            _ => (a + b) * 0.5f
        };
    }

    internal static float CombineFriction(float a, float b, PhysicMaterialCombine ca, PhysicMaterialCombine cb)
    {
        var mode = ca > cb ? ca : cb;
        return mode switch
        {
            PhysicMaterialCombine.Average => (a + b) * 0.5f,
            PhysicMaterialCombine.Minimum => MathF.Min(a, b),
            PhysicMaterialCombine.Maximum => MathF.Max(a, b),
            PhysicMaterialCombine.Multiply => a * b,
            _ => (a + b) * 0.5f
        };
    }
}
