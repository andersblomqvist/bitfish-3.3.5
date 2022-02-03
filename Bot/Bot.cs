using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace Bitfish
{
    /// <summary>
    /// TODO:
    /// Logging
    /// </summary>
    public class Bot
    {
        private struct Stats { public int seconds, startTime, fishCaught; }
        private struct Player { ulong guid; int lastSeen; }

        private readonly BackgroundWorker worker;
        private readonly BackgroundWorker clock;
        private readonly MemoryReader mem;
        private readonly Random random;
        private Config config;
        private Stats session;

        // blacklist these guids, clicked bobbers stay in memory for a small time
        // the que will hold last 5 bobbers
        private readonly Queue<ulong> prevBobbers;

        // Tracks players which have been close to us. The key(ulong) is an unique 
        // identifier for each player. The value(int) holds the time when the player
        // was spotted first time.
        private Dictionary<ulong, int> playerTracker;

        // Tracks wheter the bot as failed to fish {maxFails} number times in a row
        private bool failed;
        private readonly int maxFails;

        public readonly int afkTime;
        private int nextAfkTime;

        public Bot(MemoryReader mem)
        {
            this.mem = mem;
            worker = new BackgroundWorker();
            clock = new BackgroundWorker();
            random = new Random();
            prevBobbers = new Queue<ulong>();
            playerTracker = new Dictionary<ulong, int>();
            session = new Stats();

            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;
            worker.DoWork += new DoWorkEventHandler(DoWork);
            worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Finished);
            worker.ProgressChanged += new ProgressChangedEventHandler(FishCaught);

            clock.WorkerReportsProgress = true;
            clock.WorkerSupportsCancellation = true;
            clock.DoWork += new DoWorkEventHandler(ClockWork);
            clock.ProgressChanged += new ProgressChangedEventHandler(ClockTick);

            failed = false;
            maxFails = 3;

            afkTime = 312; // 5min 12s
        }

        /// <summary>
        /// Starts the bot. Before we start the fishing routune we first load current 
        /// config from option GUI.
        /// </summary>
        internal void Start()
        {
            // load config with current option values
            config = BitfishForm.instance.ReadOptionValues();

            // Create a background worker and start fishing
            if (!worker.IsBusy)
            {
                if (config.AutoEquip)
                {
                    // equip selected fishing pole
                    string pole = BitfishForm.instance.GetFishingPole();
                    Console.WriteLine($"Equipping: [{pole}]");
                    mem.LuaDoString($"EquipItemByName(\"{pole}\")");
                }

                worker.RunWorkerAsync();
                clock.RunWorkerAsync();
            }
        }

        /// <summary>
        /// Stops the bot. When stopped it can be started again and the current session
        /// will keep on going as normal.
        /// </summary>
        internal void Stop()
        {
            Console.WriteLine("Stopping the bot ...");
            worker.CancelAsync();
            clock.CancelAsync();
        }

        /// <summary>
        /// Make character start fish
        /// </summary>
        private void BeginFishing()
        {
            // KeyHandler.PressKey(config.CastKey);
            mem.LuaDoString("CastSpellByName('Fishing')");
            Thread.Sleep(800);
        }

        /// <summary>
        /// Main bot routine
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            // If routine generates too many fails we stop
            int fails = 0;

            nextAfkTime = afkTime + session.startTime;

            Point fishingPosition = mem.ReadPlayerPosition();
            session.startTime = session.seconds;

            // wait a global for fish pole equip
            if (config.AutoEquip)
                Thread.Sleep(1000);

            while (true && fails < maxFails)
            {
                #region BreakEvents

                if (CancellationPending(worker, e))
                    break;

                if (IsPlayerDead())
                {
                    Console.WriteLine("Player is dead.");
                    break;
                }

                if(HasPlayerMoved(fishingPosition))
                {
                    Console.WriteLine("Player has moved.");
                    break;
                }

                if (TimerExpired())
                {
                    Console.WriteLine("Timer has expired.");
                    break;
                }    

                if (config.NearbyPlayer && NearbyPlayers())
                {
                    Console.WriteLine("Detected nearby players for too long");
                    break;
                }
                    

                #endregion BreakEvents

                AntiAfk();

                // Start cast
                BeginFishing();

                // Search for bobber
                GameObject bobber = mem.FindBobber(prevBobbers);
                if (bobber == null)
                {
                    fails++;
                    Console.WriteLine("No bobber was found!");
                    Thread.Sleep(2000);
                    continue;
                }

                bool fish = false;  // fish hooked?
                int timeout = 12;   // seconds until we stop
                int ticks = 0;

                // wait for fish to hook.
                // Stop if either timer has exceeded or a fish is on the hook.
                while(!fish && ticks < timeout)
                {
                    // check if we should cancel
                    if (CancellationPending(worker, e) || HasPlayerMoved(fishingPosition) || IsPlayerDead())
                        break;

                    Thread.Sleep(1000);
                    fish = mem.ReadBobberStatus(bobber);
                    ticks++;
                }
                
                // check if a fish was hooked
                if(fish)
                {
                    // click bobber after random amount of ms
                    Thread.Sleep(random.Next(600) + 500);
                    mem.LuaMouseoverInteract(bobber);
                    session.fishCaught++;
                    worker.ReportProgress(session.fishCaught);
                    Console.WriteLine($"Caught fish: [{session.fishCaught}]");

                    // add guid to blacklist
                    prevBobbers.Enqueue(bobber.guid);
                    if (prevBobbers.Count > 5)
                        prevBobbers.Dequeue();

                    // check slots left
                    if(config.StopIfInventoryFull)
                    {
                        int slots = GetFreeInventorySlots();
                        if(slots == 0)
                        {
                            Console.WriteLine("Inventory has no free slots left, stopping");
                            break;
                        }
                    }
                } 
                else
                {
                    Console.WriteLine("Failed to click on bobber");
                    // add guid to blacklist
                    prevBobbers.Enqueue(bobber.guid);
                    if (prevBobbers.Count > 5)
                        prevBobbers.Dequeue();
                    fails++;
                }

                Thread.Sleep(random.Next(500) + 500);
            }

            if (fails == maxFails)
                failed = true;
        }

        /// <summary>
        /// Player becomes afk after around 5min 30s. Move a bit to remove it.
        /// </summary>
        private void AntiAfk()
        {
            if(session.seconds - session.startTime > nextAfkTime)
            {
                KeyHandler.PressKey(0x53, 50); // press S key
                KeyHandler.PressKey(0x57, 50); // press W key
                nextAfkTime = session.seconds + afkTime;
            }
        }

        private void FishCaught(object sender, ProgressChangedEventArgs e)
        {
            BitfishForm.instance.UpdateFishCaught(e.ProgressPercentage);
        }

        private void ClockWork(object sender, DoWorkEventArgs e)
        {
            while(true)
            {
                clock.ReportProgress(session.seconds);
                if (CancellationPending(clock, e))
                    break;
                Thread.Sleep(1000);
                session.seconds++;
            }
        }

        private void ClockTick(object sender, ProgressChangedEventArgs e)
        {
            BitfishForm.instance.UpdateTimer(session.seconds);
        }

        /// <summary>
        /// Routine when bot has finished fishing. In this state we can be either safe,
        /// in combat or dead. If we are in combat we should just wait and die, release spirit
        /// and check if logout.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Finished(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Bot has been stopped.");
            BitfishForm.instance.UpdateStatus(false);

            if (clock.IsBusy)
                clock.CancelAsync();

            if (failed)
                Console.WriteLine("The bot has been stopped due to too many failed fishing attempts!");

            bool dead = IsPlayerDead();

            if(dead)
                ReleaseAndLogout();
            else
            {
                // we are alive, but are we in combat?
                bool combat = IsPlayerInCombat();
                if(combat)
                {
                    // yea, let's wait till we die
                    Console.WriteLine("We are in combat! Waiting to die ...");
                    while (!IsPlayerDead())
                        Thread.Sleep(1000);

                    Console.WriteLine("Player has died.");
                    ReleaseAndLogout();
                }
            }

            if (config.HearthstoneWhenDone)
            {
                Console.WriteLine("Casting Hearthstone");
                mem.LuaDoString("UseItemByName(\"Hearthstone\")");
                if(config.LogoutWhenDone)
                {
                    // wait 20s for hearthstone and loading
                    Console.WriteLine("Waiting 20s for casting and loading screen until logout ...");
                    Thread.Sleep(20000);
                    mem.LuaDoString("Logout()");
                }
            }
            else if (config.LogoutWhenDone)
                mem.LuaDoString("Logout()");
        }

        /// <summary>
        /// Searches for nearby players and adds them to the playerTracker dict.
        /// If a player has been close to us for too long we return true. Otherwise
        /// we return false.
        /// </summary>
        /// <returns>True if player too close for too long time</returns>
        private bool NearbyPlayers()
        {
            List<ulong> keysToRemove = new List<ulong>();

            GameObject[] nearby = mem.GetNearbyPlayers(35);
            foreach (GameObject player in nearby)
            {
                if (player == null)
                    break;

                if (!playerTracker.ContainsKey(player.guid))
                {
                    Console.WriteLine($"Adding key: {player.guid:X}");
                    playerTracker.Add(player.guid, session.seconds);
                }
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
                        Console.WriteLine($"[{session.seconds}] Already seen player: {key:X} {lastSeen}");
                        int time = session.seconds - lastSeen;
                        if (time > 25) return true;
                    }
                }
                // key is not close anymore, remove it
                if (!seen) keysToRemove.Add(key);
            }

            foreach (ulong key in keysToRemove)
            {
                playerTracker.TryGetValue(key, out int value);
                Console.WriteLine($"[{session.seconds}] Removing key: {key:X} {value}");
                playerTracker.Remove(key);
            }

            return false;
        }

        /// <summary>
        /// Checks if the timer has expired
        /// </summary>
        /// <returns></returns>
        private bool TimerExpired()
        {
            if (!config.EnableTimer)
                return false;

            if (session.seconds > config.TimerDuration * 60 + session.startTime)
                return true;
            else
                return false;
        }

        /// <summary>
        /// Releases spirit and check if we should logout
        /// </summary>
        private void ReleaseAndLogout()
        {
            Console.WriteLine("Releasing spirit");
            Thread.Sleep(random.Next(1000) + 500);
            mem.LuaDoString("RepopMe()");
            Thread.Sleep(5000);
            if (config.HearthstoneWhenDone)
                Console.WriteLine("Can't cast hearthstone when dead!");
            if (config.LogoutWhenDone || config.LogoutWhenDead)
                mem.LuaDoString("Logout()");
        }

        /// <summary>
        /// Checks if player has moved away from its fishing position
        /// </summary>
        /// <returns>True if distance moved greater than 1, otherwise false</returns>
        private bool HasPlayerMoved(Point fishPos)
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
        private bool IsPlayerInCombat()
        {
            mem.LuaDoString("combat = UnitAffectingCombat('player')");
            string combat = mem.LuaGetLocalizedText("combat");
            if (combat.Length >= 1) return true;
            else return false;
        }

        /// <summary>
        /// </summary>
        /// <returns>True if player is dead</returns>
        private bool IsPlayerDead()
        {
            int health = mem.ReadPlayerHealth();
            if (health <= 1) return true;
            else return false;
        }

        /// <summary>
        /// Reads the number of free inventory slots available
        /// </summary>
        /// <returns>slots available in bag</returns>
        private int GetFreeInventorySlots()
        {
            int slots = 0;
            for(int i = 0; i < 5; i++)
            {
                mem.LuaDoString($"freeSlots = GetContainerNumFreeSlots({i})");
                string res = mem.LuaGetLocalizedText("freeSlots");
                slots += Convert.ToInt32(res);
            }
            return slots;
        }

        /// <summary>
        /// Check if we have a cancel pending
        /// </summary>
        /// <param name="e"></param>
        /// <returns>true if worker is cancelled</returns>
        private bool CancellationPending(BackgroundWorker w, DoWorkEventArgs e)
        {
            // Check if we have a cancel pending
            if (w.CancellationPending)
            {
                e.Cancel = true;
                return true;
            }
            return false;
        }
    }
}
