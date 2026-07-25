namespace CorsairLink.Devices.ICueLink;

/// <summary>
/// Recreates an animated lighting effect in software by mapping the palette as a
/// continuous gradient across all LEDs and scrolling it over time, so every color
/// is visible at once and flows around the system (similar to Corsair's Watercolor).
/// Used to keep the iCUE LINK hub illuminated after the plugin switches it to
/// software-controlled mode.
/// </summary>
public sealed class GradientFlowLightingEffect : ILinkHubLightingEffect
{
    private readonly IReadOnlyList<RgbColor> _colors;
    private readonly double _cycleSeconds;
    private readonly double _brightness;

    public GradientFlowLightingEffect(IReadOnlyList<RgbColor> colors, TimeSpan cycleDuration, int brightnessPercent)
    {
        if (colors is null || colors.Count == 0)
        {
            throw new ArgumentException("At least one color is required.", nameof(colors));
        }

        _colors = colors;
        _cycleSeconds = Math.Max(0.1, cycleDuration.TotalSeconds);
        _brightness = Utils.Clamp(brightnessPercent, 0, 100) / 100d;
    }

    public void Render(TimeSpan elapsed, RgbColor[] buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        var count = _colors.Count;

        if (count == 1)
        {
            var solid = Scale(_colors[0]);
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = solid;
            }

            return;
        }

        // Scroll the whole palette-loop across the LED positions over time so all
        // colors are shown simultaneously and appear to flow around the system.
        var phase = elapsed.TotalSeconds / _cycleSeconds;

        for (var i = 0; i < buffer.Length; i++)
        {
            var position = (double)i / buffer.Length + phase;
            position -= Math.Floor(position); // wrap into [0,1)

            var scaled = position * count;
            var index = (int)Math.Floor(scaled) % count;
            var nextIndex = (index + 1) % count;
            var fraction = scaled - Math.Floor(scaled);

            var from = _colors[index];
            var to = _colors[nextIndex];

            buffer[i] = new RgbColor(
                Blend(from.R, to.R, fraction),
                Blend(from.G, to.G, fraction),
                Blend(from.B, to.B, fraction));
        }
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
