#if CMPSETUP_COMPLETE
using AvocadoShark;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(GhostController))]
[RequireComponent(typeof(NetworkObject))]
public class GhostSpeakerCompat : NetworkBehaviour
{
    [Header("Audio (optional)")]
    public AudioSource source;
    public AudioClip onArriveClip;
    public AudioClip onLeaveClip;

    private GhostController _ctrl;

    public override void Spawned()
    {
        _ctrl = GetComponent<GhostController>();
        if (!source) source = GetComponent<AudioSource>();
        if (!source) source = gameObject.AddComponent<AudioSource>();

        // hook into controller events; we don't touch labels
        _ctrl.onArriveTarget.AddListener(OnArriveTarget);
        _ctrl.onStartLeave.AddListener(OnStartLeave);
    }

    private void OnArriveTarget()
    {
        if (source && onArriveClip) source.PlayOneShot(onArriveClip);
    }

    private void OnStartLeave()
    {
        if (source && onLeaveClip) source.PlayOneShot(onLeaveClip);
    }
}
#endif
