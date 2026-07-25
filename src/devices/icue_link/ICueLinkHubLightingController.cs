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
    private const double MaxBackoffMilliseconds = 1000;

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
        if (_thread is null)
        {
            return; // never started, or already fully stopped
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return; // already stopped
        }

        // The render loop can be blocked on the cross-process device guard, so
        // give it room to exit. Only clear the thread and dispose the token
        // source once the thread has actually terminated - disposing while the
        // thread still references the token risks an ObjectDisposedException on
        // a background thread, which would crash the host process. If the join
        // times out, keep both so a later Stop() can retry cleanly rather than
        // disposing a token the still-running thread is using.
        if (_thread.Join(TimeSpan.FromSeconds(5)))
        {
            _thread = null;
            _cts.Dispose();
        }
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

                if (consecutiveErrors < MaxLoggedConsecutiveErrors)
                {
                    try
                    {
                        _onError(ex);
                    }
                    catch
                    {
                        // never let a logging failure terminate the render thread
                    }
                }

                consecutiveErrors++;
            }

            // Back off while frames are failing so repeated device I/O does not
            // keep contending for the shared device guard and delay fan/pump/PSU
            // updates; the normal frame rate resumes once a frame succeeds.
            var interval = consecutiveErrors > 0 ? GetBackoffInterval(consecutiveErrors) : _frameInterval;

            try
            {
                token.WaitHandle.WaitOne(interval);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private TimeSpan GetBackoffInterval(int consecutiveErrors)
    {
        // exponential backoff from the frame interval, capped at 1 second
        var multiplier = 1 << Math.Min(consecutiveErrors, 6);
        var backoffMs = Math.Min(_frameInterval.TotalMilliseconds * multiplier, MaxBackoffMilliseconds);
        return TimeSpan.FromMilliseconds(backoffMs);
    }
}
