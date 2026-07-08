using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;
using Firebase.Auth;
using System.Linq;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Veritabanı Referansları")]
    public List<ItemData> allGameItems;
    public List<WeaponData> allGameWeapons;

    // --- PLAYER ENVANTERİ ---
    public Dictionary<ItemData, int> inventory = new Dictionary<ItemData, int>();
    public Dictionary<WeaponData, int> weaponInventory = new Dictionary<WeaponData, int>();

    // --- NPC ENVANTER HAFIZASI ---
    public Dictionary<string, NPCInventorySaveData> loadedNPCData = new Dictionary<string, NPCInventorySaveData>();

    // Firebase
    private FirebaseFirestore db;
    private bool isDirty = false;
    private float lastSaveTime;
    private const float SAVE_INTERVAL = 10f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        db = FirebaseFirestore.DefaultInstance;

        if (FirebaseAuth.DefaultInstance.CurrentUser != null)
        {
            LoadFromFirebase();
        }
    }

    private void Update()
    {
        if (isDirty && Time.time >= lastSaveTime + SAVE_INTERVAL)
        {
            SaveToFirebase();
        }
    }

    public void AddItem(ItemData item)
    {
        if (inventory.ContainsKey(item)) inventory[item]++;
        else inventory[item] = 1;
        isDirty = true;
    }

    public void RemoveItem(ItemData item)
    {
        if (!inventory.ContainsKey(item)) return;
        inventory[item]--;
        Debug.Log($"inventory.Count {inventory.Count}");

        if (inventory[item] <= 0) inventory.Remove(item);
        isDirty = true;

        Debug.Log($"inventory.Count {inventory.Count}");
    }

    public void AddWeapon(WeaponData weapon)
    {
        if (weaponInventory.ContainsKey(weapon)) weaponInventory[weapon]++;
        else weaponInventory[weapon] = 1;
        isDirty = true;
    }

    public void RemoveWeapon(WeaponData weapon)
    {
        if (!weaponInventory.ContainsKey(weapon)) return;
        weaponInventory[weapon]--;
        if (weaponInventory[weapon] <= 0) weaponInventory.Remove(weapon);
        isDirty = true;
    }

    // --- FIREBASE İŞLEMLERİ ---

    public async void SaveToFirebase()
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser == null) return;

        string currentGameId = GameManager.Instance.CurrentGameId;
        if (string.IsNullOrEmpty(currentGameId)) return;

        isDirty = false;
        lastSaveTime = Time.time;

        DocumentReference gameRef = db.Collection("Games").Document(currentGameId);

        // 1. PLAYER VERİLERİ
        List<string> itemsToSave = new List<string>();
        foreach (var kvp in inventory)
            for (int i = 0; i < kvp.Value; i++) itemsToSave.Add(kvp.Key.itemID);

        List<string> weaponsToSave = new List<string>();
        foreach (var kvp in weaponInventory)
            for (int i = 0; i < kvp.Value; i++) weaponsToSave.Add(kvp.Key.weaponID);


        // 2. NPC VERİLERİNİ TOPLA

        NPCHealth[] allNPCs = FindObjectsByType<NPCHealth>(FindObjectsSortMode.None);

        Dictionary<string, object> allNpcData = new Dictionary<string, object>();

        foreach (var npcHealth in allNPCs)
        {
            if (npcHealth != null && !string.IsNullOrEmpty(npcHealth.uniqueID))
            {
                Inventory npcInv = npcHealth.GetComponent<Inventory>();

                if (npcInv != null)
                {
                    List<string> npcItems = new List<string>();
                    foreach (var kvp in npcInv.inventory)
                        for (int i = 0; i < kvp.Value; i++) npcItems.Add(kvp.Key.itemID);

                    List<string> npcWeapons = new List<string>();
                    foreach (var kvp in npcInv.inventoryWeapon)
                        for (int i = 0; i < kvp.Value; i++) npcWeapons.Add(kvp.Key.weaponID);

                    // Envanter doluysa listeye ekle
                    if (npcItems.Count > 0 || npcWeapons.Count > 0)
                    {
                        Dictionary<string, object> singleNpcData = new Dictionary<string, object>
                        {
                            { "items", npcItems },
                            { "weapons", npcWeapons }
                        };
                        allNpcData.Add(npcHealth.uniqueID, singleNpcData);
                    }
                }
            }
        }

        // 3. GÖNDER
        Dictionary<string, object> updates = new Dictionary<string, object>
        {
            { "Inventory", itemsToSave },
            { "Weapons", weaponsToSave },
            { "NPC_Inventories", allNpcData }
        };

        await gameRef.SetAsync(updates, SetOptions.MergeAll);
    }

    public async void LoadFromFirebase()
    {
        if (FirebaseAuth.DefaultInstance.CurrentUser == null) return;
        string currentGameId = GameManager.Instance.CurrentGameId;
        if (string.IsNullOrEmpty(currentGameId)) return;

        DocumentReference gameRef = db.Collection("Games").Document(currentGameId);
        DocumentSnapshot snapshot = await gameRef.GetSnapshotAsync();

        if (snapshot.Exists)
        {
            if (snapshot.TryGetValue("Inventory", out object inventoryObj)) { /* ... */ }
            if (snapshot.TryGetValue("Weapons", out object weaponsObj)) { /* ... */ }

            // --- NPC ENVANTERLERİNİ HAFIZAYA ÇEK ---
            if (snapshot.TryGetValue("NPC_Inventories", out object npcObj))
            {
                loadedNPCData.Clear();
                Dictionary<string, object> mainDict = (Dictionary<string, object>)npcObj;

                foreach (var npcPair in mainDict)
                {
                    string npcID = npcPair.Key;
                    Dictionary<string, object> dataContent = (Dictionary<string, object>)npcPair.Value;
                    NPCInventorySaveData saveData = new NPCInventorySaveData();

                    if (dataContent.ContainsKey("items"))
                    {
                        List<object> iList = (List<object>)dataContent["items"];
                        foreach (var item in iList) saveData.itemIDs.Add(item.ToString());
                    }
                    if (dataContent.ContainsKey("weapons"))
                    {
                        List<object> wList = (List<object>)dataContent["weapons"];
                        foreach (var item in wList) saveData.weaponIDs.Add(item.ToString());
                    }
                    loadedNPCData.Add(npcID, saveData);
                }
            }
        }
    }

    public void SetDirty()
    {
        isDirty = true;
    }

    // --- RESTORE FONKSİYONU ---
    public void RestoreNPCInventory(string npcID, Inventory npcInventoryScript)
    {
        if (loadedNPCData.ContainsKey(npcID))
        {
            NPCInventorySaveData data = loadedNPCData[npcID];
            npcInventoryScript.ResetInventory();

            foreach (string id in data.itemIDs)
            {
                ItemData item = allGameItems.Find(x => x.itemID == id);
                if (item != null) npcInventoryScript.AddItem(item);
            }
            foreach (string id in data.weaponIDs)
            {
                WeaponData weapon = allGameWeapons.Find(x => x.weaponID == id);
                if (weapon != null) npcInventoryScript.AddWeapon(weapon);
            }
        }
    }
}

[System.Serializable]
public class NPCInventorySaveData
{
    public List<string> itemIDs = new List<string>();
    public List<string> weaponIDs = new List<string>();
}
