using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    public WheelSettings ContractSettings; // ensure Angles.Length == skills.Length
    public WheelSettings ExpandSettings;

    private enum RotationMode { ForceClockwise, ForceCounterClockwise, ShortestPath }

    private RectTransform[] skillIcons;
    private float[] cachedExpandAngles;
    private int highlightedSkillIndex = 2;
    private bool isExpanded = false;

    private Coroutine skillsAnimCoroutine;
    private Coroutine baseAnimCoroutine;

    void Awake()
    {
        // sanity check
        if (ContractSettings.Angles.Length != skills.Length ||
            ExpandSettings.Angles.Length  != skills.Length)
        {
            Debug.LogError("WheelSettings.Angles length must match number of skills!");
        }

        // cache transforms & hook clicks
        skillIcons = new RectTransform[skills.Length];
        for (int i = 0; i < skills.Length; i++)
        {
            skills[i].SetActiveStatus(false, false);
            int idx = i;
            skills[i].Button.onClick.AddListener(() => OnSkillClicked(idx));
            skillIcons[i] = skills[i].GetComponent<RectTransform>();
        }

        // wire up transitions
        BGButton.onClick.AddListener(() => Transition(false));
        SkillIconButton.onClick.AddListener(() => Transition(true));

        // cache expand angles
        cachedExpandAngles = (float[])ExpandSettings.Angles.Clone();

        // initial snap
        SnapLayout(ContractSettings);
    }

    public void MoveToCompactMode() => Transition(false);
    public void MoveToExpandMode()  => Transition(true);

    private void Transition(bool toExpand)
    {
        if (isExpanded == toExpand) return;
        isExpanded = toExpand;

        var from = toExpand ? ContractSettings : ExpandSettings;
        var to   = toExpand ? ExpandSettings   : ContractSettings;

        // choose rotation rule
        RotationMode mode = Mathf.Approximately(cachedExpandAngles[0], 135f)
            ? RotationMode.ShortestPath
            : (toExpand
                ? RotationMode.ForceClockwise
                : RotationMode.ForceCounterClockwise);

        // build target settings (clone angles if needed)
        WheelSettings target = to;
        if (toExpand)
            target.Angles = (float[])cachedExpandAngles.Clone();

        AnimateSkills(from, target, mode);
        AnimateBase(to);
    }

    private void AnimateSkills(WheelSettings from, WheelSettings to, RotationMode mode)
    {
        if (skillsAnimCoroutine != null) StopCoroutine(skillsAnimCoroutine);
        skillsAnimCoroutine = StartCoroutine(DoAnimateWheel(from, to, mode));
    }

    private IEnumerator DoAnimateWheel(WheelSettings from, WheelSettings to, RotationMode mode)
    {
        int n = skillIcons.Length;
        var startAngles  = new float[n];
        var targetAngles = new float[n];
        var targetScales = new float[n];

        // prepare
        for (int i = 0; i < n; i++)
        {
            startAngles[i]  = CartesianToAngle(skillIcons[i].anchoredPosition);
            float dest      = to.Angles[i] % 360f;
            float delta     = ComputeDelta(startAngles[i], dest, mode);
            targetAngles[i] = startAngles[i] + delta;
            targetScales[i] = GetTargetScale(i, to.SkillScale);
        }

        UpdateHighlights();

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            for (int i = 0; i < n; i++)
            {
                float ang    = Mathf.Lerp(startAngles[i], targetAngles[i], t);
                float radius = Mathf.Lerp(from.Radius, to.Radius, t);
                skillIcons[i].anchoredPosition = PolarToCartesian(ang, radius);

                float sc = Mathf.Lerp(skillIcons[i].localScale.x, targetScales[i], t);
                skillIcons[i].localScale = Vector3.one * sc;
            }

            yield return null;
        }

        // Snap to new state
        SnapSkills(to);
        skillsAnimCoroutine = null;
    }

    private void AnimateBase(WheelSettings s)
    {
        if (baseAnimCoroutine != null) StopCoroutine(baseAnimCoroutine);
        baseAnimCoroutine = StartCoroutine(DoAnimateBase(s));
    }

    private IEnumerator DoAnimateBase(WheelSettings s)
    {
        var startPos    = center.anchoredPosition;
        var endPos      = s.CenterPosition.anchoredPosition;
        var startScale  = center.localScale.x;
        var endScale    = s.BaseScale;
        var startSkills = skillsParent.anchoredPosition;
        var endSkills   = s.CenterOffset;
        var bgStart     = BGImageCG.alpha;
        var bgEnd       = s.BGAlpha;
        var baseStart   = BaseImageCG.alpha;
        var baseEnd     = s.BaseAlpha;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            center.anchoredPosition       = Vector2.Lerp(startPos, endPos, t);
            center.localScale             = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            skillsParent.anchoredPosition = Vector2.Lerp(startSkills, endSkills, t);
            BGImageCG.alpha               = Mathf.Lerp(bgStart, s.BGAlpha, t);
            BaseImageCG.alpha             = Mathf.Lerp(baseStart, s.BaseAlpha, t);

            yield return null;
        }

        center.anchoredPosition       = endPos;
        center.localScale             = Vector3.one * endScale;
        skillsParent.anchoredPosition = endSkills;
        BGImageCG.alpha               = s.BGAlpha;
        BaseImageCG.alpha             = s.BaseAlpha;

        baseAnimCoroutine = null;
    }

    private void SnapLayout(WheelSettings s)
    {
       SnapSkills(s);
       SnapBase(s);
    }
    
    private void SnapSkills(WheelSettings s)
    {
        for (int i = 0; i < skillIcons.Length; i++)
        {
            skillIcons[i].anchoredPosition = PolarToCartesian(s.Angles[i], s.Radius);
            skillIcons[i].localScale       = Vector3.one * GetTargetScale(i, s.SkillScale);
        }
    }

    private void SnapBase(WheelSettings s)
    {
        center.anchoredPosition       = s.CenterPosition.anchoredPosition;
        center.localScale             = Vector3.one * s.BaseScale;
        skillsParent.anchoredPosition = s.CenterOffset;
        BGImageCG.alpha               = s.BGAlpha;
        BaseImageCG.alpha             = s.BaseAlpha;
    }

    private void OnSkillClicked(int index)
    {
        if (!isExpanded || index == highlightedSkillIndex) return;

        int n = cachedExpandAngles.Length;
        int steps = (highlightedSkillIndex - index + n) % n;
        if (steps == 0) return;

        // rotate cache
        var newAngles = new float[n];
        for (int i = 0; i < n; i++)
            newAngles[i] = cachedExpandAngles[(i + steps) % n];
        cachedExpandAngles = newAngles;

        highlightedSkillIndex = index;

        var mode = (steps <= n - steps)
            ? RotationMode.ForceClockwise
            : RotationMode.ForceCounterClockwise;

        // animate with updated angles
        var to = ExpandSettings;
        to.Angles = newAngles;
        AnimateSkills(ExpandSettings, to, mode);
    }

    private void UpdateHighlights()
    {
        for (int i = 0; i < skills.Length; i++)
            skills[i].SetActiveStatus(i == highlightedSkillIndex && isExpanded, isExpanded);
    }

    // ─────────── Helpers ───────────

    private Vector2 PolarToCartesian(float deg, float radius)
    {
        float rad = deg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
    }

    private float CartesianToAngle(Vector2 pos)
    {
        return Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;
    }

    private float ComputeDelta(float fromDeg, float toDeg, RotationMode mode)
    {
        float d = Mathf.DeltaAngle(fromDeg, toDeg);
        if (mode == RotationMode.ForceClockwise     && d >  0) d -= 360f;
        if (mode == RotationMode.ForceCounterClockwise && d <  0) d += 360f;
        return d;
    }

    private float GetTargetScale(int idx, float baseScale)
    {
        if (!isExpanded) 
            return baseScale;

        int n = skills.Length;
        if (idx == highlightedSkillIndex)
            return highlightedSkillScale;
        if ((idx + 1) % n == highlightedSkillIndex ||
            (idx - 1 + n) % n == highlightedSkillIndex)
            return (highlightedSkillScale + baseScale) * 0.5f;
        return baseScale;
    }

}
