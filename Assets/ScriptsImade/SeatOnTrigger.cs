#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SeatOnTrigger : MonoBehaviour
{
    [Tooltip("Freeze duration after seating. < 0 uses PlayerLaneAutoSeat's current value.")]
    public float freezeSecondsOverride = -1f;

    [Tooltip("Seat only once per local player.")]
    public bool oneShotPerPlayer = true;

    private bool _consumed;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        // Helpful defaults if you add a Rigidbody here
        var rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (oneShotPerPlayer && _consumed) return;

        // Find player’s seater on the entering object (or its parents)
        var seat = other.GetComponentInParent<AvocadoShark.PlayerLaneAutoSeat>();
        if (!seat) return;

        // Only seat the LOCAL player on this client
        if (seat.Object == null || seat.Runner == null) return;
        if (seat.Object.InputAuthority != seat.Runner.LocalPlayer) return;

        if (freezeSecondsOverride >= 0f)
            seat.freezeAfterSeatSeconds = freezeSecondsOverride;

        seat.SendMessage("SeatNowContext", SendMessageOptions.DontRequireReceiver);

        _consumed = true;
        Debug.Log("[SeatOnTrigger] Seated local player via trigger.");
    }
}
#endif
