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
    public void PerFanFlow_EachFanShowsFullPaletteGradient()
    {
        var colors = new[]
        {
            new RgbColor(255, 255, 255),
            new RgbColor(0, 255, 255),
            new RgbColor(255, 0, 255),
            new RgbColor(255, 255, 0),
        };
        // 1 fan, 4 LEDs => the 4 palette stops land one-per-LED across the fan
        var effect = new PerFanGradientFlowLightingEffect(colors, new[] { 4 }, TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[4];

        effect.Render(TimeSpan.Zero, buffer);

        Assert.Equal(new byte[] { 255, 255, 255 }, new[] { buffer[0].R, buffer[0].G, buffer[0].B });
        Assert.Equal(new byte[] { 0, 255, 255 }, new[] { buffer[1].R, buffer[1].G, buffer[1].B });
        Assert.Equal(new byte[] { 255, 0, 255 }, new[] { buffer[2].R, buffer[2].G, buffer[2].B });
        Assert.Equal(new byte[] { 255, 255, 0 }, new[] { buffer[3].R, buffer[3].G, buffer[3].B });
    }

    [Fact]
    public void PerFanFlow_GradientRepeatsPerFan()
    {
        var colors = new[]
        {
            new RgbColor(255, 255, 255),
            new RgbColor(0, 255, 255),
            new RgbColor(255, 0, 255),
            new RgbColor(255, 255, 0),
        };
        // 2 fans of 4 LEDs => each fan shows the same full gradient independently
        var effect = new PerFanGradientFlowLightingEffect(colors, new[] { 4, 4 }, TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[8];

        effect.Render(TimeSpan.Zero, buffer);

        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(buffer[i].R, buffer[i + 4].R);
            Assert.Equal(buffer[i].G, buffer[i + 4].G);
            Assert.Equal(buffer[i].B, buffer[i + 4].B);
        }
    }

    [Fact]
    public void PerFanFlow_ScrollsOverTime()
    {
        var colors = new[]
        {
            new RgbColor(255, 255, 255),
            new RgbColor(0, 255, 255),
            new RgbColor(255, 0, 255),
            new RgbColor(255, 255, 0),
        };
        var effect = new PerFanGradientFlowLightingEffect(colors, new[] { 4 }, TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[4];

        // phase 0.25 => LED 0 (position 0 + 0.25) advances one stop to cyan
        effect.Render(TimeSpan.FromSeconds(1), buffer);

        Assert.Equal(new byte[] { 0, 255, 255 }, new[] { buffer[0].R, buffer[0].G, buffer[0].B });
    }

    [Fact]
    public void PerFanFlow_SingleColorAppliesBrightness()
    {
        var effect = new PerFanGradientFlowLightingEffect(new[] { new RgbColor(200, 100, 50) }, new[] { 3 }, TimeSpan.FromSeconds(4), 50);
        var buffer = new RgbColor[3];

        effect.Render(TimeSpan.FromSeconds(1.3), buffer);

        foreach (var color in buffer)
        {
            Assert.Equal(100, color.R);
            Assert.Equal(50, color.G);
            Assert.Equal(25, color.B);
        }
    }

    [Fact]
    public void PerFanFlow_TransitionsRotateThroughHue()
    {
        // cyan -> magenta should rotate through blue (hue 240), not fade through a
        // desaturated RGB midpoint like (128,128,255)
        var effect = new PerFanGradientFlowLightingEffect(
            new[] { new RgbColor(0, 255, 255), new RgbColor(255, 0, 255) },
            new[] { 4 },
            TimeSpan.FromSeconds(4),
            100);
        var buffer = new RgbColor[4];

        effect.Render(TimeSpan.Zero, buffer);

        // LED 1 sits halfway between cyan and magenta => pure blue via hue rotation
        Assert.Equal(new byte[] { 0, 0, 255 }, new[] { buffer[1].R, buffer[1].G, buffer[1].B });
    }

    [Fact]
    public void PerFanFlow_ReturnsExactPaletteStops()
    {
        // the transition rotates hue, but the palette colors themselves must be exact
        var effect = new PerFanGradientFlowLightingEffect(
            new[] { new RgbColor(255, 255, 255), new RgbColor(0, 255, 255), new RgbColor(255, 0, 255), new RgbColor(255, 255, 0) },
            new[] { 4 },
            TimeSpan.FromSeconds(4),
            100);
        var buffer = new RgbColor[4];

        effect.Render(TimeSpan.Zero, buffer);

        Assert.Equal(new byte[] { 255, 255, 255 }, new[] { buffer[0].R, buffer[0].G, buffer[0].B });
        Assert.Equal(new byte[] { 0, 255, 255 }, new[] { buffer[1].R, buffer[1].G, buffer[1].B });
        Assert.Equal(new byte[] { 255, 0, 255 }, new[] { buffer[2].R, buffer[2].G, buffer[2].B });
        Assert.Equal(new byte[] { 255, 255, 0 }, new[] { buffer[3].R, buffer[3].G, buffer[3].B });
    }

    [Fact]
    public void PerFanFlow_ThrowsWhenNoColors()
    {
        Assert.Throws<ArgumentException>(() =>
            new PerFanGradientFlowLightingEffect(Array.Empty<RgbColor>(), new[] { 1 }, TimeSpan.FromSeconds(4), 100));
    }
}
