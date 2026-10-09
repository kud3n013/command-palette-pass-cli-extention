using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProtonPassCliExtension.Services;

/// <summary>
/// Win32 clipboard. Plain Win32 (rather than the WinRT clipboard) works from the MTA threads the COM
/// server runs on, and lets us set the formats that keep a copy out of Win+V history and cloud sync.
/// </summary>
internal sealed class WindowsClipboard : IClipboard
{
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;

    public long SequenceNumber => GetClipboardSequenceNumber();

    public void SetSensitiveText(string text)
    {
        var textHandle = AllocUnicode(text);
        var excludeMonitor = AllocDword(1);
        var noHistory = AllocDword(0);
        var noCloud = AllocDword(0);

        var formatMonitor = RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
        var formatHistory = RegisterClipboardFormat("CanIncludeInClipboardHistory");
        var formatCloud = RegisterClipboardFormat("CanUploadToCloudClipboard");

        if (!OpenWithRetry())
        {
            var error = Marshal.GetLastWin32Error();
            Free(textHandle, excludeMonitor, noHistory, noCloud);
            throw new Win32Exception(error, "Could not open the clipboard (another app may be holding it).");
        }

        try
        {
            EmptyClipboard();

            // On success the clipboard owns the memory; on failure we must free it and report the failure,
            // otherwise the caller would claim a copy that never happened.
            if (SetClipboardData(CfUnicodeText, textHandle) == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Free(textHandle);
                throw new Win32Exception(error, "Could not place text on the clipboard.");
            }

            if (formatMonitor != 0 && SetClipboardData(formatMonitor, excludeMonitor) == IntPtr.Zero)
            {
                Free(excludeMonitor);
            }

            if (formatHistory != 0 && SetClipboardData(formatHistory, noHistory) == IntPtr.Zero)
            {
                Free(noHistory);
            }

            if (formatCloud != 0 && SetClipboardData(formatCloud, noCloud) == IntPtr.Zero)
            {
                Free(noCloud);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public string? GetText()
    {
        if (!OpenWithRetry())
        {
            return null;
        }

        try
        {
            var handle = GetClipboardData(CfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return null;
            }

            var ptr = GlobalLock(handle);
            if (ptr == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    public void Clear()
    {
        if (!OpenWithRetry())
        {
            return;
        }

        try
        {
            EmptyClipboard();
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool OpenWithRetry()
    {
        // Another process may hold the clipboard briefly.
        for (var i = 0; i < 10; i++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return false;
    }

    private static IntPtr AllocUnicode(string text)
    {
        var bytes = (text.Length + 1) * 2;
        var handle = GlobalAlloc(GmemMoveable, (UIntPtr)bytes);
        if (handle == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }

        var ptr = GlobalLock(handle);
        Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
        Marshal.WriteInt16(ptr, text.Length * 2, 0);
        GlobalUnlock(handle);
        return handle;
    }

    private static IntPtr AllocDword(int value)
    {
        var handle = GlobalAlloc(GmemMoveable, (UIntPtr)4);
        if (handle == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }

        var ptr = GlobalLock(handle);
        Marshal.WriteInt32(ptr, value);
        GlobalUnlock(handle);
        return handle;
    }

    private static void Free(params IntPtr[] handles)
    {
        foreach (var h in handles)
        {
            if (h != IntPtr.Zero)
            {
                GlobalFree(h);
            }
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string lpszFormat);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(IntPtr hMem);
}
