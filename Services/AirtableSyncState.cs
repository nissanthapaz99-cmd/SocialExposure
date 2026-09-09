namespace SocialExposure.Services;

public sealed class AirtableSyncState
{
    private readonly object _gate = new();
    private bool _isRunning;
    private AirtableSyncResult? _lastResult;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _isRunning;
        }
    }

    public AirtableSyncResult? LastResult
    {
        get
        {
            lock (_gate)
                return _lastResult;
        }
    }

    public bool TryStart()
    {
        lock (_gate)
        {
            if (_isRunning)
                return false;

            _isRunning = true;
            return true;
        }
    }

    public void Complete(AirtableSyncResult result)
    {
        lock (_gate)
        {
            _lastResult = result;
            _isRunning = false;
        }
    }
}
