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
            if (process.HasExited) return false;
            // Linux Process.StartTime derives a wall-clock boot time per process.
            // Use the kernel's start counter plus boot ID, stable across callers
            // and immune to NTP/clock adjustments and reboots.
            if (OperatingSystem.IsLinux() && session.BootId is null) return true;
            var current = CaptureGame(process);
            return current.StartTicks == session.StartTicks && current.BootId == session.BootId;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        // Access denied should block an update rather than assume the process is absent.
    }

    public static GameSession CaptureGame(Process process)
    {
        if (!OperatingSystem.IsLinux()) return new(process.Id, process.StartTime.ToUniversalTime().Ticks);
        var stat = File.ReadAllText($"/proc/{process.Id}/stat");
        var fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new(process.Id, long.Parse(fields[19], System.Globalization.CultureInfo.InvariantCulture),
            File.ReadAllText("/proc/sys/kernel/random/boot_id").Trim());
    }

    public void Dispose() => _lock.Dispose();
}
