using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HouseRandomizer : MonoBehaviour
{
    [Header("All houses in the scene")]
    public List<HouseDefinition> houses = new();

    [Header("Randomization")]
    [Tooltip("How many houses will be assigned specialties this run.")]
    public int scaryHouseCount = 6;

    [Tooltip("Ignored (rules enforce 1–2 per house). Kept for inspector visibility.")]
    public Vector2Int elementsPerHouseRange = new Vector2Int(1, 2);

    public bool autoRandomizeOnStart = true;
    public bool useDebugSeed = false;
    public int debugSeed = 12345;

    [Header("Specialty Pool (optional)")]
    [Tooltip("If empty, all ScaryType enum values are allowed. To use ONLY certain ones, list them here.")]
    public List<ScaryType> allowedSpecialties = new();

    [Header("Global Caps")]
    [Min(1), Tooltip("Max times a single specialty can appear across ALL scary houses.")]
    public int maxUsesPerSpecialty = 2;

    // Output
    public List<int> ScaryHouseIndices { get; private set; } = new();
    public Dictionary<int, HashSet<ScaryType>> ActiveByHouse { get; private set; } = new();

    private System.Random _rng;

    private void Start()
    {
        if (autoRandomizeOnStart)
            Randomize(useDebugSeed ? debugSeed : UnityEngine.Random.Range(1, int.MaxValue));

        // If you have this singleton; otherwise it's safe to leave out or null-check
        if (GhostRequests.Instance != null)
            GhostRequests.Instance.PrepareForRun();
    }

    [ContextMenu("Randomize (New Seed)")]
    public void RandomizeContext()
    {
        Randomize(UnityEngine.Random.Range(1, int.MaxValue));
    }

    public void Randomize(int seed)
    {
        _rng = new System.Random(seed);
        ScaryHouseIndices.Clear();
        ActiveByHouse.Clear();

        if (houses == null || houses.Count == 0) return;

        // Clamp selected count
        int count = Mathf.Clamp(scaryHouseCount, 1, Mathf.Max(1, houses.Count));
        ScaryHouseIndices = UniqueRandomIndices(houses.Count, count, _rng);
        var scarySet = new HashSet<int>(ScaryHouseIndices);

        // Build specialty pool
        var pool = BuildSpecialtyPool();
        if (pool.Count == 0)
        {
            Debug.LogError("[HouseRandomizer] No specialties available in pool.");
            return;
        }

        // Enforce per-house 1–2
        const int minPerHouse = 1;
        const int maxPerHouse = 2;

        // Feasibility checks (rough but helpful)
        int s = pool.Count;
        int totalBudget = s * maxUsesPerSpecialty;
        int minNeeded = count * minPerHouse;
        if (minNeeded > totalBudget)
        {
            Debug.LogError($"[HouseRandomizer] Impossible: need at least {minNeeded} uses but only {totalBudget} allowed by caps.");
            return;
        }

        // Max unique 1–2-size combos from a pool of s
        int uniqueCombosMax = s + (s * (s - 1)) / 2;
        if (count > uniqueCombosMax)
        {
            Debug.LogWarning($"[HouseRandomizer] You request {count} scary houses, but only {uniqueCombosMax} unique combos exist (size 1–2). With caps and availability, assignment may be impossible.");
        }

        // Precompute all candidate combos (1-of and 2-of)
        var allCandidates = GenerateAllCandidates(pool, minPerHouse, maxPerHouse);
        Shuffle(allCandidates, _rng);

        // Randomize house order we’ll try to assign
        var order = ScaryHouseIndices.ToList();
        Shuffle(order, _rng);

        // Prepare usage counters and uniqueness set
        var usage = pool.ToDictionary(sp => sp, sp => 0);
        var usedCombos = new HashSet<string>();

        // Stage results before applying to scene
        var staged = new Dictionary<int, List<ScaryType>>();

        // Backtracking assignment
        bool success = AssignRecursive(
            step: 0,
            order: order,
            allCandidates: allCandidates,
            usage: usage,
            usedCombos: usedCombos,
            staged: staged,
            minPerHouse: minPerHouse,
            maxPerHouse: maxPerHouse
        );

        // Clear all houses first
        for (int i = 0; i < houses.Count; i++) houses[i].DeactivateAll();

        if (!success)
        {
            Debug.LogError("[HouseRandomizer] Could not find a valid assignment under current constraints. Try adding more specialties, raising caps, or reducing scaryHouseCount.");
            return;
        }

        // Apply and record
        foreach (var idx in staged.Keys)
        {
            var set = new HashSet<ScaryType>(staged[idx]);
            houses[idx].ActivateTypes(set);
            ActiveByHouse[idx] = set;
        }
    }

    // ----------------------------------------------------
    // Public: request line for ghosts / UI
    // ----------------------------------------------------
    public string BuildRequestLineForHouse(int houseIndex)
    {
        if (!ActiveByHouse.TryGetValue(houseIndex, out var set) || set == null || set.Count == 0)
            return "I’m looking for a calm, ordinary house.";

        var arr = set.ToList();
        arr.Sort((a, b) => a.ToString().CompareTo(b.ToString()));
        return "I’m looking for a house with: " + string.Join(", ", arr.Select(HumanizeScaryType)) + ".";
    }

    public static string HumanizeScaryType(ScaryType t)
    {
        // Your full mapping:
        return t switch
        {
            ScaryType.DoorsBang => "banging doors",
            ScaryType.WindowsShatter => "shattering windows",
            ScaryType.Footsteps => "unseen footsteps",
            ScaryType.Whispers => "whispering voices",
            ScaryType.Screams => "distant screams",
            ScaryType.ShadowFigures => "moving shadows",
            ScaryType.MirrorFaces => "faces in mirrors",
            ScaryType.ObjectsMove => "objects moving on their own",
            ScaryType.TVStatic => "TV static noises",
            ScaryType.PhantomTouch => "cold phantom touches",
            ScaryType.ClocksStop => "clocks stopping suddenly",
            ScaryType.WallsBleed => "walls bleeding",
            ScaryType.CandlesBlowOut => "candles blowing out",
            ScaryType.ChildrenLaugh => "children laughing faintly",
            ScaryType.HeavyBreathing => "heavy breathing nearby",
            ScaryType.BangingPipes => "pipes banging loudly",
            ScaryType.AnimalGrowls => "beastly growls in the dark",
            ScaryType.Silhouettes => "silhouettes in the distance",
            ScaryType.PhantomKnock => "knocking with no one there",
            ScaryType.TemperatureDrop => "sudden cold air",
            _ => t.ToString()
        };
    }

    // ----------------------------------------------------
    // Backtracking core
    // ----------------------------------------------------
    private bool AssignRecursive(
        int step,
        List<int> order,
        List<List<ScaryType>> allCandidates,
        Dictionary<ScaryType, int> usage,
        HashSet<string> usedCombos,
        Dictionary<int, List<ScaryType>> staged,
        int minPerHouse,
        int maxPerHouse)
    {
        if (step >= order.Count) return true;

        int houseIdx = order[step];
        var house = houses[houseIdx];

        // What this house can take
        var available = house.AvailableTypes() ?? new List<ScaryType>();
        var availSet = new HashSet<ScaryType>(available);

        // Candidates that are:
        // - subset of available
        // - respect per-specialty caps
        // - unique combo not used yet
        var stepCandidates = new List<List<ScaryType>>();
        foreach (var combo in allCandidates)
        {
            if (!IsSubset(combo, availSet)) continue;
            if (!RespectsCaps(combo, usage, maxUsesPerSpecialty)) continue;
            if (usedCombos.Contains(Signature(combo))) continue;
            stepCandidates.Add(combo);
        }

        // Randomize attempt order to avoid bias
        Shuffle(stepCandidates, _rng);

        foreach (var combo in stepCandidates)
        {
            var sig = Signature(combo);

            // Apply
            foreach (var sp in combo) usage[sp] += 1;
            usedCombos.Add(sig);
            staged[houseIdx] = combo;

            if (AssignRecursive(step + 1, order, allCandidates, usage, usedCombos, staged, minPerHouse, maxPerHouse))
                return true;

            // Undo
            staged.Remove(houseIdx);
            usedCombos.Remove(sig);
            foreach (var sp in combo) usage[sp] -= 1;
        }

        return false;
    }

    // ----------------------------------------------------
    // Helpers
    // ----------------------------------------------------
    private List<ScaryType> BuildSpecialtyPool()
    {
        if (allowedSpecialties != null && allowedSpecialties.Count > 0)
            return allowedSpecialties.Distinct().ToList();

        // Default: use every enum value present
        return Enum.GetValues(typeof(ScaryType)).Cast<ScaryType>().Distinct().ToList();
    }

    private static bool IsSubset(List<ScaryType> combo, HashSet<ScaryType> available)
    {
        for (int i = 0; i < combo.Count; i++)
            if (!available.Contains(combo[i])) return false;
        return true;
    }

    private static bool RespectsCaps(List<ScaryType> combo, Dictionary<ScaryType, int> usage, int cap)
    {
        foreach (var sp in combo)
        {
            if (!usage.TryGetValue(sp, out int used)) used = 0;
            if (used + 1 > cap) return false;
        }
        return true;
    }

    private static string Signature(List<ScaryType> combo)
    {
        // canonical signature (sorted) to guarantee uniqueness ignoring order
        var sorted = combo.OrderBy(x => x).ToList();
        return string.Join("+", sorted);
    }

    private static List<List<ScaryType>> GenerateAllCandidates(List<ScaryType> pool, int minK, int maxK)
    {
        var result = new List<List<ScaryType>>();

        // size 1
        if (minK <= 1)
        {
            foreach (var a in pool)
                result.Add(new List<ScaryType> { a });
        }

        // size 2
        if (maxK >= 2)
        {
            for (int i = 0; i < pool.Count; i++)
                for (int j = i + 1; j < pool.Count; j++)
                    result.Add(new List<ScaryType> { pool[i], pool[j] });
        }

        return result;
    }

    private static List<int> UniqueRandomIndices(int poolSize, int take, System.Random rng)
    {
        take = Mathf.Clamp(take, 0, poolSize);
        var arr = Enumerable.Range(0, poolSize).ToList();
        Shuffle(arr, rng);
        return arr.Take(take).ToList();
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
