using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Events;
using System;
using System.Collections.Concurrent;
using System.IO;
using UnityEngine.Video;

#if UNITY_EDITOR
using UnityEditor; // OpenFilePanel
#endif

public class ChatUIManager : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform content;              // ScrollView/Viewport/Content
    [SerializeField] private GameObject bubbleLocalPrefab;   // texto (local)
    [SerializeField] private GameObject bubbleRemotePrefab;  // texto (remoto)
    [SerializeField] private TMP_InputField messageInput;
    [SerializeField] private ScrollRect scrollRect;

    [Header("Network (asigna UNO por escena)")]
    public UDPClient udpClient;
    public UDPServer udpServer;
    public TCPClient tcpClient;
    public TCPServer tcpServer;

    [Header("Burbuja (texto)")]
    [SerializeField] private float maxBubbleWidth = 420f;
    [SerializeField] private float minLineWidth  = 30f;
    [SerializeField] private float edgeMargin    = 16f;
    [SerializeField] private bool  clampToViewport = true;

    [Header("Burbuja (imagen)")]
    [SerializeField] private GameObject imageBubbleLocalPrefab;   // azul/local
    [SerializeField] private GameObject imageBubbleRemotePrefab;  // gris/remoto
    [SerializeField] private float maxImageWidth  = 420f;
    [SerializeField] private float minImageWidth  = 120f;
    [SerializeField] private float imageEdgeMargin = 16f;

    [Header("Burbuja (adjunto genérico)")]
    [SerializeField] private GameObject attachmentBubbleLocalPrefab;   // con MediaRoot (Image/Video/Audio) + FileView
    [SerializeField] private GameObject attachmentBubbleRemotePrefab;

    // ---------- EVENTOS (opcionales) ----------
    [Serializable] public class StringEvent : UnityEvent<string> {}
    [Header("Events")]
    public StringEvent OnLocalTextSent = new StringEvent();
    public StringEvent OnRemoteTextReceived = new StringEvent();
    public event Action<string> LocalTextSent;
    public event Action<string> RemoteTextReceived;

    // Protocolo simple
    private const string IMG_PREFIX  = "[img]|";      // [img]|w|h|<base64>
    private const string FILE_PREFIX = "[file]|";     // [file]|<type>|<path>|<name>
    private const string TYPE_IMAGE  = "image";
    private const string TYPE_VIDEO  = "video";
    private const string TYPE_AUDIO  = "audio";
    private const string TYPE_FILE   = "file";

    // Entrantes de red
    private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();

    // ---------------- Subcripción red ----------------
    private void OnEnable() {
        if (udpClient != null) udpClient.OnMessageReceived += EnqueueIncoming;
        if (udpServer != null) udpServer.OnMessageReceived += EnqueueIncoming;
        if (tcpClient != null) tcpClient.OnMessageReceived += EnqueueIncoming;
        if (tcpServer != null) tcpServer.OnMessageReceived += EnqueueIncoming;
    }
    private void OnDisable() {
        if (udpClient != null) udpClient.OnMessageReceived -= EnqueueIncoming;
        if (udpServer != null) udpServer.OnMessageReceived -= EnqueueIncoming;
        if (tcpClient != null) tcpClient.OnMessageReceived -= EnqueueIncoming;
        if (tcpServer != null) tcpServer.OnMessageReceived -= EnqueueIncoming;
    }

    private void EnqueueIncoming(string text) => incoming.Enqueue(text);

    // ---------------- Loop principal ----------------
    private void Update()
    {
        while (incoming.TryDequeue(out var payload))
        {
            // 1) Imagen serializada
            if (payload.StartsWith(IMG_PREFIX)) {
                var parts = payload.Split('|');
                if (parts.Length >= 4) {
                    byte[] bytes = Convert.FromBase64String(parts[3]);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (ImageConversion.LoadImage(tex, bytes)) {
                        ShowIncomingImage(tex);
                        ScrollToBottom();
                        continue;
                    }
                }
            }

            // 2) Adjunto por ruta (video/audio/archivo). Útil para tus pruebas locales.
            if (payload.StartsWith(FILE_PREFIX)) {
                var parts = payload.Split('|'); // [file]|type|path|name
                if (parts.Length >= 4) {
                    var type = parts[1];
                    var path = parts[2];
                    var name = parts[3];

                    if (type == TYPE_VIDEO) {
                        ShowIncomingVideo(path);
                    } else if (type == TYPE_AUDIO) {
                        ShowIncomingAudio(path, name);
                    } else if (type == TYPE_IMAGE) {
                        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                        if (File.Exists(path) && ImageConversion.LoadImage(tex, File.ReadAllBytes(path)))
                            ShowIncomingImage(tex);
                        else
                            ShowIncomingFileCard(path, name);
                    } else {
                        ShowIncomingFileCard(path, name);
                    }
                    ScrollToBottom();
                    continue;
                }
            }

            // 3) Texto normal
            var b = Instantiate(bubbleRemotePrefab, content);
            var label = b.GetComponentInChildren<TMP_Text>(true);
            if (label) label.text = payload;
            FitBubble(b);
            ScrollToBottom();
            OnRemoteTextReceived?.Invoke(payload);
            RemoteTextReceived?.Invoke(payload);
        }
    }

    // ---------------- Texto ----------------
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

        SendOverActiveStack(text);
        messageInput.text = "";
        messageInput.ActivateInputField();
    }

    // ---------------- Botón: Img (SOLO imágenes) ----------------
    public void OnPickAndSendImage()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Selecciona una imagen", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(path)) return;

        byte[] fileBytes = File.ReadAllBytes(path);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(tex, fileBytes)) return;

        ShowLocalImage(tex);
        ScrollToBottom();

        string payload = EncodeImageToMessage(tex);   // [img]|w|h|b64
        SendOverActiveStack(payload);
#else
        Debug.LogWarning("En build usa un file picker (p. ej. SimpleFileBrowser/NativeGallery).");
#endif
    }

    // ---------------- Botón: Adj (cualquier archivo) ----------------
    public void OnPickAndSendAttachment()
    {
#if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("Adjuntar archivo", "", "*");
        if (string.IsNullOrEmpty(path)) return;

        string ext  = Path.GetExtension(path).ToLowerInvariant();
        string name = Path.GetFileName(path);

        // a) Imágenes embebidas
        if (IsImage(ext)) {
            byte[] fileBytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, fileBytes)) return;

            ShowLocalImage(tex);
            ScrollToBottom();

            string payload = EncodeImageToMessage(tex);
            SendOverActiveStack(payload);
            return;
        }

        // b) Video por ruta (ambas UIs locales lo pueden reproducir)
        if (IsVideo(ext)) {
            ShowLocalVideo(path);
            ScrollToBottom();

            string payload = $"{FILE_PREFIX}{TYPE_VIDEO}|{path}|{name}";
            SendOverActiveStack(payload);
            return;
        }

        // c) Audio
        if (IsAudio(ext)) {
            ShowLocalAudio(path, name);
            ScrollToBottom();

            string payload = $"{FILE_PREFIX}{TYPE_AUDIO}|{path}|{name}";
            SendOverActiveStack(payload);
            return;
        }

        // d) Otros: tarjeta genérica
        ShowLocalFileCard(path, name);
        ScrollToBottom();

        string genericPayload = $"{FILE_PREFIX}{TYPE_FILE}|{path}|{name}";
        SendOverActiveStack(genericPayload);
#else
        Debug.LogWarning("En build usa un file picker y manda rutas/bytes según tipo.");
#endif
    }

    private void SendOverActiveStack(string payload)
    {
        if (udpClient != null) udpClient.SendData(payload);
        if (udpServer != null) udpServer.SendData(payload);
        if (tcpClient != null) tcpClient.SendData(payload);
        if (tcpServer != null) tcpServer.SendData(payload);
    }

    // ---------------- Mostrar IMAGEN ----------------
    private void ShowLocalImage(Texture2D tex)    => SpawnImageBubble(true,  tex);
    private void ShowIncomingImage(Texture2D tex) => SpawnImageBubble(false, tex);

    private void SpawnImageBubble(bool local, Texture2D tex)
    {
        var prefab = local ? imageBubbleLocalPrefab : imageBubbleRemotePrefab;
        if (!prefab) { Debug.LogWarning("ImageBubble prefab no asignado."); return; }

        var go  = Instantiate(prefab, content);
        var raw = go.GetComponentInChildren<RawImage>(true);
        if (raw) raw.texture = tex;
        FitImageBubble(go, tex.width, tex.height);
    }

    // ---------------- Mostrar VIDEO ----------------
    private void ShowLocalVideo(string path)    => SpawnVideoBubble(true,  path);
    private void ShowIncomingVideo(string path) => SpawnVideoBubble(false, path);

    private void SpawnVideoBubble(bool local, string path)
    {
        var prefab = local ? attachmentBubbleLocalPrefab : attachmentBubbleRemotePrefab;
        if (!prefab) { Debug.LogWarning("AttachmentBubble prefab no asignado."); return; }

        var go = Instantiate(prefab, content);
        ToggleViews(go, image:false, video:true, audio:false, file:false);

        var videoView = go.transform.Find("Content/MediaRoot/VideoView");
        var raw = videoView.GetComponent<RawImage>();
        if (!raw) raw = videoView.gameObject.AddComponent<RawImage>();

        var vp = videoView.GetComponent<VideoPlayer>();
        if (!vp) vp = videoView.gameObject.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.isLooping   = true;
        vp.source      = VideoSource.Url;
        vp.url         = path;

        // RenderTexture correcto (ancho, alto, depth, formato)
        var rt = new RenderTexture(512, 512, 0, RenderTextureFormat.ARGB32);
        vp.renderMode    = VideoRenderMode.RenderTexture;
        vp.targetTexture = rt;
        raw.texture      = rt;

        vp.prepareCompleted += _ => {
            int w = Mathf.Max(1, (int)vp.width);
            int h = Mathf.Max(1, (int)vp.height);
            FitImageBubble(go, w, h); // reutiliza ajuste de imágenes
            vp.Play();
        };
        vp.Prepare();
    }

    // ---------------- Mostrar AUDIO / FILE CARD ----------------
    private void ShowLocalAudio   (string path, string name) => SpawnFileCard(true,  path, name, TYPE_AUDIO);
    private void ShowIncomingAudio(string path, string name) => SpawnFileCard(false, path, name, TYPE_AUDIO);

    private void ShowLocalFileCard   (string path, string name) => SpawnFileCard(true,  path, name, TYPE_FILE);
    private void ShowIncomingFileCard(string path, string name) => SpawnFileCard(false, path, name, TYPE_FILE);

    private void SpawnFileCard(bool local, string path, string name, string kind)
    {
        var prefab = local ? attachmentBubbleLocalPrefab : attachmentBubbleRemotePrefab;
        if (!prefab) { Debug.LogWarning("AttachmentBubble prefab no asignado."); return; }

        var go = Instantiate(prefab, content);
        ToggleViews(go, image:false, video:false, audio:false, file:true);

        var nameTxt = go.transform.Find("Content/FileView/FileName")?.GetComponent<TMP_Text>();
        if (nameTxt) nameTxt.text = string.IsNullOrEmpty(name) ? Path.GetFileName(path) : name;

        var btn = go.transform.Find("Content/FileView/OpenBtn")?.GetComponent<Button>();
        if (btn) {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => Application.OpenURL("file://" + path));
        }

        // tamaño agradable de tarjeta
        FitImageBubble(go, 640, 360);
    }

    // ---------------- Serializar imagen ----------------
    private string EncodeImageToMessage(Texture2D original)
    {
        var tex = original;
        const int maxW = 800;
        if (tex.width > maxW) tex = ResizeTexture(tex, maxW);

        byte[] jpg = tex.EncodeToJPG(70);
        string b64 = Convert.ToBase64String(jpg);
        return $"{IMG_PREFIX}{tex.width}|{tex.height}|{b64}";
    }

    // ---------------- Utilidades UI ----------------
    private void ScrollToBottom() {
        Canvas.ForceUpdateCanvases();
        if (scrollRect) scrollRect.verticalNormalizedPosition = 0f;
    }

    // Texto
    private void FitBubble(GameObject bubbleGO)
    {
        if (!bubbleGO) return;

        RectTransform viewportRT = (scrollRect && scrollRect.viewport)
            ? scrollRect.viewport : (content.parent as RectTransform);

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

    // Imagen / Video (ancho disponible + aspecto). Funciona con ImageBubble y AttachmentBubble.
    private void FitImageBubble(GameObject bubbleGO, int imgW, int imgH)
    {
        if (!bubbleGO) return;

        RectTransform viewportRT = (scrollRect && scrollRect.viewport)
            ? scrollRect.viewport : (content.parent as RectTransform);

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

        // Busca el RawImage activo (sirve para ImageBubble y para VideoView)
        RawImage raw = null;
        var raws = bubbleGO.GetComponentsInChildren<RawImage>(true);
        foreach (var r in raws)
            if (r.gameObject.activeInHierarchy) { raw = r; break; }
        if (raw == null && raws.Length > 0) raw = raws[0]; // fallback por si está desactivado

        if (raw) {
            var le  = raw.GetComponent<LayoutElement>() ?? raw.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth  = targetW;
            le.preferredHeight = targetH;
            le.flexibleWidth   = 0f;
            le.flexibleHeight  = 0f;
        }

        var rootLE = bubbleGO.GetComponent<LayoutElement>() ?? bubbleGO.AddComponent<LayoutElement>();
        rootLE.preferredWidth = targetW + innerPad;
        rootLE.flexibleWidth  = 0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(bubbleGO.GetComponent<RectTransform>());
    }

    private Texture2D ResizeTexture(Texture2D src, int targetWidth)
    {
        int tw = targetWidth;
        int th = Mathf.RoundToInt(targetWidth * (src.height / (float)src.width));

        var rt = new RenderTexture(tw, th, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, rt);

        var tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    // Helpers
    private static bool IsImage(string ext) => ext==".png"||ext==".jpg"||ext==".jpeg";
    private static bool IsVideo(string ext) => ext==".mp4"||ext==".mov"||ext==".avi"||ext==".mkv"||ext==".webm";
    private static bool IsAudio(string ext) => ext==".wav"||ext==".mp3"||ext==".ogg"||ext==".m4a"||ext==".aac";

    private void ToggleViews(GameObject root, bool image, bool video, bool audio, bool file)
    {
        void Set(string path, bool on){
            var t = root.transform.Find(path);
            if (t) t.gameObject.SetActive(on);
        }
        Set("Content/MediaRoot/ImageView", image);
        Set("Content/MediaRoot/VideoView", video);
        Set("Content/MediaRoot/AudioView", audio);
        Set("Content/FileView",           file);
    }
}
