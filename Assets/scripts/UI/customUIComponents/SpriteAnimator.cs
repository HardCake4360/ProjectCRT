using UnityEngine;
using System;
using UnityEngine.UI;
using System.Collections;

public class SpriteAnimator : MonoBehaviour
{
    [SerializeField] private bool onlyLoopDefault;
    [SerializeField] private float fps;

    public Image targetImage;
    public SpriteSheetObject[] Anims;
    private bool isAnimEnd;

    private void Start()
    {
        if (onlyLoopDefault)
        {
            StartCoroutine(PlaySpriteAnimation(Anims[0].Sprites));
        }
    }

    private void Update()
    {
        
    }

    IEnumerator PlaySpriteAnimation(Sprite[] sprites)
    {
        int index = 0;
        while (true)
        {
            targetImage.sprite = sprites[index];
            index = (index + 1) % sprites.Length;
            yield return new WaitForSeconds(1f/fps);
        }
    }

}
