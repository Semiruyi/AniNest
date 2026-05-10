using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AniNest.Infrastructure.Thumbnails;

internal sealed class WindowsThumbnailProcessController : IThumbnailProcessController
{
    private const uint ProcessSuspendResume = 0x0800;
    private const uint ProcessQueryLimitedInformation = 0x1000;

    public void Suspend(int processId)
        => Invoke(processId, NtSuspendProcess, "suspend");

    public void Resume(int processId)
        => Invoke(processId, NtResumeProcess, "resume");

    private static void Invoke(int processId, Func<IntPtr, int> operation, string verb)
    {
        IntPtr handle = OpenProcess(ProcessSuspendResume | ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"OpenProcess failed for thumbnail {verb}: pid={processId}");

        try
        {
            int status = operation(handle);
            if (status < 0)
                throw new InvalidOperationException($"Nt{char.ToUpperInvariant(verb[0])}{verb[1..]}Process failed: pid={processId}, status=0x{status:X8}");
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("ntdll.dll")]
    private static extern int NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    private static extern int NtResumeProcess(IntPtr processHandle);
}
