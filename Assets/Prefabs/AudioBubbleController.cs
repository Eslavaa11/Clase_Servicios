using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Networking;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class AudioBubbleController : MonoBehaviour
{
    [Header("Refs (asignar en el prefab)")]
    [SerializeField] private AudioSource source;
    [SerializeField] private Button     playButton;
    [SerializeField] private TMP_Text    playButtonLabel;
    [SerializeField] private Slider      progress;
    [SerializeField] private TMP_Text    timeTxt;

    private bool loaded   = false;
    private bool scrubbing = false;

    // --- Ciclo ---
    private void Reset()
    {
        // Auto-wire cómodo si creas el prefab desde cero
        if (!source) source = GetComponent<AudioSource>();
        if (!playButton) playButton = GetComponentInChildren<Button>(true);
        if (!playButtonLabel && playButton) playButtonLabel = playButton.GetComponentInChildren<TMP_Text>(true);
        if (!progress)  progress = GetComponentInChildren<Slider>(true);
        if (!timeTxt)   timeTxt  = GetComponentInChildren<TMP_Text>(true);
    }

    private void Awake()
    {
        if (!source) source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop        = false;

        if (playButton)
        {
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(TogglePlay);
        }
        UpdateLabel();
    }

    private void OnDestroy()
    {
        if (playButton) playButton.onClick.RemoveListener(TogglePlay);
    }

    private void Update()
    {
        if (!source || !source.clip) return;

        if (progress && !scrubbing)
            progress.value = Mathf.Clamp01(source.time / Mathf.Max(0.0001f, source.clip.length));

        if (timeTxt)
            timeTxt.text = $"{Fmt(source.time)} / {Fmt(source.clip.length)}";
    }

    private string Fmt(double s)
    {
        int m = Mathf.FloorToInt((float)s / 60f);
        int ss = Mathf.FloorToInt((float)s % 60f);
        return $"{m:00}:{ss:00}";
    }

    // --- Controles ---
    public void TogglePlay()
    {
        if (!loaded || !source) return;

        if (source.isPlaying) source.Pause();
        else                  source.Play();

        UpdateLabel();
    }

    private void UpdateLabel()
    {
        if (playButtonLabel)
            playButtonLabel.text = (source != null && source.isPlaying) ? "Pausa" : "Reproducir";
    }

    // --- Carga desde ruta ---
    public void LoadFromPath(string path)               { StartCoroutine(CoLoad(path)); }
    public void LoadFromPath(string path, string _name) { LoadFromPath(path); } // Overload para tus llamadas actuales

    private IEnumerator CoLoad(string path)
    {
        if (string.IsNullOrEmpty(path)) yield break;

        string url  = path.StartsWith("file://") ? path : "file://" + path;
        AudioType t = GuessAudioType(System.IO.Path.GetExtension(path));

        using (var req = UnityWebRequestMultimedia.GetAudioClip(url, t))
        {
            yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"Audio load failed: {req.error} ({path})");
                yield break;
            }

            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (!clip) { Debug.LogError("Audio clip nulo"); yield break; }

            if (!source) source = GetComponent<AudioSource>();
            source.clip = clip;

            loaded = true;
            UpdateLabel();
        }
    }

    private AudioType GuessAudioType(string ext)
    {
        ext = (ext ?? "").ToLowerInvariant();
        switch (ext)
        {
            case ".wav":  return AudioType.WAV;
            case ".mp3":  return AudioType.MPEG;      // MP3
            case ".ogg":  return AudioType.OGGVORBIS;
            case ".aiff":
            case ".aif":  return AudioType.AIFF;
            case ".m4a":  return AudioType.MPEG;      // suele funcionar
            default:      return AudioType.UNKNOWN;   // Unity intenta inferir
        }
    }

    // --- Scrub opcional desde el Slider ---
    public void OnSliderChanged(float v)
    {
        if (!scrubbing || !source || !source.clip) return;
        source.time = Mathf.Clamp01(v) * source.clip.length;
    }
    public void BeginScrub() { scrubbing = true; }
    public void EndScrub()
    {
        scrubbing = false;
        OnSliderChanged(progress ? progress.value : 0f);
        UpdateLabel();
    }
}
