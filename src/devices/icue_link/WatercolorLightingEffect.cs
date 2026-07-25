namespace CorsairLink.Devices.ICueLink;

/// <summary>
/// Reproduces a soft "Watercolor" lighting effect for the iCUE LINK hub. Each
/// device shows a continuous, low-saturation hue sweep across its own LEDs that
/// scrolls over time, so pastel colors flow smoothly around every fan. Using a
/// constant saturation (no sharp features) keeps the gradient band-free and
/// avoids the visible stepping a lookup-table ramp produces.
///
/// The pastel-hue-sweep approach mirrors the community OpenLinkHub project''s
/// interpretation of Watercolor; the ~0.4 saturation also matches the average
/// saturation measured from iCUE''s own Watercolor USB output.
///
/// Used to keep the iCUE LINK hub illuminated after the plugin switches it to
/// software-controlled mode.
/// </summary>
public sealed class WatercolorLightingEffect : ILinkHubLightingEffect
{
    // portion of the full hue wheel shown across a single device at one instant;
    // less than a full wheel keeps neighbouring LEDs close in color (smoother)
    private const double HueSpreadPerDevice = 180.0;

    private readonly IReadOnlyList<int> _deviceLedCounts;
    private readonly double _cycleSecondsInverse;
    private readonly double _deviceCountInverse;
    private readonly double _saturation;
    private readonly double _value;

    public WatercolorLightingEffect(
        IReadOnlyList<int> deviceLedCounts,
        TimeSpan cycleDuration,
        int brightnessPercent,
        int saturationPercent = 40)
    {
        _deviceLedCounts = deviceLedCounts ?? Array.Empty<int>();
        _cycleSecondsInverse = 1d / Math.Max(0.5, cycleDuration.TotalSeconds);
        _deviceCountInverse = _deviceLedCounts.Count > 0 ? 1d / _deviceLedCounts.Count : 0d;
        _saturation = Utils.Clamp(saturationPercent, 0, 100) / 100d;
        _value = Utils.Clamp(brightnessPercent, 0, 100) / 100d;
    }

    public void Render(TimeSpan elapsed, RgbColor[] buffer)
    {
        if (buffer is null || buffer.Length == 0)
        {
            return;
        }

        // scroll the hue over time; one cycle sweeps a full 360 degrees
        var huePhase = elapsed.TotalSeconds * _cycleSecondsInverse * 360d;
        var index = 0;
        var deviceIndex = 0;

        foreach (var leds in _deviceLedCounts)
        {
            // offset each device so they are not all the same color at once
            var deviceHueOffset = deviceIndex * _deviceCountInverse * 360d;
            var spreadPerLed = leds > 0 ? HueSpreadPerDevice / leds : 0d;

            for (var j = 0; j < leds && index < buffer.Length; j++)
            {
                var hue = j * spreadPerLed + huePhase + deviceHueOffset;
                buffer[index++] = HsvToRgb(hue, _saturation, _value);
            }

            deviceIndex++;
        }

        // fill any remainder so no LED is left uninitialized
        while (index < buffer.Length)
        {
            buffer[index++] = HsvToRgb(huePhase, _saturation, _value);
        }
    }

    private static RgbColor HsvToRgb(double hue, double saturation, double value)
    {
        hue -= 360d * Math.Floor(hue / 360d); // wrap into [0,360)
        var c = value * saturation;
        var x = c * (1 - Math.Abs(hue / 60d % 2 - 1));
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
