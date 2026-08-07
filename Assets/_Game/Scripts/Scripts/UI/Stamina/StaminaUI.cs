using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class StaminaUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private StaminaController targetController;
    [SerializeField] private Image fillImage;
    [SerializeField] private Image penaltyImage;

    [Header("Juice Settings")]
    [SerializeField] private Color criticalColor = Color.red;
    [Tooltip("Tırmanırken barın ne kadar hızlı takip edeceği (Tokluk hissi)")]
    [SerializeField] private float fillLerpSpeed = 15f;
    [Tooltip("Renk değişim hızı")]
    [SerializeField] private float colorLerpSpeed = 10f;

    private Color _originalFigmaColor;
    private Color _burnColor; // Aktif harcamada barın alacağı "Yanan" renk

    private float _targetFill;
    private float _lastPenalty;
    private float _lastStamina;
    private float _idleTimer;

    private bool _isCritical = false;
    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();

        if (fillImage != null)
        {
            _originalFigmaColor = fillImage.color;
            // YANMA RENGİ: Senin yeşilinin çok daha parlak, beyaza/sarıya çalan fosforlu hali. 
            // Oyuncu efor sarf ettiğini direkt bu renkten hissedecek.
            _burnColor = Color.Lerp(_originalFigmaColor, Color.white, 0.6f);
        }
    }
    
    private void Start()
{
    if (targetController != null)
    {
        // Hedef değerleri mevcut duruma göre hesapla
        _targetFill = targetController.CurrentStamina / targetController.AbsoluteMaxStamina;
        
        // Barı yavaşça Lerp ile doldurmak yerine, oyun açılışında küt diye (anında) dolu göster
        fillImage.fillAmount = _targetFill;
        
        // Rengi de Figma'dan gelen orijinal renge eşitle
        fillImage.color = _originalFigmaColor;
        
        // Ceza barını da varsa anında yerine oturt
        if (penaltyImage != null)
        {
            penaltyImage.fillAmount = targetController.PenaltyAmount / targetController.AbsoluteMaxStamina;
        }
    }
}

    private void OnEnable()
    {
        if (targetController != null) targetController.OnStaminaChanged += UpdateTargetValues;
    }

    private void OnDisable()
    {
        if (targetController != null) targetController.OnStaminaChanged -= UpdateTargetValues;
    }

    // Event artık sadece HEDEF değerleri belirliyor, animasyon oynatmıyor! (Spaghetti'yi önledik)
    private void UpdateTargetValues(float current, float currentMax, float absoluteMax, float penalty)
    {
        _targetFill = current / absoluteMax;

        float targetPenalty = penalty / absoluteMax;
        if (penaltyImage != null && Mathf.Abs(_lastPenalty - targetPenalty) > 0.001f)
        {
            _lastPenalty = targetPenalty;
            penaltyImage.DOFillAmount(targetPenalty, 0.5f).SetEase(Ease.OutCubic);
        }

        // ANİ ZIPLAMA (LEAP) KONTROLÜ - Sadece büyük harcamalarda DOTween devreye girer
        if (fillImage.fillAmount - _targetFill >= 0.1f)
        {
            _rectTransform.DOKill(true);
            _rectTransform.DOPunchScale(new Vector3(0.15f, 0.15f, 0f), 0.3f, 10, 1f);

            fillImage.DOKill(true);
            fillImage.color = Color.white; // Anında saf beyaz flaş!
        }

        // Kritik seviye kontrolü
        _isCritical = (current / currentMax) <= 0.2f;
    }

    // GÖRSEL AKIŞIN VE HİSSİYATIN GERÇEKLEŞTİĞİ YER
    private void Update()
    {
        if (targetController == null) return;

        // 1. PÜRÜZSÜZ AZALIŞ (Sürekli Lerp)
        fillImage.fillAmount = Mathf.Lerp(fillImage.fillAmount, _targetFill, Time.deltaTime * fillLerpSpeed);

        // 2. RENK YANMASI (PULSE EFFECT)
        if (_isCritical)
        {
            // Kritik durum: Kırmızı ile Orijinal renk arası hızlı nabız
            float pingPong = Mathf.PingPong(Time.time * 6f, 1f);
            fillImage.color = Color.Lerp(criticalColor, _originalFigmaColor, pingPong);
        }
        else if (targetController.CurrentStamina < _lastStamina)
        {
            // --- AKTİF EFOR SARF EDİLİYOR (TIRMANIŞ) ---
            _idleTimer = 0f;

            // Time.time kullanarak saniyede birkaç kez 0 ile 1 arasında gidip gelen bir dalga yaratıyoruz
            // 8f değeri nabzın atış hızıdır. Artırırsan daha hızlı yanıp söner.
            float pulseWave = Mathf.PingPong(Time.time * 8f, 1f);

            // Orijinal renk ile yanma rengi arasında dalgaya göre gidip gel
            Color currentTargetColor = Color.Lerp(_originalFigmaColor, _burnColor, pulseWave);

            // Rengi pürüzsüzce uygula
            fillImage.color = Color.Lerp(fillImage.color, currentTargetColor, Time.deltaTime * colorLerpSpeed);
        }
        else
        {
            // --- DİNLENME MODU ---
            _idleTimer += Time.deltaTime;
            if (_idleTimer > 0.1f)
            {
                // Tırmanış bittiğinde dalgalanmayı kesip yavaşça orijinal renge soğut
                fillImage.color = Color.Lerp(fillImage.color, _originalFigmaColor, Time.deltaTime * colorLerpSpeed);
            }
        }

        _lastStamina = targetController.CurrentStamina;
    }
}