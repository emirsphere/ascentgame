using System;
using UnityEngine;

public class StaminaController : MonoBehaviour
{
    public float AbsoluteMaxStamina { get; private set; } // Teorik en yüksek limit (Örn: 100)
    public float CurrentMaxStamina { get; private set; }  // Ceza yemiş mevcut limit (Örn: 80)
    public float CurrentStamina { get; private set; }     // Şu anki gücümüz
    public float PenaltyAmount { get; private set; }      // Ne kadar ağırlık taşıyoruz (Örn: 20)

    // UI'ın dinleyeceği Event (Artık 4 veri yolluyor)
    public event Action<float, float, float, float> OnStaminaChanged;
    // Parametreler: (Mevcut, MevcutLimit, MutlakLimit, CezaMiktari)

private void Start()
{
    // Oyun başlar başlamaz staminayı 100 olarak kur ve UI'a "Ben hazırım" mesajı (Event) gönder
    if (AbsoluteMaxStamina <= 0)
    {
        Initialize(100f); 
    }
}
    public void Initialize(float maxStamina)
    {
        AbsoluteMaxStamina = maxStamina;
        PenaltyAmount = 0f; // Başlangıçta ceza yok
        UpdateCapacities();
        CurrentStamina = CurrentMaxStamina;

        TriggerEvent();
    }

    // Ağırlık aldığında (veya zehirlendiğinde) çağrılacak metot
    public void ApplyPenalty(float amount)
    {
        PenaltyAmount += amount;
        UpdateCapacities();
    }

    // Ağırlığı yere attığında çağrılacak metot
    public void RemovePenalty(float amount)
    {
        PenaltyAmount -= amount;
        if (PenaltyAmount < 0) PenaltyAmount = 0;
        UpdateCapacities();
    }

    // Kapasite sınırlarını hesaplayan iç mekanizma
    private void UpdateCapacities()
    {
        // Mevcut sınır, toplam kapasiteden cezanın çıkarılmasıyla bulunur
        CurrentMaxStamina = AbsoluteMaxStamina - PenaltyAmount;

        // Eğer taşıdığımız yük yüzünden mevcut staminamız yeni sınırı geçtiyse, onu da aşağı çek
        if (CurrentStamina > CurrentMaxStamina)
        {
            CurrentStamina = CurrentMaxStamina;
        }
        TriggerEvent();
    }

    // Fiziksel harcama (Tırmanma, zıplama)
    public void ConsumeStamina(float amount)
    {
        if (CurrentStamina <= 0) return;

        CurrentStamina -= amount;
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0, CurrentMaxStamina);
        TriggerEvent();
    }

    // Dinlenme (Yerde bekleme)
    public void RegenerateStamina(float amount)
    {
        if (CurrentStamina >= CurrentMaxStamina) return;

        CurrentStamina += amount;
        CurrentStamina = Mathf.Clamp(CurrentStamina, 0, CurrentMaxStamina);
        TriggerEvent();
    }

    private void TriggerEvent()
    {
        OnStaminaChanged?.Invoke(CurrentStamina, CurrentMaxStamina, AbsoluteMaxStamina, PenaltyAmount);
    }
}