#if CMPSETUP_COMPLETE
using UnityEngine;
using AvocadoShark; // for PlayerLaneAutoSeat

/// Put this on an empty GameObject where you want the zone.
/// It checks if the LOCAL player's position is inside the box each frame.
/// When true, it calls PlayerLaneAutoSeat to freeze + teleport.
public class SeatZone : MonoBehaviour
{
    [Header("Zone")]
    [Tooltip("Size of the zone box (in world space, oriented with this transform).")]
    public Vector3 boxSize = new Vector3(3f, 2f, 3f);

    [Header("Seating")]
    [Tooltip("If >= 0, overrides the freeze time on PlayerLaneAutoSeat.")]
    public float freezeSecondsOverride = 4f;

    [Tooltip("Seat only once per local player.")]
    public bool oneShotPerPlayer = true;

    [Header("Debug")]
    public bool drawGizmo = true;
    public Color gizmoColor = new Color(0, 0.7f, 1f, 0.2f);

    private PlayerLaneAutoSeat _localSeat;
    private bool _consumed;

    void Update()
    {
        // Find the local player's seater once
        if (_localSeat == null)
        {
            foreach (var seat in FindObjectsOfType<PlayerLaneAutoSeat>())
            {
                if (seat != null && seat.Object && seat.Runner &&
                    seat.Object.InputAuthority == seat.Runner.LocalPlayer)
                {
                    _localSeat = seat;
                    break;
                }
            }
            // No local player yet
            if (_localSeat == null) return;
        }

        if (oneShotPerPlayer && _consumed) return;

        // Is local player inside the oriented box?
        if (IsInsideBox(_localSeat.transform.position))
        {
            if (freezeSecondsOverride >= 0f)
                _localSeat.freezeAfterSeatSeconds = freezeSecondsOverride;

            // Use the robust path in PlayerLaneAutoSeat (freeze -> teleport -> retry)
            _localSeat.SendMessage("SeatNowContext", SendMessageOptions.DontRequireReceiver);

            _consumed = true;
            Debug.Log("[SeatZone] Seated local player via zone.");
        }
    }

    private bool IsInsideBox(Vector3 worldPos)
    {
        // Transform point into this object's local space, then AABB check
        Vector3 local = transform.InverseTransformPoint(worldPos);
        Vector3 half = boxSize * 0.5f;
        return Mathf.Abs(local.x) <= half.x &&
               Mathf.Abs(local.y) <= half.y &&
               Mathf.Abs(local.z) <= half.z;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = gizmoColor;
        Matrix4x4 m = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.matrix = m;
        Gizmos.DrawCube(Vector3.zero, boxSize);
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
    }
}
#endif
