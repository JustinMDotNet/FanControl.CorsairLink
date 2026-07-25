namespace CorsairLink.Devices.ICueLink;

public interface ILinkHubLightingEffect
{
    RgbColor GetColor(TimeSpan elapsed);
}
