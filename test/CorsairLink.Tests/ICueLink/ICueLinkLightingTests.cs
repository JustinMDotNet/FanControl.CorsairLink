using CorsairLink;
using CorsairLink.Devices.ICueLink;

namespace CorsairLink.Tests.ICueLink;

public class ICueLinkLightingTests
{
    private static byte[] BuildLedCountPacket(params (bool Connected, int Leds)[] channels)
    {
        // packet[6] = channel count; packet[7:] = data; channel ch record starts at data[ch*4]
        var data = new List<byte>(new byte[4]); // data[0..3] unused
        foreach (var (connected, leds) in channels)
        {
            data.Add((byte)(connected ? 0x02 : 0x00));
            data.Add(0x00);
            data.Add((byte)(leds & 0xff));
            data.Add((byte)((leds >> 8) & 0xff));
        }

        var packet = new List<byte>(new byte[6]);
        packet.Add((byte)channels.Length);
        packet.AddRange(data);
        return packet.ToArray();
    }

    [Fact]
    public void GetTotalLedCount_SumsConnectedChannels()
    {
        var packet = BuildLedCountPacket((true, 34), (true, 18));

        var total = LinkHubDataReader.GetTotalLedCount(packet);

        Assert.Equal(52, total);
    }

    [Fact]
    public void GetTotalLedCount_IgnoresDisconnectedChannels()
    {
        var packet = BuildLedCountPacket((true, 34), (false, 999), (true, 8));

        var total = LinkHubDataReader.GetTotalLedCount(packet);

        Assert.Equal(42, total);
    }

    [Fact]
    public void GetTotalLedCount_CapsPerChannelAtFifty()
    {
        var packet = BuildLedCountPacket((true, 60));

        var total = LinkHubDataReader.GetTotalLedCount(packet);

        Assert.Equal(50, total);
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
