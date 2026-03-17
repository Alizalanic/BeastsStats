using System.Collections.Generic;
using System.IO;
using System.Text;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;

namespace Beasts.ExileCore;

public static class Extensions
{
    // Store the plugin directory path — set this from Beasts.cs OnLoad()
    public static string PluginDirectory { get; set; } = "";

    public static BestiaryPanel GetBestiaryPanel(this IngameUIElements ui)
    {
        foreach (var firstIndex in new[] { 50, 51, 52, 49, 48, 53, 54, 55, 56 })
        {
            foreach (var lastIndex in new[] { 11, 12, 13, 10, 9, 14, 15 })
            {
                try
                {
                    var candidate = ui.GetChildAtIndex(firstIndex)
                        ?.GetChildAtIndex(2)
                        ?.GetChildAtIndex(0)
                        ?.GetChildAtIndex(1)
                        ?.GetChildAtIndex(1)
                        ?.GetChildAtIndex(lastIndex);

                    if (candidate != null && candidate.Address != 0)
                    {
                        var panel = ui.GetObject<BestiaryPanel>(candidate.Address);
                        if (panel != null && panel.IsVisible)
                        {
                            return panel;
                        }
                    }
                }
                catch { }
            }
        }

        return null;
    }

    /// <summary>
    /// Deep scanner — writes FULL output to ui_scan.txt in the plugin directory.
    /// Open Bestiary panel in-game first, then press the button.
    /// </summary>
    public static void FindBestiaryPanel(IngameUIElements ui)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== BESTIARY UI TREE SCAN ===");
        sb.AppendLine($"IngameUi has {ui.ChildCount} children");
        sb.AppendLine();

        // Phase 1: Log ALL top-level visible elements (full overview)
        sb.AppendLine("--- ALL VISIBLE TOP-LEVEL ELEMENTS ---");
        for (var i = 0; i < ui.ChildCount && i < 180; i++)
        {
            try
            {
                var child = ui.GetChildAtIndex(i);
                if (child == null) continue;
                if (!child.IsVisible && child.ChildCount == 0) continue;

                var text = child.Text ?? "(null)";
                sb.AppendLine($"[{i}] vis={child.IsVisible} ch={child.ChildCount} text=\"{Trunc(text)}\" addr={child.Address:X}");
            }
            catch { }
        }

        // Phase 2: Deep scan indices 45-65
        sb.AppendLine();
        sb.AppendLine("--- DEEP SCAN INDICES 45-65 (depth 8) ---");
        for (var i = 45; i <= 65 && i < ui.ChildCount; i++)
        {
            try
            {
                var child = ui.GetChildAtIndex(i);
                if (child == null) continue;

                var text = child.Text ?? "(null)";
                sb.AppendLine($"[{i}] vis={child.IsVisible} ch={child.ChildCount} text=\"{Trunc(text)}\" addr={child.Address:X}");

                DeepScan(child, $"[{i}]", 1, 8, sb);
            }
            catch { }
        }

        // Phase 3: Scan elements with 12 children (beast families)
        sb.AppendLine();
        sb.AppendLine("--- ELEMENTS WITH 12 CHILDREN (beast family candidates) ---");
        for (var i = 0; i < ui.ChildCount && i < 180; i++)
        {
            try
            {
                var child = ui.GetChildAtIndex(i);
                if (child == null) continue;
                if (child.ChildCount == 12)
                {
                    sb.AppendLine($"[{i}] vis={child.IsVisible} ch=12 text=\"{Trunc(child.Text ?? "(null)")}\"");
                    DeepScan(child, $"[{i}]", 1, 4, sb);
                }
            }
            catch { }
        }

        // Phase 4: Specifically trace the old path [50]->2->0->... and log what's at each step
        sb.AppendLine();
        sb.AppendLine("--- TRACING OLD PATH FROM [50] ---");
        TracePath(ui, 50, sb);
        sb.AppendLine("--- TRACING FROM [51] ---");
        TracePath(ui, 51, sb);
        sb.AppendLine("--- TRACING FROM [52] ---");
        TracePath(ui, 52, sb);

        sb.AppendLine();
        sb.AppendLine("=== SCAN COMPLETE ===");

        // Write to file
        var outputPath = Path.Combine(
            !string.IsNullOrEmpty(PluginDirectory) ? PluginDirectory : ".",
            "ui_scan.txt"
        );
        File.WriteAllText(outputPath, sb.ToString());

        DebugWindow.LogMsg($"[Beasts] UI scan written to: {outputPath}", 10);
    }

    private static void TracePath(IngameUIElements ui, int startIndex, StringBuilder sb)
    {
        try
        {
            var el = ui.GetChildAtIndex(startIndex);
            sb.AppendLine($"  [{startIndex}] vis={el?.IsVisible} ch={el?.ChildCount} text=\"{Trunc(el?.Text ?? "(null)")}\"");
            if (el == null) return;

            // Log ALL children of this element
            for (var a = 0; a < el.ChildCount && a < 20; a++)
            {
                var c1 = el.GetChildAtIndex(a);
                if (c1 == null) continue;
                sb.AppendLine($"    [{startIndex}]->[{a}] vis={c1.IsVisible} ch={c1.ChildCount} text=\"{Trunc(c1.Text ?? "(null)")}\"");

                // Go one more level
                for (var b = 0; b < c1.ChildCount && b < 10; b++)
                {
                    var c2 = c1.GetChildAtIndex(b);
                    if (c2 == null) continue;
                    sb.AppendLine($"      [{startIndex}]->[{a}]->[{b}] vis={c2.IsVisible} ch={c2.ChildCount} text=\"{Trunc(c2.Text ?? "(null)")}\"");

                    for (var c = 0; c < c2.ChildCount && c < 10; c++)
                    {
                        var c3 = c2.GetChildAtIndex(c);
                        if (c3 == null) continue;
                        sb.AppendLine($"        [{startIndex}]->[{a}]->[{b}]->[{c}] vis={c3.IsVisible} ch={c3.ChildCount} text=\"{Trunc(c3.Text ?? "(null)")}\"");

                        for (var d = 0; d < c3.ChildCount && d < 10; d++)
                        {
                            var c4 = c3.GetChildAtIndex(d);
                            if (c4 == null) continue;
                            sb.AppendLine($"          [{startIndex}]->[{a}]->[{b}]->[{c}]->[{d}] vis={c4.IsVisible} ch={c4.ChildCount} text=\"{Trunc(c4.Text ?? "(null)")}\"");

                            for (var e = 0; e < c4.ChildCount && e < 20; e++)
                            {
                                var c5 = c4.GetChildAtIndex(e);
                                if (c5 == null) continue;
                                var flags = "";
                                if (c5.ChildCount == 12) flags = " *** 12_CHILDREN ***";
                                if ((c5.Text ?? "").ToLower().Contains("bestiar")) flags = " *** BESTIARY ***";
                                sb.AppendLine($"            [{startIndex}]->[{a}]->[{b}]->[{c}]->[{d}]->[{e}] vis={c5.IsVisible} ch={c5.ChildCount} text=\"{Trunc(c5.Text ?? "(null)")}\"{flags}");
                            }
                        }
                    }
                }
            }
        }
        catch { }
    }

    private static void DeepScan(Element element, string path, int depth, int maxDepth, StringBuilder sb)
    {
        if (depth > maxDepth) return;

        var childCount = element.ChildCount;
        var indent = new string(' ', depth * 2);

        for (var i = 0; i < childCount && i < 25; i++)
        {
            try
            {
                var child = element.GetChildAtIndex(i);
                if (child == null) continue;

                var text = child.Text ?? "";
                var cc = child.ChildCount;
                var currentPath = $"{path}->[{i}]";

                var flags = new List<string>();
                if (cc == 12) flags.Add("12_CHILDREN!");
                if (text.ToLower().Contains("bestiar")) flags.Add("BESTIARY!");
                if (text.ToLower().Contains("feline")) flags.Add("FAMILY!");
                if (text.ToLower().Contains("captured")) flags.Add("CAPTURED!");
                if (text.ToLower().Contains("menag")) flags.Add("MENAGERIE!");

                var flagStr = flags.Count > 0 ? " *** " + string.Join(" ", flags) + " ***" : "";

                sb.AppendLine($"{indent}{currentPath} vis={child.IsVisible} ch={cc} text=\"{Trunc(text)}\"{flagStr}");

                if (cc > 0)
                {
                    DeepScan(child, currentPath, depth + 1, maxDepth, sb);
                }
            }
            catch { }
        }
    }

    private static string Trunc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= 50 ? s : s.Substring(0, 50) + "...";
    }
}
