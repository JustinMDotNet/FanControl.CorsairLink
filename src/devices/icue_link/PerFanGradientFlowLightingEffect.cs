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

        return BlendHsv(_colors[index], _colors[nextIndex], fraction);
    }

    // Interpolate in HSV so the hue rotates around the color wheel during a
    // transition (e.g. cyan -> magenta passes through blue) instead of fading
    // through a desaturated RGB midpoint. The palette stops themselves are
    // returned exactly (fraction 0 or 1).
    private RgbColor BlendHsv(RgbColor from, RgbColor to, double fraction)
    {
        var (h1, s1, v1) = RgbToHsv(from);
        var (h2, s2, v2) = RgbToHsv(to);

        // a desaturated endpoint (e.g. white) has no meaningful hue; borrow the
        // other's so the transition rotates saturation rather than jumping hue
        if (s1 <= 0)
        {
            h1 = h2;
        }

        if (s2 <= 0)
        {
            h2 = h1;
        }

        var deltaHue = h2 - h1;
        if (deltaHue > 180)
        {
            deltaHue -= 360;
        }
        else if (deltaHue < -180)
        {
            deltaHue += 360;
        }

        var hue = h1 + deltaHue * fraction;
        var saturation = s1 + (s2 - s1) * fraction;
        var value = (v1 + (v2 - v1) * fraction) * _brightness;

        return HsvToRgb(hue, saturation, value);
    }

    private RgbColor Scale(RgbColor color) =>
        new(
            (byte)Math.Round(color.R * _brightness),
            (byte)Math.Round(color.G * _brightness),
            (byte)Math.Round(color.B * _brightness));

    private static (double H, double S, double V) RgbToHsv(RgbColor color)
    {
        double r = color.R / 255d, g = color.G / 255d, b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double hue = 0;
        if (delta > 0)
        {
            if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }

            if (hue < 0)
            {
                hue += 360;
            }
        }

        var saturation = max <= 0 ? 0 : delta / max;
        return (hue, saturation, max);
    }

    private static RgbColor HsvToRgb(double hue, double saturation, double value)
    {
        hue -= 360d * Math.Floor(hue / 360d); // wrap into [0,360)
        var c = value * saturation;
        var x = c * (1 - Math.Abs(((hue / 60d) % 2) - 1));
        var m = value - c;

        double r, g, b;
        if (hue < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (hue < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (hue < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (hue < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (hue < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return new RgbColor(
            (byte)Utils.Clamp((int)Math.Round((r + m) * 255), 0, 255),
            (byte)Utils.Clamp((int)Math.Round((g + m) * 255), 0, 255),
            (byte)Utils.Clamp((int)Math.Round((b + m) * 255), 0, 255));
    }
}
