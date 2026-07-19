using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Universal
{
  [Serializable, VolumeComponentMenu("Post-processing/Bloom")]
  public sealed class Bloom : VolumeComponent, IPostProcessComponent
  {
    [Header("Bloom")]
    [Tooltip("Filters out pixels under this level of brightness. Value is in gamma-space.")]
    public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);

    [Tooltip("Strength of the bloom filter.")]
    public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);

    [Tooltip("Changes the extent of veiling effects.")]
    public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);

    [Tooltip("The number of final iterations to perform in the iterative bloom. Higher quality means better-looking bokeh for a reduced performance impact.")]
    public ClampedIntParameter maxIterations = new ClampedIntParameter(6, 1, 8);

    [Tooltip("Set the maximum size of the bloom to 4x.")]
    public BoolParameter highQualityFiltering = new BoolParameter(true);

    [Tooltip("Changes the resolution of the bloom texture.")]
    public DownscaleParameter downscale = new DownscaleParameter(1);

    [Header("Lens Dirt")]
    [Tooltip("Dirtiness on the lens that adds a full-screen contribution to the bloom image.")]
    public TextureParameter dirtTexture = new TextureParameter(null);

    [Tooltip("The amount of lens dirtiness.")]
    public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);

    [Header("Tint")]
    [Tooltip("Changes the color of the bloom.")]
    public ColorParameter tint = new ColorParameter(Color.white, false, false, false);

    public bool IsActive() => intensity.value > 0f;
    public bool IsTileCompatible() => true;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Color Adjustments")]
  public sealed class ColorAdjustments : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("Adjusts the overall exposure of the scene in EV units. This is applied after HDR effect and right before tonemapping so it won't affect previous effects in the chain.")]
    public FloatParameter postExposure = new FloatParameter(0f);

    [Tooltip("Expands or shrinks the overall range of tonal values.")]
    public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);

    [Tooltip("Controls the overall hue of all colors.")]
    public ClampedFloatParameter hueShift = new ClampedFloatParameter(0f, -180f, 180f);

    [Tooltip("Controls the overall saturation of all colors.")]
    public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);

    [Tooltip("Tints the render to adjust color temperature. Low values cool the image and high values warm the image.")]
    public ClampedFloatParameter temperature = new ClampedFloatParameter(0f, -100f, 100f);

    [Tooltip("Tints the render with a complementary color to compensate for color casts from the environment.")]
    public ClampedFloatParameter tint = new ClampedFloatParameter(0f, -100f, 100f);

    [Tooltip("Specifies the color filter to apply to the render.")]
    public ColorParameter colorFilter = new ColorParameter(Color.white, false, false, false);

    public bool IsActive() => contrast.value != 0f || hueShift.value != 0f || saturation.value != 0f ||
                             temperature.value != 0f || tint.value != 0f || postExposure.value != 0f ||
                             colorFilter.value != Color.white;

    public bool IsTileCompatible() => true;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Color Curves")]
  public sealed class ColorCurves : VolumeComponent, IPostProcessComponent
  {
    public TextureCurveParameter master = new TextureCurveParameter(TextureCurve.Identity(Color.white));
    public TextureCurveParameter red = new TextureCurveParameter(TextureCurve.Identity(Color.red));
    public TextureCurveParameter green = new TextureCurveParameter(TextureCurve.Identity(Color.green));
    public TextureCurveParameter blue = new TextureCurveParameter(TextureCurve.Identity(Color.blue));
    public TextureCurveParameter hueVsHue = new TextureCurveParameter(TextureCurve.Identity(Color.red));
    public TextureCurveParameter hueVsSat = new TextureCurveParameter(TextureCurve.ModifierIdentity(Color.green));
    public TextureCurveParameter satVsSat = new TextureCurveParameter(TextureCurve.ModifierIdentity(Color.blue));
    public TextureCurveParameter lumVsSat = new TextureCurveParameter(TextureCurve.ModifierIdentity(Color.white));

    public bool IsActive() => !(master.value?.IsIdentity() ?? true) ||
                              !(red.value?.IsIdentity() ?? true) ||
                              !(green.value?.IsIdentity() ?? true) ||
                              !(blue.value?.IsIdentity() ?? true) ||
                              !(hueVsHue.value?.IsIdentity() ?? true) ||
                              !(hueVsSat.value?.IsModifierIdentity() ?? true) ||
                              !(satVsSat.value?.IsModifierIdentity() ?? true) ||
                              !(lumVsSat.value?.IsModifierIdentity() ?? true);
    public bool IsTileCompatible() => true;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Vignette")]
  public sealed class Vignette : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("Vignette color.")]
    public ColorParameter color = new ColorParameter(new Color(0f, 0f, 0f, 1f), false, true, false);

    [Tooltip("Center point. The value is not scaled to the screen size, so a vignette with center (0.5, 0.5) is in the middle of the screen regardless of the aspect ratio.")]
    public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

    [Tooltip("Vignette intensity.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("Vignette smoothness.")]
    public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.2f, 0.01f, 1f);

    [Tooltip("Controls how much of the screen the vignette covers.")]
    public ClampedFloatParameter roundness = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("Should the vignette match the screen aspect ratio or be circular?")]
    public BoolParameter rounded = new BoolParameter(false);

    public bool IsActive() => intensity.value > 0f;
    public bool IsTileCompatible() => true;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Grain")]
  public sealed class FilmGrain : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("The type of grain to use.")]
    public FilmGrainLookupParameter type = new FilmGrainLookupParameter(FilmGrainLookup.Thin1);

    [Tooltip("Amount of grain to be shown on screen.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("Amount of response for the grain filter. Higher values show more grain in darker regions.")]
    public ClampedFloatParameter response = new ClampedFloatParameter(0.5f, 0f, 1f);

    [Tooltip("The texture used for the grain.")]
    public TextureParameter grainTexture = new TextureParameter(null);

    public bool IsActive() => intensity.value > 0f && (type.value != FilmGrainLookup.Custom || grainTexture.value != null);
    public bool IsTileCompatible() => true;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Tonemapping")]
  public sealed class Tonemapping : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("The tonemapping mode to use.")]
    public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None);

    public bool IsActive() => mode.value != TonemappingMode.None;
    public bool IsTileCompatible() => true;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Lens Distortion")]
  public sealed class LensDistortion : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("Controls the intensity of the distortion. Positive values distort the center of the screen outwards and negative values distort the edges of the screen inwards.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, -1f, 1f);

    [Tooltip("Controls the point at which the distortion scale starts.")]
    public ClampedFloatParameter scale = new ClampedFloatParameter(1f, 0.01f, 5f);

    [Tooltip("Controls the horizontal distortion amount.")]
    public Vector2Parameter center = new Vector2Parameter(new Vector2(0.5f, 0.5f));

    [Tooltip("Controls how much of the x-axis is affected.")]
    public ClampedFloatParameter xMultiplier = new ClampedFloatParameter(1f, 0f, 1f);

    [Tooltip("Controls how much of the y-axis is affected.")]
    public ClampedFloatParameter yMultiplier = new ClampedFloatParameter(1f, 0f, 1f);

    public bool IsActive() => Math.Abs(intensity.value) > Mathf.Epsilon;
    public bool IsTileCompatible() => false;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Depth of Field")]
  public sealed class DepthOfField : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("The distance between the camera and the focus plane. Units are in meters.")]
    public MinFloatParameter focusDistance = new MinFloatParameter(10f, 0.1f);

    [Tooltip("The distance between the near and far focus. Units are in meters.")]
    public ClampedFloatParameter aperture = new ClampedFloatParameter(5.6f, 0.1f, 32f);

    [Tooltip("The focal length of the lens. Units are in millimeters.")]
    public ClampedFloatParameter focalLength = new ClampedFloatParameter(50f, 1f, 300f);

    [Tooltip("The number of blades in the camera lens aperture.")]
    public ClampedIntParameter bladeCount = new ClampedIntParameter(5, 3, 9);

    [Tooltip("The curvature of the lens aperture blades.")]
    public ClampedFloatParameter bladeCurvature = new ClampedFloatParameter(0.5f, 0f, 1f);

    [Tooltip("The rotation of the lens aperture blades. Units are in degrees.")]
    public ClampedFloatParameter bladeRotation = new ClampedFloatParameter(0f, -360f, 360f);

    public bool IsActive() => true;
    public bool IsTileCompatible() => false;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Motion Blur")]
  public sealed class MotionBlur : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("The quality of the motion blur. Higher values give a better quality for a higher performance cost.")]
    public MotionBlurQualityParameter quality = new MotionBlurQualityParameter(MotionBlurQuality.Low);

    [Tooltip("The intensity of the motion blur.")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0.5f, 0f, 1f);

    [Tooltip("The maximum velocity of pixels affected by motion blur. Pixels with a higher velocity than this value will be clamped.")]
    public ClampedFloatParameter clamp = new ClampedFloatParameter(0.05f, 0f, 0.05f);

    public bool IsActive() => intensity.value > 0f;
    public bool IsTileCompatible() => false;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Panini Projection")]
  public sealed class PaniniProjection : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("Panini projection distance.")]
    public ClampedFloatParameter distance = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("Panini projection crop to fit.")]
    public ClampedFloatParameter cropToFit = new ClampedFloatParameter(1f, 0f, 1f);

    public bool IsActive() => distance.value > 0f;
    public bool IsTileCompatible() => false;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Screen Space Reflections")]
  public sealed class ScreenSpaceReflection : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("Screen Space Reflections quality.")]
    public ScreenSpaceReflectionQualityParameter quality = new ScreenSpaceReflectionQualityParameter(ScreenSpaceReflectionQuality.Low);

    [Tooltip("Distance at which the fade begins.")]
    public MinFloatParameter fadeDistance = new MinFloatParameter(100f, 0f);

    [Tooltip("Size of the surface and its reflection.")]
    public ClampedFloatParameter reflectance = new ClampedFloatParameter(0.1f, 0f, 1f);

    [Tooltip("Amount of specular lighting included.")]
    public ClampedFloatParameter lightBounce = new ClampedFloatParameter(0f, 0f, 1f);

    public bool IsActive() => reflectance.value > 0f;
    public bool IsTileCompatible() => false;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Shadows, Midtones, Highlights")]
  public sealed class ShadowsMidtonesHighlights : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("Adjusts the tint of the shadows.")]
    public Vector4Parameter shadows = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));

    [Tooltip("Adjusts the tint of the midtones.")]
    public Vector4Parameter midtones = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));

    [Tooltip("Adjusts the tint of the highlights.")]
    public Vector4Parameter highlights = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0f));

    [Tooltip("The start of the shadow angle")]
    public MinFloatParameter shadowsStart = new MinFloatParameter(0f, -1f);

    [Tooltip("The end of the shadow angle")]
    public MinFloatParameter shadowsEnd = new MinFloatParameter(0.3f, -1f);

    [Tooltip("The start of the highlights angle")]
    public MinFloatParameter highlightsStart = new MinFloatParameter(0.55f, -1f);

    [Tooltip("The end of the highlights angle")]
    public MinFloatParameter highlightsEnd = new MinFloatParameter(1f, -1f);

    public bool IsActive() => true;
    public bool IsTileCompatible() => true;
  }

  [Serializable, VolumeComponentMenu("Post-processing/White Balance")]
  public sealed class WhiteBalance : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("Sets the white balance to a custom color temperature. Low values cool the image and high values warm the image.")]
    public ClampedFloatParameter temperature = new ClampedFloatParameter(0f, -100f, 100f);

    [Tooltip("Sets the white balance to compensate for a green or magenta tint.")]
    public ClampedFloatParameter tint = new ClampedFloatParameter(0f, -100f, 100f);

    public bool IsActive() => temperature.value != 0f || tint.value != 0f;
    public bool IsTileCompatible() => true;
  }

  [Serializable, VolumeComponentMenu("Post-processing/Channel Mixer")]
  public sealed class ChannelMixer : VolumeComponent, IPostProcessComponent
  {
    [Tooltip("Modify the influence of the red channel.")]
    public Vector3Parameter red = new Vector3Parameter(new Vector3(1f, 0f, 0f));

    [Tooltip("Modify the influence of the green channel.")]
    public Vector3Parameter green = new Vector3Parameter(new Vector3(0f, 1f, 0f));

    [Tooltip("Modify the influence of the blue channel.")]
    public Vector3Parameter blue = new Vector3Parameter(new Vector3(0f, 0f, 1f));

    public bool IsActive() => red.value != new Vector3(1f, 0f, 0f) ||
                              green.value != new Vector3(0f, 1f, 0f) ||
                              blue.value != new Vector3(0f, 0f, 1f);

    public bool IsTileCompatible() => true;
  }

  public interface IPostProcessComponent
  {
    bool IsActive();
    bool IsTileCompatible();
  }

  public sealed class DownscaleParameter : VolumeParameter<int>
  {
    public DownscaleParameter(int value, bool overrideState = false) : base(value, overrideState) { }
  }

  public sealed class ColorCurveParameter : VolumeParameter<Vector2>
  {
    public ColorCurveParameter(Vector2 value, bool overrideState = false) : base(value, overrideState) { }
  }

  public sealed class FilmGrainLookupParameter : VolumeParameter<FilmGrainLookup>
  {
    public FilmGrainLookupParameter(FilmGrainLookup value, bool overrideState = false) : base(value, overrideState) { }
  }

  public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode>
  {
    public TonemappingModeParameter(TonemappingMode value, bool overrideState = false) : base(value, overrideState) { }
  }

  public sealed class MotionBlurQualityParameter : VolumeParameter<MotionBlurQuality>
  {
    public MotionBlurQualityParameter(MotionBlurQuality value, bool overrideState = false) : base(value, overrideState) { }
  }

  public sealed class ScreenSpaceReflectionQualityParameter : VolumeParameter<ScreenSpaceReflectionQuality>
  {
    public ScreenSpaceReflectionQualityParameter(ScreenSpaceReflectionQuality value, bool overrideState = false) : base(value, overrideState) { }
  }

  public sealed class ClampedIntParameter : VolumeParameter<int>
  {
    public int min;
    public int max;

    public ClampedIntParameter(int value, int min, int max, bool overrideState = false)
      : base(value, overrideState)
    {
      this.min = min;
      this.max = max;
    }
  }

  public sealed class TextureParameter : VolumeParameter<Texture>
  {
    public TextureParameter(Texture value, bool overrideState = false)
      : base(value, overrideState) { }
  }

  public sealed class Vector4Parameter : VolumeParameter<Vector4>
  {
    public Vector4Parameter(Vector4 value, bool overrideState = false)
      : base(value, overrideState) { }
  }

  public enum FilmGrainLookup
  {
    Thin1 = 0,
    Thin2 = 1,
    Medium1 = 2,
    Medium2 = 3,
    Medium3 = 4,
    Medium4 = 5,
    Thick1 = 6,
    Thick2 = 7,
    Custom = 8
  }

  public enum TonemappingMode
  {
    None = 0,
    Neutral = 1,
    ACES = 2,
    GRADING = 3,
    External = 4
  }

  public enum MotionBlurQuality
  {
    Low = 0,
    Medium = 1,
    High = 2
  }

  public enum ScreenSpaceReflectionQuality
  {
    Low = 0,
    Medium = 1,
    High = 2,
    Uber = 3
  }
}
