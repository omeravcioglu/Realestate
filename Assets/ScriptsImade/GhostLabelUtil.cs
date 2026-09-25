using Fusion;
using UnityEngine;

public static class GhostLabelUtil
{
    public static void SetLabel(NetworkObject ghost, PlayerRef owner, string line)
    {
        if (!ghost) return;
        var label = ghost.GetComponent<GhostLabel>(); if (!label) return;
        label.AssignedTo = owner;
        label.candidateTexts = new[] { line };
        label.TextIndex = 0;
    }
    public static void Clear(NetworkObject ghost)
    {
        var label = ghost ? ghost.GetComponent<GhostLabel>() : null;
        if (label) label.TextIndex = -1;
    }
}
