using UnityEngine;
using UnityEngine.AI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
// If you use Starter Assets, keep this using. Safe to leave even if you don't.
using StarterAssets;

public class TeleportAndFreezeTrigger : MonoBehaviour
{
    [Header("Trigger Settings")]
    public string playerTag = "Player";
    [Tooltip("Set this collider's 'Is Trigger' to true.")]
    public Vector3 targetPosition = new Vector3(0f, 1f, 0f);

    [Header("Freeze")]
    public float freezeDuration = 15f;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // Prefer the object that has FreezeMovement (usually your player root)
        FreezeMovement freezer = other.GetComponentInParent<FreezeMovement>();
        GameObject playerGO = freezer != null ? freezer.gameObject : other.gameObject;

        // TELEPORT robustly
        TeleportPlayer(playerGO, targetPosition);

        // FREEZE (keeps camera look working)
        if (freezer == null) freezer = playerGO.AddComponent<FreezeMovement>();
        freezer.FreezeForSeconds(freezeDuration);

        // DISABLE JUMP / SPACE while waiting
        var jumpBlocker = playerGO.GetComponent<JumpBlocker>();
        if (jumpBlocker == null) jumpBlocker = playerGO.AddComponent<JumpBlocker>();
        jumpBlocker.Begin(freezeDuration);
    }

    private void TeleportPlayer(GameObject player, Vector3 to)
    {
        // CharacterController-safe teleport
        var cc = player.GetComponent<CharacterController>();
        if (cc != null)
        {
            bool wasEnabled = cc.enabled;
            cc.enabled = false; // avoid step offset resisting teleports
            player.transform.position = to;
            cc.enabled = wasEnabled;
            return;
        }

        // NavMeshAgent-safe teleport
        var agent = player.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.Warp(to);
            return;
        }

        // Rigidbody-safe teleport
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Clear velocities so physics doesn't yank you back
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Direct set is fine for teleports
            rb.position = to;
            return;
        }

        // Fallback: plain transform move
        player.transform.position = to;
    }
}

/// <summary>
/// Blocks Jump (Space) without affecting camera look:
///  - Disables "Jump" InputAction if using the new Input System
///  - Forces StarterAssetsInputs.jump = false each frame while active
/// </summary>
public class JumpBlocker : MonoBehaviour
{
    private bool _active;
    private Coroutine _timerCo;

#if ENABLE_INPUT_SYSTEM
    private PlayerInput _playerInput;
    private InputAction _jumpAction;
#endif
    private StarterAssetsInputs _starterInputs;

    private void Awake()
    {
#if ENABLE_INPUT_SYSTEM
        _playerInput = GetComponentInParent<PlayerInput>();
        if (_playerInput != null && _playerInput.actions != null)
        {
            // Change "Jump" here if your action has a different name
            _jumpAction = _playerInput.actions.FindAction("Jump", false);
        }
#endif
        _starterInputs = GetComponentInParent<StarterAssetsInputs>();
    }

    public void Begin(float seconds)
    {
        Activate(true);
        if (_timerCo != null) StopCoroutine(_timerCo);
        _timerCo = StartCoroutine(StopAfter(seconds));
    }

    private System.Collections.IEnumerator StopAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        Activate(false);
        _timerCo = null;
    }

    private void Activate(bool on)
    {
        _active = on;

#if ENABLE_INPUT_SYSTEM
        if (_jumpAction != null)
        {
            if (on && _jumpAction.enabled) _jumpAction.Disable();
            else if (!on && !_jumpAction.enabled) _jumpAction.Enable();
        }
#endif
        // Also clear any latched jump in Starter Assets immediately
        if (_starterInputs != null) _starterInputs.jump = false;
    }

    private void LateUpdate()
    {
        if (!_active) return;

        // Keep clearing the Starter Assets jump flag while frozen,
        // so pressing Space won't sneak through on any frame.
        if (_starterInputs != null) _starterInputs.jump = false;
    }

    private void OnDisable()
    {
        // Safety: re-enable jump if this component gets disabled mid-freeze
        Activate(false);
    }
}
