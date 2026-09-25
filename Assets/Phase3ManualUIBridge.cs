#if CMPSETUP_COMPLETE
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class Phase3ManualUIBridge : MonoBehaviour
{
    [Header("Popup root to show/hide")]
    public GameObject popupRoot;

    [System.Serializable]
    public class HouseButton
    {
        public int serial;
        public Button button;
        public Text label; // optional
    }
    public List<HouseButton> buttons = new();

    private Dictionary<int, HouseButton> _bySerial = new();

    void Awake()
    {
        if (popupRoot) popupRoot.SetActive(false);

        _bySerial.Clear();
        foreach (var b in buttons)
        {
            if (b != null) _bySerial[b.serial] = b;
            if (b != null && b.button != null)
            {
                int capture = b.serial;
                b.button.onClick.AddListener(() => OnClick_HouseBySerial(capture));
            }
        }
    }

    void OnEnable()
    {
        AssignmentManager.LocalActiveGhostChanged += OnLocalActiveGhostChanged;
        AssignmentManager.HouseConsumedSerial += OnHouseConsumedSerial;

        // NEW: close panel only when server confirms our click
        AssignmentManager.LocalAssignmentResult += OnLocalAssignmentResult;  // NEW
        RefreshAllButtons();
    }

    void OnDisable()
    {
        AssignmentManager.LocalActiveGhostChanged -= OnLocalActiveGhostChanged;
        AssignmentManager.HouseConsumedSerial -= OnHouseConsumedSerial;

        // NEW
        AssignmentManager.LocalAssignmentResult -= OnLocalAssignmentResult;  // NEW
    }

    public void BeginMine()
    {
        if (AssignmentManager.Instance)
            AssignmentManager.Instance.RpcBeginMyPhase3();
    }

    public void OnClick_HouseBySerial(int serial)
    {
        var house = FindHouseBySerial(serial);
        if (!house)
        {
            Debug.LogWarning($"[Phase3UIBridge] No HouseTag with Serial={serial} found.");
            return;
        }

        if (house.IsAssigned)
        {
            Debug.Log($"[Phase3UIBridge] House {serial} already used.");
            return;
        }

        if (AssignmentManager.Instance)
            AssignmentManager.Instance.RpcTryAssignActiveGhostToHouse(house.Object.Id);

        // CHANGED: do NOT close here; wait for server confirmation
        // if (popupRoot) popupRoot.SetActive(false);
    }

    // NEW: called after server validates our click
    private void OnLocalAssignmentResult(bool ok, NetworkId ghostId, NetworkId houseId)  // NEW
    {
        if (ok && popupRoot) popupRoot.SetActive(false);  // close only on success
        RefreshAllButtons();                              // keep labels/interactables in sync
    }

    private void OnLocalActiveGhostChanged(NetworkId activeGhost)
    {
        bool show = activeGhost.IsValid;
        if (popupRoot) popupRoot.SetActive(show);
        RefreshAllButtons();
    }

    private void OnHouseConsumedSerial(int serial)
    {
        if (_bySerial.TryGetValue(serial, out var hb) && hb.button)
            hb.button.interactable = false;

        if (_bySerial.TryGetValue(serial, out var hb2) && hb2.label)
        {
            var baseText = $"House #{serial}";
            if (!hb2.label.text.EndsWith("(USED)"))
                hb2.label.text = baseText + " (USED)";
        }
    }

    private HouseTag FindHouseBySerial(int serial)
    {
        var all = Object.FindObjectsOfType<HouseTag>();
        return all.FirstOrDefault(h => h.Serial == serial);
    }

    private void RefreshAllButtons()
    {
        var all = Object.FindObjectsOfType<HouseTag>();
        foreach (var h in all)
        {
            if (_bySerial.TryGetValue(h.Serial, out var hb))
            {
                if (hb.label) hb.label.text = h.IsAssigned ? $"House #{h.Serial} (USED)" : $"House #{h.Serial}";
                if (hb.button) hb.button.interactable = !h.IsAssigned;
            }
        }
    }
}
#endif
