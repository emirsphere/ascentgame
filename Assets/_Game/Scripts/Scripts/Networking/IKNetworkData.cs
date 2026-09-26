using UnityEngine;
using FishNet.Documenting;

[System.Serializable]
public struct HandAnchorData
{
    public bool IsGripping; // Tutunuyor mu?
    public Vector3 Position; // Elin duracağı nokta
    public Vector3 Normal;   // Duvarın yönü (eller duvara yaslansın diye)
}