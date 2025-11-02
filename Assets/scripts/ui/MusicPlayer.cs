using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    private AudioSource audioSource;

    [Range(0f, 1f)]
    public float volume = 1f;

    private const string VolumeKey = "MusicVolume";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = GetComponent<AudioSource>();
            audioSource.loop = true;

            volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            audioSource.volume = volume;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        audioSource.volume = volume;
    }

    public void PlayMusic(AudioClip clip)
    {
        if (audioSource.clip == clip) return;
        audioSource.clip = clip;
        audioSource.Play();
    }

    public void StopMusic()
    {
        audioSource.Stop();
    }

    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        audioSource.volume = volume;
        PlayerPrefs.SetFloat(VolumeKey, volume);
    }
}
