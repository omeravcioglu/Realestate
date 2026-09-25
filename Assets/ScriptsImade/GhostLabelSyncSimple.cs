#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine;

/// Optional helper that writes the replicated Line into a GhostLabel.
/// Safe to keep even if you also use the Billboard (disable GhostLabel on the prefab to avoid duplicate text).
[RequireComponent(typeof(NetworkObject))]
public class GhostLabelSyncSimple : NetworkBehaviour
{
    [Header("Debug")]
    public bool showForAllDuringDebug = false;
    public bool debugLogs = false;

    private GhostRequestTag _tag;
    private GhostLabel _label;
    private bool _inited;

    public override void Spawned()
    {
        _tag = GetComponent<GhostRequestTag>();
        _label = GetComponent<GhostLabel>(); // may be disabled/null
        _inited = true;
        Apply("Spawned");
    }

    private void OnEnable()
    {
        if (_inited) Apply("OnEnable");
    }

    public override void Render()
    {
        Apply("Render");
    }

    private void Apply(string reason)
    {
        // If there's no label or it's disabled, nothing to do (billboard can still show the Line)
        if (Runner == null || _tag == null || _label == null || !_label.isActiveAndEnabled)
            return;

        // Owner gating
        bool isOwner = Runner.LocalPlayer == _tag.Owner;
        bool shouldShow = showForAllDuringDebug || isOwner;

        // Some GhostLabel implementations use AssignedTo internally
        _label.AssignedTo = _tag.Owner;

        if (!shouldShow)
        {
            _label.TextIndex = -1; // hide
            return;
        }

        string line = _tag.Line.ToString();
        if (string.IsNullOrEmpty(line))
            line = _tag.AssignmentMode ? "Assign to: (unknown)" : "Find: (unknown)";

        // Overwrite the label text every frame
        _label.candidateTexts = new[] { line };
        _label.TextIndex = 0;

        if (debugLogs)
            Debug.Log($"[GhostLabelSyncSimple] {reason}: '{line}' Local={Runner.LocalPlayer} Owner={_tag.Owner}");
    }
}
#endif
