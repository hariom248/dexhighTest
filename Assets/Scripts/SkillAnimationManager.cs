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

    [Header("Animation/Scaling Settings")]
    public float AnimationDuration;
    public float HighlightedSkillScale;

    [Header("Wheel Settings")]
    public WheelSettings ContractSettings; 
    public WheelSettings ExpandSettings;

    private enum RotationMode { ForceClockwise, ForceCounterClockwise, ShortestPath }

    private RectTransform[] skillIcons;
    private float[] cachedExpandAngles;
    private int highlightedSkillIndex = 2; // default highlighted skill index
    private bool isExpanded = false;

    private Coroutine skillsAnimCoroutine;
    private Coroutine baseAnimCoroutine;

    private void Awake()
    {
        ValidateConfiguration();
        InitializeSkillSystem();
        SnapLayout(ContractSettings);
    }

    private void ValidateConfiguration()
    {
        if (ContractSettings.Angles.Length != Skills.Length || 
            ExpandSettings.Angles.Length != Skills.Length)
        {
            Debug.LogError("WheelSettings angles length must match number of skills!");
        }
    }

    private void InitializeSkillSystem()
    {
        skillIcons = new RectTransform[Skills.Length];
        cachedExpandAngles = (float[])ExpandSettings.Angles.Clone();

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
    }

    /// <summary>
    /// Transition to either expanded or contracted state.
    /// If already in the target state, does nothing.
    /// </summary>
    private void Transition(bool toExpand)
    {
        if (isExpanded == toExpand) return;
        isExpanded = toExpand;

        var from = toExpand ? ContractSettings : ExpandSettings;
        var to = toExpand ? ExpandSettings : ContractSettings;

        RotationMode mode = Mathf.Approximately(cachedExpandAngles[0], 135f)
            ? RotationMode.ShortestPath
            : (toExpand
                ? RotationMode.ForceClockwise
                : RotationMode.ForceCounterClockwise);

        // build target settings
        var target = toExpand ? to.WithAngles(cachedExpandAngles) : to;

        AnimateSkills(from, target, mode);
        AnimateBaseIcon(to);
    }

    /// <summary>
    /// Handles skill icon clicks.
    /// Rotates the wheel to highlight the clicked skill.
    /// If already highlighted, does nothing.
    /// </summary>
    private void OnSkillClicked(int index)
    {
        if (!isExpanded || index == highlightedSkillIndex) return;

        int n = cachedExpandAngles.Length;
        int steps = (highlightedSkillIndex - index + n) % n;
        if (steps == 0) return;

        var newAngles = new float[n];
        for (int i = 0; i < n; i++)
            newAngles[i] = cachedExpandAngles[(i + steps) % n];
        cachedExpandAngles = newAngles;

        highlightedSkillIndex = index;

        var mode = (steps <= n - steps)
            ? RotationMode.ForceClockwise
            : RotationMode.ForceCounterClockwise;
        
        var to = ExpandSettings.WithAngles(cachedExpandAngles);
        AnimateSkills(ExpandSettings, to, mode);
    }

    /// <summary>
    /// Animates the skill icons from one WheelSettings state to another.
    /// </summary>
    private void AnimateSkills(WheelSettings from, WheelSettings to, RotationMode mode)
    {
        if (skillsAnimCoroutine != null) StopCoroutine(skillsAnimCoroutine);
        UpdateHighlights();
        skillsAnimCoroutine = StartCoroutine(DoAnimateSkills(from, to, mode));
    }

    /// <summary>
    /// Performs the actual animation of skill icons.
    /// </summary>
    private IEnumerator DoAnimateSkills(WheelSettings from, WheelSettings to, RotationMode mode)
    {
        int n = skillIcons.Length;
        var startAngles = new float[n];
        var targetAngles = new float[n];
        var targetScales = new float[n];

        for (int i = 0; i < n; i++)
        {
            startAngles[i] = CartesianToAngle(skillIcons[i].anchoredPosition);
            float dest = to.Angles[i] % 360f;
            float delta = ComputeDelta(startAngles[i], dest, mode);
            targetAngles[i] = startAngles[i] + delta;
            targetScales[i] = GetTargetScale(i, to.SkillScale);
        }

        float elapsed = 0f;
        while (elapsed < AnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / AnimationDuration);

            for (int i = 0; i < n; i++)
            {
                float ang = Mathf.Lerp(startAngles[i], targetAngles[i], t);
                float radius = Mathf.Lerp(from.Radius, to.Radius, t);
                skillIcons[i].anchoredPosition = PolarToCartesian(ang, radius);

                float sc = Mathf.Lerp(skillIcons[i].localScale.x, targetScales[i], t);
                skillIcons[i].localScale = Vector3.one * sc;
            }

            yield return null;
        }

        SnapSkills(to);
        skillsAnimCoroutine = null;
    }

    /// <summary>
    /// Animates the base icon and its related UI elements
    /// </summary>
    private void AnimateBaseIcon(WheelSettings s)
    {
        if (baseAnimCoroutine != null) StopCoroutine(baseAnimCoroutine);
        baseAnimCoroutine = StartCoroutine(DoAnimateBaseIcon(s));
    }

    /// <summary>
    /// Performs the actual animation of the base icon and related UI elements.
    /// </summary>
    private IEnumerator DoAnimateBaseIcon(WheelSettings s)
    {
        var startPos = SkillsBaseIcon.anchoredPosition;
        var endPos = s.CenterPosition.anchoredPosition;
        var startScale = SkillsBaseIcon.localScale.x;
        var endScale = s.SkillsBaseIconScale;
        var startSkills = SkillsParent.anchoredPosition;
        var endSkills = s.SkillsParentOffset;
        var bgStart = BGOverlay.alpha;
        var bgEnd = s.BGOverlayAlpha;
        var baseStart = SkillsBasePanel.alpha;
        var baseEnd = s.SkillsBasePanelAlpha;

        float elapsed = 0f;
        while (elapsed < AnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / AnimationDuration);

            SkillsBaseIcon.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            SkillsBaseIcon.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            SkillsParent.anchoredPosition = Vector2.Lerp(startSkills, endSkills, t);
            BGOverlay.alpha = Mathf.Lerp(bgStart, s.BGOverlayAlpha, t);
            SkillsBasePanel.alpha = Mathf.Lerp(baseStart, s.SkillsBasePanelAlpha, t);

            yield return null;
        }

        SkillsBaseIcon.anchoredPosition = endPos;
        SkillsBaseIcon.localScale = Vector3.one * endScale;
        SkillsParent.anchoredPosition = endSkills;
        BGOverlay.alpha = bgEnd;
        SkillsBasePanel.alpha = baseEnd;

        baseAnimCoroutine = null;
    }

    /// <summary>
    /// Snaps the layout to the specified WheelSettings.
    /// This is used to ensure the layout is correct after initialization or when settings change.
    /// </summary>
    private void SnapLayout(WheelSettings s)
    {
        SnapSkills(s);
        SnapBaseIcon(s);
    }

    /// <summary>
    /// Snaps the skill icons to their positions and scales based on the provided WheelSettings.
    /// </summary>
    private void SnapSkills(WheelSettings s)
    {
        for (int i = 0; i < skillIcons.Length; i++)
        {
            skillIcons[i].anchoredPosition = PolarToCartesian(s.Angles[i], s.Radius);
            skillIcons[i].localScale = Vector3.one * GetTargetScale(i, s.SkillScale);
        }
    }

    /// <summary>
    /// Snaps the base icon and related UI elements to their positions and scales based on the provided WheelSettings.
    /// </summary>
    private void SnapBaseIcon(WheelSettings s)
    {
        SkillsBaseIcon.anchoredPosition = s.CenterPosition.anchoredPosition;
        SkillsBaseIcon.localScale = Vector3.one * s.SkillsBaseIconScale;
        SkillsParent.anchoredPosition = s.SkillsParentOffset;
        BGOverlay.alpha = s.BGOverlayAlpha;
        SkillsBasePanel.alpha = s.SkillsBasePanelAlpha;
    }

    /// <summary>
    /// Updates the active status of skill icons based on the highlighted skill index and expansion state.
    /// </summary>
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
        if (mode == RotationMode.ForceClockwise && d > 0) d -= 360f;
        if (mode == RotationMode.ForceCounterClockwise && d < 0) d += 360f;
        return d;
    }

    private float GetTargetScale(int idx, float baseScale)
    {
        if (!isExpanded)
            return baseScale;

        int n = Skills.Length;
        if (idx == highlightedSkillIndex)
            return HighlightedSkillScale;
        if ((idx + 1) % n == highlightedSkillIndex ||
            (idx - 1 + n) % n == highlightedSkillIndex)
            return (HighlightedSkillScale + baseScale) * 0.5f;
        return baseScale;
    }

}
