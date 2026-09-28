using System.Diagnostics;

namespace LuckerParty.Launcher;

internal sealed class SessionGuard : IDisposable
{
    private readonly FileStream _lock;

    public SessionGuard(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        try
        {
            // Never delete the lock file: deleting a locked inode permits two owners on Unix.
            _lock = new FileStream(Path.Combine(dataDirectory, "session.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException error)
        {
            throw new InvalidOperationException("Lucker Party is already running. Close the game before updating.", error);
        }
    }

    public static bool IsGameRunning(GameSession? session)
    {
        if (session is null) return false;
        try
        {
            using var process = Process.GetProcessById(session.Pid);
            return !process.HasExited && process.StartTime.ToUniversalTime().Ticks == session.StartTicks;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        // Access denied should block an update rather than assume the process is absent.
    }

    public void Dispose() => _lock.Dispose();
}
