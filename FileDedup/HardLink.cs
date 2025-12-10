using System.ComponentModel;
using System.Runtime.InteropServices;

namespace FileDedup;

public static partial class HardLink
{
    public static partial class Kernel32
    {
        [LibraryImport("kernel32", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool CreateHardLinkW(string lpFileName, string lpExistingFileName, nint lpSecurityAttributes);

        public static bool CreateHardLink(string fileName, string existingFileName)
            => CreateHardLinkW(fileName, existingFileName, 0);
    }
    public static partial class LibC
    {
        [LibraryImport("libc", EntryPoint = "link", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
        private static partial int Link(string oldpath, string newpath);

        public static bool CreateHardLink(string fileName, string existingFileName)
            => Link(existingFileName, fileName) == 0;
    }
    public static bool Create(string fileName, string existingFileName)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Kernel32.CreateHardLink(fileName, existingFileName);
        return LibC.CreateHardLink(fileName, existingFileName);
    }
}