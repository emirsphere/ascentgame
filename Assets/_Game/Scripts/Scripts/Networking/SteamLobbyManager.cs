using UnityEngine;
using Steamworks;
using Steamworks.Data;
using FishNet.Managing;

public class SteamLobbyManager : MonoBehaviour
{
    [Header("Referanslar")]
    public NetworkManager NetworkManager;

    public Lobby? CurrentLobby;

    private void OnEnable()
    {
        SteamMatchmaking.OnLobbyCreated += OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered += OnLobbyEntered;
        SteamFriends.OnGameLobbyJoinRequested += OnGameLobbyJoinRequested;
    }

    private void OnDisable()
    {
        SteamMatchmaking.OnLobbyCreated -= OnLobbyCreated;
        SteamMatchmaking.OnLobbyEntered -= OnLobbyEntered;
        SteamFriends.OnGameLobbyJoinRequested -= OnGameLobbyJoinRequested;
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame)
        {
            CreateLobby();
        }
    }

    public async void CreateLobby()
    {
        Debug.Log("[LOBBY] Lobi oluşturuluyor...");
        Lobby? lobby = await SteamMatchmaking.CreateLobbyAsync(4);

        if (!lobby.HasValue)
        {
            Debug.LogError("[LOBBY] Steam lobisi oluşturulamadı!");
        }
    }

    private void OnLobbyCreated(Result result, Lobby lobby)
    {
        if (result != Result.OK)
        {
            Debug.LogError($"[LOBBY] Lobi kurma hatası: {result}");
            return;
        }

        lobby.SetFriendsOnly();
        lobby.SetData("name", SteamClient.Name + " - Tırmanış Lobisi");
        lobby.SetJoinable(true);

        Debug.Log("[LOBBY] Steam Lobisi hazır!");

        // Host olarak client adresini kendi Steam ID'mize ayarlıyoruz
        SetTransportClientAddress(SteamClient.SteamId.ToString());

        NetworkManager.ServerManager.StartConnection();
        NetworkManager.ClientManager.StartConnection();

        SteamFriends.OpenGameInviteOverlay(lobby.Id);
    }

    private async void OnGameLobbyJoinRequested(Lobby lobby, SteamId friendId)
    {
        Debug.Log($"[LOBBY] {friendId} adlı arkadaşın daveti kabul edildi. Lobiye giriliyor...");

        RoomEnter joinResult = await lobby.Join();
        if (joinResult != RoomEnter.Success)
        {
            Debug.LogError("[LOBBY] Lobiye katılım başarısız oldu!");
        }
    }

    private void OnLobbyEntered(Lobby lobby)
    {
        CurrentLobby = lobby;
        Debug.Log($"[LOBBY] Lobiye girildi: {lobby.GetData("name")}");

        if (lobby.Owner.Id != SteamClient.SteamId)
        {
            Debug.Log("[LOBBY] Arkadaşın sunucusuna bağlanılıyor...");

            // Davet edilen oyuncu, lobi sahibinin (Owner) Steam ID'sini adres olarak girer
            SetTransportClientAddress(lobby.Owner.Id.ToString());

            NetworkManager.ClientManager.StartConnection();
        }
    }

    private void SetTransportClientAddress(string address)
    {
        var transport = NetworkManager.TransportManager.Transport as FishyFacepunch.FishyFacepunch;
        if (transport != null)
        {
            // FishyFacepunch altındaki Client Address parametresini güncelliyoruz
            transport.SetClientAddress(address);
        }
        else
        {
            Debug.LogError("[LOBBY] NetworkManager üzerinde FishyFacepunch transport bileşeni bulunamadı!");
        }
    }
}