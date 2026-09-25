#if CMPSETUP_COMPLETE
using System;
using Fusion;
using UnityEngine;

public class HouseTag : NetworkBehaviour
{
    // Elements (order independent)
    [Networked] public int ElementIdA { get; set; }
    [Networked] public int ElementIdB { get; set; }

    // Visual/label + links
    [Networked] public NetworkString<_64> Label { get; set; }
    [Networked] public NetworkBool IsAssigned { get; set; }
    [Networked] public NetworkId ForGhost { get; set; }

    // Serial for UI/numbering
    [Networked] public int Serial { get; set; }

    /// Fired on every client whenever a house becomes (un)assigned. Args: (serial, isAssigned)
    public static event Action<int, bool> AssignedChanged;

    // cache last replicated value so we can detect flips
    private bool _lastAssigned;

    public override void Spawned()
    {
        _lastAssigned = IsAssigned;                // initialize cache from replicated value
        AssignedChanged?.Invoke(Serial, IsAssigned); // notify UI once on spawn (helps late joiners)
    }

    public override void Render()
    {
        // Called on all peers every render tick. When replication updates IsAssigned,
        // this condition flips once; we then notify any local UI listeners instantly.
        bool now = IsAssigned;
        if (now != _lastAssigned)
        {
            _lastAssigned = now;
            AssignedChanged?.Invoke(Serial, now);
        }
    }

    // ----- Helpers -----
    public static bool UnorderedEqual(int a1, int b1, int a2, int b2)
    {
        if (a1 > b1) (a1, b1) = (b1, a1);
        if (a2 > b2) (a2, b2) = (b2, a2);
        return a1 == a2 && b1 == b2;
    }

    public bool Matches(GhostRequestTag g)
    {
        return UnorderedEqual(ElementIdA, ElementIdB, g.ElementIdA, g.ElementIdB);
    }
}
#endif
