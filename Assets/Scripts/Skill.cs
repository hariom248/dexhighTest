using UnityEngine.UI;
using UnityEngine;

public class Skill : MonoBehaviour
{
    public Image Image;
    public Sprite ActiveSprite;
    public Sprite InactiveSprite;
    public Button Button;


    // Update is called once per frame
    public void SetActiveStatus(bool isActive, bool interactable)
    {
        Image.sprite = isActive ? ActiveSprite : InactiveSprite;
        Button.interactable = interactable;
        Button.image.raycastTarget = interactable;
    }
}
