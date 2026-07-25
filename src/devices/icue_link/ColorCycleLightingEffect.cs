namespace CorsairLink.Devices.ICueLink;

/// <summary>
/// Recreates an animated lighting effect in software by smoothly cycling a
/// uniform color through a palette. Used to keep the iCUE LINK hub illuminated
/// after the plugin switches it to software-controlled mode.
/// </summary>
public sealed class ColorCycleLightingEffect : ILinkHubLightingEffect
{
    private readonly IReadOnlyList<RgbColor> _colors;
    private readonly double _cycleSeconds;
    private readonly double _brightness;

    public ColorCycleLightingEffect(IReadOnlyList<RgbColor> colors, TimeSpan cycleDuration, int brightnessPercent)
    {
        if (colors is null || colors.Count == 0)
        {
            throw new ArgumentException("At least one color is required.", nameof(colors));
        }

        _colors = colors;
        _cycleSeconds = Math.Max(0.1, cycleDuration.TotalSeconds);
        _brightness = Utils.Clamp(brightnessPercent, 0, 100) / 100d;
    }

    public RgbColor GetColor(TimeSpan elapsed)
    {
        if (_colors.Count == 1)
        {
            return Scale(_colors[0]);
        }

        var count = _colors.Count;
        var position = elapsed.TotalSeconds % _cycleSeconds / _cycleSeconds * count;
        var index = (int)Math.Floor(position) % count;
        var nextIndex = (index + 1) % count;
        var fraction = position - Math.Floor(position);

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
