using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Secret "Lighthouse" playlist tab.
///
/// Lists only the Lighthouse-exclusive songs (MusicPlaylist.lighthouseTracks).
/// Toggling a song selects it for playback. Selected Lighthouse songs share the
/// same live shuffle queue as the selected songs from the standard playlist.
/// </summary>
public class LighthouseMenuUI : MonoBehaviour {
    [Header("Panel")]
    public GameObject lighthousePanel;

    [Header("Main Menu Button")]
    public Button lighthouseButton;

    [Header("Track List")]
    public Transform trackListContainer;
    public GameObject trackEntryPrefab;
    public TextMeshProUGUI titleText;

    [Header("Now Playing")]
    public TextMeshProUGUI nowPlayingText;
    public TextMeshProUGUI nowPlayingArtist;
    public Button playStopButton;
    public TextMeshProUGUI playStopButtonText;
    public Button skipButton;

    [Header("Controls")]
    public Button closeButton;
    public Button addSongsButton;
    public TextMeshProUGUI addSongsButtonText;
    public Button enableAllButton;
    public Button disableAllButton;
    public Slider volumeSlider;

    [Header("Password Prompt")]
    public GameObject passwordPanel;
    public TMP_InputField passwordInput;
    public TextMeshProUGUI passwordErrorText;
    public Button passwordSubmitButton;
    public Button passwordCancelButton;

    [Header("Glyph Viewer")]
    public Texture2D glyphTexture;
    public GameObject glyphWindow;

    private const string LighthousePassword = "AlbaMusikCode";

    private readonly List<GameObject> _trackEntries = new List<GameObject>();
    private bool _unlocked;

    void Awake() {
        AutoWire();
    }

    void Start() {
        if (lighthousePanel != null)
            lighthousePanel.SetActive(false);

        if (volumeSlider != null) {
            volumeSlider.value = MusicPlayer.Instance != null
                ? MusicPlayer.Instance.Volume : 0.5f;
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        // Replace any listeners copied from the main playlist panel.
        if (lighthouseButton != null) {
            lighthouseButton.onClick = new Button.ButtonClickedEvent();
            lighthouseButton.onClick.AddListener(ShowPasswordPrompt);
        }

        if (closeButton != null) {
            closeButton.onClick = new Button.ButtonClickedEvent();
            closeButton.onClick.AddListener(CloseLighthouse);
        }

        // The Lighthouse now only lists its own exclusive songs, so the old
        // "Add Songs" picker is no longer needed.
        if (addSongsButton != null)
            addSongsButton.gameObject.SetActive(false);

        if (enableAllButton != null) {
            enableAllButton.onClick = new Button.ButtonClickedEvent();
            enableAllButton.onClick.AddListener(OnEnableAll);
        }

        if (disableAllButton != null) {
            disableAllButton.onClick = new Button.ButtonClickedEvent();
            disableAllButton.onClick.AddListener(OnDisableAll);
        }

        if (playStopButton != null) {
            playStopButton.onClick = new Button.ButtonClickedEvent();
            playStopButton.onClick.AddListener(OnPlayStopClicked);
        }

        if (skipButton != null) {
            skipButton.onClick = new Button.ButtonClickedEvent();
            skipButton.onClick.AddListener(OnSkipClicked);
        }

        EnsurePasswordPanel();
    }

    void Update() {
        if (lighthousePanel != null && lighthousePanel.activeSelf)
            RefreshNowPlaying();
    }

    // ── Open / close ──────────────────────────────────────────────────────────

    public void OpenLighthouse() {
        if (lighthousePanel == null) return;

        lighthousePanel.SetActive(true);
        BuildTrackList();
        RefreshNowPlaying();
    }

    public void CloseLighthouse() {
        if (lighthousePanel != null)
            lighthousePanel.SetActive(false);
    }

    // ── Password gate ─────────────────────────────────────────────────────────

    public void ShowPasswordPrompt() {
        // The password only has to be entered once per play session.
        if (_unlocked) {
            OpenLighthouse();
            return;
        }

        EnsurePasswordPanel();

        if (passwordPanel == null) {
            // If the prompt could not be built, fail open rather than break.
            OpenLighthouse();
            return;
        }

        if (passwordInput != null)
            passwordInput.text = "";

        ClearPasswordError();

        passwordPanel.SetActive(true);

        if (passwordInput != null) {
            passwordInput.ActivateInputField();
            passwordInput.Select();
        }
    }

    public void SubmitPassword() {
        if (passwordInput == null) return;

        string entered = (passwordInput.text ?? "").Trim();

        if (string.Equals(entered, LighthousePassword, System.StringComparison.Ordinal)) {
            _unlocked = true;
            passwordInput.text = "";
            HidePasswordPrompt();
            OpenLighthouse();
            return;
        }

        passwordInput.text = "";

        if (passwordErrorText != null) {
            passwordErrorText.text = "Incorrect password";
            passwordErrorText.gameObject.SetActive(true);
        }

        passwordInput.ActivateInputField();
    }

    public void HidePasswordPrompt() {
        HideGlyphWindow();

        if (passwordPanel != null)
            passwordPanel.SetActive(false);
    }

    void ClearPasswordError() {
        if (passwordErrorText != null && passwordErrorText.gameObject.activeSelf) {
            passwordErrorText.text = "";
            passwordErrorText.gameObject.SetActive(false);
        }
    }

    void EnsurePasswordPanel() {
        if (passwordPanel != null) return;

        var font = GetUiFont();
        var templateImage = addSongsButton != null
            ? addSongsButton.image
            : (lighthouseButton != null ? lighthouseButton.image : null);
        var buttonSprite = templateImage != null ? templateImage.sprite : null;
        var buttonImageType = templateImage != null
            ? templateImage.type : Image.Type.Simple;
        var buttonImageColor = templateImage != null
            ? templateImage.color : new Color(0.42f, 0.22f, 0.58f, 1f);
        var buttonColors = addSongsButton != null
            ? addSongsButton.colors : ColorBlock.defaultColorBlock;

        // Full-screen dimmer behind the prompt.
        passwordPanel = new GameObject("PasswordPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        passwordPanel.transform.SetParent(transform, false);

        var panelRect = passwordPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var dimmer = passwordPanel.GetComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.82f);
        dimmer.raycastTarget = true;

        // Centered prompt box.
        var box = new GameObject("Box",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(passwordPanel.transform, false);

        var boxRect = box.GetComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.sizeDelta = new Vector2(620f, 360f);
        boxRect.anchoredPosition = Vector2.zero;

        var boxImage = box.GetComponent<Image>();
        boxImage.color = new Color(0.07f, 0.05f, 0.11f, 0.98f);
        boxImage.raycastTarget = true;

        CreateLabel(box.transform, "Title", "Lighthouse", font, 44f,
            new Vector2(0f, 122f), new Vector2(560f, 60f), Color.white);

        CreateLabel(box.transform, "Prompt", "Enter password", font, 26f,
            new Vector2(0f, 68f), new Vector2(560f, 36f),
            new Color(0.85f, 0.83f, 0.9f, 1f));

        passwordInput = CreatePasswordInput(box.transform, font);

        passwordErrorText = CreateLabel(box.transform, "Error", "", font, 22f,
            new Vector2(0f, -38f), new Vector2(560f, 30f),
            new Color(1f, 0.35f, 0.35f, 1f));
        passwordErrorText.gameObject.SetActive(false);

        passwordSubmitButton = CreateButton(box.transform, "UnlockButton", "Unlock",
            font, new Vector2(-95f, -112f), new Vector2(170f, 44f),
            buttonSprite, buttonImageType, buttonImageColor, buttonColors);
        passwordSubmitButton.onClick.AddListener(SubmitPassword);

        passwordCancelButton = CreateButton(box.transform, "CancelButton", "Cancel",
            font, new Vector2(95f, -112f), new Vector2(170f, 44f),
            buttonSprite, buttonImageType, buttonImageColor, buttonColors);
        passwordCancelButton.onClick.AddListener(HidePasswordPrompt);

        var viewGlyphButton = CreateButton(box.transform, "ViewGlyphButton", "view glyph",
            font, new Vector2(0f, -158f), new Vector2(180f, 34f),
            buttonSprite, buttonImageType, buttonImageColor, buttonColors);
        viewGlyphButton.onClick.AddListener(ShowGlyphWindow);

        EnsureGlyphWindow();

        passwordPanel.SetActive(false);
    }

    // ── Glyph viewer (Windows 95 style) ──────────────────────────────────────

    public void ShowGlyphWindow() {
        EnsureGlyphWindow();

        if (glyphWindow == null) return;

        glyphWindow.SetActive(true);
        glyphWindow.transform.SetAsLastSibling();
    }

    public void HideGlyphWindow() {
        if (glyphWindow != null)
            glyphWindow.SetActive(false);
    }

    void EnsureGlyphWindow() {
        if (glyphWindow != null) return;
        if (passwordPanel == null) return;

        var font = GetUiFont();
        var raisedBevel = CreateBevelSprite(true);
        var sunkenBevel = CreateBevelSprite(false);
        var titleGradient = CreateGradientSprite(
            new Color32(0, 0, 128, 255),
            new Color32(16, 132, 208, 255));

        // Window root.
        glyphWindow = new GameObject("GlyphWindow",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glyphWindow.transform.SetParent(passwordPanel.transform, false);

        var windowRect = glyphWindow.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(700f, 740f);
        windowRect.anchoredPosition = Vector2.zero;

        var windowImage = glyphWindow.GetComponent<Image>();
        windowImage.sprite = raisedBevel;
        windowImage.type = Image.Type.Sliced;
        windowImage.color = Color.white;

        // Title bar.
        var titleBar = new GameObject("TitleBar",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(DraggableWindow));
        titleBar.transform.SetParent(glyphWindow.transform, false);

        var titleBarRect = titleBar.GetComponent<RectTransform>();
        titleBarRect.anchorMin = new Vector2(0f, 1f);
        titleBarRect.anchorMax = new Vector2(1f, 1f);
        titleBarRect.pivot = new Vector2(0.5f, 1f);
        titleBarRect.sizeDelta = new Vector2(-6f, 27f);
        titleBarRect.anchoredPosition = new Vector2(0f, -3f);

        var titleBarImage = titleBar.GetComponent<Image>();
        titleBarImage.sprite = titleGradient;
        titleBarImage.type = Image.Type.Simple;
        titleBarImage.color = Color.white;
        titleBarImage.raycastTarget = true;

        var draggable = titleBar.GetComponent<DraggableWindow>();
        draggable.target = windowRect;

        var titleLabel = CreateLabel(titleBar.transform, "Title", "AlbaModCypher",
            font, 18f, Vector2.zero, Vector2.zero, Color.white);
        var titleLabelRect = titleLabel.rectTransform;
        titleLabelRect.anchorMin = Vector2.zero;
        titleLabelRect.anchorMax = Vector2.one;
        titleLabelRect.offsetMin = new Vector2(8f, 0f);
        titleLabelRect.offsetMax = new Vector2(-34f, 0f);
        titleLabel.alignment = TextAlignmentOptions.Left;
        titleLabel.fontStyle = FontStyles.Bold;

        // Close button.
        var closeButton = CreateButton(titleBar.transform, "CloseButton", "X",
            font, Vector2.zero, new Vector2(22f, 20f),
            raisedBevel, Image.Type.Sliced, Color.white,
            ColorBlock.defaultColorBlock);
        var closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-4f, 0f);

        var closeLabel = closeButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (closeLabel != null) {
            closeLabel.color = Color.black;
            closeLabel.fontSize = 15f;
        }

        closeButton.onClick.AddListener(HideGlyphWindow);

        // Client area.
        var client = new GameObject("Client",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        client.transform.SetParent(glyphWindow.transform, false);

        var clientRect = client.GetComponent<RectTransform>();
        clientRect.anchorMin = Vector2.zero;
        clientRect.anchorMax = Vector2.one;
        clientRect.offsetMin = new Vector2(6f, 6f);
        clientRect.offsetMax = new Vector2(-6f, -36f);

        var clientImage = client.GetComponent<Image>();
        clientImage.sprite = sunkenBevel;
        clientImage.type = Image.Type.Sliced;
        clientImage.color = Color.white;

        if (glyphTexture != null) {
            var glyphImage = new GameObject("Glyph",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glyphImage.transform.SetParent(client.transform, false);

            var glyphRect = glyphImage.GetComponent<RectTransform>();
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = new Vector2(6f, 6f);
            glyphRect.offsetMax = new Vector2(-6f, -6f);

            var image = glyphImage.GetComponent<Image>();
            image.sprite = Sprite.Create(glyphTexture,
                new Rect(0f, 0f, glyphTexture.width, glyphTexture.height),
                new Vector2(0.5f, 0.5f), 100f);
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
        }
        else {
            var missing = CreateLabel(client.transform, "Missing",
                "AlbaModCypher not found", font, 22f,
                Vector2.zero, new Vector2(500f, 40f),
                new Color(0.2f, 0.2f, 0.2f, 1f));
            missing.alignment = TextAlignmentOptions.Center;
        }

        glyphWindow.SetActive(false);
    }

    Sprite CreateBevelSprite(bool raised) {
        const int size = 8;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        var white = new Color32(255, 255, 255, 255);
        var light = new Color32(223, 223, 223, 255);
        var face = new Color32(192, 192, 192, 255);
        var shadow = new Color32(128, 128, 128, 255);
        var dark = new Color32(64, 64, 64, 255);

        var outerTopLeft = raised ? white : dark;
        var outerBottomRight = raised ? dark : white;
        var innerTopLeft = raised ? light : shadow;
        var innerBottomRight = raised ? shadow : light;

        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                var color = face;

                if (y == size - 1 || x == 0)
                    color = outerTopLeft;
                if (y == 0 || x == size - 1)
                    color = outerBottomRight;
                if (y == size - 2 || x == 1)
                    color = innerTopLeft;
                if (y == 1 || x == size - 2)
                    color = innerBottomRight;

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect,
            new Vector4(2f, 2f, 2f, 2f));
    }

    Sprite CreateGradientSprite(Color32 left, Color32 right) {
        const int width = 64;
        var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < width; x++) {
            float t = x / (float)(width - 1);
            texture.SetPixel(x, 0, Color32.Lerp(left, right, t));
        }

        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, width, 1),
            new Vector2(0.5f, 0.5f), 100f);
    }

    TMP_FontAsset GetUiFont() {
        if (titleText != null && titleText.font != null)
            return titleText.font;

        if (addSongsButton != null) {
            var label = addSongsButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && label.font != null)
                return label.font;
        }

        if (lighthouseButton != null) {
            var label = lighthouseButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && label.font != null)
                return label.font;
        }

        return TMP_Settings.defaultFontAsset;
    }

    TextMeshProUGUI CreateLabel(Transform parent, string name, string text,
        TMP_FontAsset font, float fontSize, Vector2 position, Vector2 size,
        Color color) {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var label = go.GetComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return label;
    }

    TMP_InputField CreatePasswordInput(Transform parent, TMP_FontAsset font) {
        var go = new GameObject("PasswordInput",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(TMP_InputField));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(400f, 50f);
        rect.anchoredPosition = new Vector2(0f, 12f);

        var background = go.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.12f);
        background.raycastTarget = true;

        var textArea = new GameObject("Text Area",
            typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(go.transform, false);

        var textAreaRect = textArea.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(14f, 6f);
        textAreaRect.offsetMax = new Vector2(-14f, -6f);

        var placeholder = CreateLabel(textArea.transform, "Placeholder", "Password",
            font, 26f, Vector2.zero, Vector2.zero,
            new Color(1f, 1f, 1f, 0.4f));
        Stretch(placeholder.rectTransform);
        placeholder.alignment = TextAlignmentOptions.Left;

        var text = CreateLabel(textArea.transform, "Text", "",
            font, 26f, Vector2.zero, Vector2.zero, Color.white);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.Left;

        var input = go.GetComponent<TMP_InputField>();
        input.textViewport = textAreaRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.Password;
        input.characterLimit = 32;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.onValueChanged.AddListener(_ => ClearPasswordError());
        input.onSubmit.AddListener(_ => SubmitPassword());

        return input;
    }

    Button CreateButton(Transform parent, string name, string label,
        TMP_FontAsset font, Vector2 position, Vector2 size,
        Sprite sprite, Image.Type imageType, Color imageColor,
        ColorBlock colors) {
        var go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = imageType;
        image.color = imageColor;
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.colors = colors;

        var text = CreateLabel(go.transform, "Text (TMP)", label, font, 24f,
            Vector2.zero, Vector2.zero, Color.white);
        Stretch(text.rectTransform);

        return button;
    }

    void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ── List building ─────────────────────────────────────────────────────────

    void BuildTrackList() {
        foreach (var entry in _trackEntries)
            if (entry != null) Destroy(entry);
        _trackEntries.Clear();

        if (MusicPlayer.Instance == null) return;

        var playlist = MusicPlayer.Instance.playlist;
        if (playlist == null || playlist.lighthouseTracks == null) return;

        for (int i = 0; i < playlist.lighthouseTracks.Length; i++) {
            var track = playlist.lighthouseTracks[i];
            if (track == null) continue;

            var entryGO = Instantiate(trackEntryPrefab, trackListContainer);

            var nameText = entryGO.transform.Find("TrackName")
                ?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = track.trackName;

            var toggle = entryGO.transform.Find("Toggle")
                ?.GetComponent<Toggle>();
            if (toggle != null) {
                int index = i;

                // Toggle = selected for the shared live playback queue.
                toggle.SetIsOnWithoutNotify(
                    MusicPlayer.Instance.IsLighthouseTrackEnabled(index));
                toggle.onValueChanged.AddListener(val =>
                    MusicPlayer.Instance.SetLighthouseTrackEnabled(index, val));
            }

            _trackEntries.Add(entryGO);
        }
    }

    // ── Now playing ───────────────────────────────────────────────────────────

    void RefreshNowPlaying() {
        if (MusicPlayer.Instance == null) return;

        var track = MusicPlayer.Instance.GetCurrentTrack();

        if (nowPlayingText != null)
            nowPlayingText.text = track != null ? track.trackName : "Nothing playing";

        if (nowPlayingArtist != null)
            nowPlayingArtist.text = track != null ? track.artistName : "";

        if (playStopButtonText != null)
            playStopButtonText.text = MusicPlayer.Instance.IsPlaying ? "Stop" : "Play";
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    void OnPlayStopClicked() {
        if (MusicPlayer.Instance == null) return;

        if (MusicPlayer.Instance.IsPlaying)
            MusicPlayer.Instance.StopPlayback();
        else
            MusicPlayer.Instance.StartPlayback();
    }

    void OnSkipClicked() => MusicPlayer.Instance?.SkipTrack();

    void OnEnableAll() {
        MusicPlayer.Instance?.EnableAllLighthouse();
        BuildTrackList();
    }

    void OnDisableAll() {
        MusicPlayer.Instance?.DisableAllLighthouse();
        BuildTrackList();
    }

    void OnVolumeChanged(float val) => MusicPlayer.Instance?.SetVolume(val);

    // ── Auto wiring ───────────────────────────────────────────────────────────

    void AutoWire() {
        if (lighthousePanel == null)
            lighthousePanel = transform.Find("LighthousePanel")?.gameObject;

        if (lighthouseButton == null)
            lighthouseButton = transform.Find("MainPanel/LighthouseButton")
                ?.GetComponent<Button>();

        if (lighthousePanel == null) return;

        var content = lighthousePanel.transform.Find("Content");
        if (content == null) return;

        if (trackListContainer == null)
            trackListContainer = content.Find("TrackListContainer");

        if (trackEntryPrefab == null)
            trackEntryPrefab = GetComponent<PlaylistMenuUI>()?.trackEntryPrefab;

        if (titleText == null)
            titleText = content.Find("Title")?.GetComponent<TextMeshProUGUI>();

        if (closeButton == null)
            closeButton = content.Find("LeaveButton")?.GetComponent<Button>();

        if (addSongsButton == null) {
            addSongsButton = content.Find("AddSongsButton")?.GetComponent<Button>();
            if (addSongsButton != null)
                addSongsButtonText = addSongsButton
                    .GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (enableAllButton == null)
            enableAllButton = content.Find("EnableAllButton")?.GetComponent<Button>();

        if (disableAllButton == null)
            disableAllButton = content.Find("DisableAllButton")?.GetComponent<Button>();

        if (volumeSlider == null)
            volumeSlider = content.Find("VolumeSlider")?.GetComponent<Slider>();

        var nowPlaying = content.Find("NowPlayingSection");
        if (nowPlaying != null) {
            if (nowPlayingText == null)
                nowPlayingText = nowPlaying.Find("NowPlayingText")
                    ?.GetComponent<TextMeshProUGUI>();

            if (nowPlayingArtist == null)
                nowPlayingArtist = nowPlaying.Find("NowPlayingArtist")
                    ?.GetComponent<TextMeshProUGUI>();

            if (playStopButton == null)
                playStopButton = nowPlaying.Find("PlayButton")?.GetComponent<Button>();

            if (playStopButtonText == null && playStopButton != null)
                playStopButtonText = playStopButton
                    .GetComponentInChildren<TextMeshProUGUI>(true);

            if (skipButton == null)
                skipButton = nowPlaying.Find("SkipButton")?.GetComponent<Button>();
        }
    }
}
