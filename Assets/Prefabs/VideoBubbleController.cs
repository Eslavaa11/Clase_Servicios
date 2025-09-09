using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class VideoBubbleController : MonoBehaviour
{
    [Header("Refs (puedes arrastrar o se resuelven solos)")]
    public VideoPlayer player;     // En VideoView
    public Image overlay;          // PlayOverlay (Image)
    public Slider progress;        // Opcional
    public TMP_Text timeTxt;       // Opcional

    [Header("Overlay alphas")]
    [Range(0,1f)] public float hoverAlpha  = 0.35f;
    [Range(0,1f)] public float pausedAlpha = 0.65f;
    [Range(0,1f)] public float hiddenAlpha = 0.00f;
    [Tooltip("Duración del fade del overlay (seg)")]
    public float fadeSeconds = 0.12f;

    CanvasGroup overlayGroup;
    bool pointerInside = false;
    Coroutine fadeCo;

    void Awake()
    {
        // Autoresolver referencias si no están asignadas
        if (!player)  player  = GetComponent<VideoPlayer>();
        if (!overlay)
        {
            var t = transform.parent != null ? transform.parent.Find("PlayOverlay") : null;
            overlay = t ? t.GetComponent<Image>() : null;
        }

        if (!player)
            Debug.LogWarning("[VideoBubbleController] No hay VideoPlayer en VideoView.");

        if (!overlay)
            Debug.LogWarning("[VideoBubbleController] No se encontró PlayOverlay (Image).");

        if (overlay)
        {
            overlayGroup = overlay.GetComponent<CanvasGroup>();
            if (!overlayGroup) overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();

            // Estado inicial: oculto y sin bloquear raycasts
            overlayGroup.alpha = hiddenAlpha;
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable   = false;
        }

        // Ajustes sanos del reproductor
        if (player)
        {
            player.playOnAwake       = true;
            player.waitForFirstFrame = true;
            player.isLooping         = true;
        }
    }

    void Update()
    {
        // Opcional: progreso/tiempo
        if (player && player.frameCount > 0)
        {
            if (progress)
            {
                double d = (player.length > 0.0001) ? player.length : 1.0;
                progress.value = Mathf.Clamp01((float)(player.time / d));
            }

            if (timeTxt)
                timeTxt.text = $"{Fmt(player.time)} / {Fmt(player.length)}";
        }

        // Si está reproduciendo y el puntero NO está encima, mantenemos overlay oculto
        if (player && player.isPlaying && !pointerInside)
            ShowOverlay(hiddenAlpha, block:false, interact:false);
    }

    string Fmt(double seconds)
    {
        int m = Mathf.FloorToInt((float)seconds / 60f);
        int s = Mathf.FloorToInt((float)seconds % 60f);
        return $"{m:00}:{s:00}";
    }

    // --- Click (toggle play/pause)
    public void Toggle()
    {
        if (!player) return;

        if (player.isPlaying)
        {
            player.Pause();
            // En pausa se deja visible para que pueda reanudarse
            ShowOverlay(pausedAlpha, block:true, interact:true);
        }
        else
        {
            player.Play();
            // Si el puntero está encima: tenue; si no, oculto
            if (pointerInside) ShowOverlay(hoverAlpha,  block:true,  interact:true);
            else               ShowOverlay(hiddenAlpha, block:false, interact:false);
        }
    }

    // Mantengo estos métodos para que puedan llamarse desde EventTrigger si alguna vez lo usas
    public void TogglePlay() => Toggle();

    // --- Hover in/out (los llama el forwarder)
    public void HoverEnter(BaseEventData _)
    {
        pointerInside = true;
        if (!player) return;

        float a = player.isPlaying ? hoverAlpha : pausedAlpha;
        ShowOverlay(a, block:true, interact:true);
    }

    public void HoverExit(BaseEventData _)
    {
        pointerInside = false;
        if (!player) return;

        if (player.isPlaying) ShowOverlay(hiddenAlpha, block:false, interact:false);
        else                  ShowOverlay(pausedAlpha, block:true,  interact:true);
    }

    void ShowOverlay(float alpha, bool block, bool interact)
    {
        if (!overlayGroup) return;

        if (fadeCo != null) StopCoroutine(fadeCo);
        fadeCo = StartCoroutine(FadeTo(alpha));

        overlayGroup.blocksRaycasts = block;
        overlayGroup.interactable   = interact;
    }

    IEnumerator FadeTo(float a)
    {
        float from = overlayGroup.alpha;
        float t = 0f;
        float dur = Mathf.Max(0.01f, fadeSeconds);

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            overlayGroup.alpha = Mathf.Lerp(from, a, t);
            yield return null;
        }
        overlayGroup.alpha = a;
    }
}
