using UnityEngine;

/// <summary>
/// Implement on any MonoBehaviour to make it interactable by the player.
/// PlayerController raycasts for this interface on E press and Left Click.
/// </summary>
public interface IInteractable
{
    void Interact(GameObject instigator);
}