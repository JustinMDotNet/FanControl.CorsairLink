namespace CorsairLink.Devices.ICueLink;

/// <summary>
/// Reproduces iCUE's "Watercolor" effect for the iCUE LINK hub. iCUE cycles a
/// soft loop of cyan, magenta, yellow and white whose transitions pass through
/// pastel midtones - the behaviour of linear RGB interpolation around those
/// colors, which was confirmed by sampling iCUE's own USB output.
///
/// The palette loop is spread as a gradient across each device's LEDs and
/// scrolled over time so the colors flow smoothly around every fan. Linear
/// interpolation keeps the gradient band-free (no visible stepping).
///
/// Used to keep the iCUE LINK hub illuminated after the plugin switches it to
/// software-controlled mode.
/// </summary>
public sealed class WatercolorLightingEffect : ILinkHubLightingEffect
{
    // iCUE's Watercolor palette (measured hue clusters plus a white stop)
    private static readonly RgbColor[] DefaultPalette =
    {
        new RgbColor(0, 255, 255),     // cyan
        new RgbColor(255, 0, 255),     // magenta
        new RgbColor(255, 255, 0),     // yellow
        new RgbColor(255, 255, 255),   // white
    };

    // number of LEDs one full palette loop spans across a device;
    // iCUE's measured spatial wavelength was ~16 LEDs
    private const double LedsPerLoop = 16.0;

    private readonly RgbColor[] _palette;
    private readonly IReadOnlyList<int> _deviceLedCounts;
    private readonly double _loopsPerSecond;
    private readonly double _deviceHueSpacing;
    private readonly double _brightness;

    public WatercolorLightingEffect(
        IReadOnlyList<int> deviceLedCounts,
        TimeSpan loopDuration,
        int brightnessPercent,
        IReadOnlyList<RgbColor>? palette = null)
    {
        _palette = palette is { Count: > 0 } ? palette.ToArray() : DefaultPalette;
        _deviceLedCounts = deviceLedCounts ?? Array.Empty<int>();
        _loopsPerSecond = 1d / Math.Max(0.5, loopDuration.TotalSeconds);
        _deviceHueSpacing = _deviceLedCounts.Count > 0 ? 1d / _deviceLedCounts.Count : 0d;
        _brightness = Utils.Clamp(brightnessPercent, 0, 100) / 100d;
    }

    public void Render(TimeSpan elapsed, RgbColor[] buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        // fraction of a full palette loop the animation has scrolled through
        var scrollOffset = elapsed.TotalSeconds * _loopsPerSecond;
        var ledIndex = 0;
        var deviceIndex = 0;

        foreach (var deviceLedCount in _deviceLedCounts)
        {
            // stagger each device so they are not all showing the same color
            var deviceOffset = deviceIndex * _deviceHueSpacing;

            for (var led = 0; led < deviceLedCount && ledIndex < buffer.Length; led++)
            {
                var loopPosition = led / LedsPerLoop - scrollOffset + deviceOffset;
                buffer[ledIndex++] = SamplePalette(loopPosition);
            }

            deviceIndex++;
        }

        // fill any remaining LEDs (e.g. a count mismatch) so none are left dark
        while (ledIndex < buffer.Length)
        {
            buffer[ledIndex++] = SamplePalette(-scrollOffset);
        }
    }

    /// <summary>
    /// Maps a position along the looping palette (any real number, wrapped into
    /// [0,1)) to an interpolated color.
    /// </summary>
    private RgbColor SamplePalette(double loopPosition)
    {
        var wrapped = loopPosition - Math.Floor(loopPosition);
        var scaled = wrapped * _palette.Length;

        var fromIndex = (int)scaled % _palette.Length;
        var toIndex = (fromIndex + 1) % _palette.Length;
        var fraction = scaled - fromIndex;

        var from = _palette[fromIndex];
        var to = _palette[toIndex];

        return new RgbColor(
            Interpolate(from.R, to.R, fraction),
            Interpolate(from.G, to.G, fraction),
            Interpolate(from.B, to.B, fraction));
    }

    private byte Interpolate(byte from, byte to, double fraction)
    {
        var value = (from + (to - from) * fraction) * _brightness;
        return (byte)Utils.Clamp((int)Math.Round(value), 0, 255);
    }
}
