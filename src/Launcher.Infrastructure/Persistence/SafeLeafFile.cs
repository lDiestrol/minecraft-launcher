using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Launcher.Infrastructure.Persistence;

/// <summary>Opens the leaf itself, never its reparse target, and verifies the opened handle.</summary>
internal static class SafeLeafFile
{
    public static FileStream OpenRead(string path) => Open(path, 0x80000000, 1, 3, FileAccess.Read);

    // FILE_APPEND_DATA, not GENERIC_WRITE: concurrent log handles cannot overwrite existing bytes.
    public static FileStream OpenAppend(string path) => Open(path, 0x00000084, 3, 4, FileAccess.Write);

    private static FileStream Open(string path, uint access, uint share, uint disposition, FileAccess fileAccess)
    {
        StoragePathPolicy.EnsureNoReparsePoints(path);
        SafeFileHandle handle = CreateFile(path, access, share, IntPtr.Zero, disposition, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is 2 or 3)
            {
                throw new FileNotFoundException("File was not found.", path);
            }

            throw new IOException("Could not open local file safely.", new Win32Exception(error));
        }

        try
        {
            if (!GetFileInformationByHandleEx(handle, 9, out AttributeTag information, Marshal.SizeOf<AttributeTag>()))
            {
                throw new IOException("Could not verify local file attributes.", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            if ((information.Attributes & (uint)FileAttributes.ReparsePoint) != 0)
            {
                throw new StorageRootException(StorageRootError.UnsafePath, "Файл настроек или логов является ссылкой файловой системы.");
            }

            return new FileStream(handle, fileAccess);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTag { public uint Attributes, Tag; }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int informationClass,
        out AttributeTag information, int size);
}
