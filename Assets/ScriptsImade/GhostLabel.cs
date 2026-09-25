using Fusion;
using TMPro;
using UnityEngine;

public class GhostLabel : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshPro label;
    [SerializeField] private Canvas labelCanvas; // optional

    [Header("Texts shown by index (-1 = clear)")]
    [TextArea(2, 8)]
    public string[] candidateTexts = {
        "Looking for a calm ordinary house",
        "Seeking whispers in the hallway",
        "Drawn to flickering lights",
        "Following cold spots",
        "Watching from the mirrors",
        "Listening to distant knocks",
        "Collecting old memories",
        "Scent of candle smoke",
        "Avoiding the attic",
        "Waiting by the window",
        "Footsteps in the foyer",
        "Breath on the neck",
        "Static on the TV",
        "Pipes that moan",
        "Clocks that stall",
        "Children’s lullaby",
        "Shadows at the door",
        "Keys on the floor",
        "Pages turning",
        "Hands on the wall"
    };

    // Networked primitives only
    [Networked] public PlayerRef AssignedTo { get; set; }
    [Networked] public int TextIndex { get; set; }  // -1 = clear

    private PlayerRef _lastAssigned;
    private int _lastIndex = int.MinValue;

    public override void Spawned()
    {
        _lastAssigned = PlayerRef.None;
        _lastIndex = int.MinValue;
        ApplyAll();
    }

    public override void Render()
    {
        if (_lastAssigned != AssignedTo || _lastIndex != TextIndex)
            ApplyAll();
    }

    private void ApplyAll()
    {
        _lastAssigned = AssignedTo;
        _lastIndex = TextIndex;

        bool show = (Object && Object.Runner && Object.Runner.LocalPlayer == AssignedTo);

        if (label) label.enabled = show && TextIndex >= 0;
        if (labelCanvas) labelCanvas.enabled = show && TextIndex >= 0;
        if (!show || !label) return;

        if (TextIndex >= 0 && TextIndex < candidateTexts.Length)
            label.text = candidateTexts[TextIndex];
        else
            label.text = "";
    }
}
