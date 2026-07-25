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
    public void GradientFlow_SingleColor_FillsAllLedsWithBrightness()
    {
        var effect = new GradientFlowLightingEffect(new[] { new RgbColor(200, 100, 50) }, TimeSpan.FromSeconds(4), 50);
        var buffer = new RgbColor[4];

        effect.Render(TimeSpan.FromSeconds(1.3), buffer);

        foreach (var color in buffer)
        {
            Assert.Equal(100, color.R);
            Assert.Equal(50, color.G);
            Assert.Equal(25, color.B);
        }
    }

    [Fact]
    public void GradientFlow_ShowsAllPaletteColorsSimultaneously()
    {
        // 4 LEDs, 4 colors, phase 0 => each LED sits exactly on a palette stop
        var effect = new GradientFlowLightingEffect(
            new[]
            {
                new RgbColor(255, 255, 255),
                new RgbColor(0, 255, 255),
                new RgbColor(255, 0, 255),
                new RgbColor(255, 255, 0),
            },
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
    public void GradientFlow_ScrollsOverTime()
    {
        var effect = new GradientFlowLightingEffect(
            new[]
            {
                new RgbColor(255, 255, 255),
                new RgbColor(0, 255, 255),
                new RgbColor(255, 0, 255),
                new RgbColor(255, 255, 0),
            },
            TimeSpan.FromSeconds(4),
            100);
        var buffer = new RgbColor[4];

        // after one full cycle LED 0 returns to the first stop; after a quarter it advances one stop
        effect.Render(TimeSpan.FromSeconds(1), buffer); // phase = 0.25

        Assert.Equal(new byte[] { 0, 255, 255 }, new[] { buffer[0].R, buffer[0].G, buffer[0].B });
    }

    [Fact]
    public void GradientFlow_InterpolatesBetweenStops()
    {
        var effect = new GradientFlowLightingEffect(
            new[] { new RgbColor(255, 0, 0), new RgbColor(0, 0, 255) },
            TimeSpan.FromSeconds(4),
            100);
        var buffer = new RgbColor[4];

        effect.Render(TimeSpan.Zero, buffer);

        // 2 colors across 4 LEDs: LED 1 sits at position 0.25 => scaled 0.5 => half red->blue
        Assert.Equal(128, buffer[1].R);
        Assert.Equal(0, buffer[1].G);
        Assert.Equal(128, buffer[1].B);
    }

    [Fact]
    public void GradientFlow_ThrowsWhenNoColors()
    {
        Assert.Throws<ArgumentException>(() =>
            new GradientFlowLightingEffect(Array.Empty<RgbColor>(), TimeSpan.FromSeconds(4), 100));
    }
}
