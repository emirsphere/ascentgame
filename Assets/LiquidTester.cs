using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LiquidTester : MonoBehaviour
{
    [Header("Referanslar")]
    public Image flashOverlay; // Hiyerarşideki Flash_Overlay objesini buraya sürükleyeceğiz

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TakeDamageEffect();
        }
    }

    public void TakeDamageEffect()
    {
        if (flashOverlay == null)
        {
            Debug.LogError("HATA: flashOverlay atanmamış! Lütfen Inspector'dan objeyi sürükle.");
            return;
        }

        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float flashDuration = 0.15f;
        float elapsedTime = 0f;

        // Vurulduğu an Alpha'yı %100 (1) yap, ekran bembeyaz olsun
        Color flashColor = Color.white;
        flashColor.a = 1f;
        flashOverlay.color = flashColor;

        // 0.15 saniye içinde Alpha değerini yumuşakça 0'a (görünmeze) indir
        while (elapsedTime < flashDuration)
        {
            elapsedTime += Time.deltaTime;
            flashColor.a = Mathf.Lerp(1f, 0f, elapsedTime / flashDuration);
            flashOverlay.color = flashColor;

            yield return null;
        }

        // Animasyon bitince tam 0'a sabitle
        flashColor.a = 0f;
        flashOverlay.color = flashColor;
    }
}