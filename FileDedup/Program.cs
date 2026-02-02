using SimpleArgs;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace FileDedup;

class Program
{
    const string argHelp = "--help";
    const string argFilter = "--filter";
    const string argSkip = "--skip";
    const string argResume = "--resume";
    const string argMinSize = "--min-size";
    const string argDryRun = "--dry-run";
    const string argDupOnly = "--dup-only";
    const string argFollowSymbolLinks = "--follow-symlink";
    const RegexOptions regexOptions = RegexOptions.ExplicitCapture | RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant;
    static int Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        ArgParser argx = new(args, true,
            new(argHelp, 0, "-h", "-?"),
            new(argFilter, 1, "-f")
            {
                Info = "regex: Filter *file* paths by given regular expression"
            },
            new(argSkip, 1, "-s")
            {
                Info = "regex: Skip *directory* paths by given regular expression"
            },
            new(argResume, 1, "-r")
            {
                Info = "string: Start enumerating from the specific file"
            },
            new(argMinSize, 1, "-min", "-m")
            {
                Default = "1",
                Info = "int64: Minimal file size"
            },
            new(argDryRun, 1, "-dry", "-d")
            {
                Default = "true",
                Info = "boolean: Whether should not actually make hard links"
            },
            new(argDupOnly, 1, "-duponly", "-do")
            {
                Default = "true",
                Info = "boolean: Whether should show duplicated only"
            },
            new(argFollowSymbolLinks, 1)
            {
                Default = "true",
                Info = "boolean: Whether should follow symlink target"
            });
        if (argx.Results.ContainsKey(argHelp) || argx.UnknownArgs.Count == 0)
        {
            argx.WriteHelp(Console.Error);
            Console.Error.WriteLine("""


                    Remaining arguments: input files/folders
                    """);
            return 0;
        }
        if (argx.TryGetString(argFilter, out string? filterRegexStr)
            && !TryParseRegex(filterRegexStr, regexOptions, out filterRegex, out string? filterRegexMsg))
        {
            if (filterRegexMsg is null)
                Console.Error.WriteLine($"{argFilter}: Invalid argument value");
            else
                Console.Error.WriteLine($"{argFilter}: Invalid argument value ({filterRegexMsg})");
            return 1;
        }
        if (argx.TryGetString(argSkip, out string? skipRegexStr)
            && !TryParseRegex(skipRegexStr, regexOptions, out skipRegex, out string? skipRegexMsg))
        {
            if (skipRegexMsg is null)
                Console.Error.WriteLine($"{argSkip}: Invalid argument value");
            else
                Console.Error.WriteLine($"{argSkip}: Invalid argument value ({skipRegexMsg})");
            return 1;
        }
        if (!argx.TryGetString(argSkip, out string? restore))
            restore = null;
        if (!argx.TryGet(argMinSize, out long minSize))
        {
            Console.Error.WriteLine($"{argMinSize}: Invalid argument value");
            return 1;
        }
        if (!argx.TryGetBoolean(argDryRun, out bool dryRun))
        {
            Console.Error.WriteLine($"{argDryRun}: Invalid argument value");
            return 1;
        }
        if (!argx.TryGetBoolean(argDupOnly, out bool dupOnly))
        {
            Console.Error.WriteLine($"{argDupOnly}: Invalid argument value");
            return 1;
        }
        if (!argx.TryGetBoolean(argFollowSymbolLinks, out bool followSymLinks))
        {
            Console.Error.WriteLine($"{argFollowSymbolLinks}: Invalid argument value");
            return 1;
        }
        bool exiting = false;
        Console.CancelKeyPress += (s, e) =>
        {
            if (!exiting)
            {
                Console.Error.WriteLine("Canceling...");
                exiting = true;
                e.Cancel = true;
            }
        };
        HashSet<FileID> visited = [];
        Dictionary<long, string?> fastLookup = [];
        Dictionary<FileHash, string> fileMappings = [];
        foreach (FileInfo fi in EnumerateFiles(argx.UnknownArgs
            .SelectMany<string, FileSystemInfo>(it => File.Exists(it) ? [new FileInfo(it)]
                                                    : Directory.Exists(it) ? [new DirectoryInfo(it)]
                                                    : []), followSymLinks))
        {
            if (exiting)
                break;
            string currentFileName = fi.FullName;
            if (restore is not null)
            {
                if (restore != currentFileName)
                    continue;
                else
                    restore = null;
            }
            if (!AddVisited(visited, currentFileName)) // Skip files associated with visited hard links
                continue;
            long length = fi.Length;
            if (length < minSize)
                continue;
            if (!fastLookup.TryGetValue(length, out string? file))
            {
                fastLookup[length] = currentFileName;
                if (!dupOnly)
                    PrintInfo(currentFileName, length, "New fast-lookup entry.");
                continue;
            }
            else if (file is not null)
            {
                FileHash thatHash = new(file);
                fileMappings.Add(thatHash, file);
                fastLookup[length] = null;
                if (!dupOnly)
                    PrintInfo(file, in thatHash, "New entry.");
            }

            FileHash hash = new(currentFileName);
            if (!fileMappings.TryGetValue(hash, out string? existingFileName))
            {
                if (!dupOnly)
                    PrintInfo(currentFileName, in hash, "New entry.");
                fileMappings.Add(hash, currentFileName);
            }
            else
            {
                PrintInfo(currentFileName, existingFileName, in hash);
                if (!dryRun)
                {
                    fi.Delete();
                    string? error = null;
                    try
                    {
                        if (!HardLink.Create(currentFileName, existingFileName))
                            error = $"Error {Marshal.GetLastPInvokeError()}: {Marshal.GetLastPInvokeErrorMessage()}";
                    }
                    catch (Exception ex)
                    {
                        error = ex.ToString();
                    }
                    if (error is not null)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(error);
                        Console.ResetColor();
                        return 1;
                    }
                }
            }
        }
        return 0;
    }
    private static bool AddVisited(HashSet<FileID> visited, string file)
    {
        try
        {
            FileID id = FileID.GetFromFile(file);
            if (!(id.IsInvalid || visited.Add(id)))
                return false;
        }
        catch { }
        return true;
    }
    private static void PrintInfo(string file, long length, string message)
    {
        Console.Out.Write("* LEN: ");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Out.WriteLine(length);
        Console.ResetColor();
        Console.Out.WriteLine(file);
        Console.Out.WriteLine(message);
    }
    private static void PrintInfo(string file, in FileHash hash, string message)
    {
        Console.Out.Write("* HASH: ");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Out.WriteLine(Convert.ToHexString(hash.AsBytes()));
        Console.ResetColor();
        Console.Out.WriteLine(file);
        Console.Out.WriteLine(message);
    }
    private static void PrintInfo(string file, string target, in FileHash hash)
    {
        Console.Out.Write("* HASH: ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Out.WriteLine(Convert.ToHexString(hash.AsBytes()));
        Console.ResetColor();
        Console.Out.WriteLine(file);
        Console.Out.Write(" => ");
        Console.Out.WriteLine(target);
    }

    static bool TryParseRegex(
        string regexString,
        RegexOptions options,
        [NotNullWhen(true)] out Regex? regex,
        [NotNullWhen(false)] out string? message)
    {
        try
        {
            regex = new(regexString, options);
            message = null;
            return true;
        }
        catch (Exception ex)
        {
            regex = null;
            message = ex.Message;
            return false;
        }
    }

    static Regex? filterRegex = null;
    static Regex? skipRegex = null;
    static readonly EnumerationOptions options = new()
    {
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        IgnoreInaccessible = true,
        MatchType = MatchType.Simple
    };
    static readonly HashSet<string> visited = [];
    static IEnumerable<FileInfo> EnumerateFiles(IEnumerable<FileSystemInfo> entries, bool followSymLinks)
    {
        foreach (FileSystemInfo entry in entries)
        {
            if (!followSymLinks && entry.LinkTarget is not null)
                continue;
            switch (entry)
            {
                case FileInfo fi when filterRegex?.IsMatch(fi.FullName) is not false:
                    try
                    {
                        while (fi.LinkTarget is string link)
                        {
                            if (!visited.Add(fi.FullName))
                                break;
                            fi = new(Path.Combine(fi.DirectoryName ?? "", link));
                        }
                    }
                    catch
                    {
                        // Skip broken symbolic link
                        break;
                    }
                    if (visited.Add(fi.FullName))
                        yield return fi;
                    break;
                case DirectoryInfo di when skipRegex?.IsMatch(di.FullName) is not true:
                    try
                    {
                        while (di.LinkTarget is string link)
                        {
                            if (!visited.Add(di.FullName))
                                break;
                            di = new(Path.Combine(di.Parent?.FullName ?? "", link));
                        }
                    }
                    catch
                    {
                        // Skip broken symbolic link
                        break;
                    }
                    if (visited.Add(di.FullName))
                    {
                        foreach (FileInfo fi in EnumerateFiles(di.EnumerateFileSystemInfos("*", options), followSymLinks))
                            yield return fi;
                    }
                    break;
            }
        }
    }
}