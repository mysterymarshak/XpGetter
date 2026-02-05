using System.Diagnostics.CodeAnalysis;

namespace XpGetter.Application.Dto;

public class CsgoItemByNameComparer : IEqualityComparer<CsgoItem>
{
    public bool Equals(CsgoItem? x, CsgoItem? y) => x?.Name == y?.Name;
    public int GetHashCode([DisallowNull] CsgoItem obj) => obj.GetHashCode();
}
