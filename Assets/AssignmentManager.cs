#if CMPSETUP_COMPLETE
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class AssignmentManager : NetworkBehaviour
{
    public static AssignmentManager Instance { get; private set; }

    // ---------- Tuning ----------
    [Header("Phase 3 Gating")]
    [SerializeField] private float arrivalRadius = 1.1f;            // how close to lane.target before popping UI
    [SerializeField] private float arrivalTimeout = 8f;             // safety max wait (seconds)
    [SerializeField] private float popupDelayAfterArrival = 0.35f;  // small pause so players see the ghost

    // Server-side per-player state
    private readonly Dictionary<PlayerRef, Queue<NetworkId>> _queues = new();
    private readonly Dictionary<PlayerRef, NetworkId> _active = new();
    private readonly HashSet<PlayerRef> _begun = new();

    // Client-side (local) cache
    private NetworkId _localActiveGhost; // default(NetworkId) => none

    // -------- UI hooks for MANUAL popups --------
    // Open/close your popup as active ghost changes (LOCAL only)
    public static event Action<NetworkId> LocalActiveGhostChanged;
    // Disable a house button instantly everywhere by Serial (GLOBAL)
    public static event Action<int> HouseConsumedSerial;
    // Optional success/failure toast for the clicker (LOCAL)
    public static event Action<bool, NetworkId, NetworkId> LocalAssignmentResult;

    public override void Spawned()
    {
        if (Instance != null && Instance != this)
        {
            if (Object.HasStateAuthority) Runner.Despawn(Object);
            return;
        }
        Instance = this;
    }

    // For manual UI (read-only)
    public NetworkId GetActiveGhostForLocalPlayer() => _localActiveGhost;

    // =================== CLIENT -> SERVER ========================

    /// Begin Phase 3 for THIS caller only (one ghost at a time).
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcBeginMyPhase3(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;
        var who = info.Source;

        // Already have an active ghost? just notify
        if (_active.TryGetValue(who, out var cur) && cur.IsValid)
        {
            RpcActiveGhostFor(who, cur);
            return;
        }

        // Queue exists but no active yet? recall next now
        if (_queues.TryGetValue(who, out var q) && q != null && q.Count > 0)
        {
            RecallNext(who);
            return;
        }

        // Otherwise, build the queue and start
        BeginFor(who);
    }

    /// Begin Phase 3 for ALL players at once (each gets exactly one ghost).
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcBeginPhase3ForAll(RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;

        foreach (var p in Runner.ActivePlayers)
        {
            if (_begun.Contains(p)) continue;

            // Already has active? just notify
            if (_active.TryGetValue(p, out var cur) && cur.IsValid)
            {
                RpcActiveGhostFor(p, cur);
                continue;
            }

            // Queue exists but no active yet? recall next now
            if (_queues.TryGetValue(p, out var q) && q != null && q.Count > 0)
            {
                RecallNext(p);
                continue;
            }

            // Otherwise build queue and start
            BeginFor(p);
        }
    }

    /// Player clicked a house for their active ghost (from manual popup).
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcTryAssignActiveGhostToHouse(NetworkId houseId, RpcInfo info = default)
    {
        if (!Object.HasStateAuthority) return;
        var who = info.Source;

        if (!_active.TryGetValue(who, out var ghostId) || !ghostId.IsValid)
        {
            RpcAssignmentResult(false, default, houseId, who);
            return;
        }

        var ghost = Runner.FindObject(ghostId);
        var house = Runner.FindObject(houseId);
        if (!ghost || !house) { RpcAssignmentResult(false, ghostId, houseId, who); return; }

        var gTag = ghost.GetComponent<GhostRequestTag>();
        var hTag = house.GetComponent<HouseTag>();
        if (!gTag || !hTag) { RpcAssignmentResult(false, ghostId, houseId, who); return; }

        // Only assign your own ghost
        if (gTag.Owner != who) { RpcAssignmentResult(false, ghostId, houseId, who); return; }

        // Already used?
        if (hTag.IsAssigned) { RpcAssignmentResult(false, ghostId, houseId, who); return; }

        // Elements must match (order-independent)
        bool match =
            (gTag.ElementIdA == hTag.ElementIdA && gTag.ElementIdB == hTag.ElementIdB) ||
            (gTag.ElementIdA == hTag.ElementIdB && gTag.ElementIdB == hTag.ElementIdA);

        if (!match) { RpcAssignmentResult(false, ghostId, houseId, who); return; }

        // --- success: consume the key (replicates to all clients immediately) ---
        hTag.IsAssigned = true;
        RpcHouseConsumedSerial(hTag.Serial); // instant "USED" everywhere

        // Send this ghost away (prefer GSM; fallback despawn)
        var gsm = AvocadoShark.GhostSequenceManager.Instance;
        bool moved = false;
        if (gsm) moved = gsm.SendAway(ghost);
        if (!moved && ghost) Runner.Despawn(ghost);

        _active.Remove(who);

        // Inform the clicking client (they should close popup in their UI handler)
        RpcAssignmentResult(true, ghostId, houseId, who);

        // Recall the next ghost for THIS player (with arrival gating)
        RecallNext(who);
        Debug.Log($"[Assign] {who} assigning ghost {ghostId} -> house {houseId}");

    }

    // =================== SERVER helpers ==========================

    private void BeginFor(PlayerRef who)
    {
        var gsm = AvocadoShark.GhostSequenceManager.Instance;
        if (!gsm) { RpcActiveGhostFor(who, default); return; }

        var ghosts = gsm.GetOwnedGhosts(who);
        if (ghosts == null || ghosts.Count == 0)
        {
            RpcActiveGhostFor(who, default);
            return;
        }

        var q = new Queue<NetworkId>(ghosts.Count);
        foreach (var g in ghosts) if (g) q.Enqueue(g.Id);
        _queues[who] = q;
        _active.Remove(who);
        _begun.Add(who);

        RecallNext(who); // recalls exactly ONE (popup shows only after arrival)
    }

    private void RecallNext(PlayerRef who)
    {
        if (!Object.HasStateAuthority) return;

        if (!_queues.TryGetValue(who, out var q) || q.Count == 0)
        {
            RpcActiveGhostFor(who, default); // none left -> hide popup on that client
            return;
        }

        var nextId = q.Dequeue();
        var ghost = Runner.FindObject(nextId);
        if (!ghost)
        {
            // Skip nulls and continue
            RecallNext(who);
            return;
        }

        _active[who] = nextId;

        var gsm = AvocadoShark.GhostSequenceManager.Instance;
        if (gsm) gsm.RecallForAssignment(ghost);

        // Gate the popup until the ghost arrives at its lane target
        StartCoroutine(CoNotifyWhenArrived(who, nextId));
    }

    private IEnumerator CoNotifyWhenArrived(PlayerRef who, NetworkId ghostId)
    {
        var gsm = AvocadoShark.GhostSequenceManager.Instance;
        if (!gsm) { RpcActiveGhostFor(who, default); yield break; }

        var start = Time.time;
        bool arrived = false;

        // Ask GSM where this ghost should arrive (lane target)
        if (!gsm.TryGetLaneTarget(ghostId, out var targetPos))
        {
            // Fallback: notify anyway after a short wait
            yield return new WaitForSeconds(popupDelayAfterArrival);
            RpcActiveGhostFor(who, ghostId);
            yield break;
        }

        while (Time.time - start < arrivalTimeout)
        {
            var ghost = Runner.FindObject(ghostId);
            if (!ghost) break;

            var dist = Vector3.Distance(ghost.transform.position, targetPos);
            if (dist <= Mathf.Max(0.2f, arrivalRadius))
            {
                arrived = true;
                break;
            }
            yield return null;
        }

        if (!arrived)
            yield return new WaitForSeconds(0.15f); // small grace
        else
            yield return new WaitForSeconds(popupDelayAfterArrival); // let players see the ghost

        RpcActiveGhostFor(who, ghostId);
    }

    // =================== SERVER -> CLIENT =========================

    // Global: tell everyone which serial was consumed so UI can disable instantly
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcHouseConsumedSerial(int serial)
    {
        HouseConsumedSerial?.Invoke(serial);
    }

    // Local: tell this client which ghost is currently active (opens/closes popup)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcActiveGhostFor(PlayerRef who, NetworkId ghostId)
    {
        if (Runner.LocalPlayer == who)
        {
            _localActiveGhost = ghostId; // default => .IsValid == false (no popup)
            LocalActiveGhostChanged?.Invoke(_localActiveGhost);
        }
    }

    // Local: result of clicking a house (close popup on success in your UI)
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RpcAssignmentResult(bool ok, NetworkId ghostId, NetworkId houseId, PlayerRef who)
    {
        if (Runner.LocalPlayer == who)
            LocalAssignmentResult?.Invoke(ok, ghostId, houseId);
    }
}
#endif
