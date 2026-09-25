#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine;

public class Phase3Hotkey : MonoBehaviour
{
    [Header("Key")]
    public KeyCode startKey = KeyCode.R;

    [Header("Who can start?")]
    [Tooltip("If true, only the host/state authority can start Phase 3 for everyone. Non-hosts do nothing on key press.")]
    public bool onlyHostCanStartForAll = true;

    void Update()
    {
        if (!Input.GetKeyDown(startKey)) return;

        var am = AssignmentManager.Instance;
        if (!am)
        {
            Debug.LogWarning("[Phase3Hotkey] No AssignmentManager in scene.");
            return;
        }

        // Find current Runner
        var runner = FindObjectOfType<NetworkRunner>();
        if (!runner)
        {
            Debug.LogWarning("[Phase3Hotkey] No NetworkRunner found.");
            return;
        }

        // Host triggers "for all" (one ghost per player, sequential per player)
        if (!onlyHostCanStartForAll)
        {
            // Anyone can start their **own** Phase 3 (just themselves)
            am.RpcBeginMyPhase3();
            return;
        }

        // Only host/state authority can start for everyone
        // Host checks: Host mode => IsServer; Shared mode => IsSharedModeMasterClient
        bool iAmHost = runner.IsServer || runner.IsSharedModeMasterClient;
        if (iAmHost)
        {
            am.RpcBeginPhase3ForAll();
        }
        else
        {
            Debug.Log("[Phase3Hotkey] Not host; ignoring global start.");
        }
    }
}
#endif
