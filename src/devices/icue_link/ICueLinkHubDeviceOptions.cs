namespace CorsairLink.Devices.ICueLink;

public class ICueLinkHubDeviceOptions
{
    public static readonly int MinimumPumpPowerDefault = 50;

    public int? MinimumPumpPower { get; set; }

    public bool LightingEnabled { get; set; }

    public IReadOnlyList<RgbColor>? LightingColors { get; set; }

    public int? LightingBrightness { get; set; }

    public int? LightingCycleSeconds { get; set; }
}
