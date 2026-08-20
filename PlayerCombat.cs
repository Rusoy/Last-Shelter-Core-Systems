using UnityEngine;

public class PlayerCombat : PlayerBehaviour
{
    private float baseDamage;
    private bool isAttacking = false;

    private float nextAttackTime = 0f;

    // Combat animations live on layer 2; defined as a constant so magic numbers
    // don't scatter across the class if this ever changes.
    private const int COMBAT_LAYER = 2;
    private int currentAnimationLayer = COMBAT_LAYER;

    // Pre-allocated buffer — OverlapSphereNonAlloc fills this in-place, avoiding heap allocations per frame.
    // Buffer size of 10 is intentional; combat isn't designed for more than 10 simultaneous targets.
    private Collider[] hitResults = new Collider[10];

    public override void PlayerAct() { }

    protected override void Awake()
    {
        base.Awake();
        baseDamage = playerStats.attack;

        if (playerAnimator != null)
            playerAnimator.speed = 1f;
    }

    public void StartAttack(AttackData attackToUse)
    {
        if (isAttacking || Time.time < nextAttackTime)
            return;

        isAttacking = true;
        currentAttack = attackToUse;

        int animIndex = Random.Range(0, currentAttack.attackName.Length);
        playerAnimator.Play(currentAttack.attackName[animIndex], currentAnimationLayer, 0f);

        nextAttackTime = Time.time + currentAttack.cooldown;

        // Failsafe: force-unlock the attack state if the animation event is swallowed by Unity
        float backupTimer = currentAttack.animationDuration > 0.2f ? currentAttack.animationDuration : 1.5f;
        Invoke(nameof(ResetAttackState), backupTimer);
    }

    // Called by animation event at the exact moment of impact
    public void TriggerHit()
    {
        if (currentAttack == null) return;

        Vector3 origin = transform.position + transform.forward * 0.5f;
        int hitCount = Physics.OverlapSphereNonAlloc(origin, currentAttack.range, hitResults, LayerMask.GetMask("NPC"));

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitResults[i];
            if (hit.CompareTag("AttackableNPC"))
            {
                if (hit.TryGetComponent(out NPCHealth health))
                    health.TakeDamage(baseDamage + currentAttack.damage, false);
            }
        }
    }

    // Called by animation event OR the failsafe Invoke — whichever fires first
    public void ResetAttackState()
    {
        isAttacking = false;
        currentAttack = null;
        CancelInvoke(nameof(ResetAttackState));
    }

    public void SetAnimationLayer(int layerIndex)
    {
        if (layerIndex >= 0 && layerIndex < playerAnimator.layerCount)
            currentAnimationLayer = layerIndex;
    }

    private AttackData currentAttack;
}
