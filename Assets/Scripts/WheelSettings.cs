using UnityEngine;

[System.Serializable]
public struct WheelSettings
{
    public float[] Angles;
    public float  Radius;
    public float  SkillScale;
    public float  BaseScale;
    public float  BGAlpha;
    public float  BaseAlpha;
    public Vector2 CenterOffset;
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
        BaseScale = baseScale;
        BGAlpha = bgAlpha;
        BaseAlpha = baseAlpha;
        CenterOffset = centerOffset;
        CenterPosition = centerPosition;
    }
}