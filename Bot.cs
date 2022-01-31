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
    /// HP/death check
    /// Spirit Release
    /// Logout when dead
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

            while(true && fails < 3)
            {
                if(CancellationPending(worker, e) || HasPlayerMoved(fishingPosition))
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
                    if (CancellationPending(worker, e) || HasPlayerMoved(fishingPosition))
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

        private void Finished(object sender, RunWorkerCompletedEventArgs e)
        {
            Console.WriteLine("Bot has been stopped.");
            BitfishForm.instance.UpdateStatus(false);

            if (config.HearthstoneWhenDone)
            {
                mem.LuaDoString("UseItemByName(\"Hearthstone\")");
                if(config.LogoutWhenDone)
                {
                    // wait 20s for hearthstone and loading
                    Thread.Sleep(20000);
                    mem.LuaDoString("Logout()");
                }
            }
            else if (config.LogoutWhenDone)
                mem.LuaDoString("Logout()");

            if (clock.IsBusy)
                clock.CancelAsync();
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
