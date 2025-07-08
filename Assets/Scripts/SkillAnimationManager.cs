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
    public WheelSettings ContractSettings; // e.g. SkillScale = 1.1f
    public WheelSettings ExpandSettings;

    private enum RotationMode { ForceClockwise, ForceCounterClockwise, ShortestPath }

    private RectTransform[] skillIcons;
    private float[] cachedExpandAngles;
    private int highlightedSkillIndex = 2;
    private bool isExpanded = false;

    private Coroutine wheelAnim;
    private Coroutine baseAnim;

    private void Awake()
    {
        // cache icons & hook clicks
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

        // copy expand angles
        cachedExpandAngles = (float[])ExpandSettings.Angles.Clone();

        // initial snap to contracted
        SnapToState(ContractSettings, false);
    }

    public void MoveToCompactMode() => Transition(false);
    public void MoveToExpandMode()  => Transition(true);

    private void Transition(bool toExpand)
    {
        // update flag explicitly
        isExpanded = toExpand;

        // decide from/to settings
        var from = toExpand ? ContractSettings : ExpandSettings;
        var to   = toExpand ? ExpandSettings   : ContractSettings;

        // choose rotation rule
        bool freePath = Mathf.Approximately(cachedExpandAngles[0], 135f);
        var mode = freePath
            ? RotationMode.ShortestPath
            : (toExpand
                ? RotationMode.ForceClockwise
                : RotationMode.ForceCounterClockwise);

        // pick angles array
        var angles = toExpand ? cachedExpandAngles : to.Angles;

        AnimateWheel(from, to, angles, mode);
        AnimateBase(to);
    }

    private void AnimateWheel(
        WheelSettings from,
        WheelSettings to,
        float[] finalAngles,
        RotationMode mode)
    {
        if (wheelAnim != null) StopCoroutine(wheelAnim);
        wheelAnim = StartCoroutine(DoAnimateWheel(from, to, finalAngles, mode));
    }

    private IEnumerator DoAnimateWheel(
        WheelSettings from,
        WheelSettings to,
        float[] finalAngles,
        RotationMode mode)
    {
        int n = skillIcons.Length;
        var startA  = new float[n];
        var targetA = new float[n];
        var targetS = new float[n];

        for (int i = 0; i < n; i++)
        {
            // capture start angle
            Vector2 off = skillIcons[i].anchoredPosition;
            startA[i] = Mathf.Atan2(off.y, off.x) * Mathf.Rad2Deg;

            // compute shortest/forced rotation
            float dest = finalAngles[i] % 360f;
            float delta = Mathf.DeltaAngle(startA[i], dest);
            if (mode == RotationMode.ForceClockwise && delta > 0) delta -= 360f;
            if (mode == RotationMode.ForceCounterClockwise && delta < 0) delta += 360f;
            targetA[i] = startA[i] + delta;

            // assign target scale based on explicit flag
            targetS[i] = isExpanded
                ? GetExpandedScale(i, to.SkillScale)
                : to.SkillScale;
        }

        float elapsed = 0f;
        UpdateHighlights();
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            for (int i = 0; i < n; i++)
            {
                // position
                float ang    = Mathf.Lerp(startA[i], targetA[i], t) * Mathf.Deg2Rad;
                float radius = Mathf.Lerp(from.Radius, to.Radius, t);
                skillIcons[i].anchoredPosition = new Vector2(
                    Mathf.Cos(ang), Mathf.Sin(ang)
                ) * radius;

                // scale
                float scOld = skillIcons[i].localScale.x;
                float scNew = targetS[i];
                skillIcons[i].localScale = Vector3.one * Mathf.Lerp(scOld, scNew, t);
            }

            yield return null;
        }

        // finalize
        SnapAngles(finalAngles, to.Radius);
        SnapScales(targetS);
        wheelAnim = null;
    }

    private void AnimateBase(WheelSettings s)
    {
        if (baseAnim != null) StopCoroutine(baseAnim);
        baseAnim = StartCoroutine(DoAnimateBase(s));
    }

    private IEnumerator DoAnimateBase(WheelSettings s)
    {
        var startPos    = center.anchoredPosition;
        var startScale  = center.localScale.x;
        var startSkills = skillsParent.anchoredPosition;
        var bgStart     = BGImageCG.alpha;
        var baseStart   = BaseImageCG.alpha;

        var endPos      = s.CenterPosition.anchoredPosition;
        var endScale    = s.BaseScale;
        var endSkills   = s.CenterOffset;
        var endBG       = s.BGAlpha;
        var endBaseBG   = s.BaseAlpha;

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);

            center.anchoredPosition       = Vector2.Lerp(startPos, endPos, t);
            center.localScale             = Vector3.one * Mathf.Lerp(startScale, endScale, t);
            skillsParent.anchoredPosition = Vector2.Lerp(startSkills, endSkills, t);
            BGImageCG.alpha               = Mathf.Lerp(bgStart, endBG, t);
            BaseImageCG.alpha             = Mathf.Lerp(baseStart, endBaseBG, t);

            yield return null;
        }

        center.anchoredPosition       = endPos;
        center.localScale             = Vector3.one * endScale;
        skillsParent.anchoredPosition = endSkills;
        BGImageCG.alpha               = endBG;
        BaseImageCG.alpha             = endBaseBG;

        baseAnim = null;
    }

    private void SnapToState(WheelSettings s, bool expanded)
    {
        // position & scale
        for (int i = 0; i < skillIcons.Length; i++)
        {
            float rad = s.Angles[i] * Mathf.Deg2Rad;
            skillIcons[i].anchoredPosition = new Vector2(
                Mathf.Cos(rad), Mathf.Sin(rad)
            ) * s.Radius;

            float scale = expanded
                ? GetExpandedScale(i, s.SkillScale)
                : s.SkillScale;  // CONTRACT uses s.SkillScale (e.g. 1.1)
            skillIcons[i].localScale = Vector3.one * scale;
        }

        UpdateHighlights();

        center.anchoredPosition       = s.CenterPosition.anchoredPosition;
        center.localScale             = Vector3.one * s.BaseScale;
        skillsParent.anchoredPosition = s.CenterOffset;
        BGImageCG.alpha               = s.BGAlpha;
        BaseImageCG.alpha             = s.BaseAlpha;
    }

    private void SnapAngles(float[] angles, float radius)
    {
        for (int i = 0; i < skillIcons.Length; i++)
        {
            float deg = angles[i] * Mathf.Deg2Rad;
            skillIcons[i].anchoredPosition = new Vector2(
                Mathf.Cos(deg), Mathf.Sin(deg)
            ) * radius;
        }
    }

    private void SnapScales(float[] scales)
    {
        for (int i = 0; i < skillIcons.Length; i++)
            skillIcons[i].localScale = Vector3.one * scales[i];
    }

    private void OnSkillClicked(int index)
    {
        if (!isExpanded || index == highlightedSkillIndex) return;

        int n     = cachedExpandAngles.Length;
        int steps = (highlightedSkillIndex - index + n) % n;
        if (steps == 0) return;

        // rotate cache
        var newA = new float[n];
        for (int i = 0; i < n; i++)
            newA[i] = cachedExpandAngles[(i + steps) % n];
        cachedExpandAngles = newA;

        highlightedSkillIndex = index;
        var mode = (steps <= n - steps)
            ? RotationMode.ForceClockwise
            : RotationMode.ForceCounterClockwise;

        AnimateWheel(ExpandSettings, ExpandSettings, cachedExpandAngles, mode);
    }

    private void UpdateHighlights()
    {
        for (int i = 0; i < skills.Length; i++)
            skills[i].SetActiveStatus(
                isExpanded && i == highlightedSkillIndex,
                isExpanded
            );
    }

    private float GetExpandedScale(int idx, float baseScale)
    {
        int n = skills.Length;
        if (idx == highlightedSkillIndex)
            return highlightedSkillScale;
        if ((idx + 1) % n == highlightedSkillIndex ||
            (idx - 1 + n) % n == highlightedSkillIndex)
            return (highlightedSkillScale + baseScale) * 0.5f;
        return baseScale;
    }
}
