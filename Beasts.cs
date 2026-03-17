using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Beasts.Api;
using Beasts.Data;
using Beasts.ExileCore;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using Newtonsoft.Json;

namespace Beasts;

public partial class Beasts : BaseSettingsPlugin<BeastsSettings>
{
    private readonly Dictionary<long, Entity> _trackedBeasts = new();
    private DateTime _lastExportTime = DateTime.MinValue;

    public override void OnLoad()
    {
        Settings.FetchBeastPrices.OnPressed += async () => await FetchPrices();
        Settings.ExportBeastsNow.OnPressed += () => ExportCapturedBeasts();
        Settings.ScanUiTree.OnPressed += () => ScanUiTree();
        Task.Run(FetchPrices);
    }

    /// <summary>
    /// Debug: Scans the UI tree to find the correct child indices for the Bestiary panel.
    /// Open the Bestiary in-game FIRST, then press this button, then check the Debug Window log.
    /// </summary>
    private void ScanUiTree()
    {
        DebugWindow.LogMsg("[Beasts] Starting UI tree scan... Make sure Bestiary panel is OPEN!");
        Extensions.FindBestiaryPanel(GameController.IngameState.IngameUi);
    }

    private async Task FetchPrices()
    {
        DebugWindow.LogMsg("Fetching Beast Prices from PoeNinja...");
        var prices = await PoeNinja.GetBeastsPrices();
        foreach (var beast in BeastsDatabase.AllBeasts)
        {
            Settings.BeastPrices[beast.DisplayName] = prices.TryGetValue(beast.DisplayName, out var price) ? price : -1;
        }

        Settings.LastUpdate = DateTime.Now;
    }

    // ============================================================
    // BEAST RECORDING / EXPORT
    // ============================================================

    /// <summary>
    /// Reads all captured beasts from the bestiary panel and exports to JSON.
    /// Call this when the Bestiary panel is open in-game.
    /// </summary>
    private void ExportCapturedBeasts()
    {
        try
        {
            var bestiary = GameController.IngameState.IngameUi.GetBestiaryPanel();
            if (bestiary == null || !bestiary.IsVisible)
            {
                DebugWindow.LogMsg("[Beasts] Bestiary panel not open! Open it first, then export.", 5);
                return;
            }

            var capturedBeastsPanel = bestiary.CapturedBeastsPanel;
            if (capturedBeastsPanel == null || !capturedBeastsPanel.IsVisible)
            {
                DebugWindow.LogMsg("[Beasts] Captured Beasts tab not visible!", 5);
                return;
            }

            var beasts = capturedBeastsPanel.CapturedBeasts;
            if (beasts == null || beasts.Count == 0)
            {
                DebugWindow.LogMsg("[Beasts] No captured beasts found.", 5);
                return;
            }

            // Count beasts by display name
            var beastCounts = new Dictionary<string, int>();
            var beastDetails = new List<ExportedBeast>();

            foreach (var beast in beasts)
            {
                try
                {
                    var name = beast.DisplayName;
                    if (string.IsNullOrEmpty(name)) continue;

                    if (beastCounts.ContainsKey(name))
                        beastCounts[name]++;
                    else
                        beastCounts[name] = 1;

                    // Try to match to known beast for craft info
                    var knownBeast = BeastsDatabase.AllBeasts.FirstOrDefault(b => b.DisplayName == name);
                    var price = Settings.BeastPrices.TryGetValue(name, out var p) ? p : -1;

                    beastDetails.Add(new ExportedBeast
                    {
                        DisplayName = name,
                        Crafts = knownBeast?.Crafts ?? Array.Empty<string>(),
                        Price = price
                    });
                }
                catch
                {
                    // Skip beasts that fail to read
                }
            }

            // Build export object
            var export = new BeastExport
            {
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                TotalBeasts = beastDetails.Count,
                UniqueBeastTypes = beastCounts.Count,
                BeastCounts = beastCounts.OrderByDescending(x => x.Value)
                    .ToDictionary(x => x.Key, x => x.Value),
                AllBeasts = beastDetails
            };

            // Ensure export directory exists
            var exportDir = Path.Combine(DirectoryFullName, "exports");
            Directory.CreateDirectory(exportDir);

            // Save timestamped snapshot
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var snapshotPath = Path.Combine(exportDir, $"beasts_{timestamp}.json");
            File.WriteAllText(snapshotPath, JsonConvert.SerializeObject(export, Formatting.Indented));

            // Also save a "latest" file that always has the most recent data
            var latestPath = Path.Combine(exportDir, "beasts_latest.json");
            File.WriteAllText(latestPath, JsonConvert.SerializeObject(export, Formatting.Indented));

            // Append summary to CSV log for tracking over time
            var csvPath = Path.Combine(exportDir, "beast_log.csv");
            var csvExists = File.Exists(csvPath);
            using (var writer = new StreamWriter(csvPath, append: true))
            {
                if (!csvExists)
                {
                    writer.WriteLine("timestamp,beast_name,count,price,crafts");
                }

                foreach (var kvp in beastCounts)
                {
                    var price = Settings.BeastPrices.TryGetValue(kvp.Key, out var pr) ? pr : -1;
                    var knownBeast = BeastsDatabase.AllBeasts.FirstOrDefault(b => b.DisplayName == kvp.Key);
                    var crafts = knownBeast != null ? string.Join(" | ", knownBeast.Crafts) : "";
                    // Escape CSV fields
                    var safeName = kvp.Key.Replace("\"", "\"\"");
                    var safeCrafts = crafts.Replace("\"", "\"\"");
                    writer.WriteLine($"{export.Timestamp},\"{safeName}\",{kvp.Value},{price},\"{safeCrafts}\"");
                }
            }

            _lastExportTime = DateTime.Now;
            DebugWindow.LogMsg(
                $"[Beasts] Exported {beastDetails.Count} beasts ({beastCounts.Count} types) to {snapshotPath}", 5);
        }
        catch (Exception e)
        {
            DebugWindow.LogMsg($"[Beasts] Export failed: {e.Message}", 10);
        }
    }

    // Data classes for JSON export
    private class ExportedBeast
    {
        public string DisplayName;
        public string[] Crafts;
        public float Price;
    }

    private class BeastExport
    {
        public string Timestamp;
        public int TotalBeasts;
        public int UniqueBeastTypes;
        public Dictionary<string, int> BeastCounts;
        public List<ExportedBeast> AllBeasts;
    }

    // ============================================================
    // ORIGINAL ENTITY TRACKING
    // ============================================================

    public override void AreaChange(AreaInstance area)
    {
        _trackedBeasts.Clear();
    }

    public override void EntityAdded(Entity entity)
    {
        if (entity.Rarity != MonsterRarity.Rare) return;
        foreach (var _ in BeastsDatabase.AllBeasts.Where(beast => entity.Metadata == beast.Path))
        {
            _trackedBeasts.Add(entity.Id, entity);
        }
    }

    public override void EntityRemoved(Entity entity)
    {
        if (_trackedBeasts.ContainsKey(entity.Id))
        {
            _trackedBeasts.Remove(entity.Id);
        }
    }
}
