using UnityEngine;
using UnityEngine.AI;

public abstract class NPCBehaviour : MonoBehaviour
{
    protected NavMeshAgent agent;
    protected NPCStats npcStats;
    protected NPCHealth npcHealth;
    protected NPCAnimation npcAnim;
    protected Inventory inventory;
    protected GameObject player;
    protected CharacterController characterController;
    protected Rigidbody rb;
    protected Transform weaponSpawn;
    protected float time;
    protected AttackData[] attacks;
    protected NPCController NPCController;
    protected Animator npcAnimator;
    protected Animation npcAnimation;
    protected bool isDead;
    protected int weaponCount;
    protected Canvas npcCanvas;

    protected virtual void Awake()
    {
        NPCController = GetComponent<NPCController>();

        // GetComponent can return null if components aren't on this GameObject.
        if (NPCController != null)
            attacks = NPCController.attacks;

        npcCanvas = GetComponentInChildren<Canvas>(true);
        agent = GetComponent<NavMeshAgent>();
        npcStats = GetComponent<NPCStats>();
        npcAnimator = GetComponent<Animator>();
        npcHealth = GetComponent<NPCHealth>();
        npcAnim = GetComponent<NPCAnimation>();
        inventory = GetComponent<Inventory>();

        if (inventory != null)
            weaponSpawn = inventory.weaponSpawn;

        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        player = GameObject.FindWithTag("Player");
    }

    public abstract void Act(); // Overridden per scene
}
