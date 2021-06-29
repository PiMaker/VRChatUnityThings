using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

public class PlayerListSync : UdonSharpBehaviour
{
    [UdonSynced]
    public string Name;

    [UdonSynced]
    public int UTCOffset;

    [UdonSynced]
    public double JoinTime;

    public PlayerList Controller;

    public void ManualUpdate()
    {
        var player = Networking.LocalPlayer;
        if (player.displayName != Name || Controller.local_joinTime != JoinTime || Controller.local_UTCOffset != UTCOffset)
        {
            Debug.Log($"[PlayerListSync] set data for {player.displayName} #{player.playerId}");

            this.Name = player.displayName;
            this.UTCOffset = Controller.local_UTCOffset;
            this.JoinTime = Controller.local_joinTime;

            this.RequestSerialization();
        }
    }
}