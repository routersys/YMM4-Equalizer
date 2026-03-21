namespace Equalizer.Models;

public sealed class GroupItem(string name, string tag)
{
    public string Name { get; set; } = name;
    public string Tag { get; set; } = tag;

    public GroupItem() : this("", "") { }

    public override string ToString() => Name;

    public override bool Equals(object? obj) =>
        obj is GroupItem other && Tag == other.Tag;

    public override int GetHashCode() => Tag.GetHashCode(StringComparison.Ordinal);
}