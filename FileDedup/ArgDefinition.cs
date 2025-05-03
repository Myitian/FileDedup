namespace FileDedup;

class ArgDefinition(string name, int paramCount, params string[] aliases) : IEquatable<ArgDefinition>
{
    public string Name { get; set; } = name;
    public string[] Aliases { get; set; } = aliases;
    public int ParamCount { get; set; } = paramCount;
    public string? Info { get; set; }

    public bool Equals(ArgDefinition? other)
        => StringComparer.OrdinalIgnoreCase.Equals(Name, other?.Name);
    public override bool Equals(object? obj)
        => Equals(obj as ArgDefinition);
    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
}
