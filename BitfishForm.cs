using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace Bitfish
{
    public partial class BitfishForm : Form
    {
        public static BitfishForm instance; 

        private readonly MemoryReader memoryReader;
        private readonly ConfigHandler configHandler;
        private readonly Bot bot;

        private int savedChecksum;

        public BitfishForm()
        {
            if (instance == null)
                instance = this;

            InitializeComponent();
            memoryReader = new MemoryReader();
            configHandler = new ConfigHandler();
            bot = new Bot(memoryReader);

            savedChecksum = configHandler.GetChecksum();
            Console.WriteLine($"Got checksum: {savedChecksum:X}");
            UpdateOptions(configHandler.GetConfig());
        }

        private void BitfishOnLoad(object sender, EventArgs e)
        {
            // Open process
            bool open = memoryReader.OpenProcess();
            bool hooked = false;
            if(open)
            {
                Console.WriteLine("Found Wow.exe process. Initializing ...");
                hooked = memoryReader.Init();
            }

            if(open && hooked)
            {
                StatusLabel.Text = "Ready";
                StatusLabel.ForeColor = Color.Green;
                Console.WriteLine("Bot is ready");
            }
            else
            {
                Console.WriteLine("Initialization failed!");
                StatusLabel.Text = "Failed. Start Wow and enter world";
                StatusLabel.ForeColor = Color.Red;
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            bot.Start();
            UpdateStatus(true);
            CurrentSessionBox.Visible = true;
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            bot.Stop();
            UpdateStatus(false);
        }

        /// <summary>
        /// Updates the Start, Stop button and the status label.
        /// </summary>
        /// <param name="status"></param>
        internal void UpdateStatus(bool running)
        {
            if(running)
            {
                StatusLabel.Text = "Fishing ...";
                StatusLabel.ForeColor = Color.Green;
                StartButton.Enabled = false;
                StopButton.Enabled = true;
            }
            else
            {
                StatusLabel.Text = "Stopped.";
                StatusLabel.ForeColor = Color.Green;
                StartButton.Enabled = true;
                StopButton.Enabled = false;
            }
        }

        /// <summary>
        /// Updates the Fish Caught label with specifed value
        /// </summary>
        /// <param name="v"></param>
        internal void UpdateFishCaught(int fishCaught)
        {
            FishCaughtLabel.Text = $"Fish Caught: {fishCaught}";
        }

        /// <summary>
        /// Updates the current session timer value
        /// </summary>
        internal void UpdateTimer(int seconds)
        {
            // transform seconds to MM:SS
            int sec = seconds % 60;
            int min = seconds / 60;
            TimerLabel.Text = $"Time: {min}m {sec}s";
        }

        /// <summary>
        /// Sets values for each option alternative
        /// </summary>
        /// <param name="config"></param>
        internal void UpdateOptions(Config cfg)
        {
            EnableTimerCheckBox.Checked = cfg.EnableTimer;
            TimerDuration.Value = cfg.TimerDuration;
            LogoutWhenDoneCheckBox.Checked = cfg.LogoutWhenDone;
            LogoutWhenDeadCheckBox.Checked = cfg.LogoutWhenDead;
            HearthstoneCheckBox.Checked = cfg.HearthstoneWhenDone;
        }

        #region OPTIONS

        private int GetCurrentOptionChecksum()
        {
            return ((EnableTimerCheckBox.Checked ? 1 : 0) << 0) |
                ((LogoutWhenDoneCheckBox.Checked ? 1 : 0) << 1) |
                ((LogoutWhenDeadCheckBox.Checked ? 1 : 0) << 2) |
                ((HearthstoneCheckBox.Checked ? 1 : 0) << 3) |
                ((int)TimerDuration.Value << 4);
        }

        private void CompareChecksum()
        {
            Console.WriteLine($"Compaing {GetCurrentOptionChecksum():X} with {savedChecksum:X}");

            if (GetCurrentOptionChecksum() != savedChecksum)
                SaveOptions.Enabled = true;
            else
                SaveOptions.Enabled = false;
        }

        private void EnableTimerCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            CompareChecksum();
        }

        private void TimerDuration_ValueChanged(object sender, EventArgs e)
        {
            CompareChecksum();
        }

        private void LogoutWhenDoneCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            CompareChecksum();
        }

        private void LogoutWhenDeadCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            CompareChecksum();
        }

        private void HearthstoneCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            CompareChecksum();
        }

        private void SaveOptions_Click(object sender, EventArgs e)
        {
            Config config = ReadOptionValues();
            configHandler.SaveConfig(config);
            savedChecksum = configHandler.GetChecksum();
            CompareChecksum();
        }

        internal Config ReadOptionValues()
        {
            return new Config
            {
                EnableTimer = EnableTimerCheckBox.Checked,
                TimerDuration = (int)TimerDuration.Value,
                LogoutWhenDone = LogoutWhenDoneCheckBox.Checked,
                LogoutWhenDead = LogoutWhenDeadCheckBox.Checked,
                HearthstoneWhenDone = HearthstoneCheckBox.Checked
            };
        }

        #endregion
    }
}
