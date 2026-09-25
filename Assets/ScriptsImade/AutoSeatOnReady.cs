using System.Collections;
using UnityEngine;

public class AutoSeatOnReady : MonoBehaviour
{
    [Header("When to start")]
    [Tooltip("Delay before the first attempt (seconds).")]
    public float startDelay = 0.5f;

    [Tooltip("Try again every N seconds until it works or times out.")]
    public float retryInterval = 0.5f;

    [Tooltip("Stop trying after this many seconds. 0 = try once only.")]
    public float maxDuration = 15f;

    [Header("Seating options")]
    [Tooltip("If >= 0, override the seater's freeze time (seconds).")]
    public float freezeSecondsOverride = -1f;

    [Tooltip("Stop after the first successful seat call.")]
    public bool seatOnce = true;

    private Coroutine _runner;

    private void OnEnable()
    {
        if (_runner == null) _runner = StartCoroutine(Co_AutoSeat());
    }

    private IEnumerator Co_AutoSeat()
    {
        if (startDelay > 0f) yield return new WaitForSeconds(startDelay);

        float deadline = (maxDuration > 0f) ? Time.time + maxDuration : float.PositiveInfinity;

        while (Time.time <= deadline)
        {
            var seaters = FindObjectsOfType<AvocadoShark.PlayerLaneAutoSeat>();
            AvocadoShark.PlayerLaneAutoSeat mySeat = null;

            // Find the LOCAL player's seater
            foreach (var s in seaters)
            {
                if (s != null && s.Object != null && s.Runner != null &&
                    s.Object.InputAuthority == s.Runner.LocalPlayer)
                {
                    mySeat = s;
                    break;
                }
            }

            if (mySeat != null)
            {
                if (freezeSecondsOverride >= 0f)
                    mySeat.freezeAfterSeatSeconds = freezeSecondsOverride;

                // Call the same method you clicked manually
                mySeat.SendMessage("SeatNowContext", SendMessageOptions.DontRequireReceiver);
                Debug.Log("[AutoSeatOnReady] Triggered SeatNowContext on local player.");

                if (seatOnce) yield break; // done
            }

            yield return new WaitForSeconds(Mathf.Max(0.05f, retryInterval));
        }

        Debug.LogWarning("[AutoSeatOnReady] Gave up trying to seat (timed out).");
    }
}
