using System.Collections;
using Fusion;
using UnityEngine;

/// Drop this on the PLAYER ROOT (same GO as CharacterController).
/// Press the on-screen buttons to seat/freeze the LOCAL player.
/// Uses lane 0 if you only configured one lane; auto-offsets by player index so players don't stack.
public class SeatDoctor : NetworkBehaviour
{
    [Header("Visual UI")]
    public bool showPanel = true;

    [Header("Seating Params")]
    public float fallbackBackDistance = 2.2f;
    public float lookAtHeadHeight = 1.6f;
    public float laneSpacing = 1.6f;      // side offset between players if only lane0 exists
    public float freezeSeconds = 5f;

    private string _status = "(idle)";

    public override void Spawned()
    {
        Log($"Spawned. InputAuth={HasInputAuthority}, StateAuth={HasStateAuthority}");
    }

    void OnGUI()
    {
        if (!showPanel) return;
        if (!HasInputAuthority) return;

        var rect = new Rect(12, 12, 360, 220);
        GUILayout.BeginArea(rect, GUI.skin.box);
        GUILayout.Label("<b>SeatDoctor</b>");
        GUILayout.Label(_status);

        if (GUILayout.Button("1) Dump Environment"))
        {
            DumpEnv();
        }
        if (GUILayout.Button("2) Seat Local Player Now"))
        {
            StartCoroutine(Co_SeatNow());
        }
        if (GUILayout.Button("3) Freeze Movement Only"))
        {
            var f = GetComponent<FreezeMovement>() ?? gameObject.AddComponent<FreezeMovement>();
            f.FreezeForSeconds(freezeSeconds);
            Log($"Freeze for {freezeSeconds:0.0}s");
        }
        if (GUILayout.Button("4) Nudge Right (+0.5m)"))
        {
            var t = transform;
            t.position += Vector3.right * 0.5f;
            Log("Manual nudge right 0.5m");
        }
        GUILayout.EndArea();
    }

    private void DumpEnv()
    {
        var gsm = AvocadoShark.GhostSequenceManager.Instance;
        if (gsm == null)
        {
            Log("GhostSequenceManager.Instance = NULL (is it in scene and spawned?)");
            return;
        }

        int playerCount = 0, myIndex = -1;
        foreach (var p in Runner.ActivePlayers)
        {
            if (p == Runner.LocalPlayer) myIndex = playerCount;
            playerCount++;
        }

        Log($"Players={playerCount}, MyIndex={myIndex}, Lanes={(gsm.lanes != null ? gsm.lanes.Count : 0)}");
        if (gsm.lanes != null && gsm.lanes.Count > 0)
        {
            var l0 = gsm.lanes[0];
            Log($"Lane0: spawn={Exists(l0?.spawnPoint)}, target={Exists(l0?.targetPoint)}, leave={Exists(l0?.leavePoint)}, viewer={Exists(l0?.viewerPoint)}");
        }
    }

    private IEnumerator Co_SeatNow()
    {
        // Freeze first so your controller won't tug you back
        var freezer = GetComponent<FreezeMovement>() ?? gameObject.AddComponent<FreezeMovement>();
        freezer.FreezeForSeconds(freezeSeconds);

        var gsm = AvocadoShark.GhostSequenceManager.Instance;
        if (gsm == null)
        {
            Log("No GhostSequenceManager.Instance. Aborting seat.");
            yield break;
        }
        if (gsm.lanes == null || gsm.lanes.Count == 0 || gsm.lanes[0] == null || gsm.lanes[0].targetPoint == null)
        {
            Log("Lane0 or targetPoint missing. Aborting seat.");
            yield break;
        }

        // Resolve my local player index
        int myIndex = -1, idx = 0;
        foreach (var p in Runner.ActivePlayers)
        {
            if (p == Runner.LocalPlayer) { myIndex = idx; break; }
            idx++;
        }
        if (myIndex < 0)
        {
            Log("Could not find LocalPlayer in Runner.ActivePlayers. Aborting seat.");
            yield break;
        }

        // Choose lane: real one if exists; else virtual offset from lane0 so we don't stack
        var baseLane = gsm.lanes[0];
        var lane = (myIndex < gsm.lanes.Count) ? gsm.lanes[myIndex] : null;

        Vector3 seatPos;
        Quaternion seatRot;

        if (lane != null && lane.viewerPoint != null)
        {
            seatPos = lane.viewerPoint.position;
            Vector3 face = (lane.targetPoint.position - seatPos);
            if (face.sqrMagnitude < 0.0001f) face = lane.targetPoint.forward;
            seatRot = Quaternion.LookRotation(face.sqrMagnitude > 0.0001f ? face.normalized : Vector3.forward, Vector3.up);
            Log($"Seating at REAL viewerPoint of lane {myIndex}");
        }
        else if (lane != null && lane.targetPoint != null)
        {
            Vector3 forward = lane.targetPoint.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = (lane.targetPoint.position - transform.position).normalized;
            seatPos = lane.targetPoint.position - forward.normalized * Mathf.Max(0.3f, fallbackBackDistance);
            Vector3 lookAt = lane.targetPoint.position + Vector3.up * lookAtHeadHeight;
            Vector3 face = lookAt - seatPos;
            seatRot = face.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(face.normalized, Vector3.up) : transform.rotation;
            Log($"Seating at REAL lane {myIndex} (no viewerPoint)");
        }
        else
        {
            // Virtual lane from lane0
            Vector3 fwd = baseLane.targetPoint.forward; if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            Vector3 baseTarget = baseLane.targetPoint.position + right * (laneSpacing * myIndex);
            Vector3 forward = baseLane.targetPoint.forward; if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            seatPos = baseTarget - forward.normalized * Mathf.Max(0.3f, fallbackBackDistance);
            Vector3 lookAt = baseTarget + Vector3.up * lookAtHeadHeight;
            Vector3 face = lookAt - seatPos;
            seatRot = Quaternion.LookRotation(face.normalized, Vector3.up);
            Log($"Seating at VIRTUAL lane (idx={myIndex}, spacing={laneSpacing})");
        }

        // Teleport (inline so you don't need TeleportUtility)
        var cc = GetComponent<CharacterController>();
        if (cc && cc.enabled)
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(seatPos, seatRot);
            cc.enabled = true;
        }
        else
        {
            transform.SetPositionAndRotation(seatPos, seatRot);
        }

        Log($"SEATED at {seatPos} rot {seatRot.eulerAngles}");

        // tiny settle wait (optional)
        yield return new WaitForSeconds(0.1f);
    }

    private void Log(string msg)
    {
        _status = msg;
        Debug.Log($"[SeatDoctor] {msg}");
    }

    private string Exists(Transform t) => t ? "Y" : "N";
}
