using UnityEngine;
using System;
using UnityEngine.UI;
using System.Collections;

public struct SpriteNode
{
    public string name;
    public Sprite[] sprites;
}

public class SpriteAnimator : MonoBehaviour
{
    [SerializeField] private bool onlyLoopDefault;
    [SerializeField] private float fps;

    public Image targetImage;
    public SpriteNode[] nodes;
    public Sprite[] sprites0;
    public Sprite[] sprites1;
    private bool isAnimEnd;

    private void Start()
    {
        if (onlyLoopDefault)
        {
            StartCoroutine(PlaySpriteAnimation(sprites1));
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
