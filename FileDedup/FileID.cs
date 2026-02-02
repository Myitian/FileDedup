using Microsoft.Win32.SafeHandles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace FileDedup;

readonly partial struct FileID(ulong volumeIndex, UInt128 fileIndex) : IEquatable<FileID>
{
    public static FileID Invalid => new(ulong.MaxValue, UInt128.MaxValue);
    public ulong VolumeIndex { get; } = volumeIndex;
    public UInt128 FileIndex { get; } = fileIndex;
    public readonly bool IsInvalid => VolumeIndex == ulong.MaxValue && FileIndex == UInt128.MaxValue;
    public override readonly string ToString() => $"<{VolumeIndex:X8}@{FileIndex:X16}>";

    public static FileID GetFromFile(string file)
    {
        if (!OperatingSystem.IsWindows())
            return Stat.GetFileID(file); // call SystemNative_Stat (in native part in .NET runtime)
        if (OperatingSystem.IsWindowsVersionAtLeast(6, 2)) // Windows 8+
            return FILE_ID_INFO.GetFileID(file); // ReFS use 128-bit ID.
        return BY_HANDLE_FILE_INFORMATION.GetFileID(file); // Other Windows
    }

    public readonly bool Equals(FileID other)
        => VolumeIndex == other.VolumeIndex && FileIndex == other.FileIndex;
    public override readonly bool Equals(object? obj)
        => obj is FileID other && Equals(other);
    public override readonly int GetHashCode()
        => HashCode.Combine(VolumeIndex, FileIndex);
    public static bool operator ==(FileID left, FileID right)
        => left.Equals(right);
    public static bool operator !=(FileID left, FileID right)
        => !left.Equals(right);

    [SupportedOSPlatform("windows")]
    [StructLayout(LayoutKind.Sequential)]
    private readonly partial struct BY_HANDLE_FILE_INFORMATION
    {
        readonly uint FileAttributes;       // unused in this project
        readonly uint CreationTimeLow;      // unused in this project
        readonly uint CreationTimeHigh;     // unused in this project
        readonly uint LastAccessTimeLow;    // unused in this project
        readonly uint LastAccessTimeHigh;   // unused in this project
        readonly uint LastWriteTimeLow;     // unused in this project
        readonly uint LastWriteTimeHigh;    // unused in this project
        readonly uint VolumeSerialNumber;
        readonly uint FileSizeHigh;         // unused in this project
        readonly uint FileSizeLow;          // unused in this project
        readonly uint NumberOfLinks;        // unused in this project
        readonly uint FileIndexHigh;
        readonly uint FileIndexLow;

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetFileInformationByHandle(
            SafeFileHandle hFile,
            out BY_HANDLE_FILE_INFORMATION lpFileInformation);

        internal static FileID GetFileID(string file)
        {
            if (File.Exists(file))
                using (FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (GetFileInformationByHandle(fs.SafeFileHandle, out BY_HANDLE_FILE_INFORMATION info))
                        return new(info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
            return Invalid;
        }
    }

    [SupportedOSPlatform("windows6.2")] // windows6.2 = Windows 8
    [StructLayout(LayoutKind.Sequential)]
    private readonly partial struct FILE_ID_INFO
    {
        readonly ulong VolumeSerialNumber;
        readonly ulong FileIdLow;
        readonly ulong FileIdHigh;

        [LibraryImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetFileInformationByHandleEx(
            SafeFileHandle hFile,
            int FileInformationClass,
            out FILE_ID_INFO lpFileInformation,
            int dwBufferSize);

        internal static FileID GetFileID(string file)
        {
            const int FileIdInfo = 18;
            if (File.Exists(file))
                using (FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    if (GetFileInformationByHandleEx(fs.SafeFileHandle, FileIdInfo, out FILE_ID_INFO info, Unsafe.SizeOf<FILE_ID_INFO>()))
                        return new(info.VolumeSerialNumber, new(info.FileIdHigh, info.FileIdLow));
            return Invalid;
        }
    }

    // compatible with .NET 7+, see
    // https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Unix/System.Native/Interop.Stat.cs
    // https://github.com/dotnet/runtime/blob/main/src/native/libs/System.Native/pal_io.h
    [UnsupportedOSPlatform("windows")]
    [StructLayout(LayoutKind.Sequential)]
    internal readonly partial struct Stat
    {
        readonly int Flags;             // unused in this project
        readonly int Mode;              // unused in this project
        readonly uint Uid;              // unused in this project
        readonly uint Gid;              // unused in this project
        readonly long Size;             // unused in this project
        readonly long ATime;            // unused in this project
        readonly long ATimeNsec;        // unused in this project
        readonly long MTime;            // unused in this project
        readonly long MTimeNsec;        // unused in this project
        readonly long CTime;            // unused in this project
        readonly long CTimeNsec;        // unused in this project
        readonly long BirthTime;        // unused in this project
        readonly long BirthTimeNsec;    // unused in this project
        readonly long Dev;
        readonly long RDev;             // unused in this project
        readonly long Ino;
        readonly uint UserFlags;        // unused in this project

        [LibraryImport("libSystem.Native", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
        private static partial int SystemNative_Stat(string path, out Stat output);

        internal static FileID GetFileID(string file)
        {
            if (SystemNative_Stat(file, out Stat stat) == 0)
                return new((ulong)stat.Dev, (ulong)stat.Ino);
            return Invalid;
        }
    }
}