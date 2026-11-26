using System.Collections.Generic;

public static class Flags
{
    static HashSet<string> set = new HashSet<string>();

    public static bool Has(string f) => !string.IsNullOrEmpty(f) && set.Contains(f);
    public static void Set(string f) { if (!string.IsNullOrEmpty(f)) set.Add(f); }
    public static void Clear() => set.Clear();
    public static List<string> Save() => new List<string>(set);
    public static void Load(List<string> saved) => set = new HashSet<string>(saved);
}
