using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Collections.Concurrent;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ChatUIManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform content;              // ScrollView/Viewport/Content
    [SerializeField] private GameObject bubbleLocalPrefab;
    [SerializeField] private GameObject bubbleRemotePrefab;
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Network (asigna UNO por escena)")]
    // UDP (si usas la escena UDP)
    public UDPClient udpClient;
    public UDPServer udpServer;

    // TCP (si usas las escenas TCP)
    public TCPClient tcpClient;
    public TCPServer tcpServer;

    [Header("Burbuja (texto)")]
    [SerializeField] private float maxBubbleWidth = 420f;
    [SerializeField] private float minLineWidth  = 30f;
    [SerializeField] private float edgeMargin    = 16f;
    [SerializeField] private bool  clampToViewport = true;

    [Header("Burbuja (imagen)")]
    [SerializeField] private GameObject imageBubbleLocalPrefab;   // burbuja azul/local
    [SerializeField] private GameObject imageBubbleRemotePrefab;  // burbuja gris/remoto
    [SerializeField] private float maxImageWidth  = 420f;
    [SerializeField] private float minImageWidth  = 120f;
    [SerializeField] private float imageEdgeMargin = 16f;

    // ---------- EVENTOS (opcionales) ----------
    [Serializable] public class StringEvent : UnityEvent<string> {}
    [Header("Events")]
    public StringEvent OnLocalTextSent = new StringEvent();
    public StringEvent OnRemoteTextReceived = new StringEvent();
    public event Action<string> LocalTextSent;
    public event Action<string> RemoteTextReceived;

    // Protocolo simple para imágenes
    private const string IMG_PREFIX = "[img]|";

    // Cola thread-safe de mensajes entrantes (desde hilos de red)
    private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();

    private void OnEnable()
    {
        // UDP
        if (udpClient != null) udpClient.OnMessageReceived += EnqueueIncoming;
        if (udpServer != null) udpServer.OnMessageReceived += EnqueueIncoming;

        // TCP
        if (tcpClient != null) tcpClient.OnMessageReceived += EnqueueIncoming;
        if (tcpServer != null) tcpServer.OnMessageReceived += EnqueueIncoming;
    }

    private void OnDisable()
    {
        if (udpClient != null) udpClient.OnMessageReceived -= EnqueueIncoming;
        if (udpServer != null) udpServer.OnMessageReceived -= EnqueueIncoming;

        if (tcpClient != null) tcpClient.OnMessageReceived -= EnqueueIncoming;
        if (tcpServer != null) tcpServer.OnMessageReceived -= EnqueueIncoming;
    }

    private void EnqueueIncoming(string text) => incoming.Enqueue(text);

    private void Update()
    {
        while (incoming.TryDequeue(out var payload))
        {
            // ¿Imagen?
            if (payload.StartsWith(IMG_PREFIX))
            {
                var parts = payload.Split('|');
                if (parts.Length >= 4)
                {
                    byte[] bytes = Convert.FromBase64String(parts[3]);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, bytes))
                    {
                        ShowIncomingImage(tex);     // burbuja REMOTA (gris)
                        ScrollToBottom();
                        continue;
                    }
                }
            }

            // Texto normal (remoto)
            var b = Instantiate(bubbleRemotePrefab, content);
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = payload;

            FitBubble(b);
            ScrollToBottom();

            OnRemoteTextReceived?.Invoke(payload);
            RemoteTextReceived?.Invoke(payload);
        }
    }

    // ======== TEXTO: enviar ========
    public void OnSendClicked()
    {
        if (!messageInput) return;
        var text = messageInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var b = Instantiate(bubbleLocalPrefab, content);
        var label = b.GetComponentInChildren<TMP_Text>(true);
        if (label) label.text = text;

        FitBubble(b);
        ScrollToBottom();

        OnLocalTextSent?.Invoke(text);
        LocalTextSent?.Invoke(text);

        // Enviar por el stack asignado en esta escena
        if (udpClient != null) udpClient.SendData(text);
        if (udpServer != null) udpServer.SendData(text);
        if (tcpClient != null) tcpClient.SendData(text);
        if (tcpServer != null) tcpServer.SendData(text);

        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    // ======== IMAGEN: botón “Adjuntar” ========
    public void OnPickAndSendImage()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Selecciona una imagen", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(path)) return;

        byte[] fileBytes = System.IO.File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(tex, fileBytes)) return;

        // pinta local (azul)
        ShowLocalImage(tex);
        ScrollToBottom();

        // envía como texto (Base64 con prefijo)
        string payload = EncodeImageToMessage(tex);
        if (udpClient != null) udpClient.SendData(payload);
        if (udpServer != null) udpServer.SendData(payload);
        if (tcpClient != null) tcpClient.SendData(payload);
        if (tcpServer != null) tcpServer.SendData(payload);
#else
        Debug.LogWarning("En build usa un file picker (ej. SimpleFileBrowser/NativeGallery) y luego llama a EncodeImageToMessage(...) con los bytes.");
#endif
    }

    // ========= Mostrar imagen =========
    private void ShowLocalImage(Texture2D tex)
    {
        var prefab = imageBubbleLocalPrefab != null ? imageBubbleLocalPrefab : imageBubbleRemotePrefab;
        if (!prefab) { Debug.LogWarning("Prefabs de imagen no asignados."); return; }

        var go = Instantiate(prefab, content);
        FitImageBubble(go, tex.width, tex.height);
        var raw = go.GetComponentInChildren<RawImage>(true);
        if (raw) raw.texture = tex;
    }

    private void ShowIncomingImage(Texture2D tex)
    {
        var prefab = imageBubbleRemotePrefab != null ? imageBubbleRemotePrefab : imageBubbleLocalPrefab;
        if (!prefab) { Debug.LogWarning("Prefabs de imagen no asignados."); return; }

        var go = Instantiate(prefab, content);
        FitImageBubble(go, tex.width, tex.height);
        var raw = go.GetComponentInChildren<RawImage>(true);
        if (raw) raw.texture = tex;
    }

    // ========= Serializar imagen a Base64 con prefijo =========
    private string EncodeImageToMessage(Texture2D original)
    {
        var tex = original;
        const int maxW = 800;
        if (tex.width > maxW) tex = ResizeTexture(tex, maxW);

        byte[] jpg = tex.EncodeToJPG(70);
        string b64 = Convert.ToBase64String(jpg);
        return $"{IMG_PREFIX}{tex.width}|{tex.height}|{b64}";
    }

    // ========= UTILIDADES DE LAYOUT =========
    private void ScrollToBottom()
    {
        Canvas.ForceUpdateCanvases();
        if (scrollRect) scrollRect.verticalNormalizedPosition = 0f;
    }

    // Texto
    private void FitBubble(GameObject bubbleGO)
    {
        if (!bubbleGO) return;

        RectTransform viewportRT = (scrollRect && scrollRect.viewport)
            ? scrollRect.viewport
            : (content.parent as RectTransform);

        float available = viewportRT ? viewportRT.rect.width : maxBubbleWidth;

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) available -= (vlg.padding.left + vlg.padding.right);
        available -= Mathf.Max(0f, edgeMargin);

        float hardMax = clampToViewport ? Mathf.Min(available, maxBubbleWidth) : maxBubbleWidth;

        var bubbleHLG = bubbleGO.GetComponent<HorizontalLayoutGroup>();
        float innerPad = bubbleHLG != null ? (bubbleHLG.padding.left + bubbleHLG.padding.right) : 0f;

        var text = bubbleGO.GetComponentInChildren<TMP_Text>(true);
        if (!text) return;

        text.enableWordWrapping = true;
        text.overflowMode       = TextOverflowModes.Overflow;
        text.ForceMeshUpdate();

        float naturalWidth = text.preferredWidth;
        float maxLine = Mathf.Max(minLineWidth, hardMax - innerPad);
        float targetLineWidth = Mathf.Clamp(naturalWidth, minLineWidth, maxLine);

        var textLE = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
        textLE.preferredWidth = targetLineWidth;
        textLE.flexibleWidth  = 0f;

        var bubbleLE = bubbleGO.GetComponent<LayoutElement>() ?? bubbleGO.AddComponent<LayoutElement>();
        bubbleLE.preferredWidth = targetLineWidth + innerPad;
        bubbleLE.flexibleWidth  = 0f;
        bubbleLE.minWidth       = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleGO.GetComponent<RectTransform>());
    }

    // Imagen
    private void FitImageBubble(GameObject bubbleGO, int imgW, int imgH)
    {
        if (!bubbleGO) return;

        RectTransform viewportRT = (scrollRect && scrollRect.viewport)
            ? scrollRect.viewport
            : (content.parent as RectTransform);

        float available = viewportRT ? viewportRT.rect.width : maxImageWidth;

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) available -= (vlg.padding.left + vlg.padding.right);
        available -= Mathf.Max(0f, imageEdgeMargin);

        float hardMax = Mathf.Min(available, maxImageWidth);

        var hlg = bubbleGO.GetComponent<HorizontalLayoutGroup>();
        float innerPad = hlg != null ? (hlg.padding.left + hlg.padding.right) : 0f;

        float maxLine = Mathf.Max(minImageWidth, hardMax - innerPad);
        float targetW = Mathf.Clamp(imgW, minImageWidth, maxLine);

        float aspect  = (imgH <= 0 || imgW <= 0) ? 1f : (float)imgH / imgW;
        float targetH = targetW * aspect;

        var raw = bubbleGO.GetComponentInChildren<RawImage>(true);
        var le  = raw.GetComponent<LayoutElement>() ?? raw.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth  = targetW;
        le.preferredHeight = targetH;
        le.flexibleWidth   = 0f;
        le.flexibleHeight  = 0f;

        var rootLE = bubbleGO.GetComponent<LayoutElement>() ?? bubbleGO.AddComponent<LayoutElement>();
        rootLE.preferredWidth = targetW + innerPad;
        rootLE.flexibleWidth  = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleGO.GetComponent<RectTransform>());
    }

    private Texture2D ResizeTexture(Texture2D src, int targetWidth)
    {
        int tw = targetWidth;
        int th = Mathf.RoundToInt(targetWidth * (src.height / (float)src.width));

        var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);

        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }
}
