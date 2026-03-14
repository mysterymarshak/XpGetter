namespace XpGetter.Application.Dto;

public class Cs2ItemByNameComparer : IEqualityComparer<Cs2Item>
{
    public bool Equals(Cs2Item? x, Cs2Item? y) => x?.Name == y?.Name;
    public int GetHashCode(Cs2Item obj) => obj.GetHashCode();
}
