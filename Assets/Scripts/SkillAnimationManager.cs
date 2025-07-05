using UnityEngine;

public class SkillAnimationManager : MonoBehaviour
{

    public RectTransform center;          // Assign via Inspector, the center of the wheel

    [Header("Skill Icon Transforms (RectTransforms)")]
    public RectTransform[] skillIcons;         // Assign via Inspector, 5 elements expected
    [Header("Wheel Settings")]
    public float radius = 100f;                // Radius of the circle

    // Internal state
    private enum WheelMode { Contracted, Expanded }
    private WheelMode currentMode = WheelMode.Contracted;

    // Angles in degrees
    public float[] contractAngles;
    public float[] defaultExpandAngles;
    private float[] cachedExpandAngles;

    private void Awake()
    {

        // Initialize cache to default expand arrangement
        cachedExpandAngles = new float[defaultExpandAngles.Length];
        defaultExpandAngles.CopyTo(cachedExpandAngles, 0);

        // Start in contracted mode
        MoveToCompactMode();
    }

    [ContextMenu("Move Skills to Compact Mode")]
    /// <summary>
    /// Immediately positions skills into their contracted (full-circle) layout.
    /// </summary>
    public void MoveToCompactMode()
    {
        currentMode = WheelMode.Contracted;
        SetPositions(contractAngles);
    }

    [ContextMenu("Move Skills to Expand Mode")]
    /// <summary>
    /// Immediately positions skills into their expanded (half-circle) layout,
    /// either using the cached angles or the default expand set.
    /// </summary>
    public void MoveToExpandMode()
    {
        currentMode = WheelMode.Expanded;
        SetPositions(cachedExpandAngles);
    }

    [ContextMenu("Move Skills to Left")]
    /// <summary>
    /// Shift all skills one slot forward (rotating the cached angles left).
    /// Only valid in expanded mode.
    /// </summary>
    public void MoveSkillsToLeft()
    {
        if (currentMode != WheelMode.Expanded)
            return;

        int n = cachedExpandAngles.Length;
        float firstAngle = cachedExpandAngles[0];
        for (int i = 0; i < n - 1; i++)
        {
            cachedExpandAngles[i] = cachedExpandAngles[i + 1];
        }
        cachedExpandAngles[n - 1] = firstAngle;

        SetPositions(cachedExpandAngles);
    }

    [ContextMenu("Move Skills Right")]
    /// <summary>
    /// Shift all skills one slot backward (rotating the cached angles right).
    /// Only valid in expanded mode.
    /// </summary>
    public void MoveSkillsToRight()
    {
        if (currentMode != WheelMode.Expanded)
            return;

        int n = cachedExpandAngles.Length;
        float lastAngle = cachedExpandAngles[n - 1];
        for (int i = n - 1; i > 0; i--)
        {
            cachedExpandAngles[i] = cachedExpandAngles[i - 1];
        }
        cachedExpandAngles[0] = lastAngle;

        SetPositions(cachedExpandAngles);
    }

    /// <summary>
    /// Calculates and sets the anchored positions based on given angles around the center.
    /// </summary>
    private void SetPositions(float[] angles)
    {
        for (int i = 0; i < skillIcons.Length; i++)
        {
            float deg = angles[i];
            float rad = deg * Mathf.Deg2Rad;
            Vector2 pos = new Vector2(
                Mathf.Cos(rad) * radius + center.position.x,
                Mathf.Sin(rad) * radius + center.position.y
            );
            skillIcons[i].position = pos;
        }

        HighlightSelectedSkill();
    }

    /// <summary>
    /// Highlights the skill at 270 degrees (downward) when in expanded mode.
    /// This example simply changes its scale; adapt as needed.
    /// </summary>
    private void HighlightSelectedSkill()
    {
        if (currentMode != WheelMode.Expanded)
            return;

        // Reset all
        foreach (var rt in skillIcons)
        {
            rt.localScale = Vector3.one;
        }

        // Find index closest to 180 degrees
        int selected = 0;
        float bestDiff = Mathf.Infinity;
        for (int i = 0; i < cachedExpandAngles.Length; i++)
        {
            float diff = Mathf.Abs(Mathf.DeltaAngle(cachedExpandAngles[i], 180f));
            if (diff < bestDiff)
            {
                bestDiff = diff;
                selected = i;
            }
        }

        // Example highlight: enlarge slightly
        skillIcons[selected].localScale = Vector3.one * 1.2f;
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
    
    // public float value = 0f; // Value to adjust angles by, can be set in Inspector


    // [ContextMenu("Rotate Angles by Degrees Expand")]
    // public void RotateAnglesByDegreesExpand()
    // {
    //     for (int i = 0; i < defaultExpandAngles.Length; i++)
    //     {
    //         defaultExpandAngles[i] += value;
    //         if (defaultExpandAngles[i] >= 360f)
    //         {
    //             defaultExpandAngles[i] -= 360f; // Wrap around to keep within 0-360 degrees
    //         }
    //     }
    // }

    // [ContextMenu("Rotate Angles by Degrees Contract")]
    // public void RotateAnglesByDegreesContract()
    // {
    //     for (int i = 0; i < contractAngles.Length; i++)
    //     {
    //         contractAngles[i] += value;
    //         if (contractAngles[i] >= 360f)
    //         {
    //             contractAngles[i] -= 360f; // Wrap around to keep within 0-360 degrees
    //         }
    //     }
    // }
}