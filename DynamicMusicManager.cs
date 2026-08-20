using UnityEngine;

public class DynamicMusicManager : MonoBehaviour
{
    public static DynamicMusicManager Instance;

    [Header("Audio Sources")]
    public AudioSource baseMusic;
    public AudioSource tensionMusic;
    public AudioSource panicMusic;
    public AudioSource sfxSource;

    [Header("Stinger Clips")]
    public AudioClip warningStinger; // plays at ~33% time remaining
    public AudioClip finalStinger;   // plays at ~11% time remaining
    public AudioClip tickSound;      // plays each second in the final 10 seconds

    private bool playedWarning = false;
    private bool playedFinal = false;
    private int lastTickSecond = -1;

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void UpdateMusicState(float currentSeconds, float totalSeconds)
    {
        if (totalSeconds <= 0) return;

        float percentage = (currentSeconds / totalSeconds) * 100f;

        // Music state machine — evaluated from high to low percentage for clarity.
        if (percentage > 66.6f)
        {
            FadeOut(tensionMusic);
            FadeOut(panicMusic);
            ResetFlags();
        }
        else if (percentage > 33.3f)
        {
            FadeIn(tensionMusic, 0.4f);
            FadeOut(panicMusic);
        }
        else if (percentage > 11.1f)
        {
            FadeIn(tensionMusic, 1.0f);
            if (!playedWarning)
            {
                PlaySFX(warningStinger);
                playedWarning = true;
            }
        }
        else if (percentage > 6f)
        {
            FadeIn(tensionMusic, 1.0f);
            FadeIn(panicMusic, 0.5f);
        }
        else if (percentage > 0f)
        {
            FadeIn(panicMusic, 1.0f);
            if (!playedFinal)
            {
                PlaySFX(finalStinger);
                playedFinal = true;
            }
        }

        // Tick countdown — checked independently so it fires regardless of which
        // music state is active (previously was unreachable inside the else-if chain).
        int currentIntSecond = Mathf.CeilToInt(currentSeconds);
        if (currentIntSecond <= 10 && currentSeconds > 0 && currentIntSecond != lastTickSecond)
        {
            PlaySFX(tickSound);
            lastTickSecond = currentIntSecond;
        }
    }

    private void FadeIn(AudioSource source, float targetVol)
    {
        if (source != null)
            source.volume = Mathf.Lerp(source.volume, targetVol, Time.deltaTime * 2f);
    }

    private void FadeOut(AudioSource source)
    {
        if (source != null)
            source.volume = Mathf.Lerp(source.volume, 0f, Time.deltaTime * 2f);
    }

    private void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null) sfxSource.PlayOneShot(clip);
    }

    public void ResetFlags()
    {
        playedWarning = false;
        playedFinal = false;
        lastTickSecond = -1;
    }
}
