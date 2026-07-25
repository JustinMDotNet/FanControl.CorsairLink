namespace CorsairLink.Devices.ICueLink;

public interface ILinkHubLightingEffect
{
    void Render(TimeSpan elapsed, RgbColor[] buffer);
}
