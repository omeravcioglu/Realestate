#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine;
using UBehaviour = UnityEngine.Behaviour;

[RequireComponent(typeof(NetworkObject))]
public class GhostOwnerBillboard : NetworkBehaviour
{
    [Header("Visual")]
    public Vector3 localOffset = new Vector3(0f, 1.9f, 0f);
    [Tooltip("Base world scale when distance = 1m (only used if constantScreenSize is true).")]
    public float scalePerMeter = 0.06f;
    public float minScale = 0.03f;
    public float maxScale = 0.25f;

    [Header("TextMesh Tuning")]
    [Tooltip("High font size + small characterSize = sharper sampling.")]
    public int fontSize = 96;
    public float characterSize = 0.02f;
    public Font font; // optional—leave null to use default dynamic font

    [Header("Behavior")]
    public bool constantScreenSize = true;  // keep readable at distance
    public bool showForAllDuringDebug = false;
    public bool debugLogs = false;

    [Header("Compatibility")]
    public bool disableLegacyLabels = true;

    private GhostRequestTag _tag;
    private Transform _pivot;
    private TextMesh _tm;
    private MeshRenderer _mr;

    public override void Spawned()
    {
        _tag = GetComponent<GhostRequestTag>();
        EnsureText();
        if (disableLegacyLabels) KillLegacyLabels();
        ApplyNow("Spawned");
    }

    private void OnEnable()
    {
        if (_tm == null) EnsureText();
        if (disableLegacyLabels) KillLegacyLabels();
        ApplyNow("OnEnable");
    }

    private void EnsureText()
    {
        if (_pivot != null && _tm != null) return;

        var go = new GameObject("BillboardText");
        go.transform.SetParent(transform, false);
        _pivot = go.transform;
        _pivot.localPosition = localOffset;

        _tm = go.AddComponent<TextMesh>();
        _tm.anchor = TextAnchor.MiddleCenter;
        _tm.alignment = TextAlignment.Center;
        _tm.fontSize = Mathf.Max(32, fontSize);
        _tm.characterSize = Mathf.Max(0.005f, characterSize);
        _tm.color = Color.white;
        _tm.text = "";

        if (font) _tm.font = font; // dynamic font recommended

        _mr = go.GetComponent<MeshRenderer>();
        if (_mr)
        {
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.allowOcclusionWhenDynamic = false;
            if (_tm.font && _tm.font.material && _tm.font.material.mainTexture)
                _tm.font.material.mainTexture.filterMode = FilterMode.Bilinear; // or FilterMode.Point for pixel crisp
        }
    }

    private void KillLegacyLabels()
    {
        foreach (var mb in GetComponents<MonoBehaviour>())
        {
            if (!mb) continue;
            var tn = mb.GetType().Name;
            if (tn == "GhostLabel" || tn == "GhostSpeaker")
            {
                mb.enabled = false;
                if (debugLogs) Debug.Log($"[Billboard] Disabled {tn} on {name}");
            }
        }

        foreach (var b in GetComponentsInChildren<UBehaviour>(true))
        {
            if (!b) continue;
            if (_pivot != null && b.gameObject == _pivot.gameObject) continue;

            // Disable other text components (legacy UI/TMP) if present
            string fn = b.GetType().FullName;
            if (b is TextMesh || fn == "TMPro.TMP_Text" || fn == "TMPro.TextMeshPro" ||
                fn == "TMPro.TextMeshProUGUI" || fn == "UnityEngine.UI.Text")
            {
                b.gameObject.SetActive(false);
            }
        }
    }

    public override void Render()
    {
        if (_pivot == null || _tm == null) return;

        var cam = Camera.main;
        if (cam)
        {
            // face camera
            _pivot.rotation = Quaternion.LookRotation(_pivot.position - cam.transform.position, Vector3.up);

            // keep readable with distance
            if (constantScreenSize)
            {
                float d = Mathf.Max(0.1f, Vector3.Distance(_pivot.position, cam.transform.position));
                float s = Mathf.Clamp(d * scalePerMeter, minScale, maxScale);
                _pivot.localScale = Vector3.one * s;
            }
            else
            {
                _pivot.localScale = Vector3.one; // manual control
            }
        }

        ApplyNow("Render");
    }

    private void ApplyNow(string reason)
    {
        if (_tag == null || _tm == null || Runner == null) return;

        bool isOwner = Runner.LocalPlayer == _tag.Owner;
        bool shouldShow = showForAllDuringDebug || isOwner;

        _tm.text = shouldShow
            ? (!string.IsNullOrEmpty(_tag.Line.ToString())
                ? _tag.Line.ToString()
                : (_tag.AssignmentMode ? "Assign to: (unknown)" : "Find: (unknown)"))
            : "";
    }
}
#endif
