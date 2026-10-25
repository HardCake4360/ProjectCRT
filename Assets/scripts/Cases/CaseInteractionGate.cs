using UnityEngine;

namespace ProjectCRT.Cases
{
    [DisallowMultipleComponent, RequireComponent(typeof(Interactable))]
    public sealed class CaseInteractionGate : MonoBehaviour, IInteractionGate
    {
        public ShangrilaCaseManager manager;
        public bool AllowsInteraction => manager != null && manager.InteractionsUnlocked;
    }
}
