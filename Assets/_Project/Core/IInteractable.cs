using UnityEngine;

namespace LastPatrol.Core
{
    public interface IInteractable
    {
        string PromptLabel { get; }
        bool CanInteract(GameObject actor);
        void Interact(GameObject actor);
    }
}
