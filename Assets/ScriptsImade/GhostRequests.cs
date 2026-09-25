using System.Collections.Generic;
using UnityEngine;

public class GhostRequests : MonoBehaviour
{
    public static GhostRequests Instance { get; private set; }

    [Header("Refs")]
    public HouseRandomizer randomizer;

    [Header("Behavior")]
    [Tooltip("Try to give each ghost a different house first. After we run out, we allow repeats.")]
    public bool preferUniqueUntilExhausted = true;

    // ghostIndex -> houseIndex
    private readonly Dictionary<int, int> _assignment = new Dictionary<int, int>();
    private Queue<int> _uniqueBag;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // NEW: pass ghostCount so we can lock in unique picks for the whole run
    public void PrepareForRun(int ghostCount = -1)
    {
        _assignment.Clear();
        RebuildUniqueBag();

        if (randomizer == null || randomizer.ScaryHouseIndices == null) return;

        if (preferUniqueUntilExhausted && ghostCount > 0)
        {
            // If we have enough scary houses, pre-assign unique houses to each ghost index
            int available = randomizer.ScaryHouseIndices.Count;
            int need = Mathf.Max(0, ghostCount);

            if (available >= need)
            {
                // Consume unique bag first
                for (int i = 0; i < need; i++)
                {
                    if (_uniqueBag.Count == 0) break;
                    _assignment[i] = _uniqueBag.Dequeue();
                }
            }
            else
            {
                // Use all uniques, then fill remainder with random picks (duplicates unavoidable)
                int i = 0;
                for (; i < available && i < need; i++)
                    _assignment[i] = _uniqueBag.Dequeue();

                var list = randomizer.ScaryHouseIndices;
                for (; i < need; i++)
                    _assignment[i] = list[Random.Range(0, list.Count)];
            }
        }
    }

    private void RebuildUniqueBag()
    {
        _uniqueBag = new Queue<int>();
        if (randomizer == null || randomizer.ScaryHouseIndices == null) return;

        var list = new List<int>(randomizer.ScaryHouseIndices);
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        foreach (var idx in list) _uniqueBag.Enqueue(idx);
    }

    /// Assign (or return existing) solution house for this ghost index.
    public int AssignHouseForGhost(int ghostIndex)
    {
        if (randomizer == null || randomizer.ScaryHouseIndices == null || randomizer.ScaryHouseIndices.Count == 0)
            return -1;

        if (_assignment.TryGetValue(ghostIndex, out int existing))
            return existing;

        int chosen = -1;

        if (preferUniqueUntilExhausted && _uniqueBag != null && _uniqueBag.Count > 0)
        {
            chosen = _uniqueBag.Dequeue();
        }
        else
        {
            var list = randomizer.ScaryHouseIndices;
            chosen = list[Random.Range(0, list.Count)];
        }

        _assignment[ghostIndex] = chosen;
        return chosen;
    }

    public string GetRequestLine(int ghostIndex)
    {
        if (ghostIndex < 0) return "…";
        int houseIndex = AssignHouseForGhost(ghostIndex);
        if (houseIndex < 0) return "I’m looking for a calm, ordinary house.";
        return randomizer.BuildRequestLineForHouse(houseIndex);
    }

    public bool CheckMatch(int ghostIndex, HouseDefinition chosen)
    {
        if (chosen == null) return false;
        if (!_assignment.TryGetValue(ghostIndex, out int solHouseIdx)) return false;
        int chosenIdx = randomizer.houses.IndexOf(chosen);
        return chosenIdx == solHouseIdx;
    }

    // Optional: quick debug
    [ContextMenu("DEBUG Dump Assignments")]
    public void DebugDumpAssignments()
    {
        if (randomizer == null) { Debug.Log("[GhostRequests] No randomizer."); return; }
        foreach (var kv in _assignment)
        {
            int gi = kv.Key;
            int hi = kv.Value;
            string name = (hi >= 0 && hi < randomizer.houses.Count) ? randomizer.houses[hi].houseName : $"index {hi}";
            Debug.Log($"Ghost {gi} -> {name}");
        }
    }
}
