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
    public void CreateColorData_RepeatsColorPerLed()
    {
        var data = LinkHubDataWriter.CreateColorData(3, new RgbColor(10, 20, 30));

        Assert.Equal(new byte[] { 10, 20, 30, 10, 20, 30, 10, 20, 30 }, data);
    }

    [Fact]
    public void ColorCycleLightingEffect_SingleColor_AppliesBrightness()
    {
        var effect = new ColorCycleLightingEffect(new[] { new RgbColor(200, 100, 50) }, TimeSpan.FromSeconds(4), 50);

        var color = effect.GetColor(TimeSpan.FromSeconds(1.3));

        Assert.Equal(100, color.R);
        Assert.Equal(50, color.G);
        Assert.Equal(25, color.B);
    }

    [Fact]
    public void ColorCycleLightingEffect_ReturnsPaletteColorsAtStops()
    {
        var effect = new ColorCycleLightingEffect(
            new[] { new RgbColor(255, 0, 0), new RgbColor(0, 0, 255) },
            TimeSpan.FromSeconds(4),
            100);

        var atStart = effect.GetColor(TimeSpan.Zero);
        var atHalf = effect.GetColor(TimeSpan.FromSeconds(2));

        Assert.Equal(new byte[] { 255, 0, 0 }, new[] { atStart.R, atStart.G, atStart.B });
        Assert.Equal(new byte[] { 0, 0, 255 }, new[] { atHalf.R, atHalf.G, atHalf.B });
    }

    [Fact]
    public void ColorCycleLightingEffect_InterpolatesBetweenStops()
    {
        var effect = new ColorCycleLightingEffect(
            new[] { new RgbColor(255, 0, 0), new RgbColor(0, 0, 255) },
            TimeSpan.FromSeconds(4),
            100);

        var midpoint = effect.GetColor(TimeSpan.FromSeconds(1));

        Assert.Equal(128, midpoint.R);
        Assert.Equal(0, midpoint.G);
        Assert.Equal(128, midpoint.B);
    }

    [Fact]
    public void ColorCycleLightingEffect_ThrowsWhenNoColors()
    {
        Assert.Throws<ArgumentException>(() =>
            new ColorCycleLightingEffect(Array.Empty<RgbColor>(), TimeSpan.FromSeconds(4), 100));
    }
}
