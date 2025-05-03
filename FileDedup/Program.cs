using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace FileDedup;

class Program
{
    static void Main(string[] args)
    {
        ArgDefinition[] argDefinitions = [
            new("-Help", 0, "-h", "-?"),
            new("-Directory", 1, "-dir", "-d") { Info = "string: Working folder" },
            new("-Filter", 1, "-f") { Info = "string: Filter file paths by given regular expression" },
            new("-Skip", 1, "-s") { Info = "string: Skip directory paths by given regular expression" },
            new("-DryRun", 1, "-dry", "-dr") { Info = "boolean: Whether should not actually make hard links" },
            new("-ShowDuplicatedOnly", 1, "-DupOnly", "-du") { Info = "boolean: Whether should show duplicated only" }
        ];
        ArgParser parser = new(argDefinitions);
        Dictionary<string, Range> parseResult = parser.Parse(args);
        if (parseResult.ContainsKey("-Help") || ArgParser.GetString(parseResult, "-Directory", args) is not string dirStr)
        {
            int nLen = "Name".Length, cLen = "Parameter Count".Length, aLen = "Alias".Length, iLen = "Info".Length;
            foreach (ArgDefinition aDef in argDefinitions)
            {
                int nnLen = aDef.Name.Length;
                int ncLen = aDef.ParamCount.ToString().Length;
                int naLen = aDef.Aliases.Sum(a => a.Length) + Math.Max(aDef.Aliases.Length - 1, 0) * ", ".Length;
                int niLen = aDef.Info?.Length ?? 0;
                if (nnLen > nLen)
                    nLen = nnLen;
                if (ncLen > cLen)
                    cLen = ncLen;
                if (naLen > aLen)
                    aLen = naLen;
                if (niLen > iLen)
                    iLen = niLen;
            }
            StringBuilder sb = new("Arguments:");
            sb.AppendLine()
                .AppendPadLeft("Name", nLen).Append(" | ")
                .AppendPadLeft("Parameter Count", cLen).Append(" | ")
                .AppendPadLeft("Alias", aLen).Append(" | ")
                .AppendPadLeft("Info", iLen);
            foreach (ArgDefinition aDef in argDefinitions)
            {
                sb.AppendLine()
                    .AppendPadLeft(aDef.Name, nLen).Append(' ', 3)
                    .AppendPadRight(aDef.ParamCount, cLen).Append(' ', 3)
                    .AppendPadLeft(string.Join(", ", aDef.Aliases), aLen).Append(' ', 3)
                    .AppendPadLeft(aDef.Info ?? "", iLen);
            }
            Console.Error.WriteLine(sb.ToString());
            return;
        }

        bool dryRun = ArgParser.GetBoolean(parseResult, "-DryRun", args) ?? false;
        bool dupOnly = ArgParser.GetBoolean(parseResult, "-ShowDuplicatedOnly", args) ?? false;
        Regex? filterRegex = ArgParser.GetString(parseResult, "-Filter", args) is string regexStrF ?
            new(regexStrF, RegexOptions.ExplicitCapture) : null;
        Regex? skipRegex = ArgParser.GetString(parseResult, "-Skip", args) is string regexStrS ?
            new(regexStrS, RegexOptions.ExplicitCapture) : null;

        Dictionary<long, string?> fastLookup = [];
        Dictionary<FileHash, string> fileMappings = [];
        foreach (FileInfo fi in EnumerateFiles(new(dirStr), filterRegex, skipRegex))
        {
            try
            {
                string currentFileName = fi.FullName;
                long length = fi.Length;
                if (!fastLookup.TryGetValue(length, out string? file))
                {
                    fastLookup[length] = currentFileName;
                    continue;
                }
                else if (file is not null)
                {
                    FileHash thatHash = new(new(file));
                    fileMappings.Add(thatHash, file);
                    fastLookup[length] = null;
                }

                FileHash hash = new(fi);
                if (!fileMappings.TryGetValue(hash, out string? existingFileName))
                {
                    if (!dupOnly)
                    {
                        Console.Out.WriteLine(currentFileName);
                        Console.Out.WriteLine(Convert.ToHexString(MemoryMarshal.AsBytes<ulong>(hash)));
                        Console.Out.WriteLine("New entry.");
                    }
                    fileMappings.Add(hash, currentFileName);
                }
                else
                {
                    Console.Out.WriteLine(currentFileName);
                    Console.Out.WriteLine(Convert.ToHexString(MemoryMarshal.AsBytes<ulong>(hash)));
                    Console.Out.Write(" => ");
                    Console.Out.WriteLine(existingFileName);
                    if (!dryRun)
                    {
                        fi.Delete();
                        HardLink.Create(currentFileName, existingFileName);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }
        }
    }

    static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo rootDir, Regex? filterRegex = null, Regex? skipRegex = null)
    {
        EnumerationOptions options = new()
        {
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
            IgnoreInaccessible = true
        };
        HashSet<string> visited = [];
        Stack<IEnumerator<FileSystemInfo>> stack = [];
        IEnumerator<FileSystemInfo>? currentEnumerator = rootDir.EnumerateFileSystemInfos("*", options).GetEnumerator();
        do
        {
            while (currentEnumerator.MoveNext())
            {
                switch (currentEnumerator.Current)
                {
                    case FileInfo fi when filterRegex?.IsMatch(fi.FullName) ?? true:
                        while (fi.LinkTarget is string link)
                            fi = new(Path.Combine(Path.GetDirectoryName(fi.FullName) ?? "", link));
                        if (!visited.Add(fi.FullName))
                            continue;
                        yield return fi;
                        break;
                    case DirectoryInfo di when (!skipRegex?.IsMatch(di.FullName)) ?? true:
                        while (di.LinkTarget is string link)
                            di = new(Path.Combine(Path.GetDirectoryName(di.FullName) ?? "", link));
                        if (!visited.Add(di.FullName))
                            continue;
                        stack.Push(currentEnumerator);
                        currentEnumerator = di.EnumerateFileSystemInfos("*", options).GetEnumerator();
                        break;
                }
            }
            currentEnumerator.Dispose();
        }
        while (stack.TryPop(out currentEnumerator));
    }
}