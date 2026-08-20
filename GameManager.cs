using Firebase.Auth;
using Firebase.Firestore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Game State")]
    public string CurrentGameId;
    public bool HasGameStarted = false;
    public int GameDays = 2;
    public float lootTime;
    public float shelterTime;
    public float shelterCoolDown;

    public bool isScoreless = false;

    public List<string> DeadNpcIDs = new List<string>();

    [Header("Scene Management")]
    public string PreviousScene;
    public string CurrentScene;
    public string LastGameplayScene;
    public GameObject[] npcSpawned;
    public GameObject attackableNPC;

    [Header("References")]
    public CharacterData selectedCharacter;
    public CharacterData[] allCharacters;

    public GameObject playerObj;
    private FirebaseFirestore db;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Track scene name via event instead of polling in Update every frame.
        CurrentScene = SceneManager.GetActiveScene().name;
        SceneManager.activeSceneChanged += (_, next) => CurrentScene = next.name;
    }

    private void Start()
    {
        db = FirebaseFirestore.DefaultInstance;
    }

    public async void StartNewRun()
    {
        try
        {
            if (FirebaseAuth.DefaultInstance.CurrentUser == null) return;

            string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;

            string newGameId = Guid.NewGuid().ToString();
            CurrentGameId = newGameId;
            HasGameStarted = true;
            DeadNpcIDs.Clear();

            Dictionary<string, object> newGameData = new Dictionary<string, object>
            {
                { "userId", userId },
                { "day", 1 },
                { "currentScene", "LootScene" },
                { "characterName", selectedCharacter != null ? selectedCharacter.characterName : "Default" },
                { "isCompleted", false },
                { "Inventory", new List<string>() },
                { "Weapons", new List<string>() },
                { "deadNpcs", new List<string>() },
                { "createdAt", FieldValue.ServerTimestamp }
            };

            await db.Collection("Games").Document(newGameId).SetAsync(newGameData);

            await db.Collection("Users").Document(userId).SetAsync(new Dictionary<string, object>
            {
                { "activeGameId", newGameId }
            }, SetOptions.MergeAll);

            if (DayManager.Instance != null) DayManager.Instance.ResetDay();

            SceneLoader.LoadScene("CharacterSelection");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] StartNewRun failed: {e}");
        }
    }

    public async void ContinueLastGame()
    {
        try
        {
            if (FirebaseAuth.DefaultInstance.CurrentUser == null) return;

            string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;

            DocumentSnapshot userDoc = await db.Collection("Users").Document(userId).GetSnapshotAsync();

            if (!userDoc.Exists || !userDoc.ContainsField("activeGameId"))
                return;

            string activeGameId = userDoc.GetValue<string>("activeGameId");
            if (string.IsNullOrEmpty(activeGameId)) return;

            CurrentGameId = activeGameId;
            HasGameStarted = true;

            DocumentSnapshot gameDoc = await db.Collection("Games").Document(activeGameId).GetSnapshotAsync();

            if (!gameDoc.Exists) return;

            if (gameDoc.ContainsField("day"))
            {
                int savedDay = gameDoc.GetValue<int>("day");
                if (DayManager.Instance != null) DayManager.Instance.currentDay = savedDay;
            }

            if (gameDoc.ContainsField("characterName"))
            {
                string savedCharName = gameDoc.GetValue<string>("characterName");
                foreach (var character in allCharacters)
                {
                    if (character.characterName == savedCharName)
                    {
                        selectedCharacter = character;
                        break;
                    }
                }
            }

            DeadNpcIDs.Clear();
            if (gameDoc.ContainsField("deadNpcs"))
            {
                var raw = gameDoc.GetValue<object>("deadNpcs") as List<object>;
                if (raw != null)
                    foreach (var item in raw)
                        DeadNpcIDs.Add(item.ToString());
            }

            string sceneToLoad = gameDoc.ContainsField("currentScene")
                ? gameDoc.GetValue<string>("currentScene")
                : "LootScene";

            SceneLoader.LoadScene(sceneToLoad);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] ContinueLastGame failed: {e}");
        }
    }

    public async void RegisterNpcDeath(string npcId)
    {
        try
        {
            if (DeadNpcIDs.Contains(npcId)) return;

            DeadNpcIDs.Add(npcId);

            if (!string.IsNullOrEmpty(CurrentGameId))
            {
                DocumentReference gameRef = db.Collection("Games").Document(CurrentGameId);
                // ArrayUnion is a server-side atomic append — safe when multiple clients
                // write simultaneously, unlike a read-modify-write approach.
                await gameRef.UpdateAsync(new Dictionary<string, object>
                {
                    { "deadNpcs", FieldValue.ArrayUnion(npcId) }
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] RegisterNpcDeath failed: {e}");
        }
    }

    public async void EndGameOnDeath()
    {
        try
        {
            if (FirebaseAuth.DefaultInstance.CurrentUser == null) return;

            string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;

            if (!string.IsNullOrEmpty(CurrentGameId))
            {
                await db.Collection("Games").Document(CurrentGameId).UpdateAsync(new Dictionary<string, object>
                {
                    { "isCompleted", true }
                });
            }

            // FieldValue.Delete() removes the field entirely; setting it to null leaves
            // a null entry which HasActiveGame() would misread as an active session.
            await db.Collection("Users").Document(userId).UpdateAsync(new Dictionary<string, object>
            {
                { "activeGameId", FieldValue.Delete() }
            });

            CurrentGameId = "";
            HasGameStarted = false;
            DeadNpcIDs.Clear();

            if (npcSpawned != null)
                for (int i = 0; i < npcSpawned.Length; i++)
                    if (npcSpawned[i] != null) Destroy(npcSpawned[i]);

            if (playerObj != null) Destroy(playerObj);

            if (DayManager.Instance != null) DayManager.Instance.ResetDay();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] EndGameOnDeath failed: {e}");
        }
    }

    public async void SaveAndReturnToMainMenu()
    {
        try
        {
            if (FirebaseAuth.DefaultInstance.CurrentUser == null || string.IsNullOrEmpty(CurrentGameId))
            {
                Debug.LogWarning("No active game to save — returning to menu.");
                SceneLoader.LoadScene("Main");
                return;
            }

            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "currentScene", SceneManager.GetActiveScene().name },
                { "day", DayManager.Instance != null ? DayManager.Instance.currentDay : 1 }
            };

            await db.Collection("Games").Document(CurrentGameId).UpdateAsync(updates);

            // InventoryManager owns its own full save (player items + NPC inventories).
            // Awaiting it here ensures data is written before the scene clears.
            if (InventoryManager.Instance != null)
                await InventoryManager.Instance.SaveToFirebase();

            CurrentGameId = "";
            HasGameStarted = false;
            DeadNpcIDs.Clear();

            if (npcSpawned != null)
                for (int i = 0; i < npcSpawned.Length; i++)
                    if (npcSpawned[i] != null) Destroy(npcSpawned[i]);

            if (playerObj != null) Destroy(playerObj);

            if (DayManager.Instance != null) DayManager.Instance.ResetDay();

            Time.timeScale = 1f;

            SceneLoader.LoadScene("Main");
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameManager] SaveAndReturnToMainMenu failed: {e}");
        }
    }

    public void SetSceneTransition(string from, string to)
    {
        PreviousScene = from;
        CurrentScene = to;
        if (from != "Loading") LastGameplayScene = from;
    }

    public async Task<bool> HasActiveGame()
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser == null) return false;
        string userId = FirebaseAuth.DefaultInstance.CurrentUser.UserId;

        DocumentSnapshot userDoc = await db.Collection("Users").Document(userId).GetSnapshotAsync();
        if (userDoc.Exists && userDoc.ContainsField("activeGameId"))
        {
            string id = userDoc.GetValue<string>("activeGameId");
            return !string.IsNullOrEmpty(id);
        }
        return false;
    }

    public void UpdateDay()
    {
        if (DayManager.Instance == null || playerObj == null) return;

        PlayerUI playerUI = playerObj.GetComponent<PlayerUI>();

        if (playerObj.CompareTag("isDead"))
        {
            isScoreless = true;
            if (playerUI) playerUI.Scoreless();
        }
        else
        {
            if (DayManager.Instance.currentDay <= GameDays - 1)
            {
                ShelterManager shelterManager = FindAnyObjectByType<ShelterManager>();
                if (shelterManager) shelterManager.ShelterController();
            }
            else
            {
                if (playerUI) playerUI.EndDay();
            }
        }
    }
}
