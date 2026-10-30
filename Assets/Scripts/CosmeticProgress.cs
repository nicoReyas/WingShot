using System;
using UnityEngine;

public enum CosmeticCategory { Crosshair, Bird, Background }

public sealed class CosmeticItem
{
    public readonly string Id;
    public readonly string Name;
    public readonly int Cost;
    public readonly Color Color;
    // Store one cosmetic's save ID, display name, price, and preview color.
    public CosmeticItem(string id, string name, int cost, Color color)
    {
        Id = id; Name = name; Cost = cost; Color = color;
    }
}

public static class CosmeticCatalog
{
    // Each category starts with a free style; the other entries cost saved points.
    public static readonly CosmeticItem[] Crosshairs =
    {
        new CosmeticItem("classic", "Classic", 0, new Color(1f, .86f, .32f)),
        new CosmeticItem("ring", "Bullseye", 75, new Color(.7f, 1f, .87f)),
        new CosmeticItem("diamond", "Diamond", 150, new Color(1f, .68f, .76f)),
        new CosmeticItem("pinpoint", "Pinpoint", 500, new Color(.6f, .92f, 1f)),
        new CosmeticItem("brackets", "Tactical", 1000, new Color(.7f, 1f, .35f)),
        new CosmeticItem("orbital", "Orbital", 2500, new Color(.77f, .65f, 1f)),
        new CosmeticItem("star", "Supernova", 5000, new Color(1f, .94f, .63f))
    };
    public static readonly CosmeticItem[] Birds =
    {
        new CosmeticItem("amber", "Golden Finch", 0, new Color(1f, .68f, .25f)),
        new CosmeticItem("coral", "Coral Swift", 100, new Color(1f, .37f, .42f)),
        new CosmeticItem("mint", "Mint Glider", 200, new Color(.31f, .88f, .69f)),
        new CosmeticItem("azure", "Azure Streak", 500, new Color(.22f, .59f, 1f)),
        new CosmeticItem("violet", "Royal Plumage", 1000, new Color(.67f, .36f, .95f)),
        new CosmeticItem("frost", "Arctic Pearl", 2200, new Color(.84f, .96f, 1f)),
        new CosmeticItem("phoenix", "Phoenix Gold", 4500, new Color(1f, .43f, .12f))
    };
    public static readonly CosmeticItem[] Backgrounds =
    {
        new CosmeticItem("day", "Blue Skies", 0, new Color(.514f, .918f, 1f)),
        new CosmeticItem("sunset", "Golden Hour", 125, new Color(.98f, .65f, .53f)),
        new CosmeticItem("night", "Moonlit Meadow", 250, new Color(.10f, .18f, .33f)),
        new CosmeticItem("autumn", "Autumn Grove", 600, new Color(.87f, .69f, .48f)),
        new CosmeticItem("winter", "Alpine Frost", 1200, new Color(.58f, .76f, .87f)),
        new CosmeticItem("aurora", "Aurora Wilds", 2400, new Color(.055f, .14f, .23f)),
        new CosmeticItem("ember", "Ember Valley", 5000, new Color(.30f, .16f, .32f))
    };

    // Return the available styles for the requested cosmetic category.
    public static CosmeticItem[] Items(CosmeticCategory category)
    {
        switch (category)
        {
            case CosmeticCategory.Bird: return Birds;
            case CosmeticCategory.Background: return Backgrounds;
            default: return Crosshairs;
        }
    }
}

// Uses stable IDs instead of array indices so adding cosmetics never invalidates an existing save.
public sealed class CosmeticProgress
{
    // This prefix keeps Wing Shot's save keys separate from other settings.
    public const string DefaultPrefix = "WingShot.v1.";
    readonly string prefix;
    public int Points { get; private set; }
    public int LifetimePoints { get; private set; }
    public int BestScore { get; private set; }
    public bool Muted { get; private set; }

    // Read saved points, best score, and the sound setting when the game opens.
    public CosmeticProgress(string keyPrefix = DefaultPrefix)
    {
        prefix = keyPrefix;
        Points = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "points", 0));
        LifetimePoints = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "lifetime", 0));
        BestScore = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "best", 0));
        Muted = PlayerPrefs.GetInt(prefix + "muted", 0) == 1;
    }

    // Free styles are always available; paid styles need a saved purchase.
    public bool IsUnlocked(CosmeticCategory category, int index)
    {
        var items = CosmeticCatalog.Items(category);
        return index >= 0 && index < items.Length &&
            (items[index].Cost == 0 || PlayerPrefs.GetInt(UnlockKey(category, index), 0) == 1);
    }

    // Find the saved selection, or use the free default if the saved choice is invalid.
    public int Selected(CosmeticCategory category)
    {
        var items = CosmeticCatalog.Items(category);
        string saved = PlayerPrefs.GetString(prefix + "selected." + category, items[0].Id);
        for (int i = 0; i < items.Length; i++)
            if (items[i].Id == saved && IsUnlocked(category, i)) return i;
        return 0;
    }

    // Charge once for a new style, select it, and save the change.
    public bool UnlockAndSelect(CosmeticCategory category, int index)
    {
        var items = CosmeticCatalog.Items(category);
        if (index < 0 || index >= items.Length) return false;
        // Only a first purchase spends points; selecting an owned style is free.
        if (!IsUnlocked(category, index))
        {
            if (Points < items[index].Cost) return false;
            Points -= items[index].Cost;
            PlayerPrefs.SetInt(UnlockKey(category, index), 1);
        }
        PlayerPrefs.SetString(prefix + "selected." + category, items[index].Id);
        Save();
        return true;
    }

    // Add earned points to the wallet and update the best score.
    public void Earn(int amount, int runScore)
    {
        if (amount <= 0) return;
        Points = (int)Math.Min(int.MaxValue, (long)Points + amount);
        LifetimePoints = (int)Math.Min(int.MaxValue, (long)LifetimePoints + amount);
        BestScore = Mathf.Max(BestScore, runScore);
        Save();
    }

    // Record the settled score so a later miss cannot leave a provisional high score behind.
    public void RecordScore(int runScore)
    {
        BestScore = Mathf.Max(BestScore, runScore);
        Save();
    }

    // Remember whether the player has turned sound off.
    public void SetMuted(bool muted) { Muted = muted; Save(); }

    // Write the player's progress to PlayerPrefs so it survives closing the game.
    public void Save()
    {
        PlayerPrefs.SetInt(prefix + "points", Points);
        PlayerPrefs.SetInt(prefix + "lifetime", LifetimePoints);
        PlayerPrefs.SetInt(prefix + "best", BestScore);
        PlayerPrefs.SetInt(prefix + "muted", Muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    // Build a separate save key for each owned cosmetic.
    string UnlockKey(CosmeticCategory category, int index)
    {
        return prefix + "unlocked." + category + "." + CosmeticCatalog.Items(category)[index].Id;
    }
}
