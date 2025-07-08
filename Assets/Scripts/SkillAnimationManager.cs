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
}