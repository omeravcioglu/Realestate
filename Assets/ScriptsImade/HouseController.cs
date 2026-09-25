using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class HouseController : NetworkBehaviour
{
    [Header("Networked State")]
    [Networked] public int ElementId { get; set; } = -1;
    [Networked] public NetworkId ClaimedByGhost { get; set; }

    [Header("Events (optional wiring in Inspector)")]
    public UnityEvent<int> OnElementAssignedId;
    public UnityEvent<string> OnElementAssignedName;
    public UnityEvent OnElementCleared;

    private int _appliedElemId = int.MinValue; // last applied
    private string _cachedName;

    public override void Spawned()
    {
        // Apply once on spawn (will no-op if ElementId is still default)
        ApplyIfChanged();
    }

    // Works on all peers without OnChanged/Changed<>, and with any Fusion version.
    public override void Render()
    {
        ApplyIfChanged();
    }

    private void ApplyIfChanged()
    {
        if (_appliedElemId == ElementId) return;
        _appliedElemId = ElementId;

        var lib = Object ? FindObjectOfType<ScaryElementLibrary>() : null;
        _cachedName = (lib && ElementId >= 0 && ElementId < lib.elements.Count)
            ? lib.elements[ElementId]
            : $"Element#{ElementId}";

        if (ElementId >= 0)
        {
            OnElementAssignedId?.Invoke(ElementId);
            OnElementAssignedName?.Invoke(_cachedName);
        }
        else
        {
            OnElementCleared?.Invoke();
        }

        // Optional loose coupling to your HouseDefinition
        gameObject.SendMessage("OnHouseElementAssignedId", ElementId, SendMessageOptions.DontRequireReceiver);
        gameObject.SendMessage("OnHouseElementAssignedName", _cachedName, SendMessageOptions.DontRequireReceiver);
        if (ElementId < 0)
            gameObject.SendMessage("OnHouseElementCleared", SendMessageOptions.DontRequireReceiver);
    }

    // Helper if you ever want to clear manually
    public void ClearElement()
    {
        ElementId = -1;
        ClaimedByGhost = default; // <-- instead of NetworkId.Invalid
                                  // Apply on next Render() automatically
    }

}
