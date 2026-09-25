using UnityEngine;
using UnityEngine.AI;

public static class TeleportUtility
{
    /// Robust teleport that handles common controller types.
    public static void Teleport(GameObject go, Vector3 pos, Quaternion rot)
    {
        if (!go) return;

        var cc = go.GetComponent<CharacterController>();
        if (cc && cc.enabled)
        {
            cc.enabled = false;
            go.transform.SetPositionAndRotation(pos, rot);
            cc.enabled = true;
            return;
        }

        var agent = go.GetComponent<NavMeshAgent>();
        if (agent && agent.enabled)
        {
            agent.Warp(pos);
            go.transform.rotation = rot;
            return;
        }

        var rb = go.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.position = pos;
            rb.rotation = rot;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        go.transform.SetPositionAndRotation(pos, rot);
    }
}
