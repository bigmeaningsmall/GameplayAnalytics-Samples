using System.IO;
using UnityEngine;

/// <summary>
///  Gameplay Analytics System
///  - Tracks gameplay metrics like time played, enemies defeated, items collected, and custom events
///  - Saves/loads data to/from a JSON file for persistence
///  - Singleton pattern for easy access across the project
/// </summary>

public class GameplayAnalytics : MonoBehaviour
{
    public static GameplayAnalytics Instance { get; private set; }

    private string saveFilePath;
    public GameplayData gameplayData = new GameplayData();
    
    
    public string dataName = "gameplayData_P1.json"; // change filename here if needed - multiple playthroughs can use different names
    
    public bool loadPreviosDataOnStart = true;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // use different paths for editor vs build to make it easier to find the file during development
        #if UNITY_EDITOR
                saveFilePath = Path.Combine(Application.dataPath, "GameplayData/" + dataName);
        #else
            saveFilePath = Path.Combine(Application.persistentDataPath, dataName);
        #endif


        if (loadPreviosDataOnStart){
            LoadData();
        }
    }

    // 🧠 Add or update data
    public void AddEvent(string eventName)
    {
        gameplayData.customEvents.Add(eventName);
        SaveData();
    }

    public void AddTimePlayed(float deltaTime)
    {
        gameplayData.timePlayed += deltaTime;
    }

    public void IncrementEnemiesDefeated()
    {
        gameplayData.totalEnemiesDefeated++;
    }

    public void IncrementItemsCollected()
    {
        gameplayData.itemsCollected++;
    }

    // 💾 Save to JSON
    public void SaveData()
    {
        string json = JsonUtility.ToJson(gameplayData, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log("Data saved to: " + saveFilePath);
    }

    // 📂 Load from JSON
    public void LoadData()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            gameplayData = JsonUtility.FromJson<GameplayData>(json);
            Debug.Log("Data loaded successfully.");
        }
        else
        {
            gameplayData = new GameplayData();
            Debug.Log("No save found. Created new data.");
        }
    }

    // 🧹 Reset file
    public void ClearData()
    {
        gameplayData = new GameplayData();
        SaveData();
    }
}