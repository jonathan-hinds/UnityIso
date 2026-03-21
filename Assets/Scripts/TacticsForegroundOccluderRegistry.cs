using System.Collections.Generic;

public static class TacticsForegroundOccluderRegistry
{
    private static readonly List<TacticsForegroundOccluderGroup> Groups = new List<TacticsForegroundOccluderGroup>();

    public static IReadOnlyList<TacticsForegroundOccluderGroup> RegisteredGroups => Groups;

    public static void Register(TacticsForegroundOccluderGroup group)
    {
        if (group == null || Groups.Contains(group))
        {
            return;
        }

        Groups.Add(group);
    }

    public static void Unregister(TacticsForegroundOccluderGroup group)
    {
        if (group == null)
        {
            return;
        }

        Groups.Remove(group);
    }
}
