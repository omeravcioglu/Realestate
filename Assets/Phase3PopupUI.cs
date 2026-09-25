#if CMPSETUP_COMPLETE
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class Phase3PopupUI : MonoBehaviour
{
    [Header("Behaviour (optional)")]
    public bool autoBeginOnStart = false;
    public List<MonoBehaviour> disableWhileOpen = new();

    [Header("Look & Feel")]
    public int fontSize = 24;
    public Vector2 windowSize = new Vector2(560, 700);
    public float dimAlpha = 0.7f;

    [Header("Grid Settings")]
    public int columns = 2;
    public Vector2 cellSize = new Vector2(240, 56);
    public Vector2 cellSpacing = new Vector2(8, 8);
    // ints on fields (don’t construct RectOffset in a field)
    public int gridPadLeft = 12, gridPadRight = 12, gridPadTop = 12, gridPadBottom = 12;

    [Header("Font (optional)")]
    public Font overrideFont;

    Canvas _canvas;
    CanvasGroup _dim;
    RectTransform _window;
    RectTransform _content;     // ScrollRect content (grid)
    ScrollRect _scroll;
    Button _buttonPrefab;

    NetworkId _lastActiveGhost;
    Font _uiFont;

    // serial -> button/label for instant updates
    readonly Dictionary<int, Button> _btnBySerial = new();
    readonly Dictionary<int, Text> _labelBySerial = new();

    void OnEnable() { HouseTag.AssignedChanged += OnHouseAssignedChanged; }
    void OnDisable() { HouseTag.AssignedChanged -= OnHouseAssignedChanged; }

    void Start()
    {
        _uiFont = overrideFont ? overrideFont : GetSafeBuiltinUIFont();
        EnsureEventSystem();
        BuildUI();

        if (autoBeginOnStart && AssignmentManager.Instance)
            AssignmentManager.Instance.RpcBeginMyPhase3();
    }

    void Update()
    {
        if (!AssignmentManager.Instance) { Hide(); return; }

        var current = AssignmentManager.Instance.GetActiveGhostForLocalPlayer();
        if (current != _lastActiveGhost)
        {
            _lastActiveGhost = current;
            if (current.IsValid) { Show(); RebuildButtons(); }
            else Hide();
        }

        if (_window && _window.gameObject.activeSelf)
            RefreshButtonInteractables(); // polling fallback so it never goes stale
    }

    // ---------- Events from HouseTag ----------
    void OnHouseAssignedChanged(int serial, bool isAssigned)
    {
        if (_btnBySerial.TryGetValue(serial, out var btn))
            btn.interactable = !isAssigned;

        if (_labelBySerial.TryGetValue(serial, out var label))
        {
            var baseText = $"House #{serial}";
            label.text = isAssigned ? baseText + " (USED)" : baseText;
        }
    }

    // ---------- Safe font picker ----------
    static Font GetSafeBuiltinUIFont()
    {
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (!f) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        return f;
    }

    // Make sure there is an EventSystem so the buttons can be clicked
    static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
        Object.DontDestroyOnLoad(es);
    }

    // ---------- UI building ----------
    void BuildUI()
    {
        var canvasGO = new GameObject("Phase3Popup_Canvas");
        canvasGO.layer = LayerMask.NameToLayer("UI");
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);

        // Dim
        var dimGO = new GameObject("Dim"); dimGO.transform.SetParent(canvasGO.transform, false);
        var dimRT = dimGO.AddComponent<RectTransform>();
        dimRT.anchorMin = Vector2.zero; dimRT.anchorMax = Vector2.one; dimRT.offsetMin = Vector2.zero; dimRT.offsetMax = Vector2.zero;
        var img = dimGO.AddComponent<Image>(); img.color = new Color(0, 0, 0, dimAlpha);
        _dim = dimGO.AddComponent<CanvasGroup>();

        // Window
        var winGO = new GameObject("Window"); winGO.transform.SetParent(canvasGO.transform, false);
        _window = winGO.AddComponent<RectTransform>();
        _window.sizeDelta = windowSize;
        _window.anchorMin = _window.anchorMax = new Vector2(0.5f, 0.5f);
        _window.anchoredPosition = Vector2.zero;

        var winImg = winGO.AddComponent<Image>(); winImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        var layout = winGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 16, 16);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.spacing = 10;

        CreateText(winGO.transform, "Choose a House for this Ghost", fontSize + 8, FontStyle.Bold);

        // --- Scroll Area container (create viewport & content BEFORE adding ScrollRect) ---
        var scrollGO = new GameObject("ScrollArea");
        scrollGO.transform.SetParent(winGO.transform, false);
        scrollGO.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        var scrollLE = scrollGO.AddComponent<LayoutElement>();
        scrollLE.minHeight = windowSize.y - 120;
        scrollLE.flexibleHeight = 1;

        var scrollRT = scrollGO.AddComponent<RectTransform>();

        // Viewport
        var viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        var viewportRT = viewportGO.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero; viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero; viewportRT.offsetMax = Vector2.zero;
        viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        viewportGO.AddComponent<Mask>().showMaskGraphic = false;

        // Content
        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        _content = contentGO.AddComponent<RectTransform>();
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = new Vector2(1, 1);
        _content.pivot = new Vector2(0.5f, 1);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = new Vector2(0, 0);

        var grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = cellSpacing;
        grid.padding = new RectOffset(gridPadLeft, gridPadRight, gridPadTop, gridPadBottom);
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);

        var fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // NOW add ScrollRect and wire it up
        _scroll = scrollGO.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.vertical = true;
        _scroll.movementType = ScrollRect.MovementType.Elastic;
        _scroll.viewport = viewportRT;
        _scroll.content = _content;

        _buttonPrefab = CreateButtonPrefab();
        Hide();
    }

    Text CreateText(Transform parent, string txt, int size, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.text = txt;
        t.font = _uiFont ? _uiFont : GetSafeBuiltinUIFont();
        t.fontSize = size;
        t.fontStyle = style;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        var le = go.AddComponent<LayoutElement>(); le.minHeight = 40;
        return t;
    }

    Button CreateButtonPrefab()
    {
        var go = new GameObject("ButtonPrefab"); go.SetActive(false); go.transform.SetParent(transform, false);
        var img = go.AddComponent<Image>(); img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.27f, 0.27f, 0.27f, 1f);
        colors.pressedColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.6f);
        btn.colors = colors;

        var labelGO = new GameObject("Label"); labelGO.transform.SetParent(go.transform, false);
        var t = labelGO.AddComponent<Text>();
        t.text = "House";
        t.font = _uiFont ? _uiFont : GetSafeBuiltinUIFont();
        t.fontSize = fontSize;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;

        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return btn;
    }

    // ---------- Show/Hide ----------
    void Show()
    {
        if (_dim) { _dim.alpha = 1f; _dim.blocksRaycasts = true; }
        if (_window) _window.gameObject.SetActive(true);
        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        foreach (var b in disableWhileOpen) if (b) b.enabled = false;
        if (_scroll && _scroll.content) _scroll.verticalNormalizedPosition = 1f; // guard

        // force layout so the grid measures correctly and buttons appear immediately
        Canvas.ForceUpdateCanvases();
        if (_scroll) _scroll.verticalNormalizedPosition = 1f;
    }

    void Hide()
    {
        if (_dim) { _dim.alpha = 0f; _dim.blocksRaycasts = false; }
        if (_window) _window.gameObject.SetActive(false);
        Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked;
        foreach (var b in disableWhileOpen) if (b) b.enabled = true;
    }

    // ---------- Buttons ----------
    void ClearButtons()
    {
        _btnBySerial.Clear();
        _labelBySerial.Clear();

        if (!_content) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);
    }

    void RebuildButtons()
    {
        ClearButtons();

        var houses = FindObjectsOfType<HouseTag>();
        var ordered = houses.OrderBy(h => h.Serial).ToArray();

        foreach (var h in ordered)
        {
            var b = Instantiate(_buttonPrefab, _content);
            b.gameObject.SetActive(true);

            var text = b.GetComponentInChildren<Text>();
            if (text)
            {
                var baseText = $"House #{h.Serial}";
                text.text = h.IsAssigned ? baseText + " (USED)" : baseText;
                text.font = _uiFont ? _uiFont : text.font;
                text.fontSize = fontSize;
            }

            b.interactable = !h.IsAssigned;

            _btnBySerial[h.Serial] = b;
            if (text) _labelBySerial[h.Serial] = text;

            var houseId = h.Object.Id; // capture
            b.onClick.AddListener(() =>
            {
                if (AssignmentManager.Instance)
                    AssignmentManager.Instance.RpcTryAssignActiveGhostToHouse(houseId);
                Hide();
            });
        }

        Canvas.ForceUpdateCanvases();
        if (_scroll) _scroll.verticalNormalizedPosition = 1f;
    }

    // Polling fallback so state never desyncs
    void RefreshButtonInteractables()
    {
        var houses = FindObjectsOfType<HouseTag>();
        for (int i = 0; i < houses.Length; i++)
        {
            var h = houses[i];
            if (_btnBySerial.TryGetValue(h.Serial, out var btn))
                btn.interactable = !h.IsAssigned;

            if (_labelBySerial.TryGetValue(h.Serial, out var label))
            {
                var baseText = $"House #{h.Serial}";
                var want = h.IsAssigned ? baseText + " (USED)" : baseText;
                if (label.text != want) label.text = want;
            }
        }
    }
}
#endif
