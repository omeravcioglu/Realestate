#if CMPSETUP_COMPLETE
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class HouseSpawnManager : NetworkBehaviour
{
    public static HouseSpawnManager Instance { get; private set; }

    [Header("Prefabs & Points")]
    [Tooltip("Must be a NetworkObject and be listed in Fusion's Network Prefabs on BOTH host and clients (ParrelSync clones too).")]
    public NetworkObject housePrefab;

    [Tooltip("Optional explicit points. If empty, we fallback to the lane's anchor / ghost position.")]
    public List<Transform> houseSpawnPoints = new();

    [Tooltip("If true, we duplicate point[0] to get as many points as needed.")]
    public bool autoExpandPoints = true;

    [Tooltip("Spacing used when auto-expanding from point[0].")]
    public float pointSpacing = 3.5f;

    [Tooltip("If set, houses will face this transform's forward. Otherwise they face the chosen spawn point's forward.")]
    public Transform forwardHint;

    // ghostId -> spawned house
    private readonly Dictionary<NetworkId, NetworkObject> _ghostToHouse = new();

    public override void Spawned()
    {
        if (Instance != null && Instance != this)
        {
            if (Object.HasStateAuthority) Runner.Despawn(Object);
            return;
        }
        Instance = this;

        Debug.Log($"[HSM] Spawned. SA={Object.HasStateAuthority}, IsServer={Runner.IsServer}, Prefab={(housePrefab ? housePrefab.name : "<NULL>")}");
    }

    public bool TrySpawnForGhost(
        NetworkObject ghost,
        int serialIndex,
        Transform fallbackAnchor,
        int elementIdA,
        int elementIdB,
        out NetworkObject house)
    {
        house = null;

        if (!Object.HasStateAuthority)
        {
            Debug.LogWarning("[HSM] TrySpawnForGhost ignored (not StateAuthority).");
            return false;
        }
        if (!ghost)
        {
            Debug.LogWarning("[HSM] TrySpawnForGhost: ghost is null.");
            return false;
        }
        if (!housePrefab)
        {
            Debug.LogError("[HSM] TrySpawnForGhost: housePrefab is null. Add a NetworkObject prefab and register it in Network Prefabs.");
            return false;
        }
        if (_ghostToHouse.ContainsKey(ghost.Id))
        {
            return false;
        }

        // Choose a spawn point
        Transform sp = null;
        if (houseSpawnPoints.Count > 0)
        {
            EnsurePoints(serialIndex + 1);
            sp = houseSpawnPoints[serialIndex % houseSpawnPoints.Count];
        }
        if (!sp) sp = fallbackAnchor ? fallbackAnchor : ghost.transform;

        // Face forwardHint if provided
        Vector3 fwd = forwardHint ? forwardHint.forward : sp.forward;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

        // IMPORTANT: world props -> no input authority
        Debug.Log($"[HSM] Spawning house for ghost={ghost.Id} at {sp.position}  rot={rot.eulerAngles}  serial={serialIndex}");
        house = Runner.Spawn(housePrefab, sp.position, rot, PlayerRef.None);

        if (!house)
        {
            Debug.LogError("[HSM] Runner.Spawn returned null (check Network Prefabs list on all clients).");
            return false;
        }

        // Tag with BOTH elements (+ Serial)
        if (house.TryGetComponent(out HouseTag tag))
        {
            tag.ElementIdA = elementIdA;
            tag.ElementIdB = elementIdB;
            tag.Serial = serialIndex;
            tag.ForGhost = ghost.Id;

            var lib = UnityEngine.Object.FindObjectOfType<ScaryElementLibrary>();
            string niceA = (lib && elementIdA >= 0 && elementIdA < lib.elements.Count) ? lib.elements[elementIdA] : $"Element#{elementIdA}";
            string niceB = (lib && elementIdB >= 0 && elementIdB < lib.elements.Count) ? lib.elements[elementIdB] : $"Element#{elementIdB}";
            tag.Label = $"{niceA} + {niceB}";
        }
        else if (house.TryGetComponent(out HouseController hc))
        {
            hc.ElementId = elementIdA;
        }

        _ghostToHouse[ghost.Id] = house;

        // Despawn the house automatically if the ghost goes away
        StartCoroutine(CoTie(ghost, house));
        Debug.Log($"[HSM] Spawned House NO={house.Id} for Ghost NO={ghost.Id}");
        return true;
    }

    public void DespawnForGhost(NetworkObject ghost)
    {
        if (!Object.HasStateAuthority || !ghost) return;

        if (_ghostToHouse.TryGetValue(ghost.Id, out var house) && house)
        {
            Debug.Log($"[HSM] Despawning house {house.Id} for ghost {ghost.Id}");
            Runner.Despawn(house);
        }
        _ghostToHouse.Remove(ghost.Id);
    }

    public bool TryGetHouseForGhost(NetworkObject ghost, out NetworkObject house)
    {
        return _ghostToHouse.TryGetValue(ghost.Id, out house);
    }

    public IEnumerable<HouseTag> AllHouses()
    {
        foreach (var kv in _ghostToHouse)
        {
            var no = kv.Value;
            if (no && no.TryGetComponent(out HouseTag tag))
                yield return tag;
        }
    }

    private IEnumerator CoTie(NetworkObject ghost, NetworkObject house)
    {
        while (ghost && house) yield return null;

        if (Object.HasStateAuthority && house)
        {
            Debug.Log($"[HSM] Ghost/House lost – despawning house {house.Id}");
            Runner.Despawn(house);
        }
    }

    public void EnsurePoints(int needed)


    {
        if (!autoExpandPoints || houseSpawnPoints.Count == 0 || houseSpawnPoints.Count >= needed) return;

        var basePt = houseSpawnPoints[0];
        if (!basePt) return;

        Vector3 fwd = basePt.forward;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        for (int i = houseSpawnPoints.Count; i < needed; i++)
        {
            var go = new GameObject($"AutoHousePoint_{i}");
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.SetPositionAndRotation(basePt.position + right * pointSpacing * i, basePt.rotation);
            houseSpawnPoints.Add(go.transform);
        }
    }

    [ContextMenu("TEST: Spawn One House Here (SA only)")]
    private void TestSpawnOne()
    {
        if (!Object.HasStateAuthority)
        {
            Debug.Log("[HSM] TestSpawnOne ignored (not StateAuthority).");
            return;
        }
        if (!housePrefab)
        {
            Debug.LogError("[HSM] TestSpawnOne: housePrefab missing.");
            return;
        }
        var pos = transform.position + Vector3.right * Random.Range(-2f, 2f);
        var rot = Quaternion.identity;
        var ho = Runner.Spawn(housePrefab, pos, rot, PlayerRef.None);
        Debug.Log($"[HSM] TestSpawnOne -> {(ho ? ho.Id.ToString() : "NULL")}");
    }
}
#endif
