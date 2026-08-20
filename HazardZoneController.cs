using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class HazardZoneController : MonoBehaviour
{
    [Header("Zone Settings")]
    public Transform shelterDoor;
    public float moveSpeed = 12f;
    private float activationDelay;
    public float damagePerSecond = 40f;
    private float shrinkSpeed = 0.6f;
    private float minRadius = 11f;
    private float damageCheckInterval = 0.25f;

    private float initialRadius;
    private float initialHeight;
    private float time = 0f;
    private float damageTimer = 0f;
    private bool isActive = false;
    private SphereCollider zoneCollider;
    private MeshRenderer zoneRenderer;
    private Dictionary<HazardDamage, bool> playerStatus = new Dictionary<HazardDamage, bool>();
    private static List<HazardDamage> allPlayers = new List<HazardDamage>();

    void Awake()
    {
        // Zone activates 30 seconds before loot phase ends, giving players a warning window
        activationDelay = GameManager.Instance.lootTime - 30f;
        zoneCollider = GetComponent<SphereCollider>();
        initialRadius = transform.localScale.x;
        initialHeight = transform.localScale.y;
        // Calculated so the zone reaches minRadius exactly when the 30-second window ends
        shrinkSpeed = (initialRadius - minRadius) / 30f;
        zoneRenderer = GetComponent<MeshRenderer>();
        zoneRenderer.enabled = false;
    }

    void Update()
    {
        if (!isActive)
        {
            time += Time.deltaTime;
            if (time >= activationDelay)
                ActivateZone();
            return;
        }

        UpdateZoneMovement();
        UpdateDamage();
    }

    void ActivateZone()
    {
        isActive = true;
        zoneRenderer.enabled = true;
        CacheAllPlayers();
    }

    void UpdateZoneMovement()
    {
        // Move toward shelter exit
        transform.position = Vector3.MoveTowards(transform.position, shelterDoor.position, moveSpeed * Time.deltaTime);

        // Shrink the zone radius over time
        float newScale = Mathf.Max(transform.localScale.x - (shrinkSpeed * Time.deltaTime), minRadius);
        transform.localScale = new Vector3(newScale, initialHeight, newScale);

        // SphereCollider.radius is in local space; 0.5 fills a unit sphere at scale 1.
        // Multiplying by the scale ratio keeps it correct as the zone shrinks.
        zoneCollider.radius = 0.5f * (newScale / initialRadius);
    }

    void UpdateDamage()
    {
        damageTimer += Time.deltaTime;
        if (damageTimer >= damageCheckInterval)
        {
            damageTimer = 0f;
            ApplyDamageToOutsidePlayers();
        }
    }

    void CacheAllPlayers()
    {
        allPlayers.Clear();
        var foundPlayers = FindObjectsByType<HazardDamage>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );
        allPlayers.AddRange(foundPlayers);
    }

    void ApplyDamageToOutsidePlayers()
    {
        // Remove stale references left over from a previous scene load.
        allPlayers.RemoveAll(p => p == null);

        foreach (var player in allPlayers)
        {
            if (player.currentHealth <= 0) continue;

            bool isInside = playerStatus.TryGetValue(player, out var inside) && inside;
            if (!isInside)
                player.HazardZoneDamage(damagePerSecond * damageCheckInterval);
        }
    }

    void OnTriggerEnter(Collider other)  => HandlePlayerTrigger(other, true);
    void OnTriggerStay(Collider other)   => HandlePlayerTrigger(other, true);  // Catches players already inside when the zone spawns
    void OnTriggerExit(Collider other)   => HandlePlayerTrigger(other, false);

    void HandlePlayerTrigger(Collider other, bool isInside)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("NPC")) return;

        var player = other.GetComponent<HazardDamage>();
        if (player != null)
            playerStatus[player] = isInside;
    }

    void OnDestroy()
    {
        playerStatus.Clear();
        allPlayers.Clear();
    }

    public static void RegisterPlayer(HazardDamage player)
    {
        if (!allPlayers.Contains(player))
            allPlayers.Add(player);
    }

    public static void UnregisterPlayer(HazardDamage player)
    {
        allPlayers.Remove(player);
    }
}
