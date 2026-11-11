using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Door that only moves while its handle is held. Put this on the hinge pivot (the door panel
/// extends along the pivot's local +X when closed). When released it stays where it was left,
/// and latches shut if it was let go close to the frame.
/// </summary>
public class HingedDoor : MonoBehaviour
{
    [SerializeField] private XRBaseInteractable handle;
    [SerializeField] private float minAngle = 0f;
    [SerializeField] private float maxAngle = 100f;
    [Tooltip("Released within this many degrees of closed, the door snaps shut.")]
    [SerializeField] private float latchAngle = 6f;

    private Quaternion closedRotation;
    private IXRSelectInteractor holder;
    private float grabOffset;

    public float Angle { get; private set; }
    public bool IsHeld => holder != null;

    private void Awake()
    {
        closedRotation = transform.localRotation;
    }

    private void OnEnable()
    {
        if (handle == null) return;
        handle.selectEntered.AddListener(OnGrab);
        handle.selectExited.AddListener(OnRelease);
    }

    private void OnDisable()
    {
        if (handle == null) return;
        handle.selectEntered.RemoveListener(OnGrab);
        handle.selectExited.RemoveListener(OnRelease);
    }

    private void OnGrab(SelectEnterEventArgs args)
    {
        holder = args.interactorObject;
        grabOffset = Angle - AngleTowards(HolderPosition());
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (args.interactorObject != holder) return;
        holder = null;
        if (Mathf.Abs(Angle) <= latchAngle) SetAngle(0f);
    }

    private void Update()
    {
        if (holder != null)
            SetAngle(AngleTowards(HolderPosition()) + grabOffset);
    }

    private Vector3 HolderPosition() => holder.GetAttachTransform(handle).position;

    /// <summary>Angle around the hinge, measured from the closed position, pointing at a world position.</summary>
    public float AngleTowards(Vector3 worldPoint)
    {
        Vector3 local = transform.parent != null ? transform.parent.InverseTransformPoint(worldPoint) : worldPoint;
        Vector3 offset = local - transform.localPosition;
        offset.y = 0f;
        return Vector3.SignedAngle(closedRotation * Vector3.right, offset, Vector3.up);
    }

    public void SetAngle(float angle)
    {
        Angle = Mathf.Clamp(angle, minAngle, maxAngle);
        transform.localRotation = Quaternion.AngleAxis(Angle, Vector3.up) * closedRotation;
    }
}
