using UnityEngine;

public class ActivateAfterDelay : MonoBehaviour
{
    [Tooltip("The object you want to activate.")]
    public GameObject targetObject;

    [Tooltip("Delay time in seconds before activation.")]
    public float delay = 15f;

    private void Start()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(false); // Make sure it's off at the start
            Invoke(nameof(ActivateObject), delay);
        }
        else
        {
            Debug.LogWarning("No targetObject assigned on " + gameObject.name);
        }
    }

    private void ActivateObject()
    {
        targetObject.SetActive(true);
    }
}
