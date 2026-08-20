using UnityEngine;
using UnityEngine.AI;

public abstract class PlayerBehaviour : MonoBehaviour
{
    protected NavMeshAgent agent;
    protected PlayerStats playerStats;
    protected PlayerHealth playerHealth;
    protected AnimationScript playerAnim;
    protected InventoryPlayer inventory;
    protected GameObject npc, attackableNPC;
    protected CharacterController characterController;
    protected Rigidbody rb;
    protected Transform weaponSpawn;
    protected PlayerUI playerUI;
    protected bool isDead;
    protected Animator playerAnimator;
    protected Animation playerAnimation;
    protected PlayerMovement playerMovement;
    protected int weaponCount;

    protected virtual void Awake()
    {
        playerAnimator = GetComponent<Animator>();
        playerStats = GetComponent<PlayerStats>();
        playerHealth = GetComponent<PlayerHealth>();

        // GetComponent can return null if the component isn't on this GameObject.
        if (playerStats != null)
            isDead = playerStats.isDead;

        playerAnim = GetComponent<AnimationScript>();
        inventory = GetComponent<InventoryPlayer>();
        characterController = GetComponent<CharacterController>();
        playerMovement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
        playerUI = GetComponent<PlayerUI>();
        npc = GameObject.FindWithTag("NPC");
    }

    public abstract void PlayerAct(); // Overridden per scene
}
