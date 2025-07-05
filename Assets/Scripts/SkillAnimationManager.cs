using UnityEngine;
using System.Collections;

public class SkillAnimationManager : MonoBehaviour
{
    public RectTransform center;          // Assign via Inspector, the center of the wheel

    [Header("Skill Icon Transforms (RectTransforms)")]
    public RectTransform[] skillIcons;         // Assign via Inspector, 5 elements expected
    [Header("Wheel Settings")]
    public float radius = 100f;                // Radius of the circle
    [Header("Animation Settings")]
    public float animationDuration = 0.5f;     // Duration of position animation in seconds

    // Internal state
    private enum WheelMode { Contracted, Expanded }
    private WheelMode currentMode = WheelMode.Contracted;

    // Angles in degrees
    public float[] contractAngles = {90f, 18f, 306f, 234f, 162f};
    public float[] defaultExpandAngles = {270f, 225f, 180f, 135f, 90f};
    private float[] cachedExpandAngles;

    // Animation state
    private Coroutine currentAnimation;
    private float[] startAngles;
    private float[] targetAngles;

    private void Awake()
    {
        // Initialize cache to default expand arrangement
        cachedExpandAngles = new float[defaultExpandAngles.Length];
        defaultExpandAngles.CopyTo(cachedExpandAngles, 0);

        // Prepare angle buffers
        startAngles = new float[skillIcons.Length];
        targetAngles = new float[skillIcons.Length];

        // Start in contracted mode with snapping
        StartInCompactMode();
    }

    public void StartInCompactMode()
    {
        currentMode = WheelMode.Contracted;
        SnapToAngles(contractAngles);
    }

    public void StartInExpandMode()
    {
        currentMode = WheelMode.Expanded;
        SnapToAngles(cachedExpandAngles);
    }

    [ContextMenu("Move Skills to Compact Mode")]
    public void MoveToCompactMode()
    {
        currentMode = WheelMode.Contracted;
        AnimateToAngles(contractAngles, forceClockwise: true);
    }

    [ContextMenu("Move Skills to Expand Mode")]
    public void MoveToExpandMode()
    {
        currentMode = WheelMode.Expanded;
        AnimateToAngles(cachedExpandAngles, forceClockwise: true);
    }

    [ContextMenu("Move Skills to Left")]
    public void MoveSkillsToLeft()
    {
        if (currentMode != WheelMode.Expanded) return;
        RotateArrayLeft(cachedExpandAngles);
        AnimateToAngles(cachedExpandAngles, forceClockwise: true);
    }

    [ContextMenu("Move Skills Right")]
    public void MoveSkillsToRight()
    {
        if (currentMode != WheelMode.Expanded) return;
        RotateArrayRight(cachedExpandAngles);
        AnimateToAngles(cachedExpandAngles, forceClockwise: false);
    }

    private void RotateArrayLeft(float[] arr)
    {
        float first = arr[0];
        for (int i = 0; i < arr.Length - 1; i++) arr[i] = arr[i + 1];
        arr[arr.Length - 1] = first;
    }

    private void RotateArrayRight(float[] arr)
    {
        float last = arr[arr.Length - 1];
        for (int i = arr.Length - 1; i > 0; i--) arr[i] = arr[i - 1];
        arr[0] = last;
    }

    private void AnimateToAngles(float[] angles, bool forceClockwise)
    {
        // Stop any animation
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);

        // Compute start and targetAngles based on direction flag
        for (int i = 0; i < skillIcons.Length; i++)
        {
            Vector2 offset = skillIcons[i].position - (Vector3)center.position;
            float start = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            startAngles[i] = start;

            float target = angles[i] % 360f;
            float delta = Mathf.DeltaAngle(start, target);

            if (forceClockwise)
            {
                if (delta > 0) delta -= 360f;  // ensure CW path
            }
            else
            {
                if (delta < 0) delta += 360f;  // ensure CCW path
            }

            targetAngles[i] = start + delta;
        }

        currentAnimation = StartCoroutine(AnimatePositions(angles));
    }

    private IEnumerator AnimatePositions(float[] finalAngles)
    {
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float s = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < skillIcons.Length; i++)
            {
                float ang = Mathf.Lerp(startAngles[i], targetAngles[i], s) % 360f;
                Vector2 pos = new Vector2(
                    Mathf.Cos(ang * Mathf.Deg2Rad) * radius,
                    Mathf.Sin(ang * Mathf.Deg2Rad) * radius
                );
                skillIcons[i].anchoredPosition = pos;
            }
            yield return null;
        }

        SnapToAngles(finalAngles);
        currentAnimation = null;
    }

    private void SnapToAngles(float[] angles)
    {
        for (int i = 0; i < skillIcons.Length; i++)
        {
            float ang = angles[i] % 360f;
            Vector2 pos = new Vector2(
                Mathf.Cos(ang * Mathf.Deg2Rad) * radius,
                Mathf.Sin(ang * Mathf.Deg2Rad) * radius
            );
            skillIcons[i].anchoredPosition = pos;
        }
        HighlightSelectedSkill();
    }

    private void HighlightSelectedSkill()
    {
        if (currentMode != WheelMode.Expanded) return;
        for (int i = 0; i < skillIcons.Length; i++)
            skillIcons[i].localScale = Vector3.one;

        int sel = 0;
        float best = Mathf.Infinity;
        for (int i = 0; i < cachedExpandAngles.Length; i++)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(cachedExpandAngles[i], 270f));
            if (diff < best) { best = diff; sel = i; }
        }
        skillIcons[sel].localScale = Vector3.one * 1.2f;
    }
    
    private void OnDrawGizmos()
    {
        if (center == null) return;

        Vector3 centerPosContract = center.position;
        Vector3 centerPosExpand = center.position;
        centerPosExpand.x += 200f; // Offset for expanded mode visualization

        centerPosContract.x -= 200f;

        // Draw center point
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(centerPosContract, 5f);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(centerPosExpand, 5f);

        // Draw contracted mode layout
        DrawSkillWheelGizmos(centerPosContract, contractAngles, Color.green, "Contracted");

        // Draw expanded mode layout
        DrawSkillWheelGizmos(centerPosExpand, defaultExpandAngles, Color.blue, "Expanded");

        // Draw current mode indicator
        Gizmos.color = currentMode == WheelMode.Contracted ? Color.green : Color.blue;
        Gizmos.DrawWireCube(centerPosContract + Vector3.up * (radius + 20f), Vector3.one * 10f);
    }

    private void DrawSkillWheelGizmos(Vector3 centerPos, float[] angles, Color color, string modeName)
    {
        Gizmos.color = color;

        // Draw circle outline
        Gizmos.DrawWireSphere(centerPos, radius);

        // Draw skill positions and indices
        for (int i = 0; i < angles.Length; i++)
        {
            float angle = angles[i];
            float radian = angle * Mathf.Deg2Rad;

            Vector3 skillPos = new Vector3(
                Mathf.Cos(radian) * radius + centerPos.x,
                Mathf.Sin(radian) * radius + centerPos.y,
                centerPos.z
            );

            // Draw skill position marker
            Gizmos.DrawWireSphere(skillPos, 8f);

            // Draw line from center to skill
            Gizmos.DrawLine(centerPos, skillPos);

            // Draw angle arc
            DrawAngleArc(centerPos, radius, angle, color);

            // Draw skill index label
#if UNITY_EDITOR
            Vector3 labelOffset = new Vector3(0f, 15f, 0f);
            UnityEditor.Handles.Label(skillPos + labelOffset, $"Skill {i}", new GUIStyle { normal = { textColor = color } });
#endif
        }

        // Draw mode label
        Vector3 labelPos = centerPos + Vector3.up * (radius + 40f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPos, $"{modeName} Mode", new GUIStyle { normal = { textColor = color } });
#endif
    }

    private void DrawAngleArc(Vector3 center, float radius, float angle, Color color)
    {
        Gizmos.color = new Color(color.r, color.g, color.b, 0.3f);

        int segments = 10;
        float startAngle = 0f;
        float endAngle = angle * Mathf.Deg2Rad;

        Vector3 prevPoint = center + new Vector3(Mathf.Cos(startAngle) * radius, Mathf.Sin(startAngle) * radius, 0f);

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

            Vector3 currentPoint = center + new Vector3(Mathf.Cos(currentAngle) * radius, Mathf.Sin(currentAngle) * radius, 0f);
            Gizmos.DrawLine(prevPoint, currentPoint);
            prevPoint = currentPoint;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (center == null || skillIcons == null) return;

        Vector3 centerPos = center.position;

        // Draw current skill positions with indices
        Gizmos.color = Color.red;
        for (int i = 0; i < skillIcons.Length; i++)
        {
            if (skillIcons[i] != null)
            {
                Vector3 skillPos = skillIcons[i].position;

                // Draw current position
                Gizmos.DrawWireSphere(skillPos, 12f);

                // Draw index number
#if UNITY_EDITOR
                UnityEditor.Handles.Label(skillPos + Vector3.up * 15f, $"Skill {i}", new GUIStyle { normal = { textColor = Color.red } });
#endif

                // Draw line to center
                Gizmos.DrawLine(centerPos, skillPos);
            }
        }

        // Draw current mode info
#if UNITY_EDITOR
        Vector3 infoPos = centerPos + Vector3.right * (radius + 30f);
        string modeInfo = $"Current Mode: {currentMode}\nRadius: {radius}\nSkills: {skillIcons.Length}";
        UnityEditor.Handles.Label(infoPos, modeInfo, new GUIStyle { normal = { textColor = Color.white } });
#endif
    }
}