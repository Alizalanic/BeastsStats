using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory;

namespace Beasts.ExileCore;

public class BestiaryPanel : Element
{
    public CapturedBeastsPanel CapturedBeastsPanel =>
        GetObject<CapturedBeastsPanel>(GetChildAtIndex(0)?.GetChildAtIndex(18).Address ?? 0);
}

public class CapturedBeastsPanel : Element
{
    private static readonly List<string> BeastTypes = new()
    {
        "Felines", "Primates", "Canines", "Ursae", "Unnaturals", "Avians", "Reptiles", "Insects", "Arachnids",
        "Cephalopods", "Crustaceans", "Amphibians"
    };

    // Map each family to its region
    private static readonly Dictionary<string, string> FamilyToRegion = new()
    {
        { "Felines", "The Wilds" },
        { "Primates", "The Wilds" },
        { "Canines", "The Wilds" },
        { "Ursae", "The Wilds" },
        { "Unnaturals", "The Wilds" },
        { "Avians", "The Sands" },
        { "Reptiles", "The Sands" },
        { "Insects", "The Caverns" },
        { "Arachnids", "The Caverns" },
        { "Cephalopods", "The Deep" },
        { "Crustaceans", "The Deep" },
        { "Amphibians", "The Deep" },
    };

    private Element BeastsDisplay => GetChildAtIndex(1)?.GetChildAtIndex(0);

    // Original method — flat list (kept for backward compatibility with Render code)
    public List<CapturedBeast> CapturedBeasts
    {
        get
        {
            var beasts = new List<CapturedBeast>();
            for (var i = 0; i < BeastTypes.Count; i++)
            {
                var beastContainer = BeastsDisplay?.GetChildAtIndex(i);
                if (beastContainer == null || beastContainer.IsVisible == false) continue;

                beasts.AddRange(beastContainer.GetChildAtIndex(1).Children
                    .Select(beast => GetObject<CapturedBeast>(beast.Address)));
            }

            return beasts;
        }
    }

    // NEW: Returns beasts with family and region info
    public List<CapturedBeastWithFamily> CapturedBeastsWithFamily
    {
        get
        {
            var beasts = new List<CapturedBeastWithFamily>();
            for (var i = 0; i < BeastTypes.Count; i++)
            {
                var beastContainer = BeastsDisplay?.GetChildAtIndex(i);
                if (beastContainer == null || beastContainer.IsVisible == false) continue;

                var family = BeastTypes[i];
                var region = FamilyToRegion.TryGetValue(family, out var r) ? r : "Unknown";

                var children = beastContainer.GetChildAtIndex(1)?.Children;
                if (children == null) continue;

                foreach (var child in children)
                {
                    var beast = GetObject<CapturedBeast>(child.Address);
                    beasts.Add(new CapturedBeastWithFamily
                    {
                        Beast = beast,
                        Family = family,
                        Region = region,
                    });
                }
            }

            return beasts;
        }
    }
}

public class CapturedBeast : Element
{
    public string DisplayName => Tooltip?.GetChildAtIndex(1)?.GetChildAtIndex(0).Text.Replace("-", "").Trim();
}

public class CapturedBeastWithFamily
{
    public CapturedBeast Beast;
    public string Family;
    public string Region;
}
