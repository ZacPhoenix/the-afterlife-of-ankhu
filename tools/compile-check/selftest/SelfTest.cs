// Canary for the reference set: exercises Unity 6-only APIs plus InputSystem, uGUI, TMP and UnityEditor.
// If this stops compiling, the refs (not Ankhu code) are broken.
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Ankhu.CompileCheck.SelfTest
{
    public class Canary : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private UnityEngine.UI.Text label;
        [SerializeField] private TMPro.TextMeshProUGUI tmp;

        private void Update()
        {
            body.linearVelocity = new Vector2(1f, body.linearVelocity.y); // Unity 6 rename of velocity
            body.linearDamping = 0.5f;                                     // Unity 6 rename of drag
            var cam = FindFirstObjectByType<Camera>();                     // 2023.1+ API
            var any = FindAnyObjectByType<Canary>();
            var objs = FindObjectsByType<Canary>(FindObjectsSortMode.None);
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) label.text = "x";
            Collider2D hit = Physics2D.OverlapPoint(Vector2.zero);
            tmp.text = cam ? cam.name : (any ? any.name : objs.Length.ToString());
            Awaitable a = Awaitable.NextFrameAsync();                      // Unity 6 Awaitable
        }
    }

#if UNITY_EDITOR
    public class CanaryWindow : EditorWindow
    {
        [MenuItem("Tools/Canary")] private static void Open() => GetWindow<CanaryWindow>();
        private void OnGUI() { EditorGUILayout.LabelField(AssetDatabase.GetAssetPath(Selection.activeObject)); }
    }
#endif
}
