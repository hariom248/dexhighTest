using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SkillAnimationManager : MonoBehaviour
{
    public RectTransform center;
    private RectTransform[] skillIcons;
    public float animationDuration = 0.5f;

    private enum WheelMode { Contracted, Expanded }

    private enum RotationMode
    {
        ForceClockwise,
        ForceCounterClockwise,
        ShortestPath
    }

    private WheelMode currentMode = WheelMode.Contracted;

    public float[] contractAngles = { 90f, 18f, 306f, 234f, 162f };
    public float[] defaultExpandAngles = { 270f, 225f, 180f, 135f, 90f };
    private float[] cachedExpandAngles;

    private Coroutine centerAnimation;
    private Coroutine currentAnimation;
    private float[] startAngles;
    private float[] targetAngles;

    public RectTransform contractPosition;
    public RectTransform expandPosition;

    public RectTransform skillsParent;

    public CanvasGroup BGImageCG;
    public CanvasGroup BaseImageCG;

    public float BGImageExpandAlpha = 0.3f;
    public float BaseImageExpandAlpha = 1f;

    public float contractBaseScale = 1f;
    public float expandBaseScale = 2f;

    public float contractSkillRadius = 100f;
    public float expandSkillRadius = 75f;

    public float contractSkillScale = 1f;
    public float expandSkillScale = 0.6f;

    public float highlightedSkillScale = 1f;

    public Vector2 skillsCenterOffsetExpand;
    public Vector2 skillsCenterOffsetContract;

    public Button BGButton;
    public Button SkillIconButton;
    
    public Skill[] skills; // Array of Skill components for interaction

    // Cache the highlighted skill index (default is 2 for 180° position)
    private int highlightedSkillIndex = 2;

    private void Awake()
    {
        skillIcons = new RectTransform[skills.Length];
        for (int i = 0; i < skills.Length; i++)
        {
            // Set initial positions and scales
            skills[i].SetActiveStatus(false, false);
            int j = i;
            skills[i].Button.onClick.AddListener(() => SkillClicked(j));
            skillIcons[i] = skills[i].GetComponent<RectTransform>();
        }
        BGButton.onClick.AddListener(MoveToCompactMode);
        SkillIconButton.onClick.AddListener(MoveToExpandMode);
        // Initialize caches
        cachedExpandAngles = new float[defaultExpandAngles.Length];
        defaultExpandAngles.CopyTo(cachedExpandAngles, 0);
        startAngles = new float[skillIcons.Length];
        targetAngles = new float[skillIcons.Length];

        // Start snapped in contracted mode
        SnapToAngles(contractAngles, contractSkillRadius);
    }

    [ContextMenu("Move Skills to Compact Mode")]
    /// <summary>
    /// Collapse wheel back to contracted layout, choosing sweep direction
    /// based on Skill1's last expand angle:
    /// angles 0–180° → sweep clockwise; angles 180–360° → sweep counter-clockwise.
    /// </summary>
    public void MoveToCompactMode()
    {
        if (currentMode == WheelMode.Contracted) return;

        currentMode = WheelMode.Contracted;

        // Special case when coming from 135° position
        bool useFreePaths = cachedExpandAngles[0] == 135f;
        AnimateToAngles(contractAngles,
                    useFreePaths ? RotationMode.ShortestPath :
                    RotationMode.ForceCounterClockwise);

        if (centerAnimation != null) StopCoroutine(centerAnimation);
        centerAnimation = StartCoroutine(AnimateBase());
    }

    [ContextMenu("Move Skills to Expand Mode")]
    public void MoveToExpandMode()
    {
        if (currentMode == WheelMode.Expanded) return;

        currentMode = WheelMode.Expanded;

        // Special case when returning to 135° position
        bool useFreePaths = cachedExpandAngles[0] == 135f;
        AnimateToAngles(cachedExpandAngles,
                    useFreePaths ? RotationMode.ShortestPath :
                    RotationMode.ForceClockwise);

        if (centerAnimation != null) StopCoroutine(centerAnimation);
        centerAnimation = StartCoroutine(AnimateBase());
    }


    private void AnimateToAngles(float[] angles, RotationMode mode)
    {
        if (currentAnimation != null) StopCoroutine(currentAnimation);

        var targetScales = new float[skillIcons.Length];

        // record start angles/scales
        for (int i = 0; i < skillIcons.Length; i++)
        {
            Vector2 offset = skillIcons[i].anchoredPosition;
            startAngles[i] = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            // compute target scale
            if (currentMode == WheelMode.Contracted)
                targetScales[i] = contractSkillScale;
            else
                targetScales[i] = GetScaleFromIndex(i);
            // compute rotation delta
            float target = angles[i] % 360f;
            float delta = Mathf.DeltaAngle(startAngles[i], target);

            switch (mode)
            {
                case RotationMode.ForceClockwise:
                    if (delta > 0) delta -= 360f;
                    break;

                case RotationMode.ForceCounterClockwise:
                    if (delta < 0) delta += 360f;
                    break;

                case RotationMode.ShortestPath:
                default:
                    // Keep natural delta for shortest path
                    break;
            }

            targetAngles[i] = startAngles[i] + delta;
        }

        currentAnimation = StartCoroutine(AnimatePositions(angles, targetScales));

        float GetScaleFromIndex(int index)
        {
            if (index == highlightedSkillIndex)
            {
                return highlightedSkillScale;
            }
            else if (index - 1 == highlightedSkillIndex || index + 1 == highlightedSkillIndex)
            {
                return (highlightedSkillScale + expandSkillScale) * 0.5f;
            }
            else
            {
                return expandSkillScale;
            }
        }
    }

    private IEnumerator AnimatePositions(float[] finalAngles, float[] targetScales)
    {
        float elapsed = 0f;
        float radius = currentMode == WheelMode.Contracted ? contractSkillRadius : expandSkillRadius;
        UpdateHighlightedSkill();
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
                // scale
                float sc = Mathf.Lerp(skillIcons[i].localScale.x, targetScales[i], s);
                skillIcons[i].localScale = Vector3.one * sc;
            }

            yield return null;
        }
        // finalize
        SnapToAngles(finalAngles, radius);
        SnapToScales(targetScales);
        currentAnimation = null;
    }

    private IEnumerator AnimateBase()
    {
        Vector2 endPos = currentMode == WheelMode.Expanded ? expandPosition.anchoredPosition : contractPosition.anchoredPosition;
        float endScale = currentMode == WheelMode.Expanded ? expandBaseScale : contractBaseScale;
        Vector2 skillsParentPos = currentMode == WheelMode.Expanded ? skillsCenterOffsetExpand : skillsCenterOffsetContract;
        float bgImageAlpha = currentMode == WheelMode.Expanded ? BGImageExpandAlpha : 0f;
        float baseImageAlpha = currentMode == WheelMode.Expanded ? BaseImageExpandAlpha : 0f;
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);
            center.anchoredPosition = Vector2.Lerp(center.anchoredPosition, endPos, t);
            center.localScale = Vector3.one * Mathf.Lerp(center.localScale.x, endScale, t);
            skillsParent.localPosition = Vector2.Lerp(skillsParent.localPosition, skillsParentPos, t);
            BGImageCG.alpha = Mathf.Lerp(BGImageCG.alpha, bgImageAlpha, t);
            BaseImageCG.alpha = Mathf.Lerp(BaseImageCG.alpha, baseImageAlpha, t);
            yield return null;
        }

        center.anchoredPosition = endPos;
        center.localScale = Vector3.one * endScale;
    }

    private void SnapToScales(float[] targetScales)
    {
        for (int i = 0; i < skillIcons.Length; i++)
            skillIcons[i].localScale = Vector3.one * targetScales[i];
    }

    private void SnapToAngles(float[] angles, float radius)
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
    }

    public void SkillClicked(int index)
    {
        if (currentMode != WheelMode.Expanded) return;

        if (index == highlightedSkillIndex) return; 

        int n = cachedExpandAngles.Length;

        // Calculate how many steps to rotate to bring clicked skill to highlighted position
        int stepsToRotate = (highlightedSkillIndex - index + n) % n;
        
        if (stepsToRotate == 0) return; // Already at target position

        // Create new angles array by rotating
        float[] newAngles = new float[n];
        for (int i = 0; i < n; i++)
        {
            newAngles[i] = cachedExpandAngles[(i + stepsToRotate) % n];
        }
        
        // Update cached angles
        cachedExpandAngles = newAngles;

        // Update highlighted skill index - the clicked skill is now at the highlighted position
        highlightedSkillIndex = index;

        // Determine rotation direction based on shortest path
        int forwardSteps = stepsToRotate;
        int backwardSteps = (n - stepsToRotate) % n;
        
        RotationMode mode = forwardSteps <= backwardSteps 
            ? RotationMode.ForceClockwise      // Forward/left shift = clockwise
            : RotationMode.ForceCounterClockwise;  // Backward/right shift = counter-clockwise

        AnimateToAngles(cachedExpandAngles, mode);
    }

    private void UpdateHighlightedSkill()
    {
        for (int i = 0; i < skillIcons.Length; i++)
        {
            if (currentMode == WheelMode.Contracted)
            {
                skills[i].SetActiveStatus(false, false);
            }
            else
            {
                // Use cached highlighted skill index instead of calculating from 180°
                bool isHighlighted = i == highlightedSkillIndex;
                skills[i].SetActiveStatus(isHighlighted, true);
            }
        }
    }
    
    private void OnDrawGizmos()
    {
        if (center == null) return;

        Vector3 centerPosContract = center.position;
        Vector3 centerPosExpand = center.position;
        centerPosExpand.x += 200f; // Offset for expanded mode visualization

        centerPosContract.x -= 200f;

        float radius = currentMode == WheelMode.Contracted ? contractSkillRadius : expandSkillRadius;

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
        float radius = currentMode == WheelMode.Contracted ? contractSkillRadius : expandSkillRadius;
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

        float radius = currentMode == WheelMode.Contracted ? contractSkillRadius : expandSkillRadius;

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