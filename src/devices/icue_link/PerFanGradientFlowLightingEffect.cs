namespace CorsairLink.Devices.ICueLink;

/// <summary>
/// Recreates an animated lighting effect in software where each connected RGB
/// device (fan/cooler) shows the full palette as a gradient across its own LEDs,
/// scrolling over time so the colors shift/flow ("rain") within every device.
/// Used to keep the iCUE LINK hub illuminated after the plugin switches it to
/// software-controlled mode.
/// </summary>
public sealed class PerFanGradientFlowLightingEffect : ILinkHubLightingEffect
{
    private readonly IReadOnlyList<RgbColor> _colors;
    private readonly IReadOnlyList<int> _fanLedCounts;
    private readonly double _cycleSeconds;
    private readonly double _brightness;

    public PerFanGradientFlowLightingEffect(
        IReadOnlyList<RgbColor> colors,
        IReadOnlyList<int> fanLedCounts,
        TimeSpan cycleDuration,
        int brightnessPercent)
    {
        if (colors is null || colors.Count == 0)
        {
            throw new ArgumentException("At least one color is required.", nameof(colors));
        }

        _colors = colors;
        _fanLedCounts = fanLedCounts ?? Array.Empty<int>();
        _cycleSeconds = Math.Max(0.1, cycleDuration.TotalSeconds);
        _brightness = Utils.Clamp(brightnessPercent, 0, 100) / 100d;
    }

    public void Render(TimeSpan elapsed, RgbColor[] buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        var phase = elapsed.TotalSeconds / _cycleSeconds;
        var index = 0;

        foreach (var leds in _fanLedCounts)
        {
            for (var j = 0; j < leds && index < buffer.Length; j++)
            {
                // spread the whole palette across this device's LEDs and scroll it
                // over time so the colors flow within each device independently
                var position = leds > 1 ? (double)j / leds + phase : phase;
                buffer[index++] = ColorAt(position);
            }
        }

        // fill any remainder (e.g. a count mismatch) so no LED is left uninitialized
        while (index < buffer.Length)
        {
            buffer[index++] = ColorAt(phase);
        }
    }

    private RgbColor ColorAt(double position)
    {
        var count = _colors.Count;

        if (count == 1)
        {
            return Scale(_colors[0]);
        }

        position -= Math.Floor(position); // wrap into [0,1)
        var scaled = position * count;
        var index = (int)Math.Floor(scaled) % count;
        var nextIndex = (index + 1) % count;
        var fraction = scaled - Math.Floor(scaled);

        var from = _colors[index];
        var to = _colors[nextIndex];

        return new RgbColor(
            Blend(from.R, to.R, fraction),
            Blend(from.G, to.G, fraction),
            Blend(from.B, to.B, fraction));
    }

    private byte Blend(byte from, byte to, double fraction)
    {
        var value = (int)Math.Round((from + (to - from) * fraction) * _brightness);
        return (byte)Utils.Clamp(value, 0, 255);
    }

    private RgbColor Scale(RgbColor color) =>
        new(
            (byte)Math.Round(color.R * _brightness),
            (byte)Math.Round(color.G * _brightness),
            (byte)Math.Round(color.B * _brightness));
}
