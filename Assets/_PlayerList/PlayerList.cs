using System;
using UdonSharp;
using UnityEngine.UI;
using VRC.SDKBase;
using UnityEngine;

public class PlayerList : UdonSharpBehaviour
{
    public Text Text;
    public GameObject SyncPool;

    public int UpdatesPerFrame = 5;

    [UdonSynced]
    private double master_joinTime;
    [UdonSynced]
    private int master_UTCOffset;
    [UdonSynced]
    private string master_name;

    public double local_joinTime;
    public int local_UTCOffset;
    public string local_name;

    private PlayerListSync[] pool;
    private int curUpdate = -1;

    private string[] texts;

    void Start()
    {
        var now = DateTime.Now;
        var utcNow = now.ToUniversalTime();
        var localOffsetMin = (int)(now - utcNow).TotalMinutes;
        this.local_UTCOffset = localOffsetMin;
        this.local_joinTime = Networking.GetServerTimeInSeconds();
        this.local_name = Networking.LocalPlayer.displayName;

        pool = new PlayerListSync[SyncPool.transform.childCount];
        var i = 0;
        foreach (Transform t in SyncPool.transform)
        {
            // GetComponent<> is apparently slow, so it makes double sense to cache it here
            pool[i] = t.gameObject.GetComponent<PlayerListSync>();
            i++;
        }

        texts = new string[pool.Length + 1];
    }

    private string Format(string name, double joinTime, long localOffsetMin, bool master)
    {
        var residency = TimeSpan.FromSeconds(Networking.GetServerTimeInSeconds() - joinTime);
        var joinTimeStr = residency.ToString(residency.TotalHours >= 1 ? @"h\:mm\:ss" : @"mm\:ss");

        var localTimeStr = (DateTime.UtcNow +
            TimeSpan.FromMinutes(localOffsetMin))
                .ToString("hh:mm tt");

        var suffix = "";
        if (name == local_name)
        {
            suffix += " (you)";
        }
        if (master)
        {
            suffix += " [master]";
        }
        if (name == "_pi_")
        {
            // hey that's me!
            suffix += " :)";
        }

        return $"{joinTimeStr} ({localTimeStr}) - {name}{suffix}";
    }

    void Update()
    {
        var changed = false;
        for (int i = 0; i < UpdatesPerFrame; i++)
        {
            if (curUpdate == -1)
            {
                // first entry is always master
                var tmp = Format(master_name, master_joinTime, master_UTCOffset, true);
                if (texts[0] != tmp)
                {
                    texts[0] = tmp;
                    changed = true;
                }
            }
            else
            {
                // else iterate entries
                string tmp = null;
                var cur = pool[curUpdate];
                var owner = Networking.GetOwner(cur.gameObject);
                if (!owner.isMaster)
                {
                    // the master is synced via the master_ props in this object, so if the Sync is owned
                    // by them, we know it's invalid (i.e. has been returned upon leaving of the assigned player)
                    if (cur.Name != owner.displayName)
                    {
                        // yet to set data
                        tmp = $"syncing - {owner.displayName}";
                    }
                    else
                    {
                        tmp = Format(cur.Name, cur.JoinTime, cur.UTCOffset, false);
                    }

                    if (owner.isLocal)
                    {
                        // this is our object, let's update it
                        // (doing Update() on all objects would be to expensive)
                        cur.ManualUpdate();
                    }
                }

                if (texts[curUpdate + 1] != tmp)
                {
                    texts[curUpdate + 1] = tmp;
                    changed = true;
                }
            }

            curUpdate++;
            if (curUpdate >= pool.Length)
            {
                curUpdate = -1;
            }
        }

        if (changed)
        {
            var text = "";
            // I'd really love to not have to iterate all 'texts' here, but I don't see how...
            foreach (var t in texts)
            {
                if (t != null)
                {
                    text += t + "\n";
                }
            }
            this.Text.text = text;

            var count = VRCPlayerApi.GetPlayerCount();
            this.Text.fontSize = count > 24 ? (count > 48 ? 20 : 34) : 70;
        }

        if (Networking.IsMaster && Networking.IsOwner(this.gameObject))
        {
            // Master update logic
            if (master_joinTime != local_joinTime || master_UTCOffset != local_UTCOffset || local_name != master_name)
            {
                master_joinTime = local_joinTime;
                master_UTCOffset = local_UTCOffset;
                master_name = local_name;
                this.RequestSerialization();
            }
        }
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsMaster && Networking.IsOwner(this.gameObject) && !player.isMaster)
        {
            // we're master, so handle join by giving out a Sync from the pool
            // note that there is no leave logic, as the object will be returned to master when it's owner leaves automatically
            foreach (var sync in pool)
            {
                if (Networking.IsOwner(sync.gameObject))
                {
                    Debug.Log($"[PlayerList] giving out object #{sync.gameObject.name} from pool to: {player.displayName} #{player.playerId}");
                    Networking.SetOwner(player, sync.gameObject);
                    break;
                }
            }
        }
    }
}
