using UnityEngine;

public class GameDataEventTester : MonoBehaviour
{
    [Tooltip("If true, every key press will call SaveData() right after updating stats.")]
    public bool saveOnEveryEvent = true;

    private void Start()
    {
        // Optional: show where the file is saved
        Debug.Log($"Save path: {Application.persistentDataPath}/gameplayData.json");

        // Not strictly required to call LoadData here because GameplayAnalytics does it in Awake,
        // but you could force it if you want:
        // GameplayAnalytics.Instance.LoadData();
    }

    private void Update()
    {
        // 1 – Item collected
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            GameplayAnalytics.Instance.IncrementItemsCollected();
            GameplayAnalytics.Instance.AddEvent("ItemCollected");
            MaybeSave("Item collected");
        }

        // 2 – Enemy defeated
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            GameplayAnalytics.Instance.IncrementEnemiesDefeated();
            GameplayAnalytics.Instance.AddEvent("EnemyDefeated");
            MaybeSave("Enemy defeated");
        }

        // 3 – Checkpoint reached
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            GameplayAnalytics.Instance.AddEvent("CheckpointReached");
            MaybeSave("Checkpoint reached");
        }

        // 4 – Quest started
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            GameplayAnalytics.Instance.AddEvent("QuestStarted");
            MaybeSave("Quest started");
        }

        // 5 – Quest completed
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            GameplayAnalytics.Instance.AddEvent("QuestCompleted");
            MaybeSave("Quest completed");
        }

        // 6 – Level up
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            GameplayAnalytics.Instance.AddEvent("PlayerLevelUp");
            MaybeSave("Player leveled up");
        }

        // 7 – Player death
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            GameplayAnalytics.Instance.AddEvent("PlayerDied");
            MaybeSave("Player died");
        }

        // 8 – Secret found
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            GameplayAnalytics.Instance.AddEvent("SecretFound");
            MaybeSave("Secret found");
        }

        // 9 – Boss defeated
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            GameplayAnalytics.Instance.IncrementEnemiesDefeated();
            GameplayAnalytics.Instance.AddEvent("BossDefeated");
            MaybeSave("Boss defeated");
        }

        // Optional hotkeys:

        // S – Save now
        if (Input.GetKeyDown(KeyCode.S))
        {
            GameplayAnalytics.Instance.SaveData();
            Debug.Log("[Tester] Manual save.");
        }

        // L – Force reload from disk (overwrites in-memory GameplayData)
        if (Input.GetKeyDown(KeyCode.L))
        {
            GameplayAnalytics.Instance.LoadData();
            Debug.Log("[Tester] Data reloaded from disk.");
        }

        // C – Clear data (resets and saves a fresh file)
        if (Input.GetKeyDown(KeyCode.C))
        {
            GameplayAnalytics.Instance.ClearData();
            Debug.Log("[Tester] Data cleared and saved.");
        }

        // (Optional) track time played each frame:
        GameplayAnalytics.Instance.AddTimePlayed(Time.deltaTime);
    }

    private void MaybeSave(string msg)
    {
        if (saveOnEveryEvent)
            GameplayAnalytics.Instance.SaveData();

        Debug.Log($"[Tester] {msg}. " +
                  $"Items: {GameplayAnalytics.Instance.gameplayData.itemsCollected}, " +
                  $"Enemies: {GameplayAnalytics.Instance.gameplayData.totalEnemiesDefeated}, " +
                  $"Events: {GameplayAnalytics.Instance.gameplayData.customEvents.Count}");
    }
}
