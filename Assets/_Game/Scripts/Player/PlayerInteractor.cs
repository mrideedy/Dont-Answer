using UnityEngine;

namespace DontAnswer
{
    /// <summary>Looks for an <see cref="Interactable"/> under the crosshair and uses it on E / click (or hold).</summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public PlayerController player;
        public float range = 2.4f;

        Interactable current;
        float held;
        // A finished hold does its thing once. Keep holding and nothing more happens: let go before the ring starts again.
        bool waitForRelease;

        void Update()
        {
            var gd = GameDirector.Instance;
            bool free = gd != null && gd.CanFreelyInteract;

            Interactable found = null;
            if (free)
            {
                var t = player.cam.transform;
                if (Physics.Raycast(t.position, t.forward, out var hit, range, ~(1 << 2), QueryTriggerInteraction.Collide))
                    found = hit.collider.GetComponentInParent<Interactable>();
                if (found != null && (!found.CanInteract || string.IsNullOrEmpty(found.Prompt))) found = null;
            }

            if (found != current) held = 0f;
            current = found;
            if (!GameInput.InteractHeld) waitForRelease = false;
            var ui = UIManager.Instance;
            if (ui != null) ui.SetPrompt(current != null ? current.Prompt : null);

            if (!free || current == null)
            {
                if (ui != null) ui.SetHoldProgress(0f);
                return;
            }

            if (current.HoldDuration > 0f)
            {
                if (GameInput.InteractHeld && !waitForRelease)
                {
                    held += Time.deltaTime;
                    if (ui != null) ui.SetHoldProgress(held / current.HoldDuration);
                    if (held >= current.HoldDuration)
                    {
                        held = 0f;
                        waitForRelease = true;
                        if (ui != null) ui.SetHoldProgress(0f);
                        current.Interact();
                    }
                }
                else
                {
                    held = 0f;
                    if (ui != null) ui.SetHoldProgress(0f);
                }
                return;
            }

            if (GameInput.Interact) current.Interact();
        }
    }
}
