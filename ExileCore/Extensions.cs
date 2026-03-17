using System.Collections.Generic;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;

namespace Beasts.ExileCore;

public static class Extensions
{
    /// <summary>
    /// Original method — tries the known index path.
    /// If this returns null or a non-visible panel, run FindBestiaryPanel() to discover the correct indices.
    /// </summary>
    public static BestiaryPanel GetBestiaryPanel(this IngameUIElements ui)
    {
        // Try a range of indices around the old value (50) in case it shifted slightly
        foreach (var firstIndex in new[] { 50, 51, 52, 49, 48, 53, 54, 55 })
        {
            try
            {
                var candidate = ui.GetChildAtIndex(firstIndex)
                    ?.GetChildAtIndex(2)
                    ?.GetChildAtIndex(0)
                    ?.GetChildAtIndex(1)
                    ?.GetChildAtIndex(1)
                    ?.GetChildAtIndex(11);

                if (candidate != null && candidate.Address != 0)
                {
                    var panel = ui.GetObject<BestiaryPanel>(candidate.Address);
                    if (panel != null && panel.IsVisible)
                    {
                        DebugWindow.LogMsg($"[Beasts] Found bestiary panel at index {firstIndex} -> 2 -> 0 -> 1 -> 1 -> 11");
                        return panel;
                    }
                }
            }
            catch
            {
                // Skip invalid indices
            }
        }

        // If none of the quick guesses worked, do a deeper scan
        return null;
    }

    /// <summary>
    /// Brute-force scanner: walks the IngameUi children looking for anything
    /// that looks like a Bestiary panel. Call this with the Bestiary open in-game,
    /// check the debug log for results.
    /// </summary>
    public static void FindBestiaryPanel(IngameUIElements ui)
    {
        DebugWindow.LogMsg("[Beasts] === SCANNING UI TREE FOR BESTIARY PANEL ===");

        var childCount = ui.ChildCount;
        DebugWindow.LogMsg($"[Beasts] IngameUi has {childCount} children");

        for (var i = 0; i < childCount && i < 80; i++)
        {
            try
            {
                var child = ui.GetChildAtIndex(i);
                if (child == null) continue;

                // Log visible top-level elements
                if (child.IsVisible)
                {
                    var text = child.Text ?? "(no text)";
                    var cc = child.ChildCount;
                    DebugWindow.LogMsg($"[Beasts]   [{i}] VISIBLE children={cc} text=\"{Truncate(text, 60)}\" addr={child.Address:X}");

                    // Drill into visible elements looking for bestiary-like structures
                    ScanForBestiary(child, i, 1);
                }
            }
            catch
            {
                // Skip
            }
        }

        DebugWindow.LogMsg("[Beasts] === SCAN COMPLETE — check above for 'BESTIARY' matches ===");
    }

    private static void ScanForBestiary(Element element, int parentIndex, int depth)
    {
        if (depth > 5) return; // Don't go too deep

        var childCount = element.ChildCount;
        for (var i = 0; i < childCount && i < 30; i++)
        {
            try
            {
                var child = element.GetChildAtIndex(i);
                if (child == null) continue;

                var text = child.Text ?? "";
                var cc = child.ChildCount;
                var indent = new string(' ', depth * 2);

                // Log elements that have "bestiary" or "beast" in their text, 
                // or that have a suspicious number of children (12 = the beast type categories)
                var isSuspicious = text.ToLower().Contains("bestiar") 
                                   || text.ToLower().Contains("beast")
                                   || text.ToLower().Contains("menag")
                                   || text.ToLower().Contains("feline")
                                   || text.ToLower().Contains("captured")
                                   || (cc == 12 && child.IsVisible);  // 12 beast families

                if (isSuspicious || (child.IsVisible && cc > 0 && depth <= 2))
                {
                    var marker = isSuspicious ? " *** BESTIARY? ***" : "";
                    DebugWindow.LogMsg($"[Beasts] {indent}[{parentIndex}→{i}] vis={child.IsVisible} children={cc} text=\"{Truncate(text, 50)}\"{marker}");

                    if (child.IsVisible && cc > 0)
                    {
                        ScanForBestiary(child, i, depth + 1);
                    }
                }
            }
            catch
            {
                // Skip
            }
        }
    }

    private static string Truncate(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= maxLen ? s : s.Substring(0, maxLen) + "...";
    }
}
