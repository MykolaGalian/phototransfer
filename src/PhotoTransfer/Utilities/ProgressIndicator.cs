namespace PhotoTransfer.Utilities;

public class ProgressIndicator : IDisposable
{
    private readonly Timer _timer;
    private readonly string[] _frames = { "*", " " };
    private int _currentFrame = 0;
    private bool _disposed = false;

    public ProgressIndicator(string message = "Processing")
    {
        Console.Write($"{message}... ");
        _timer = new Timer(UpdateProgress, null, 0, 500);
    }

    private void UpdateProgress(object? state)
    {
        if (_disposed) return;
        
        try
        {
            Console.Write($"\r{_frames[_currentFrame]}");
            _currentFrame = (_currentFrame + 1) % _frames.Length;
            
            // Only set cursor position if console supports it
            if (!Console.IsOutputRedirected)
            {
                try
                {
                    Console.SetCursorPosition(Math.Max(0, Console.CursorLeft - 1), Console.CursorTop);
                }
                catch
                {
                    // Ignore cursor positioning errors
                }
            }
        }
        catch
        {
            // Ignore console output errors
        }
    }

    public void Stop()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            Console.Write("\r ");
            Console.SetCursorPosition(0, Console.CursorTop);
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}