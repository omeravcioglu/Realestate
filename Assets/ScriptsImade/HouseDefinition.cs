using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ScaryType
{
    DoorsBang,
    WindowsShatter,
    Footsteps,
    Whispers,
    Screams,
    ShadowFigures,
    MirrorFaces,
    ObjectsMove,
    TVStatic,
    PhantomTouch,
    ClocksStop,
    WallsBleed,
    CandlesBlowOut,
    ChildrenLaugh,
    HeavyBreathing,
    BangingPipes,
    AnimalGrowls,
    Silhouettes,
    PhantomKnock,
    TemperatureDrop,
    // add any others you actually use, e.g. Spiders
    Spiders
}
[Serializable]
public class ScaryHook
{
    public ScaryType type;
    [Tooltip("Toggled ON when active for this run; OFF otherwise.")]
    public GameObject target;
}

public class HouseDefinition : MonoBehaviour
{
    [Header("House")]
    public string houseName = "House";

    [Header("Scary elements this house CAN have")]
    public List<ScaryHook> scaryHooks = new();

    public void DeactivateAll()
    {
        foreach (var h in scaryHooks)
            if (h != null && h.target) h.target.SetActive(false);
    }

    public void ActivateTypes(HashSet<ScaryType> active)
    {
        foreach (var h in scaryHooks)
            if (h != null && h.target)
                h.target.SetActive(active.Contains(h.type));
    }

    public List<ScaryType> AvailableTypes()
    {
        return scaryHooks.Where(h => h != null && h.target != null)
                         .Select(h => h.type)
                         .Distinct()
                         .ToList();
    }
}
