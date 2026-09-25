#if CMPSETUP_COMPLETE
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace AvocadoShark
{
    [System.Serializable]
    public class Lane
    {
        public Transform spawnPoint;
        public Transform targetPoint;
        public Transform leavePoint;
        public Transform viewerPoint; // optional
        public Transform housePoint;  // optional
    }

    [System.Serializable]
    public class GhostStep
    {
        public NetworkObject ghostPrefab;
        public float approachSpeed = 0f;
        public float visitSeconds = 5f;
    }

    public class GhostSequenceManager : NetworkBehaviour
    {
        public static GhostSequenceManager Instance { get; private set; }

        [Header("Ghost Setup")]
        public List<GhostStep> templates = new();
        public List<Lane> lanes = new();
        public NetworkObject defaultGhostPrefab;

        [Header("Per-Player Assignment")]
        public int ghostsPerPlayer = 5;

        [Header("Timing")]
        public float startDelaySeconds = 12f;
        public float delayBetweenWaves = 0.75f;
        public bool autoStart = true;

        [Header("Requests / Elements")]
        public RequestAllocator requestAllocator;
        public bool autoFindAllocator = true;
        public float arrivalSpeakDistance = 1.1f;

        [Header("Debug / Legacy")]
        public bool allowLegacyRecallAtRuntime = false;

        private bool _started;
        private int _ghostSerialIndex = 0;

        private class GhostRec
        {
            public NetworkObject ghost;
            public PlayerRef owner;
            public Lane lane;
            public int elementIdA;
            public int elementIdB;
        }
        private readonly List<GhostRec> _ghosts = new();

        // --------------------- Lifecycle ---------------------
        public override void Spawned()
        {
            if (Instance != null && Instance != this)
            {
                if (Object.HasStateAuthority)
                    Runner.Despawn(Object);
                return;
            }
            Instance = this;

            if (Object.HasStateAuthority && autoStart)
                StartCoroutine(Co_StartAfterDelay());
        }

        private IEnumerator Co_StartAfterDelay()
        {
            if (startDelaySeconds > 0f)
                yield return new WaitForSeconds(startDelaySeconds);
            StartWaves();
        }

        [ContextMenu("Start Waves (StateAuthority only)")]
        public void StartWaves()
        {
            if (!Object.HasStateAuthority || _started) return;
            _started = true;

            if (autoFindAllocator && requestAllocator == null)
                requestAllocator = FindObjectOfType<RequestAllocator>();

            StartCoroutine(Co_RunWaves());
        }

        private IEnumerator Co_RunWaves()
        {
            var perPlayerCount = new Dictionary<PlayerRef, int>();
            int wave = 0;

            while (true)
            {
                var players = new List<PlayerRef>(Runner.ActivePlayers);
                if (players.Count == 0)
                {
                    yield return new WaitForSeconds(0.25f);
                    continue;
                }

                AutoExpandLanes(players.Count);

                bool allDone = true;
                foreach (var p in players)
                {
                    if (!perPlayerCount.ContainsKey(p)) perPlayerCount[p] = 0;
                    if (perPlayerCount[p] < ghostsPerPlayer) allDone = false;
                }
                if (allDone) break;

                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (perPlayerCount[player] >= ghostsPerPlayer) continue;

                    var lane = lanes[i];
                    if (!ValidateLane(lane))
                    {
                        Debug.LogWarning($"[GhostSeq] Invalid lane {i}");
                        continue;
                    }

                    var tpl = (templates.Count > 0) ? templates[(wave + i) % templates.Count] : null;
                    var prefab = tpl?.ghostPrefab ?? defaultGhostPrefab;
                    if (!prefab)
                    {
                        Debug.LogWarning("[GhostSeq] Missing ghost prefab.");
                        continue;
                    }

                    float speed = (tpl?.approachSpeed > 0f) ? tpl.approachSpeed : 2f;
                    float visit = (tpl != null) ? Mathf.Max(0.1f, tpl.visitSeconds) : 5f;

                    SpawnGhost(prefab, lane, speed, visit, player);
                    perPlayerCount[player]++;
                }

                wave++;
                yield return new WaitForSeconds(delayBetweenWaves);
            }

            Debug.Log($"[GhostSeq] Waves complete after {wave} wave(s).");
        }

        // --------------------- Core Logic ---------------------
        private bool ValidateLane(Lane lane) =>
            lane != null && lane.spawnPoint && lane.targetPoint && lane.leavePoint;

        private static string NiceName(ScaryElementLibrary lib, int id) =>
            (lib && id >= 0 && id < lib.elements.Count) ? lib.elements[id] : $"Element#{id}";

        private (int a, int b) NextTwoElementIds()
        {
            int a, b;
            if (requestAllocator)
            {
                a = requestAllocator.NextElementId();
                int guard = 0;
                do { b = requestAllocator.NextElementId(); guard++; } while (b == a && guard < 8);
            }
            else
            {
                var lib = FindObjectOfType<ScaryElementLibrary>();
                int n = (lib != null) ? Mathf.Max(1, lib.elements.Count) : 8;
                a = Random.Range(0, n);
                b = (a + 1) % n;
            }
            return (a, b);
        }

        private void SpawnGhost(NetworkObject prefab, Lane lane, float speed, float visit, PlayerRef owner)
        {
            var pos = lane.spawnPoint.position;
            var dir = lane.targetPoint.position - pos;
            var rot = (dir.sqrMagnitude > 0.001f)
                ? Quaternion.LookRotation(dir.normalized, Vector3.up)
                : Quaternion.identity;

            var no = Runner.Spawn(prefab, pos, rot, PlayerRef.None);

            var ctrl = no.GetComponent<GhostController>();
            if (ctrl)
                ctrl.MoveVisitLeave(lane.targetPoint.position, lane.leavePoint.position, speed, visit, false);

            var (a, b) = NextTwoElementIds();

            var tag = no.GetComponent<GhostRequestTag>();
            if (tag)
            {
                tag.Owner = owner;
                tag.ElementIdA = a;
                tag.ElementIdB = b;
                var lib = FindObjectOfType<ScaryElementLibrary>();
                tag.Line = $"Find: {NiceName(lib, a)} + {NiceName(lib, b)}";
            }

            if (HouseSpawnManager.Instance)
            {
                var anchor = lane.housePoint ? lane.housePoint : lane.targetPoint;
                HouseSpawnManager.Instance.TrySpawnForGhost(no, _ghostSerialIndex, anchor, a, b, out _);
            }

            _ghosts.Add(new GhostRec { ghost = no, owner = owner, lane = lane, elementIdA = a, elementIdB = b });
            _ghostSerialIndex++;
        }

        // --------------------- Phase 3 (Assignment) ---------------------
        public bool RecallForAssignment(NetworkObject ghost)
        {
            if (!Object.HasStateAuthority || !ghost) return false;

            foreach (var rec in _ghosts)
            {
                if (rec.ghost == ghost)
                {
                    var ctrl = ghost.GetComponent<GhostController>();
                    if (ctrl)
                        ctrl.GoToAndWait(rec.lane.targetPoint.position, 9999f, ctrl.moveSpeed);

                    var tag = ghost.GetComponent<GhostRequestTag>();
                    if (tag)
                    {
                        var lib = FindObjectOfType<ScaryElementLibrary>();
                        tag.AssignmentMode = true;
                        tag.Line = $"Assign to: {NiceName(lib, rec.elementIdA)} + {NiceName(lib, rec.elementIdB)}";
                    }
                    return true;
                }
            }
            return false;
        }

        public bool SendAway(NetworkObject ghost)
        {
            if (!Object.HasStateAuthority || !ghost) return false;

            foreach (var rec in _ghosts)
            {
                if (rec.ghost == ghost)
                {
                    var ctrl = ghost.GetComponent<GhostController>();
                    if (ctrl)
                    {
                        ctrl.ForceLeaveNow(rec.lane.leavePoint.position);
                        Debug.Log($"[GhostSeq] Sent ghost {ghost.Id} away to leave point.");
                    }
                    else
                    {
                        Runner.Despawn(ghost);
                    }

                    var tag = ghost.GetComponent<GhostRequestTag>();
                    if (tag) tag.AssignmentMode = false;
                    return true;
                }
            }
            return false;
        }

        // --------------------- Utility ---------------------
        public List<NetworkObject> GetOwnedGhosts(PlayerRef owner)
        {
            var list = new List<NetworkObject>();
            foreach (var rec in _ghosts)
                if (rec.owner == owner && rec.ghost) list.Add(rec.ghost);
            return list;
        }

        public bool TryGetLaneTarget(NetworkId ghostId, out Vector3 targetPos)
        {
            foreach (var rec in _ghosts)
            {
                if (rec.ghost && rec.ghost.Id == ghostId && rec.lane?.targetPoint)
                {
                    targetPos = rec.lane.targetPoint.position;
                    return true;
                }
            }
            targetPos = Vector3.zero;
            return false;
        }

        // Legacy support for hotkey recall
        [ContextMenu("Recall All For Assignment (Legacy)")]
        public void RecallAllForAssignment()
        {
            if (!Object.HasStateAuthority) return;
            Debug.Log("[GhostSeq] Legacy RecallAllForAssignment triggered.");

            foreach (var rec in _ghosts)
            {
                if (!rec.ghost || rec.lane == null) continue;
                var ctrl = rec.ghost.GetComponent<GhostController>();
                if (ctrl)
                    ctrl.GoToAndWait(rec.lane.targetPoint.position, 9999f, ctrl.moveSpeed);
            }
        }

        private void AutoExpandLanes(int needed)
        {
            if (lanes.Count >= needed || lanes.Count == 0) return;
            var src = lanes[0];
            Vector3 right = Vector3.Cross(Vector3.up, src.targetPoint.forward).normalized;
            float spacing = 1.6f;

            for (int i = lanes.Count; i < needed; i++)
            {
                Vector3 offs = right * spacing * i;

                Transform MakeChild(string name, Vector3 basePos, Quaternion baseRot)
                {
                    var go = new GameObject($"Auto_{name}_{i}");
                    go.transform.SetParent(transform, true);
                    go.transform.position = basePos + offs;
                    go.transform.rotation = baseRot;
                    return go.transform;
                }

                var newLane = new Lane
                {
                    spawnPoint = MakeChild("Spawn", src.spawnPoint.position, src.spawnPoint.rotation),
                    targetPoint = MakeChild("Target", src.targetPoint.position, src.targetPoint.rotation),
                    leavePoint = MakeChild("Leave", src.leavePoint.position, src.leavePoint.rotation),
                };

                if (src.viewerPoint)
                    newLane.viewerPoint = MakeChild("Viewer", src.viewerPoint.position, src.viewerPoint.rotation);
                if (src.housePoint)
                    newLane.housePoint = MakeChild("House", src.housePoint.position, src.housePoint.rotation);

                lanes.Add(newLane);
            }
        }
    }
}
#endif
