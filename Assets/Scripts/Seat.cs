using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Makes a chair sittable: teleporting onto it (via its TeleportationAnchor) places the player
/// on the seat facing the anchor's forward. The player's body ignores the chair's colliders
/// so it isn't pushed off the seat.
/// </summary>
[RequireComponent(typeof(TeleportationAnchor))]
public class Seat : MonoBehaviour
{
    private void Start()
    {
        var origin = FindAnyObjectByType<XROrigin>();
        var body = origin != null ? origin.GetComponentInChildren<CharacterController>() : null;
        if (body == null) return;

        foreach (var col in GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(body, col);
    }
}
