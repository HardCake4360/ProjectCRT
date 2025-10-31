using UnityEngine;
using System;
using UnityEngine.UI;
using System.Collections;

public class PortraitAnimator : MonoBehaviour
{
    [SerializeField] private float fps;
    public Image TargetImage;

    public void SetPortrait(SpriteSheetObject sheet)
    {
        if(sheet == null) return;
        if(sheet.name == "null")
        {
            TargetImage.sprite = sheet.Sprites[0];
            return;
        }
        StopAllCoroutines();
        StartCoroutine(PlaySpriteAnimation(sheet.Sprites));
    }

    IEnumerator PlaySpriteAnimation(Sprite[] sprites)
    {
        int index = 0;
        while (true)
        {
            TargetImage.sprite = sprites[index];
            index = (index + 1) % sprites.Length;
            yield return new WaitForSeconds(1f / fps);
        }
    }
}
