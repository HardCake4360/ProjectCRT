using UnityEngine;
using System;
using UnityEngine.UI;
using System.Collections;

public class SpriteAnimator : MonoBehaviour
{
    public Image targetImage;
    public Sprite[] sprites0;
    public Sprite[] sprites1;
    private bool isAnimEnd;

    private void Update()
    {
        if (isAnimEnd)
        {
            //StartCoroutine(DelayAction(UnityEngine.Random.RandomRange(5f, 8f), { PlaySpriteAnimation(sprites1)}));
            
        }

    }

    IEnumerator DelayAction(float time,Action action)
    {
        yield return new WaitForSeconds(time);
        
    }

    IEnumerator PlaySpriteAnimation(Sprite[] sprites)
    {
        int index = 0;
        while (true)
        {
            targetImage.sprite = sprites[index];
            index = (index + 1) % sprites.Length;
            yield return new WaitForSeconds(0.1f); // 0.1초마다 변경
        }
    }

}
