using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MaxTech.UI
{
    /// Step-by-step tutorial for Level 1. Shows one floating hint at a time in the world;
    /// the step completes when the player does the action, then the next step appears.
    /// Grapple and roulette stay switched off until their step, and a step can block the path
    /// with an invisible wall until it's done. Lives on the HUD prefab so the level scene isn't edited.
    /// Select this object in the Scene view to see where every hint and wall will appear.
    public class TutorialHints : MonoBehaviour
    {
        public enum Action { Move, Jump, DoubleJump, WallJump, OpenRoulette, Grapple, WalkPast }
        public enum Ability { None, Roulette, Grapple }

        [Serializable]
        public class Step
        {
            [TextArea(1, 3)] public string text = "Press [A] [D] to move";
            [Tooltip("World position of the text. Player plays at Z = 0.")]
            public Vector3 position;
            [Tooltip("What the player must do to finish this step.")]
            public Action completeWhen = Action.Move;
            [Tooltip("Switched on when this step starts (locked before).")]
            public Ability unlocks = Ability.None;
            [Tooltip("Put an invisible wall at Wall X until this step is done.")]
            public bool blockPath;
            public float wallX;
        }

        [Tooltip("The tutorial only runs in this scene.")]
        [SerializeField] string onlyInScene = "Level 1";
        [SerializeField] List<Step> steps = new();

        [Header("Look")]
        [SerializeField] float fontSize = 3f;
        [SerializeField] Color textColor = new Color32(0xC7, 0x9A, 0x4A, 0xFF);    // brass C79A4A
        [Tooltip("Colour of [keys] written in square brackets.")]
        [SerializeField] Color keyColor = new Color32(0xE8, 0xC7, 0x7A, 0xFF);     // pale gold E8C77A
        [SerializeField] Color doneColor = new Color32(0xF2, 0xE6, 0xC8, 0xFF);    // parchment F2E6C8
        [SerializeField] Color outlineColor = new Color32(0x1A, 0x16, 0x12, 0xFF); // dark brown 1A1612
        [SerializeField, Range(0f, 0.5f)] float outlineWidth = 0.25f;

        [Header("Behaviour")]
        [Tooltip("The player must be this close to a hint to see it and complete it.")]
        [SerializeField] float showDistance = 8f;
        [SerializeField] float bobHeight = 0.15f;
        [SerializeField] float bobSpeed = 2f;

        int current = -1;
        TextMeshPro text;
        GameObject wall;
        Transform player;
        PlayerMovement movement;
        GrapplingHook hook;
        RussianRouletteController roulette;
        bool grappleUnlocked, rouletteUnlocked, finished;

        void Start()
        {
            if (SceneManager.GetActiveScene().name != onlyInScene || steps.Count == 0)
            {
                enabled = false;
                return;
            }

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                movement = p.GetComponent<PlayerMovement>();
            }
            hook = FindAnyObjectByType<GrapplingHook>();
            roulette = FindAnyObjectByType<RussianRouletteController>();

            // Abilities that a step unlocks start locked.
            grappleUnlocked = !steps.Exists(s => s.unlocks == Ability.Grapple);
            rouletteUnlocked = !steps.Exists(s => s.unlocks == Ability.Roulette);

            StartStep(0);
        }

        void StartStep(int index)
        {
            current = index;
            if (current >= steps.Count)
            {
                finished = true;
                Unlock(Ability.Grapple);
                Unlock(Ability.Roulette);
                return;
            }

            Step step = steps[current];
            Unlock(step.unlocks);
            ApplyLocks();

            text = SpawnText(step);
            if (step.blockPath) wall = SpawnWall(step);
        }

        void Update()
        {
            if (finished || player == null || text == null) return;

            Step step = steps[current];
            text.transform.position = step.position + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

            bool near = Vector2.Distance(player.position, step.position) <= showDistance;
            text.alpha = Mathf.MoveTowards(text.alpha, near ? 1f : 0f, Time.deltaTime * 3f);

            if (near && IsDone(step)) CompleteStep();
        }

        // Runs after RouletteFX, which switches the hook back on when the roulette closes.
        void LateUpdate()
        {
            if (!finished) ApplyLocks();
        }

        // Locked abilities are kept off every frame; unlocking switches them on once.
        void ApplyLocks()
        {
            if (hook != null && !grappleUnlocked && !hook.IsGrappling) hook.enabled = false;
            if (roulette != null && !rouletteUnlocked && !roulette.IsOpen) roulette.enabled = false;
        }

        void Unlock(Ability ability)
        {
            if (ability == Ability.Grapple && !grappleUnlocked)
            {
                grappleUnlocked = true;
                if (hook != null && !RouletteFX.IsRouletteOpen) hook.enabled = true;
            }
            if (ability == Ability.Roulette && !rouletteUnlocked)
            {
                rouletteUnlocked = true;
                if (roulette != null) roulette.enabled = true;
            }
        }

        bool IsDone(Step step)
        {
            Keyboard kb = Keyboard.current;
            bool jumpPressed = kb != null && kb.spaceKey.wasPressedThisFrame;
            bool airborne = movement != null && !movement.isGrounded;

            switch (step.completeWhen)
            {
                case Action.Move:         return kb != null && (kb.aKey.isPressed || kb.dKey.isPressed);
                case Action.Jump:         return jumpPressed;
                case Action.DoubleJump:   return jumpPressed && airborne;
                case Action.WallJump:     return jumpPressed && airborne && TouchingWall();
                case Action.OpenRoulette: return roulette != null && roulette.IsOpen;
                case Action.Grapple:      return hook != null && hook.IsGrappling;
                case Action.WalkPast:     return player.position.x > step.position.x + 2f;
            }
            return false;
        }

        bool TouchingWall()
        {
            const float reach = 0.8f;
            return Physics.Raycast(player.position, Vector3.right, reach, ~0, QueryTriggerInteraction.Ignore)
                || Physics.Raycast(player.position, Vector3.left, reach, ~0, QueryTriggerInteraction.Ignore);
        }

        void CompleteStep()
        {
            if (wall != null) Destroy(wall);
            StartCoroutine(FinishText(text));
            text = null;
            StartStep(current + 1);
        }

        // Done: flash parchment, pop, fade out.
        IEnumerator FinishText(TextMeshPro tmp)
        {
            tmp.color = doneColor;
            Vector3 start = tmp.transform.localScale;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = t / 0.5f;
                tmp.transform.localScale = start * (1f + 0.25f * Mathf.Sin(k * Mathf.PI));
                tmp.alpha = 1f - k;
                yield return null;
            }
            Destroy(tmp.gameObject);
        }

        TextMeshPro SpawnText(Step step)
        {
            var go = new GameObject("Tutorial: " + step.text.Split('\n')[0]);
            go.transform.position = step.position; // scene root, not under the HUD canvas

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = ColorKeys(step.text);
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.outlineColor = outlineColor;
            tmp.outlineWidth = outlineWidth;
            tmp.alpha = 0f;
            return tmp;
        }

        static GameObject SpawnWall(Step step)
        {
            var go = new GameObject("Tutorial Wall");
            go.transform.position = new Vector3(step.wallX, step.position.y, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.5f, 40f, 6f); // tall, so it can't be jumped over
            return go;
        }

        // "[Space]" -> bold, key-coloured "[Space]".
        string ColorKeys(string s)
        {
            string hex = ColorUtility.ToHtmlStringRGB(keyColor);
            return s.Replace("[", $"<b><color=#{hex}>[").Replace("]", "]</color></b>");
        }

        void OnDestroy()
        {
            if (text != null) Destroy(text.gameObject);
            if (wall != null) Destroy(wall);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            for (int i = 0; i < steps.Count; i++)
            {
                Step step = steps[i];
                Gizmos.color = textColor;
                Gizmos.DrawWireCube(step.position, new Vector3(4f, 1f, 0.1f));
                UnityEditor.Handles.Label(step.position, $"{i + 1}. {step.text}");

                if (step.blockPath)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawWireCube(new Vector3(step.wallX, step.position.y, 0f), new Vector3(0.5f, 40f, 6f));
                }
            }
        }
#endif
    }
}
