namespace UnityEngine;

public class PhysicsMaterial2D : Object
{
    private float _friction = 0.4f;
    private float _bounciness;

    public float friction
    {
        get => _friction;
        set => _friction = value;
    }

    public float bounciness
    {
        get => _bounciness;
        set => _bounciness = value;
    }

    public PhysicsMaterialCombine2D frictionCombine { get; set; } = PhysicsMaterialCombine2D.Average;
    public PhysicsMaterialCombine2D bouncinessCombine { get; set; } = PhysicsMaterialCombine2D.Average;
}

public enum PhysicsMaterialCombine2D
{
    Average,
    Minimum,
    Multiply,
    Maximum
}
