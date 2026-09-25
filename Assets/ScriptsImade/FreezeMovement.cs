using UnityEngine;
using System.Collections;

public class FreezeMovement : MonoBehaviour
{
    private bool isFrozen;
    private float thawTime;

    public bool IsFrozen => isFrozen;

    /// Freeze movement for `duration` seconds (camera code should run elsewhere).
    public void FreezeForSeconds(float duration)
    {
        thawTime = Time.time + Mathf.Max(0f, duration);
        if (!isFrozen) StartCoroutine(DoFreeze());
    }

    private IEnumerator DoFreeze()
    {
        isFrozen = true;
        while (Time.time < thawTime)
            yield return null;
        isFrozen = false;
    }
}
