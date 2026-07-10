namespace UnityEngine;

/// <summary>
/// Physics material for 2D colliders.
/// </summary>
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
}
