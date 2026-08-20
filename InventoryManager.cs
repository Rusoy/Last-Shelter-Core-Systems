using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Auth;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Item Databases")]
    public List<ItemData> allGameItems;
    public List<WeaponData> allGameWeapons;

    // --- PLAYER INVENTORY ---
    public Dictionary<ItemData, int> inventory = new Dictionary<ItemData, int>();
    public Dictionary<WeaponData, int> weaponInventory = new Dictionary<WeaponData, int>();

    // --- NPC INVENTORY CACHE (loaded once, restored to NPCs on spawn) ---
    public Dictionary<string, NPCInventorySaveData> loadedNPCData = new Dictionary<string, NPCInventorySaveData>();

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
            _ = LoadFromFirebase(); // fire-and-forget; exceptions caught inside
    }

    private void Update()
    {
        if (isDirty && Time.time >= lastSaveTime + SAVE_INTERVAL)
            _ = SaveToFirebase(); // fire-and-forget; exceptions caught inside
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
        if (inventory[item] <= 0) inventory.Remove(item);
        isDirty = true;
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

    // --- FIREBASE OPERATIONS ---

    public async System.Threading.Tasks.Task SaveToFirebase()
    {
        try
        {
            if (FirebaseAuth.DefaultInstance.CurrentUser == null) return;

            string currentGameId = GameManager.Instance.CurrentGameId;
            if (string.IsNullOrEmpty(currentGameId)) return;

            // Set optimistically before the await so that rapid pickups during a slow write
            // don't queue a redundant second save. Accepted tradeoff: changes made during
            // an in-flight write won't be caught until the next 10-second interval.
            isDirty = false;
            lastSaveTime = Time.time;

            DocumentReference gameRef = db.Collection("Games").Document(currentGameId);

            // 1. Player inventory
            List<string> itemsToSave = new List<string>();
            foreach (var kvp in inventory)
                for (int i = 0; i < kvp.Value; i++) itemsToSave.Add(kvp.Key.itemID);

            List<string> weaponsToSave = new List<string>();
            foreach (var kvp in weaponInventory)
                for (int i = 0; i < kvp.Value; i++) weaponsToSave.Add(kvp.Key.weaponID);

            // 2. Collect NPC inventories from all active NPCs in the scene
            NPCHealth[] allNPCs = FindObjectsByType<NPCHealth>(FindObjectsSortMode.None);
            Dictionary<string, object> allNpcData = new Dictionary<string, object>();

            foreach (var npcHealth in allNPCs)
            {
                if (npcHealth == null || string.IsNullOrEmpty(npcHealth.uniqueID)) continue;

                Inventory npcInv = npcHealth.GetComponent<Inventory>();
                if (npcInv == null) continue;

                List<string> npcItems = new List<string>();
                foreach (var kvp in npcInv.inventory)
                    for (int i = 0; i < kvp.Value; i++) npcItems.Add(kvp.Key.itemID);

                List<string> npcWeapons = new List<string>();
                foreach (var kvp in npcInv.inventoryWeapon)
                    for (int i = 0; i < kvp.Value; i++) npcWeapons.Add(kvp.Key.weaponID);

                if (npcItems.Count > 0 || npcWeapons.Count > 0)
                {
                    allNpcData.Add(npcHealth.uniqueID, new Dictionary<string, object>
                    {
                        { "items", npcItems },
                        { "weapons", npcWeapons }
                    });
                }
            }

            // 3. Write to Firestore
            Dictionary<string, object> updates = new Dictionary<string, object>
            {
                { "Inventory", itemsToSave },
                { "Weapons", weaponsToSave },
                { "NPC_Inventories", allNpcData }
            };

            await gameRef.SetAsync(updates, SetOptions.MergeAll);
        }
        catch (Exception e)
        {
            Debug.LogError($"[InventoryManager] SaveToFirebase failed: {e}");
            isDirty = true; // Re-mark dirty so the next interval retries.
        }
    }

    public async System.Threading.Tasks.Task LoadFromFirebase()
    {
        try
        {
            if (FirebaseAuth.DefaultInstance.CurrentUser == null) return;
            string currentGameId = GameManager.Instance.CurrentGameId;
            if (string.IsNullOrEmpty(currentGameId)) return;

            DocumentSnapshot snapshot = await db.Collection("Games").Document(currentGameId).GetSnapshotAsync();
            if (!snapshot.Exists) return;

            // 1. Restore player items
            if (snapshot.TryGetValue("Inventory", out object inventoryObj))
            {
                var savedItems = inventoryObj as List<object>;
                if (savedItems != null)
                    foreach (var itemIdObj in savedItems)
                    {
                        ItemData item = allGameItems.Find(x => x.itemID == itemIdObj.ToString());
                        if (item != null) AddItem(item);
                    }
            }

            // 2. Restore player weapons
            if (snapshot.TryGetValue("Weapons", out object weaponsObj))
            {
                var savedWeapons = weaponsObj as List<object>;
                if (savedWeapons != null)
                    foreach (var weaponIdObj in savedWeapons)
                    {
                        WeaponData weapon = allGameWeapons.Find(x => x.weaponID == weaponIdObj.ToString());
                        if (weapon != null) AddWeapon(weapon);
                    }
            }

            // 3. Cache NPC inventories (applied to each NPC when it spawns via RestoreNPCInventory)
            if (snapshot.TryGetValue("NPC_Inventories", out object npcObj))
            {
                var mainDict = npcObj as Dictionary<string, object>;
                if (mainDict != null)
                {
                    loadedNPCData.Clear();
                    foreach (var npcPair in mainDict)
                    {
                        var dataContent = npcPair.Value as Dictionary<string, object>;
                        if (dataContent == null) continue;

                        NPCInventorySaveData saveData = new NPCInventorySaveData();

                        if (dataContent.ContainsKey("items"))
                        {
                            var iList = dataContent["items"] as List<object>;
                            if (iList != null)
                                foreach (var item in iList) saveData.itemIDs.Add(item.ToString());
                        }
                        if (dataContent.ContainsKey("weapons"))
                        {
                            var wList = dataContent["weapons"] as List<object>;
                            if (wList != null)
                                foreach (var item in wList) saveData.weaponIDs.Add(item.ToString());
                        }
                        loadedNPCData.Add(npcPair.Key, saveData);
                    }
                }
            }

            // AddItem/AddWeapon above set isDirty = true; reset so we don't
            // immediately trigger a redundant write back to Firestore.
            isDirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[InventoryManager] LoadFromFirebase failed: {e}");
        }
    }

    public void SetDirty()
    {
        isDirty = true;
    }

    // --- RESTORE ---
    // Called by each NPC on Start to recover its saved inventory
    public void RestoreNPCInventory(string npcID, Inventory npcInventoryScript)
    {
        if (!loadedNPCData.ContainsKey(npcID)) return;

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

[System.Serializable]
public class NPCInventorySaveData
{
    public List<string> itemIDs = new List<string>();
    public List<string> weaponIDs = new List<string>();
}
