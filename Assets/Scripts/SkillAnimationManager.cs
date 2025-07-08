using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SkillAnimationManager : MonoBehaviour
{
    [Header("Core References")]
    public RectTransform SkillsBaseIcon;
    public RectTransform SkillsParent;
    public Skill[] Skills;

    public CanvasGroup BGOverlay;
    public CanvasGroup SkillsBasePanel;

    [Header("Buttons")]
    public Button BGButton;
    public Button SkillsBaseIconButton;

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
        if (ContractSettings.Angles.Length != Skills.Length ||
            ExpandSettings.Angles.Length  != Skills.Length)
        {
            Debug.LogError("WheelSettings.Angles length must match number of skills!");
        }

        // cache transforms & hook clicks
        skillIcons = new RectTransform[Skills.Length];
        for (int i = 0; i < Skills.Length; i++)
        {
            Skills[i].SetActiveStatus(false, false);
            int idx = i;
            Skills[i].Button.onClick.AddListener(() => OnSkillClicked(idx));
            skillIcons[i] = Skills[i].GetComponent<RectTransform>();
        }

        // wire up transitions
        BGButton.onClick.AddListener(() => Transition(false));
        SkillsBaseIconButton.onClick.AddListener(() => Transition(true));

        // cache expand angles
        cachedExpandAngles = (float[])ExpandSettings.Angles.Clone();

        // initial snap
        SnapLayout(ContractSettings);
    }

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
        var target = toExpand ? to.WithAngles(cachedExpandAngles) : to;

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
        var startPos    = SkillsBaseIcon.anchoredPosition;
        var endPos      = s.CenterPosition.anchoredPosition;
        var startScale  = SkillsBaseIcon.localScale.x;
        var endScale    = s.SkillsBaseIconScale;
        var startSkills = SkillsParent.anchoredPosition;
        var endSkills   = s.SkillsParentOffset;
        var bgStart     = BGOverlay.alpha;
        var bgEnd       = s.BGOverlayAlpha;
        var baseStart   = SkillsBasePanel.alpha;
        var baseEnd     = s.SkillsBasePanelAlpha;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            SkillsBaseIcon.anchoredPosition       = Vector2.Lerp(startPos, endPos, t);
            SkillsBaseIcon.localScale             = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            SkillsParent.anchoredPosition = Vector2.Lerp(startSkills, endSkills, t);
            BGOverlay.alpha               = Mathf.Lerp(bgStart, s.BGOverlayAlpha, t);
            SkillsBasePanel.alpha             = Mathf.Lerp(baseStart, s.SkillsBasePanelAlpha, t);

            yield return null;
        }

        SkillsBaseIcon.anchoredPosition       = endPos;
        SkillsBaseIcon.localScale             = Vector3.one * endScale;
        SkillsParent.anchoredPosition = endSkills;
        BGOverlay.alpha               = s.BGOverlayAlpha;
        SkillsBasePanel.alpha             = s.SkillsBasePanelAlpha;

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
        SkillsBaseIcon.anchoredPosition       = s.CenterPosition.anchoredPosition;
        SkillsBaseIcon.localScale             = Vector3.one * s.SkillsBaseIconScale;
        SkillsParent.anchoredPosition = s.SkillsParentOffset;
        BGOverlay.alpha               = s.BGOverlayAlpha;
        SkillsBasePanel.alpha             = s.SkillsBasePanelAlpha;
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
        var to = ExpandSettings.WithAngles(cachedExpandAngles);
        AnimateSkills(ExpandSettings, to, mode);
    }

    private void UpdateHighlights()
    {
        for (int i = 0; i < Skills.Length; i++)
            Skills[i].SetActiveStatus(i == highlightedSkillIndex && isExpanded, isExpanded);
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

        int n = Skills.Length;
        if (idx == highlightedSkillIndex)
            return highlightedSkillScale;
        if ((idx + 1) % n == highlightedSkillIndex ||
            (idx - 1 + n) % n == highlightedSkillIndex)
            return (highlightedSkillScale + baseScale) * 0.5f;
        return baseScale;
    }

}
