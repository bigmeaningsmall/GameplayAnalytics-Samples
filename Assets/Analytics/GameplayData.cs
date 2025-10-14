using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GameplayData
{
    public float timePlayed;                  // Total time played
    public int totalEnemiesDefeated;          // Example custom stat
    public int itemsCollected;                // Example custom stat
    public List<string> customEvents;         // Log of event names (expandable)

    public GameplayData()
    {
        timePlayed = 0f;
        totalEnemiesDefeated = 0;
        itemsCollected = 0;
        customEvents = new List<string>();
    }
}