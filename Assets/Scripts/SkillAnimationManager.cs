using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SkillAnimationManager : MonoBehaviour
{
    [Header("Core References")]
    public RectTransform center;
    public RectTransform skillsParent;
    public Skill[] skills;

    public CanvasGroup BGImageCG;
    public CanvasGroup BaseImageCG;
    [Header("Buttons")]
    public Button BGButton;
    public Button SkillIconButton;

    [Header("Animation")]
    public float animationDuration = 0.5f;
    public float highlightedSkillScale = 1f;

    [Header("Wheel Settings")]
    public WheelSettings ContractSettings;
    public WheelSettings ExpandSettings;

    private enum WheelMode { Contracted, Expanded }
    private enum RotationMode { ForceClockwise, ForceCounterClockwise, ShortestPath }

    private WheelMode currentMode = WheelMode.Contracted;
    private RectTransform[] skillIcons;
    private float[] cachedExpandAngles;
    private int highlightedSkillIndex = 2;

    private Coroutine wheelAnim;
    private Coroutine baseAnim;

    private void Awake()
    {
        // Cache transforms & button callbacks
        skillIcons = new RectTransform[skills.Length];
        for (int i = 0; i < skills.Length; i++)
        {
            skills[i].SetActiveStatus(false, false);
            int idx = i;
            skills[i].Button.onClick.AddListener(() => SkillClicked(idx));
            skillIcons[i] = skills[i].GetComponent<RectTransform>();
        }

        BGButton.onClick.AddListener(() => MoveToState(ContractSettings, WheelMode.Contracted));
        SkillIconButton.onClick.AddListener(() => MoveToState(ExpandSettings, WheelMode.Expanded));

        // Cache expand angles for interactive rotation
        cachedExpandAngles = new float[ExpandSettings.Angles.Length];
        ExpandSettings.Angles.CopyTo(cachedExpandAngles, 0);

        // Snap into contracted at start
        MoveToCompactMode();
    }

    [ContextMenu("Move Skills to Compact Mode")]
    public void MoveToCompactMode()
        => MoveToState(ContractSettings, WheelMode.Contracted);

    [ContextMenu("Move Skills to Expand Mode")]
    public void MoveToExpandMode()
        => MoveToState(ExpandSettings, WheelMode.Expanded);

    private void MoveToState(WheelSettings settings, WheelMode targetMode)
    {
        if (currentMode == targetMode) return;
        currentMode = targetMode;

        // decide rotation mode
        bool free = cachedExpandAngles[0] == 135f;
        var rotation = free
            ? RotationMode.ShortestPath
            : (targetMode == WheelMode.Expanded
                ? RotationMode.ForceClockwise
                : RotationMode.ForceCounterClockwise);

        // pick angles array: expanded interactive uses cachedExpandAngles
        var angles = (targetMode == WheelMode.Expanded)
            ? cachedExpandAngles
            : settings.Angles;

        AnimateToAngles(settings, angles, rotation);
        AnimateBase(settings);
    }

    private void AnimateToAngles(WheelSettings settings, float[] targetAngles, RotationMode mode)
    {
        if (wheelAnim != null) StopCoroutine(wheelAnim);
        wheelAnim = StartCoroutine(DoAnimateWheel(settings, targetAngles, mode));
    }

    private IEnumerator DoAnimateWheel(WheelSettings settings, float[] finalAngles, RotationMode mode)
    {
        int n = skillIcons.Length;
        var startA = new float[n];
        var targetA = new float[n];
        var targetS = new float[n];

        // compute start/target
        for (int i = 0; i < n; i++)
        {
            // start angle
            Vector2 off = skillIcons[i].anchoredPosition;
            startA[i] = Mathf.Atan2(off.y, off.x) * Mathf.Rad2Deg;

            // target angle delta
            float dest = finalAngles[i] % 360f;
            float delta = Mathf.DeltaAngle(startA[i], dest);

            if (mode == RotationMode.ForceClockwise && delta > 0) delta -= 360f;
            if (mode == RotationMode.ForceCounterClockwise && delta < 0) delta += 360f;

            targetA[i] = startA[i] + delta;

            // target scale
            targetS[i] = (currentMode == WheelMode.Contracted)
                ? settings.SkillScale
                : GetExpandedScale(i, settings.SkillScale);
        }

        float elapsed = 0f;
        UpdateHighlightedSkill();
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.Lerp(startA[i], targetA[i], t) * Mathf.Deg2Rad;
                float radius = settings.Radius;
                skillIcons[i].anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;

                float sc = Mathf.Lerp(skillIcons[i].localScale.x, targetS[i], t);
                skillIcons[i].localScale = Vector3.one * sc;
            }

            yield return null;
        }

        // finalize
        SnapToAngles(finalAngles, settings.Radius);
        SnapToScales(targetS);
        wheelAnim = null;
    }

    private void AnimateBase(WheelSettings settings)
    {
        if (baseAnim != null) StopCoroutine(baseAnim);
        baseAnim = StartCoroutine(DoAnimateBase(settings));
    }

    private IEnumerator DoAnimateBase(WheelSettings s)
    {
        Vector2 startPos    = center.anchoredPosition;
        float   startScale  = center.localScale.x;
        Vector2 startSkills = skillsParent.anchoredPosition;
        float   bgStart     = BGImageCG.alpha;
        float   baseStart   = BaseImageCG.alpha;

        Vector2 endPos      = s.CenterPosition.anchoredPosition;
        float   endScale    = s.BaseScale;
        Vector2 endSkills   = s.CenterOffset;
        float   endBG       = s.BGAlpha;
        float   endBaseBG   = s.BaseAlpha;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            center.anchoredPosition    = Vector2.Lerp(startPos, endPos, t);
            center.localScale          = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            skillsParent.anchoredPosition = Vector2.Lerp(startSkills, endSkills, t);
            BGImageCG.alpha            = Mathf.Lerp(bgStart, endBG, t);
            BaseImageCG.alpha          = Mathf.Lerp(baseStart, endBaseBG, t);

            yield return null;
        }

        // finalize
        center.anchoredPosition    = endPos;
        center.localScale          = Vector3.one * endScale;
        skillsParent.anchoredPosition = endSkills;
        BGImageCG.alpha            = endBG;
        BaseImageCG.alpha          = endBaseBG;

        baseAnim = null;
    }

    private void SnapToScales(float[] scales)
    {
        for (int i = 0; i < skillIcons.Length; i++)
            skillIcons[i].localScale = Vector3.one * scales[i];
    }

    private void SnapToAngles(float[] angles, float radius)
    {
        for (int i = 0; i < skillIcons.Length; i++)
        {
            float deg = angles[i] % 360f;
            skillIcons[i].anchoredPosition = new Vector2(
                Mathf.Cos(deg * Mathf.Deg2Rad) * radius,
                Mathf.Sin(deg * Mathf.Deg2Rad) * radius
            );
        }
    }

    public void SkillClicked(int index)
    {
        if (currentMode != WheelMode.Expanded || index == highlightedSkillIndex)
            return;

        int n = cachedExpandAngles.Length;
        int steps = (highlightedSkillIndex - index + n) % n;
        if (steps == 0) return;

        // rotate cached angles
        var newA = new float[n];
        for (int i = 0; i < n; i++)
            newA[i] = cachedExpandAngles[(i + steps) % n];
        cachedExpandAngles = newA;

        highlightedSkillIndex = index;

        // choose shortest
        var mode = (steps <= n - steps)
            ? RotationMode.ForceClockwise
            : RotationMode.ForceCounterClockwise;

        AnimateToAngles(ExpandSettings, cachedExpandAngles, mode);
    }

    private void UpdateHighlightedSkill()
    {
        bool expanded = currentMode == WheelMode.Expanded;
        for (int i = 0; i < skills.Length; i++)
            skills[i].SetActiveStatus(i == highlightedSkillIndex && expanded, expanded);
    }

    private float GetExpandedScale(int idx, float baseScale)
    {
        int n = skills.Length;
        if (idx == highlightedSkillIndex)
            return highlightedSkillScale;
        if ((idx + 1) % n == highlightedSkillIndex || (idx - 1 + n) % n == highlightedSkillIndex)
            return (highlightedSkillScale + baseScale) * 0.5f;
        return baseScale;
    }
}