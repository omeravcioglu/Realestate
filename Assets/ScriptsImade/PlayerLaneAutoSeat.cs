using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace AvocadoShark
{
    /// Attach to PLAYER ROOT (same GO as CharacterController/Rigidbody).
    /// Seats local player at their lane. Uses a STABLE index computed by sorting PlayerIds,
    /// so every client agrees who is player 0/1/2 and players don't stack.
    public class PlayerLaneAutoSeat : NetworkBehaviour
    {
        [Header("Seating")]
        public bool seatOnSpawn = true;

        [Tooltip("If a lane has no viewerPoint, stand this far in front of its targetPoint.")]
        public float fallbackBackDistance = 2.2f;

        [Tooltip("Vertical aim offset when computing fallback look-at.")]
        public float lookAtHeadHeight = 1.6f;

        [Tooltip("Meters between side-by-side players when virtual lanes are used.")]
        public float laneSpacing = 1.6f;

        [Tooltip("Freeze movement (camera still free) after seating.")]
        public float freezeAfterSeatSeconds = 5f;

        [Header("Readiness & Retries")]
        public float prereqTimeout = 6f;
        public int maxSeatRetries = 2;
        public float retryDelay = 0.25f;

        [Header("Debug")]
        public bool debugLogs = true;

        private bool _started;

        public override void Spawned()
        {
            if (_started) return;
            _started = true;

            if (Object.InputAuthority == Runner.LocalPlayer && seatOnSpawn)
                StartCoroutine(Co_SeatWithRetries("Spawned"));
        }

        private void Update()
        {
            // Manual fallback: F6 to seat now (local only)
            if (Object && Runner && Object.InputAuthority == Runner.LocalPlayer)
            {
                if (Input.GetKeyDown(KeyCode.F6))
                    StartCoroutine(Co_SeatWithRetries("F6"));
            }
        }

        [ContextMenu("Seat Now (local)")]
        public void SeatNowContext()
        {
            if (Object && Runner && Object.InputAuthority == Runner.LocalPlayer)
                StartCoroutine(Co_SeatWithRetries("ContextMenu"));
        }

        private IEnumerator Co_SeatWithRetries(string reason)
        {
            // Wait for manager + lane0 + stable index
            float t0 = Time.time;
            while (!ReadyToSeat())
            {
                if (Time.time - t0 > prereqTimeout)
                {
                    Log("Timed out waiting for manager/lanes/players.");
                    yield break;
                }
                yield return null;
            }

            // Freeze BEFORE teleport so controllers won't tug us back
            var freezer = GetComponent<FreezeMovement>();
            if (!freezer) freezer = gameObject.AddComponent<FreezeMovement>();
            if (freezeAfterSeatSeconds > 0f) freezer.FreezeForSeconds(freezeAfterSeatSeconds);

            for (int attempt = 0; attempt <= maxSeatRetries; attempt++)
            {
                if (!TryComputeSeatTransform(out Vector3 pos, out Quaternion rot, out int stableIndex))
                {
                    Log("Seat transform invalid (missing points).");
                    yield break;
                }

                // Teleport robustly
                var cc = GetComponent<CharacterController>();
                if (cc && cc.enabled)
                {
                    cc.enabled = false;
                    transform.SetPositionAndRotation(pos, rot);
                    cc.enabled = true;
                }
                else
                {
                    transform.SetPositionAndRotation(pos, rot);
                }

                Log($"Seated ({reason}) attempt {attempt} at {pos} rot {rot.eulerAngles} [stableIndex={stableIndex}]");

                yield return new WaitForSeconds(retryDelay);
            }
        }

        private bool ReadyToSeat()
        {
            var gsm = GhostSequenceManager.Instance;
            if (gsm == null || gsm.lanes == null || gsm.lanes.Count == 0) return false;
            if (gsm.lanes[0] == null || gsm.lanes[0].targetPoint == null) return false;

            return GetStableLocalIndexByPlayerId() >= 0;
        }

        private bool TryComputeSeatTransform(out Vector3 seatPos, out Quaternion seatRot, out int stableIndex)
        {
            seatPos = transform.position;
            seatRot = transform.rotation;

            stableIndex = GetStableLocalIndexByPlayerId();
            var gsm = GhostSequenceManager.Instance;
            if (stableIndex < 0 || gsm == null || gsm.lanes == null || gsm.lanes.Count == 0 || gsm.lanes[0] == null)
                return false;

            // Prefer the real lane if it exists at this index
            Lane laneToUse = (stableIndex < gsm.lanes.Count) ? gsm.lanes[stableIndex] : null;
            var baseLane = gsm.lanes[0];
            if (baseLane.targetPoint == null) return false;

            // Compute consistent right/forward from base lane
            Vector3 fwd = baseLane.targetPoint.forward; if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

            // Side offset based on STABLE index (same on all clients)
            Vector3 sideOffset = right * laneSpacing * stableIndex;

            if (laneToUse != null && laneToUse.viewerPoint != null && laneToUse.targetPoint != null)
            {
                // Exact viewer point for a real lane
                seatPos = laneToUse.viewerPoint.position;
                Vector3 face = (laneToUse.targetPoint.position - seatPos);
                if (face.sqrMagnitude < 0.0001f) face = laneToUse.targetPoint.forward;
                seatRot = Quaternion.LookRotation(face.sqrMagnitude > 0.0001f ? face.normalized : Vector3.forward, Vector3.up);
            }
            else if (laneToUse != null && laneToUse.targetPoint != null)
            {
                // Real lane without viewer: stand in front of its target
                Vector3 forward = laneToUse.targetPoint.forward;
                if (forward.sqrMagnitude < 0.0001f)
                    forward = (laneToUse.targetPoint.position - transform.position).normalized;

                seatPos = laneToUse.targetPoint.position - forward.normalized * Mathf.Max(0.3f, fallbackBackDistance);
                Vector3 lookAt = laneToUse.targetPoint.position + Vector3.up * lookAtHeadHeight;
                Vector3 face = lookAt - seatPos;
                seatRot = face.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(face.normalized, Vector3.up) : transform.rotation;
            }
            else
            {
                // VIRTUAL LANE from lane0: offset target sideways, stand back, and look at it
                Vector3 baseTarget = baseLane.targetPoint.position + sideOffset;
                Vector3 forward = baseLane.targetPoint.forward; if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

                seatPos = baseTarget - forward.normalized * Mathf.Max(0.3f, fallbackBackDistance);
                Vector3 lookAt = baseTarget + Vector3.up * lookAtHeadHeight;
                Vector3 face = lookAt - seatPos;
                seatRot = Quaternion.LookRotation(face.normalized, Vector3.up);
            }

            return true;
        }

        /// STABLE local index: sort all ActivePlayers by PlayerId, then take the index of LocalPlayer.
        private int GetStableLocalIndexByPlayerId()
        {
            if (Runner == null) return -1;

            // Collect PlayerIds
            var ids = new List<int>(16);
            foreach (var p in Runner.ActivePlayers)
                ids.Add(p.PlayerId);

            if (ids.Count == 0) return -1;

            // Sort ascending so all clients agree on order
            ids.Sort();

            int myId = Runner.LocalPlayer.PlayerId;
            for (int i = 0; i < ids.Count; i++)
                if (ids[i] == myId)
                    return i;

            return -1;
        }

        private void Log(string msg)
        {
            if (debugLogs) Debug.Log($"[PlayerLaneAutoSeat] {msg}");
        }
    }
}
