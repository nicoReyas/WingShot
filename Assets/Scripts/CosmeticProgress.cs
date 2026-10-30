using System;
using UnityEngine;

public enum CosmeticCategory { Crosshair, Bird, Background }

public sealed class CosmeticItem
{
    public readonly string Id;
    public readonly string Name;
    public readonly int Cost;
    public readonly Color Color;
    public CosmeticItem(string id, string name, int cost, Color color)
    {
        Id = id; Name = name; Cost = cost; Color = color;
    }
}

public static class CosmeticCatalog
{
    public static readonly CosmeticItem[] Crosshairs =
    {
        new CosmeticItem("classic", "Classic", 0, new Color(1f, .86f, .32f)),
        new CosmeticItem("ring", "Bullseye", 75, new Color(.7f, 1f, .87f)),
        new CosmeticItem("diamond", "Diamond", 150, new Color(1f, .68f, .76f))
    };
    public static readonly CosmeticItem[] Birds =
    {
        new CosmeticItem("amber", "Golden Finch", 0, new Color(1f, .68f, .25f)),
        new CosmeticItem("coral", "Coral Swift", 100, new Color(1f, .37f, .42f)),
        new CosmeticItem("mint", "Mint Glider", 200, new Color(.31f, .88f, .69f))
    };
    public static readonly CosmeticItem[] Backgrounds =
    {
        new CosmeticItem("day", "Blue Skies", 0, new Color(.514f, .918f, 1f)),
        new CosmeticItem("sunset", "Golden Hour", 125, new Color(.98f, .65f, .53f)),
        new CosmeticItem("night", "Moonlit Meadow", 250, new Color(.10f, .18f, .33f))
    };

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
    public const string DefaultPrefix = "WingShot.v1.";
    readonly string prefix;
    public int Points { get; private set; }
    public int LifetimePoints { get; private set; }
    public int BestScore { get; private set; }
    public bool Muted { get; private set; }

    public CosmeticProgress(string keyPrefix = DefaultPrefix)
    {
        prefix = keyPrefix;
        Points = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "points", 0));
        LifetimePoints = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "lifetime", 0));
        BestScore = Mathf.Max(0, PlayerPrefs.GetInt(prefix + "best", 0));
        Muted = PlayerPrefs.GetInt(prefix + "muted", 0) == 1;
    }

    public bool IsUnlocked(CosmeticCategory category, int index)
    {
        var items = CosmeticCatalog.Items(category);
        return index >= 0 && index < items.Length &&
            (items[index].Cost == 0 || PlayerPrefs.GetInt(UnlockKey(category, index), 0) == 1);
    }

    public int Selected(CosmeticCategory category)
    {
        var items = CosmeticCatalog.Items(category);
        string saved = PlayerPrefs.GetString(prefix + "selected." + category, items[0].Id);
        for (int i = 0; i < items.Length; i++)
            if (items[i].Id == saved && IsUnlocked(category, i)) return i;
        return 0;
    }

    public bool UnlockAndSelect(CosmeticCategory category, int index)
    {
        var items = CosmeticCatalog.Items(category);
        if (index < 0 || index >= items.Length) return false;
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

    public void Earn(int amount, int runScore)
    {
        if (amount <= 0) return;
        Points = (int)Math.Min(int.MaxValue, (long)Points + amount);
        LifetimePoints = (int)Math.Min(int.MaxValue, (long)LifetimePoints + amount);
        BestScore = Mathf.Max(BestScore, runScore);
        Save();
    }

    public void SetMuted(bool muted) { Muted = muted; Save(); }

    public void Save()
    {
        PlayerPrefs.SetInt(prefix + "points", Points);
        PlayerPrefs.SetInt(prefix + "lifetime", LifetimePoints);
        PlayerPrefs.SetInt(prefix + "best", BestScore);
        PlayerPrefs.SetInt(prefix + "muted", Muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    string UnlockKey(CosmeticCategory category, int index)
    {
        return prefix + "unlocked." + category + "." + CosmeticCatalog.Items(category)[index].Id;
    }
}
