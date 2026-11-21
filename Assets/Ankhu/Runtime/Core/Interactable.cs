using UnityEngine;

namespace Ankhu.Core
{
    /// <summary>
    /// Something Ankhu can inspect (Q) and optionally use (E). Using sends a physical
    /// interaction id to the rules adapter. Inspecting only shows authored text.
    /// The focus system picks the nearest enabled interactable whose trigger overlaps the player.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Interactable : MonoBehaviour
    {
        [SerializeField] private string displayName = "Object";
        [SerializeField, TextArea] private string inspectText = "";
        [Tooltip("Ankhu.Integration.InteractionIds value, or empty for inspect only.")]
        [SerializeField] private string interactionId = "";
        [SerializeField] private string usePrompt = "";
        [Tooltip("Codex/target id the speech system can use to recognise what Ankhu is facing.")]
        [SerializeField] private string targetId = "";

        public string DisplayName => displayName;
        public string InspectText => inspectText;
        public string InteractionId => interactionId;
        public string UsePrompt => usePrompt;
        public string TargetId => targetId;
        public bool HasUse => !string.IsNullOrEmpty(interactionId);

        public void Configure(string name, string inspect, string target, string interaction = "", string prompt = "")
        {
            displayName = name;
            inspectText = inspect;
            targetId = target;
            interactionId = interaction;
            usePrompt = prompt;
            GetComponent<Collider2D>().isTrigger = true;
        }
    }
}
