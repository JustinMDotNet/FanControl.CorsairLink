namespace CorsairLink.Devices.ICueLink;

/// <summary>
/// Reproduces iCUE''s "Watercolor" effect for the iCUE LINK hub. iCUE cycles a
/// soft loop of cyan, magenta, yellow and white whose transitions pass through
/// pastel midtones - the behaviour of linear RGB interpolation around those
/// colors, which was confirmed by sampling iCUE''s own USB output.
///
/// The palette loop is spread as a gradient across each device''s LEDs and
/// scrolled over time so the colors flow smoothly around every fan. Linear
/// interpolation keeps the gradient band-free (no visible stepping).
///
/// Used to keep the iCUE LINK hub illuminated after the plugin switches it to
/// software-controlled mode.
/// </summary>
public sealed class WatercolorLightingEffect : ILinkHubLightingEffect
{
    // iCUE''s Watercolor palette (measured hue clusters plus a white stop)
    private static readonly RgbColor[] DefaultPalette =
    {
        new RgbColor(0, 255, 255),     // cyan
        new RgbColor(255, 0, 255),     // magenta
        new RgbColor(255, 255, 0),     // yellow
        new RgbColor(255, 255, 255),   // white
    };

    // LEDs that one full palette loop spans across a device (iCUE''s measured
    // spatial wavelength was ~16 LEDs)
    private const double LedsPerPeriod = 16.0;

    private readonly RgbColor[] _palette;
    private readonly IReadOnlyList<int> _deviceLedCounts;
    private readonly double _cycleSecondsInverse;
    private readonly double _deviceCountInverse;
    private readonly double _brightness;

    public WatercolorLightingEffect(
        IReadOnlyList<int> deviceLedCounts,
        TimeSpan cycleDuration,
        int brightnessPercent,
        IReadOnlyList<RgbColor>? palette = null)
    {
        _palette = palette is { Count: > 0 } ? palette.ToArray() : DefaultPalette;
        _deviceLedCounts = deviceLedCounts ?? Array.Empty<int>();
        _cycleSecondsInverse = 1d / Math.Max(0.5, cycleDuration.TotalSeconds);
        _deviceCountInverse = _deviceLedCounts.Count > 0 ? 1d / _deviceLedCounts.Count : 0d;
        _brightness = Utils.Clamp(brightnessPercent, 0, 100) / 100d;
    }

    public void Render(TimeSpan elapsed, RgbColor[] buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        // scroll the palette loop over time; one cycle advances a full loop
        var timePhase = elapsed.TotalSeconds * _cycleSecondsInverse;
        var index = 0;
        var deviceIndex = 0;

        foreach (var leds in _deviceLedCounts)
        {
            // offset each device so they are not all the same color at once
            var deviceOffset = deviceIndex * _deviceCountInverse;

            for (var j = 0; j < leds && index < buffer.Length; j++)
            {
                var position = j / LedsPerPeriod - timePhase + deviceOffset;
                buffer[index++] = Sample(position);
            }

            deviceIndex++;
        }

        // fill any remainder so no LED is left uninitialized
        while (index < buffer.Length)
        {
            buffer[index++] = Sample(-timePhase);
        }
    }

    private RgbColor Sample(double position)
    {
        var count = _palette.Count();
        position -= Math.Floor(position); // wrap into [0,1)

        var scaled = position * count;
        var i0 = (int)scaled % count;
        var i1 = (i0 + 1) % count;
        var frac = scaled - Math.Floor(scaled);

        var from = _palette[i0];
        var to = _palette[i1];

        return new RgbColor(
            Blend(from.R, to.R, frac),
            Blend(from.G, to.G, frac),
            Blend(from.B, to.B, frac));
    }

    private byte Blend(byte from, byte to, double frac)
    {
        var value = (from + (to - from) * frac) * _brightness;
        return (byte)Utils.Clamp((int)Math.Round(value), 0, 255);
    }
}
