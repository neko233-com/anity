namespace UnityEngine;

[AddComponentMenu("Miscellaneous/Wind Zone")]
public class WindZone : MonoBehaviour
{
  private WindZoneMode _mode = WindZoneMode.Directional;
  private float _radius = 10f;
  private float _windMain = 1f;
  private float _windTurbulence = 1f;
  private float _windPulseMagnitude = 0.5f;
  private float _windPulseFrequency = 0.01f;

  public WindZoneMode mode
  {
    get => _mode;
    set => _mode = value;
  }

  public float radius
  {
    get => _radius;
    set => _radius = Mathf.Max(0f, value);
  }

  public float windMain
  {
    get => _windMain;
    set => _windMain = value;
  }

  public float windTurbulence
  {
    get => _windTurbulence;
    set => _windTurbulence = value;
  }

  public float windPulseMagnitude
  {
    get => _windPulseMagnitude;
    set => _windPulseMagnitude = value;
  }

  public float windPulseFrequency
  {
    get => _windPulseFrequency;
    set => _windPulseFrequency = value;
  }

  protected override void OnEnable()
  {
    base.OnEnable();
    Wind.Register(this);
  }

  protected override void OnDisable()
  {
    Wind.Unregister(this);
    base.OnDisable();
  }

  protected override void OnDestroy()
  {
    Wind.Unregister(this);
    base.OnDestroy();
  }
}

public enum WindZoneMode
{
  Directional = 0,
  Spherical = 1
}
