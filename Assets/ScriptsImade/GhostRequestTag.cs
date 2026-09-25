#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine;

public class GhostRequestTag : NetworkBehaviour
{
    // Two replicated scary elements per ghost
    [Networked] public int ElementIdA { get; set; }
    [Networked] public int ElementIdB { get; set; }

    // Who owns/sees this request
    [Networked] public PlayerRef Owner { get; set; }

    // UI state + the exact line that all clients should display
    [Networked] public NetworkBool AssignmentMode { get; set; }
    [Networked] public NetworkString<_64> Line { get; set; }
}
#endif
