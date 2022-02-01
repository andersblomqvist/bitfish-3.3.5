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
    /// Position stopper    Finished
    /// ConfigHandler       Finished
    /// Timer               Finished
    /// Hearthstone         Finished
    /// Logout              Finished
    /// HP/death check      Finished   
    /// Spirit Release      Finished
    /// Logout when dead    Finished
    /// Stats               kinda
    /// Improved GUI
    /// </summary>
    public class Bot
    {
        private readonly BackgroundWorker worker;
        private readonly BackgroundWorker clock;
        private readonly MemoryReader mem;
        private readonly Random random;

        private Config config;

        // blacklist these guids, clicked bobbers stay in memory for a small time
        // the que will hold last 5 bobbers
        private readonly Queue<ulong> prevBobbers;

        private struct Stats
        {
            public int seconds;
            public int fishCaught;
        }

        private Stats session;

        // Tracks wheter the bot as failed to fish {maxFails} number times in a row
        private bool failed;
        private readonly int maxFails;

        public Bot(MemoryReader mem)
        {
            this.mem = mem;
            worker = new BackgroundWorker();
            clock = new BackgroundWorker();
            random = new Random();
            prevBobbers = new Queue<ulong>();
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
        }

        internal void Start()
        {
            // load config with current option values
            config = BitfishForm.instance.ReadOptionValues();

            // Create a background worker and start fishing
            if (!worker.IsBusy)
            {
                worker.RunWorkerAsync();
                clock.RunWorkerAsync();
            }
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

            Point fishingPosition = mem.ReadPlayerPosition();

            while (true && fails < maxFails)
            {
                if (CancellationPending(worker, e) || HasPlayerMoved(fishingPosition) || IsPlayerDead())
                    break;

                if (config.EnableTimer && session.seconds >= config.TimerDuration * 60)
                    break;

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
                    Console.WriteLine("Caught fish - hopefully");
                    session.fishCaught++;
                    worker.ReportProgress(session.fishCaught);

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

        internal void Stop()
        {
            Console.WriteLine("Stopping the bot ...");
            worker.CancelAsync();
            clock.CancelAsync();
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
