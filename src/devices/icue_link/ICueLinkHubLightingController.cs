using System.Diagnostics;

namespace CorsairLink.Devices.ICueLink;

/// <summary>
/// Drives an <see cref="ILinkHubLightingEffect"/> on a dedicated background thread,
/// rendering a color frame at a fixed interval. Rendering (and its device I/O) is
/// delegated so this type stays free of protocol details.
/// </summary>
internal sealed class ICueLinkHubLightingController
{
    private const int MaxLoggedConsecutiveErrors = 3;

    private readonly ILinkHubLightingEffect _effect;
    private readonly int _ledCount;
    private readonly TimeSpan _frameInterval;
    private readonly Action<RgbColor[]> _renderFrame;
    private readonly Action<Exception> _onError;
    private readonly CancellationTokenSource _cts = new();

    private Thread? _thread;

    public ICueLinkHubLightingController(
        ILinkHubLightingEffect effect,
        int ledCount,
        TimeSpan frameInterval,
        Action<RgbColor[]> renderFrame,
        Action<Exception> onError)
    {
        _effect = effect;
        _ledCount = ledCount;
        _frameInterval = frameInterval;
        _renderFrame = renderFrame;
        _onError = onError;
    }

    public void Start()
    {
        if (_thread is not null)
        {
            return;
        }

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "ICueLinkLighting",
        };
        _thread.Start();
    }

    public void Stop()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already stopped
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _cts.Dispose();
    }

    private void Run()
    {
        var stopwatch = Stopwatch.StartNew();
        var token = _cts.Token;
        var consecutiveErrors = 0;
        var buffer = new RgbColor[_ledCount];

        while (!token.IsCancellationRequested)
        {
            try
            {
                _effect.Render(stopwatch.Elapsed, buffer);
                _renderFrame(buffer);
                consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                if (token.IsCancellationRequested)
                {
                    break;
                }

                if (consecutiveErrors++ < MaxLoggedConsecutiveErrors)
                {
                    _onError(ex);
                }
            }

            token.WaitHandle.WaitOne(_frameInterval);
        }
    }
}
