namespace CorsairLink.Devices.ICueLink;

/// <summary>
/// Recreates an animated lighting effect in software where each connected RGB
/// device (fan/cooler) shows a single solid color that cycles through the palette.
/// Devices are staggered around the palette loop so they cycle independently.
/// Used to keep the iCUE LINK hub illuminated after the plugin switches it to
/// software-controlled mode.
/// </summary>
public sealed class PerFanColorCycleLightingEffect : ILinkHubLightingEffect
{
    private readonly IReadOnlyList<RgbColor> _colors;
    private readonly IReadOnlyList<int> _fanLedCounts;
    private readonly double _cycleSeconds;
    private readonly double _brightness;

    public PerFanColorCycleLightingEffect(
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

        var fanCount = _fanLedCounts.Count;
        var basePhase = elapsed.TotalSeconds / _cycleSeconds;
        var index = 0;

        for (var f = 0; f < fanCount; f++)
        {
            // stagger each device around the palette loop so they cycle independently
            var offset = (double)f / fanCount;
            var color = ColorAt(basePhase + offset);

            var leds = _fanLedCounts[f];
            for (var j = 0; j < leds && index < buffer.Length; j++)
            {
                buffer[index++] = color;
            }
        }

        // fill any remainder (e.g. a count mismatch) so no LED is left uninitialized
        var fallback = ColorAt(basePhase);
        while (index < buffer.Length)
        {
            buffer[index++] = fallback;
        }
    }

    private RgbColor ColorAt(double phase)
    {
        var count = _colors.Count;

        if (count == 1)
        {
            return Scale(_colors[0]);
        }

        var position = phase - Math.Floor(phase); // wrap into [0,1)
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
