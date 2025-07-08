using UnityEngine;

[System.Serializable]
public struct WheelSettings
{
    public float[] Angles;
    public float  Radius;
    public float  SkillScale;
    public float  SkillsBaseIconScale;
    public float  BGOverlayAlpha;
    public float  SkillsBasePanelAlpha;
    public Vector2 SkillsParentOffset;
    public RectTransform CenterPosition;

    public WheelSettings(
        float[] angles,
        float radius,
        float skillScale,
        float baseScale,
        float bgAlpha,
        float baseAlpha,
        Vector2 centerOffset,
        RectTransform centerPosition)
    {
        // Clone the angles array so it’s safe to rotate in-place:
        Angles = (float[])angles.Clone();
        Radius = radius;
        SkillScale = skillScale;
        SkillsBaseIconScale = baseScale;
        BGOverlayAlpha = bgAlpha;
        SkillsBasePanelAlpha = baseAlpha;
        SkillsParentOffset = centerOffset;
        CenterPosition = centerPosition;
    }

     public WheelSettings WithAngles(float[] newAngles)
        => new WheelSettings(newAngles, Radius, SkillScale, SkillsBaseIconScale, BGOverlayAlpha, SkillsBasePanelAlpha, SkillsParentOffset, CenterPosition);
}