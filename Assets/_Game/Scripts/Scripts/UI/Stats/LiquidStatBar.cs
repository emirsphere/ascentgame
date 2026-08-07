using UnityEngine;

public class LiquidStatBar : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("İçindeki kırmızı sıvı objesinin RectTransform bileşeni")]
    [SerializeField] private RectTransform waveRect;

    [Header("Stat Settings")]
    public float maxValue = 100f;
    public float currentValue = 100f;

    [Header("Position Boundaries (Y Axis)")]
    [Tooltip("Stat 100 iken dalganın Y eksenindeki durduğu yer")]
    [SerializeField] private float maxY = 0f;
    [Tooltip("Stat 0 iken dalganın aşağıda kaybolduğu Y pozisyonu")]
    [SerializeField] private float minY = -60f;

    [Header("Animation Settings")]
    [Tooltip("Stat azaldığında aşağı inme yumuşaklığı")]
    [SerializeField] private float dropSpeed = 5f;
    [Tooltip("Suyun sağa-sola çalkalanma hızı")]
    [SerializeField] private float waveSpeed = 2f;
    [Tooltip("Suyun sağa-sola ne kadar açılacağı (piksel)")]
    [SerializeField] private float waveAmplitude = 10f;

    private float targetY;
    private Vector2 currentPos;

    void Start()
    {
        // Başlangıç pozisyonunu al ve hedef Y değerini hesapla
        currentPos = waveRect.anchoredPosition;
        UpdateTargetY();
    }

    void Update()
    {
        if (waveRect == null) return;

        // 1. Y EKSENİ: Yumuşak Düşüş (Lerp)
        // Mevcut Y pozisyonundan, hedef Y pozisyonuna Time.deltaTime kullanarak pürüzsüzce in.
        currentPos.y = Mathf.Lerp(currentPos.y, targetY, Time.deltaTime * dropSpeed);

        // 2. X EKSENİ: Sinüs Dalgası ile Çalkalanma
        // Time.time sürekli artan bir değerdir. Sinüs fonksiyonu bu artışı -1 ile 1 arasında salınan bir grafiğe çevirir.
        // waveAmplitude ile çarparak bu salınımın genişliğini piksel cinsinden (örneğin -10 ile 10 arasında) belirliyoruz.
        currentPos.x = Mathf.Sin(Time.time * waveSpeed) * waveAmplitude;

        // Hesaplanan yeni X ve Y değerlerini RectTransform'a uygula
        waveRect.anchoredPosition = currentPos;
    }

    // Başka bir scriptten karakter hasar aldığında bu fonksiyonu çağıracaksın.
    // Örnek kullanım: statBar.SetStat(75f);
    public void SetStat(float newValue)
    {
        // Gelen değeri 0 ile maksimum değer arasına kilitliyoruz (bug'ları önlemek için)
        currentValue = Mathf.Clamp(newValue, 0f, maxValue);
        UpdateTargetY();
    }

    // Hedeflenen Y pozisyonunu yüzdelik orana göre matematiksel olarak hesaplar
    private void UpdateTargetY()
    {
        float fillPercentage = currentValue / maxValue; // Örn: 50/100 = 0.5f
        targetY = Mathf.Lerp(minY, maxY, fillPercentage); // 0.5f oranını Min Y ve Max Y arasına yerleştir
    }
    // Unity'de editör (Inspector) üzerinden herhangi bir değişkeni değiştirdiğinde otomatik olarak tetiklenir.
    // Bu sayede Play modundayken veya oyun duruyorken currentValue'yu değiştirdiğinde anlık test edebilirsin.
    private void OnValidate()
    {
        // Eğer max statı veya mevcut statı Inspector'dan değiştirirsek hemen sınırla ve Y hedefini güncelle.
        currentValue = Mathf.Clamp(currentValue, 0f, maxValue);
        UpdateTargetY();
    }
}