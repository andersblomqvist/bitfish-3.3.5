using System;
using System.Collections.Generic;

namespace Bitfish
{
    /// <summary>
    /// This class stores functions used by the bot
    /// It needs to read memory and the current config.
    /// </summary>
    public class BotFunctions
    {
        private readonly MemoryReader mem;
        private Config config;

        // Tracks players which have been close to us. The key(ulong) is an unique 
        // identifier for each player. The value(int) holds the time when the player
        // was spotted first time.
        private readonly Dictionary<ulong, int> playerTracker;

        public BotFunctions(MemoryReader mem)
        {
            this.mem = mem;
            playerTracker = new Dictionary<ulong, int>();
        }

        /// <summary>
        /// Sets current config for functions to use
        /// </summary>
        /// <param name="config"></param>
        public void SetConfig(Config config)
        {
            this.config = config;
        }

        /// <summary>
        /// Checks wheter the session timers for afk has been exceeded. The afk
        /// time is hardcoded as 5 min (300s). This function also updates the
        /// nextAfkTime field in session.
        /// </summary>
        internal bool IsPlayerAfk(ref Bot.Stats session)
        {
            if (session.seconds - session.startTime > session.nextAfkTime)
            {
                session.nextAfkTime = session.seconds + Bot.Stats.afkTime;
                Console.WriteLine($"[{session.seconds}] Preventing <Away> tag. Next check at [{session.nextAfkTime}]");
                return true;
            }
            else
                return false;
        }

        /// <summary>
        /// Searches for nearby players and adds them to the playerTracker dict.
        /// If a player has been close to us for too long we return true. Otherwise
        /// we return false.
        /// </summary>
        /// <param name="radius"></param>
        /// <param name="maxTime"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        internal bool NearbyPlayers(int radius, int maxTime, Bot.Stats session)
        {
            if (!config.NearbyPlayer)
                return false;

            // Track which players have left the nearby radius. These are keys which
            // will be removed from the tracker dict.
            List<ulong> keysToRemove = new List<ulong>();

            GameObject[] nearby = mem.GetNearbyPlayers(radius);
            foreach (GameObject player in nearby)
            {
                if (player == null)
                    break;

                if (!playerTracker.ContainsKey(player.guid))
                    playerTracker.Add(player.guid, session.seconds);
            }

            foreach (ulong key in playerTracker.Keys)
            {
                // search nearby list
                bool seen = false;
                foreach (GameObject player in nearby)
                {
                    if (player == null)
                        break;

                    if (player.guid == key)
                    {
                        playerTracker.TryGetValue(key, out int lastSeen);
                        seen = true;
                        Console.WriteLine($"[{session.seconds}] Nearby player: {key:X} {lastSeen}");
                        int time = session.seconds - lastSeen;
                        if (time > maxTime) return true;
                    }
                }
                // key is not close anymore, remove it
                if (!seen) keysToRemove.Add(key);
            }

            foreach (ulong key in keysToRemove)
            {
                playerTracker.TryGetValue(key, out int value);
                Console.WriteLine($"[{session.seconds}] Player left, removing key: {key:X} {value}");
                playerTracker.Remove(key);
            }

            return false;
        }


        /// <summary>
        /// Checks if the timer has expired
        /// </summary>
        /// <returns></returns>
        internal bool HasTimerExpired(Bot.Stats session)
        {
            if (!config.EnableTimer)
                return false;

            if (session.seconds > config.TimerDuration * 60 + session.startTime)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Checks if player has moved away from its fishing position
        /// </summary>
        /// <returns>True if distance moved greater than 1, otherwise false</returns>
        internal bool HasPlayerMoved(Point fishPos)
        {
            double d = Point.Distance(fishPos, mem.ReadPlayerPosition());
            if (d > 1) return true;
            else return false;
        }

        /// <summary>
        /// Compares previous health with current.
        /// </summary>
        /// <param name="health"></param>
        /// <returns>True if current < previous</returns>
        internal bool IsPlayerInCombat()
        {
            mem.LuaDoString("combat = UnitAffectingCombat('player')");
            string combat = mem.LuaGetLocalizedText("combat");
            if (combat.Length >= 1) return true;
            else return false;
        }

        /// <summary>
        /// </summary>
        /// <returns>True if player is dead</returns>
        internal bool IsPlayerDead()
        {
            int health = mem.ReadPlayerHealth();
            if (health <= 1) return true;
            else return false;
        }

        /// <summary>
        /// Reads the number of free inventory slots available
        /// </summary>
        /// <returns>slots available in bag</returns>
        internal int GetFreeInventorySlots()
        {
            int slots = 0;
            for (int i = 0; i < 5; i++)
            {
                mem.LuaDoString($"freeSlots = GetContainerNumFreeSlots({i})");
                string res = mem.LuaGetLocalizedText("freeSlots");
                slots += Convert.ToInt32(res);
            }
            return slots;
        }

        /// <summary>
        /// Get the number of seconds until the next Wintergrasp battle. <br></br>
        /// </summary>
        /// <returns>The number of seconds until the next Wintergrasp battle, or nil if currently in progress.</returns>
        internal int GetWGTimer()
        {
            mem.LuaDoString($"seconds = GetWintergraspWaitTime()");
            string res = mem.LuaGetLocalizedText("seconds");
            if(res.Length != 0)
                return Convert.ToInt32(res);
            return 0;
        }

        /// <summary>
        /// Reads what zone player is in and determine wheter we are in WG or not.
        /// </summary>
        /// <returns>The Zone name</returns>
        internal bool IsPlayerInWG()
        {
            mem.LuaDoString("zone = GetZoneText()");
            string currentZone = mem.LuaGetLocalizedText("zone");
            Console.WriteLine($"We are in: [{currentZone}]");
            if (currentZone == "Wintergrasp")
                return true;
            else return false;
        }

        /// <summary>
        /// Equipps the fishing pole if we option enabled.
        /// </summary>
        internal void EquipFishingPole()
        {
            if (config.AutoEquip)
            {
                // equip selected fishing pole
                string pole = BitfishForm.instance.GetFishingPole();
                Console.WriteLine($"Equipping: [{pole}]");
                mem.LuaDoString($"EquipItemByName(\"{pole}\")");
            }
        }

        /// <summary>
        /// Releases spirit and check if we should logout
        /// </summary>
        /// <param name="randomLag"></param>
        internal void ReleaseAndLogout(int randomLag)
        {
            Console.WriteLine("Releasing spirit");
            System.Threading.Thread.Sleep(randomLag);
            mem.LuaDoString("RepopMe()");
            System.Threading.Thread.Sleep(5000);
            if (config.HearthstoneWhenDone)
                Console.WriteLine("Can't cast hearthstone when dead!");
            if (config.LogoutWhenDone || config.LogoutWhenDead)
                mem.LuaDoString("Logout()");
        }
    }
}
