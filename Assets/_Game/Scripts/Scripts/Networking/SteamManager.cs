using UnityEngine;
using Steamworks;
using System;
using FishNet;

public class SteamManager : MonoBehaviour
{
    [Header("Steam Ayarları")]
    public uint AppId = 480;

    private void Awake()
    {
        try
        {
            // Eğer Steam hafızada zaten açıksa (Unity Editör huyu) tekrar başlatma
            if (SteamClient.IsValid)
            {
                Debug.Log($"[STEAM] Zaten bağlı. Tekrar başlatılmadı. Hoş geldin, {SteamClient.Name}");
                return;
            }

            // Steam API'sini başlatıyoruz
            SteamClient.Init(AppId, true);

            if (SteamClient.IsValid)
            {
                Debug.Log($"[STEAM] İlk kez başarıyla bağlanıldı! Hoş geldin, {SteamClient.Name}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[STEAM] Başlatılamadı! Hata: {e.Message}");
        }
    }

    private void Update()
    {
        // Yalnızca Steam bağlıysa callback'leri (davetleri vb.) dinle
        if (SteamClient.IsValid)
        {
            SteamClient.RunCallbacks();
        }
    }

    private void OnApplicationQuit()
    {
        // 1. Önce FishNet bağlantılarını kapatıyoruz (Steam soketleri açıkken güvenlice kapansın)
        if (InstanceFinder.NetworkManager != null)
        {
            InstanceFinder.ServerManager?.StopConnection(true);
            InstanceFinder.ClientManager?.StopConnection();
        }

        // 2. Ardından Steam'i tamamen kapatıyoruz
        if (SteamClient.IsValid)
        {
            SteamClient.Shutdown();
        }
    }
}