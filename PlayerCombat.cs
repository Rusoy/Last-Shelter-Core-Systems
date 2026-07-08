using System.Collections;
using UnityEngine;

public class PlayerCombat : PlayerBehaviour
{
    private float baseDamage;
    private bool isAttacking = false;

    // Cooldown için Coroutine yerine çok daha hafif olan float (zaman) takibi kullanıyoruz
    private float nextAttackTime = 0f;

    private int currentAnimationLayer = 2;
    private AttackData currentAttack; // O an yapılan saldırının verisini tutar

    // Fizik aramaları için önceden oluşturulmuş boş küme (Sıfır Garbage Collection üretir!)
    private Collider[] hitResults = new Collider[10];

    public override void PlayerAct() { }
    // Base classtan gelen zorunlu metot. Bu sınıfta eylem mantığı animasyon eventleri ile yönetiliyor.

    protected override void Awake()
    {
        base.Awake();
        baseDamage = playerStats.attack;

        if (playerAnimator != null)
        {
            playerAnimator.speed = 1f;
        }
    }
    public void StartAttack(AttackData attackToUse)
    {
        // Karakter saldırıyorsa veya bekleme süresindeyse iptal et
        if (isAttacking || Time.time < nextAttackTime)
            return;

        isAttacking = true;
        currentAttack = attackToUse;

        // Rastgele animasyonu seç ve oynat
        int a = Random.Range(0, currentAttack.attackName.Length);
        playerAnimator.Play(currentAttack.attackName[a], currentAnimationLayer, 0f);

        // Cooldown süresini ayarla
        nextAttackTime = Time.time + currentAttack.cooldown;

        // --- GÜVENLİK KİLİDİ (FAILSAFE) ---
        // Unity event'i yutarsa diye, AttackData içindeki animasyon süresi kadar (veya varsayılan 1.5 sn) sonra kilidi zorla açıyoruz.
        float backupTimer = currentAttack.animationDuration > 0.2f ? currentAttack.animationDuration : 1.5f;
        Invoke(nameof(ResetAttackState), backupTimer);
    }

    // 2. ADIM: Animasyon Eventi Burayı Çağırır! (Tam kılıcın/yumruğun değdiği an)
    public void TriggerHit()
    {
        if (currentAttack == null) return;

        Vector3 origin = transform.position + transform.forward * 0.5f;

        // OPTİMİZASYON: OverlapSphereNonAlloc önceden yarattığımız hitResults dizisini doldurur. 
        // Hafızada yeni dizi yaratmaz, çok hızlıdır.
        int hitCount = Physics.OverlapSphereNonAlloc(origin, currentAttack.range, hitResults, LayerMask.GetMask("NPC"));

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hitResults[i];

            if (hit.CompareTag("AttackableNPC"))
            {
                if (hit.TryGetComponent(out NPCHealth health))
                {
                    health.TakeDamage(baseDamage + currentAttack.damage, false);
                }
            }
                
        }
    }

    // Animasyon Event'i VEYA Failsafe (Invoke) burayı çağırır
    public void ResetAttackState()
    {
        isAttacking = false;
        currentAttack = null;

        // Eğer event başarıyla çalıştıysa, arkadan gelen Invoke'u iptal et ki çifte tetiklenme olmasın
        CancelInvoke(nameof(ResetAttackState));
    }

    public void SetAnimationLayer(int layerIndex)
    {
        if (layerIndex >= 0 && layerIndex < playerAnimator.layerCount)
        {
            currentAnimationLayer = layerIndex;
        }
    }
}
