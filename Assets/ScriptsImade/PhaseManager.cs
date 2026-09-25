#if CMPSETUP_COMPLETE
using UnityEngine;
using AvocadoShark; // for GhostSequenceManager

public class DebugRecallHotkey : MonoBehaviour
{
    [Tooltip("Key used to recall all ghosts for assignment (host only).")]
    public KeyCode recallKey = KeyCode.R;

    private void Update()
    {
        // Ensure the GhostSequenceManager exists
        var gsm = GhostSequenceManager.Instance;
        if (gsm == null || gsm.Object == null)
            return;

        // Only the host/state authority should trigger ghost recall
        if (!gsm.Object.HasStateAuthority)
            return;

        // Check for key press
        if (Input.GetKeyDown(recallKey))
        {
            // Try the legacy recall method first
            try
            {
                gsm.RecallAllForAssignment();
                Debug.Log("[DebugRecallHotkey] Legacy RecallAllForAssignment() triggered.");
            }
            catch
            {
                // Fallback to new AssignmentManager system if legacy method not found
                if (AssignmentManager.Instance != null)
                {
                    AssignmentManager.Instance.RpcBeginPhase3ForAll();
                    Debug.Log("[DebugRecallHotkey] Sequential recall triggered via AssignmentManager.");
                }
                else
                {
                    Debug.LogWarning("[DebugRecallHotkey] No recall method found — please ensure AssignmentManager exists.");
                }
            }
        }
    }
}
#endif
