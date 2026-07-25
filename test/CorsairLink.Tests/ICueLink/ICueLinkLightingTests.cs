using CorsairLink;
using CorsairLink.Devices.ICueLink;

namespace CorsairLink.Tests.ICueLink;

public class ICueLinkLightingTests
{
    [Fact]
    public void KnownLinkDevices_ReportExpectedLedChannels()
    {
        Assert.Equal(34, KnownLinkDevices.Find(LinkDeviceModel.FanQxSeries, 0x00)!.LedChannels);
        Assert.Equal(8, KnownLinkDevices.Find(LinkDeviceModel.FanRxRgbSeries, 0x00)!.LedChannels);
        Assert.Equal(8, KnownLinkDevices.Find(LinkDeviceModel.FanRxMaxRgbSeries, 0x00)!.LedChannels);
        Assert.Equal(20, KnownLinkDevices.Find(LinkDeviceModel.LiquidCoolerTitanSeries, 0x00)!.LedChannels);
        Assert.Equal(0, KnownLinkDevices.Find(LinkDeviceModel.CapSwapModuleVrmFan, 0x00)!.LedChannels);
        Assert.Equal(0, KnownLinkDevices.Find(LinkDeviceModel.FanRxSeries, 0x00)!.LedChannels);
    }

    [Fact]
    public void CreateColorData_WritesRgbTripletPerLed()
    {
        var colors = new[]
        {
            new RgbColor(10, 20, 30),
            new RgbColor(40, 50, 60),
        };

        var data = LinkHubDataWriter.CreateColorData(colors);

        Assert.Equal(new byte[] { 10, 20, 30, 40, 50, 60 }, data);
    }

    [Fact]
    public void Watercolor_FillsAllLedsWithColor()
    {
        var effect = new WatercolorLightingEffect(new[] { 8, 8 }, TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[16];

        effect.Render(TimeSpan.FromSeconds(0.5), buffer);

        // the palette has no black entries, so every LED should be lit
        foreach (var color in buffer)
        {
            Assert.True(color.R + color.G + color.B > 0);
        }
    }

    [Fact]
    public void Watercolor_ShowsExactPaletteStopsAcrossADevice()
    {
        // a 16-LED device spans one full palette loop, so at t=0 the four stops
        // (cyan, magenta, yellow, white) land exactly on LEDs 0, 4, 8 and 12
        var effect = new WatercolorLightingEffect(new[] { 16 }, TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[16];

        effect.Render(TimeSpan.Zero, buffer);

        Assert.Equal(new byte[] { 0, 255, 255 }, new[] { buffer[0].R, buffer[0].G, buffer[0].B });
        Assert.Equal(new byte[] { 255, 0, 255 }, new[] { buffer[4].R, buffer[4].G, buffer[4].B });
        Assert.Equal(new byte[] { 255, 255, 0 }, new[] { buffer[8].R, buffer[8].G, buffer[8].B });
        Assert.Equal(new byte[] { 255, 255, 255 }, new[] { buffer[12].R, buffer[12].G, buffer[12].B });
    }

    [Fact]
    public void Watercolor_ScrollsOverTime()
    {
        var effect = new WatercolorLightingEffect(new[] { 8 }, TimeSpan.FromSeconds(4), 100);
        var start = new RgbColor[8];
        var later = new RgbColor[8];

        effect.Render(TimeSpan.Zero, start);
        effect.Render(TimeSpan.FromSeconds(1), later);

        var changed = false;
        for (var i = 0; i < start.Length; i++)
        {
            if (start[i].R != later[i].R || start[i].G != later[i].G || start[i].B != later[i].B)
            {
                changed = true;
                break;
            }
        }

        Assert.True(changed, "expected the effect to animate over time");
    }

    [Fact]
    public void Watercolor_AppliesBrightness()
    {
        var full = new WatercolorLightingEffect(new[] { 8 }, TimeSpan.FromSeconds(4), 100);
        var half = new WatercolorLightingEffect(new[] { 8 }, TimeSpan.FromSeconds(4), 50);
        var bufFull = new RgbColor[8];
        var bufHalf = new RgbColor[8];

        full.Render(TimeSpan.Zero, bufFull);
        half.Render(TimeSpan.Zero, bufHalf);

        for (var i = 0; i < bufFull.Length; i++)
        {
            Assert.InRange(bufHalf[i].R, (bufFull[i].R / 2) - 1, (bufFull[i].R / 2) + 1);
            Assert.InRange(bufHalf[i].G, (bufFull[i].G / 2) - 1, (bufFull[i].G / 2) + 1);
            Assert.InRange(bufHalf[i].B, (bufFull[i].B / 2) - 1, (bufFull[i].B / 2) + 1);
        }
    }

    [Fact]
    public void Watercolor_HandlesEmptyDeviceListWithoutThrowing()
    {
        var effect = new WatercolorLightingEffect(Array.Empty<int>(), TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[10];

        // remainder fill path: should not throw and should light every LED
        effect.Render(TimeSpan.FromSeconds(0.3), buffer);

        foreach (var color in buffer)
        {
            Assert.True(color.R + color.G + color.B > 0);
        }
    }
}
