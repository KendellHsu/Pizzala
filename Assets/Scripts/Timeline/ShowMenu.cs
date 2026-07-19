using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ShowMenu : MonoBehaviour
{
    public CanvasGroup menu;

    public float fadeTime = 1f;

    public void OpenMenu()
    {
        StartCoroutine(FadeMenu());
    }

    IEnumerator FadeMenu()
    {
        float t = 0;

        menu.gameObject.SetActive(true);

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            menu.alpha = Mathf.Lerp(0, 1, t / fadeTime);
            yield return null;
        }

        menu.alpha = 1;
        menu.interactable = true;
        menu.blocksRaycasts = true;
    }
}