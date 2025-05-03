using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FileDedup;

static partial class HardLink
{
    public static partial class Kernel32
    {
        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CreateHardLinkW(string lpFileName, string lpExistingFileName, nint lpSecurityAttributes);

        public static void CreateHardLink(string fileName, string existingFileName)
        {
            if (!CreateHardLinkW(fileName, existingFileName, 0))
                throw new Win32Exception(Marshal.GetLastPInvokeError(), Marshal.GetLastPInvokeErrorMessage());
        }
    }
    public static partial class LibC
    {
        [LibraryImport("libc.so", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int Link(string oldpath, string newpath);

        public static void CreateHardLink(string fileName, string existingFileName)
        {
            if (Link(existingFileName, fileName) != 0)
                throw new ExternalException(Marshal.GetLastPInvokeErrorMessage(), Marshal.GetLastPInvokeError());
        }
    }
    public static void Create(string fileName, string existingFileName)
    {
        switch (Environment.OSVersion.Platform)
        {
            case PlatformID.Win32NT:
                Kernel32.CreateHardLink(fileName, existingFileName);
                break;
            case PlatformID.Unix:
                LibC.CreateHardLink(fileName, existingFileName);
                break;
            default:
                throw new PlatformNotSupportedException();
        }
    }
}
