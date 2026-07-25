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
    public void PerFanCycle_EachFanShowsOneSolidColor()
    {
        var colors = new[]
        {
            new RgbColor(255, 255, 255),
            new RgbColor(0, 255, 255),
            new RgbColor(255, 0, 255),
            new RgbColor(255, 255, 0),
        };
        // 2 fans, 2 LEDs each; at t=0 fan0 phase 0 (white), fan1 phase 0.5 (magenta)
        var effect = new PerFanColorCycleLightingEffect(colors, new[] { 2, 2 }, TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[4];

        effect.Render(TimeSpan.Zero, buffer);

        Assert.Equal(new byte[] { 255, 255, 255 }, new[] { buffer[0].R, buffer[0].G, buffer[0].B });
        Assert.Equal(new byte[] { 255, 255, 255 }, new[] { buffer[1].R, buffer[1].G, buffer[1].B });
        Assert.Equal(new byte[] { 255, 0, 255 }, new[] { buffer[2].R, buffer[2].G, buffer[2].B });
        Assert.Equal(new byte[] { 255, 0, 255 }, new[] { buffer[3].R, buffer[3].G, buffer[3].B });
    }

    [Fact]
    public void PerFanCycle_StaggersDevicesAcrossPalette()
    {
        var colors = new[]
        {
            new RgbColor(255, 255, 255),
            new RgbColor(0, 255, 255),
            new RgbColor(255, 0, 255),
            new RgbColor(255, 255, 0),
        };
        // 4 fans, 1 LED each => offsets 0, .25, .5, .75 land on the 4 stops
        var effect = new PerFanColorCycleLightingEffect(colors, new[] { 1, 1, 1, 1 }, TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[4];

        effect.Render(TimeSpan.Zero, buffer);

        Assert.Equal(new byte[] { 255, 255, 255 }, new[] { buffer[0].R, buffer[0].G, buffer[0].B });
        Assert.Equal(new byte[] { 0, 255, 255 }, new[] { buffer[1].R, buffer[1].G, buffer[1].B });
        Assert.Equal(new byte[] { 255, 0, 255 }, new[] { buffer[2].R, buffer[2].G, buffer[2].B });
        Assert.Equal(new byte[] { 255, 255, 0 }, new[] { buffer[3].R, buffer[3].G, buffer[3].B });
    }

    [Fact]
    public void PerFanCycle_DeviceCyclesOverTime()
    {
        var colors = new[]
        {
            new RgbColor(255, 255, 255),
            new RgbColor(0, 255, 255),
            new RgbColor(255, 0, 255),
            new RgbColor(255, 255, 0),
        };
        var effect = new PerFanColorCycleLightingEffect(colors, new[] { 1 }, TimeSpan.FromSeconds(4), 100);
        var buffer = new RgbColor[1];

        effect.Render(TimeSpan.FromSeconds(1), buffer); // phase 0.25 => second stop (cyan)

        Assert.Equal(new byte[] { 0, 255, 255 }, new[] { buffer[0].R, buffer[0].G, buffer[0].B });
    }

    [Fact]
    public void PerFanCycle_SingleColorAppliesBrightness()
    {
        var effect = new PerFanColorCycleLightingEffect(new[] { new RgbColor(200, 100, 50) }, new[] { 3 }, TimeSpan.FromSeconds(4), 50);
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
    public void PerFanCycle_ThrowsWhenNoColors()
    {
        Assert.Throws<ArgumentException>(() =>
            new PerFanColorCycleLightingEffect(Array.Empty<RgbColor>(), new[] { 1 }, TimeSpan.FromSeconds(4), 100));
    }
}
