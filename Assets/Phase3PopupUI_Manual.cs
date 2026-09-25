#if CMPSETUP_COMPLETE
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class Phase3PopupUI_Manual : MonoBehaviour
{
    [Header("Behaviour")]
    public bool autoBeginOnStart = false;
    public List<MonoBehaviour> disableWhileOpen = new();

    [Header("Scene UI References")]
    [Tooltip("Whole popup canvas (can be the root canvas). Set enabled via Show/Hide.")]
    public Canvas rootCanvas;
    [Tooltip("Black dim overlay over the game. Optional.")]
    public CanvasGroup dimGroup;
    [Tooltip("Window panel RectTransform (the visible modal).")]
    public RectTransform window;
    [Tooltip("ScrollRect that contains the grid of house buttons.")]
    public ScrollRect scroll;
    [Tooltip("The Content Rect under the ScrollRect (should have a GridLayoutGroup).")]
    public RectTransform content;
    [Tooltip("Grid on Content (FixedColumnCount is recommended).")]
    public GridLayoutGroup grid;
    [Tooltip("A disabled Button under the window that we clone for each house.")]
    public Button buttonPrefab;
    [Tooltip("Optional title text at the top.")]
    public Text titleText;
    [Tooltip("Font for button labels (optional). Leave empty to keep the one on the prefab).")]
    public Font overrideFont;
    [Tooltip("Button label font size.")]
    public int buttonFontSize = 24;

    NetworkId _lastActiveGhost;

    // local caches so we can update interactable/label instantly
    readonly Dictionary<int, Button> _btnBySerial = new();
    readonly Dictionary<int, Text> _labelBySerial = new();

    void OnEnable()
    {
        HouseTag.AssignedChanged += OnHouseAssignedChanged;
    }

    void OnDisable()
    {
        HouseTag.AssignedChanged -= OnHouseAssignedChanged;
    }

    void Start()
    {
        // safety: if not set in inspector, try to find the components
        if (!rootCanvas) rootCanvas = GetComponentInParent<Canvas>();
        if (!grid && content) grid = content.GetComponent<GridLayoutGroup>();

        Hide(); // start hidden

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

        // Fallback polling to keep labels/interactables in sync
        if (IsOpen()) RefreshButtonInteractables();
    }

    // === Show / Hide ===
    bool IsOpen() => rootCanvas && rootCanvas.enabled && window && window.gameObject.activeSelf;

    void Show()
    {
        if (rootCanvas) rootCanvas.enabled = true;
        if (dimGroup) { dimGroup.alpha = 1f; dimGroup.blocksRaycasts = true; }
        if (window) window.gameObject.SetActive(true);

        Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
        foreach (var b in disableWhileOpen) if (b) b.enabled = false;

        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

    void Hide()
    {
        if (dimGroup) { dimGroup.alpha = 0f; dimGroup.blocksRaycasts = false; }
        if (window) window.gameObject.SetActive(false);
        if (rootCanvas) rootCanvas.enabled = false;

        Cursor.visible = false; Cursor.lockState = CursorLockMode.Locked;
        foreach (var b in disableWhileOpen) if (b) b.enabled = true;

        ClearButtons();
    }

    // === Buttons ===
    void ClearButtons()
    {
        _btnBySerial.Clear();
        _labelBySerial.Clear();

        if (!content) return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (!ReferenceEquals(child.gameObject, buttonPrefab ? buttonPrefab.gameObject : null))
                Destroy(child.gameObject);
        }
    }

    void RebuildButtons()
    {
        if (!content || !buttonPrefab)
        {
            Debug.LogWarning("[Phase3PopupUI_Manual] Missing Content or ButtonPrefab refs.");
            return;
        }

        ClearButtons();

        var houses = FindObjectsOfType<HouseTag>();
        if (houses.Length == 0) return;

        // ensure we have enough auto points on *this* client too (so any UI code that reads them won't break)
        if (HouseSpawnManager.Instance)
        {
            int maxSerial = houses.Max(h => h.Serial);
            HouseSpawnManager.Instance.EnsurePoints(maxSerial + 1);
        }

        var ordered = houses.OrderBy(h => h.Serial).ToArray();

        foreach (var h in ordered)
        {
            var b = Instantiate(buttonPrefab, content);
            b.gameObject.SetActive(true);

            var text = b.GetComponentInChildren<Text>();
            if (text)
            {
                var baseText = $"House #{h.Serial}";
                text.text = h.IsAssigned ? baseText + " (USED)" : baseText;
                if (overrideFont) text.font = overrideFont;
                if (buttonFontSize > 0) text.fontSize = buttonFontSize;
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

        if (scroll) scroll.verticalNormalizedPosition = 1f;
    }

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
}
#endif
