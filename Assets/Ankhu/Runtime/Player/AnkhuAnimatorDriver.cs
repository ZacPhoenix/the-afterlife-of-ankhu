using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Speech;
using UnityEngine;

namespace Ankhu.Player
{
    /// <summary>
    /// Sits on the root of the Ankhu character prefab and turns gameplay events into Animator triggers.
    /// Speed, Climbing, Grounded and facing are already passed every frame by <see cref="AnkhuVisual"/>
    /// from <see cref="PlayerController2D"/>, so this only adds the one-shot actions:
    /// Interact or InteractLow when <see cref="InteractionFocus"/> uses a target (low when the target sits
    /// below <see cref="lowReachHeight"/> above the feet), and Speak when the speech bubble submits words.
    /// Root motion is always off. Gameplay never reads this component.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class AnkhuAnimatorDriver : MonoBehaviour
    {
        public static readonly int InteractId = Animator.StringToHash("Interact");
        public static readonly int InteractLowId = Animator.StringToHash("InteractLow");
        public static readonly int SpeakId = Animator.StringToHash("Speak");

        [Tooltip("Targets whose centre is lower than this above Ankhu's feet get the low pick-up instead of the eye-level reach.")]
        [SerializeField, Min(0f)] private float lowReachHeight = 0.8f;

        private Animator animator;
        private PlayerController2D controller;
        private InteractionFocus focus;
        private SpeechBubbleController bubble;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            controller = GetComponentInParent<PlayerController2D>();
            focus = GetComponentInParent<InteractionFocus>();
            bubble = FindAnyObjectByType<SpeechBubbleController>();
            if (focus != null)
            {
                focus.Used += OnUsed;
            }
            if (bubble != null)
            {
                bubble.Spoken += OnSpoken;
            }
        }

        private void OnDisable()
        {
            if (focus != null)
            {
                focus.Used -= OnUsed;
            }
            if (bubble != null)
            {
                bubble.Spoken -= OnSpoken;
            }
            focus = null;
            bubble = null;
        }

        private void OnUsed(Interactable target, CommandResult result)
        {
            if (animator == null || (controller != null && controller.IsClimbing))
            {
                return;
            }
            float feetY = controller != null ? controller.FeetPosition.y : transform.position.y;
            float targetY = TargetHeight(target);
            animator.ResetTrigger(InteractId);
            animator.ResetTrigger(InteractLowId);
            animator.SetTrigger(targetY - feetY < lowReachHeight ? InteractLowId : InteractId);
        }

        private void OnSpoken(string utterance, CommandResult result)
        {
            if (animator == null)
            {
                return;
            }
            animator.ResetTrigger(SpeakId);
            animator.SetTrigger(SpeakId);
        }

        private static float TargetHeight(Interactable target)
        {
            if (target.TryGetComponent(out Collider2D c))
            {
                return c.bounds.center.y;
            }
            return target.transform.position.y;
        }
    }
}
