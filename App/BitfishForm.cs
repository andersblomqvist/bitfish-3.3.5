using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections.Generic;

namespace Bitfish
{
    public partial class BitfishForm : Form
    {
        public static BitfishForm instance; 

        private readonly MemoryReader memoryReader;
        private readonly ConfigHandler configHandler;
        private readonly Bot bot;

        private List<Process> processList;
        private List<Process> wowList;

        private int savedChecksum;

        private bool processFound;

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

            WowIDList.Visible = false;
            ConfirmProcessButton.Visible = false;
            processFound = false;
        }

        private void BitfishOnLoad(object sender, EventArgs e)
        {
            FindWowProcess();
        }

        /// <summary>
        /// Searches the process list for a process named Wow. If multiple is found
        /// we allow user to select one with GUI. If no process was found the retry
        /// button appears.
        /// </summary>
        private void FindWowProcess()
        {
            // Search for processes.
            processList = new List<Process>(Process.GetProcessesByName("Wow"));
            wowList = new List<Process>();
            int id = 0;
            RetryButton.Visible = false;
            foreach (Process p in processList)
            {
                Console.WriteLine("Found Wow process! pid={0}", p.Id);
                id = p.Id;
                wowList.Add(p);
                processFound = true;
            }

            if (wowList.Count > 1)
            {
                Console.WriteLine("Multiple process was found"); ;
                for (int i = 0; i < wowList.Count; i++)
                    WowIDList.Items.Add(wowList[i].Id);
                WowIDList.SelectedIndex = 0;
                WowIDList.Visible = true;
                ConfirmProcessButton.Visible = true;
                StatusLabel.Text = "Please choose a specific process >";
                StatusLabel.ForeColor = Color.Orange;
            }
            else if (wowList.Count <= 0)
            {
                Console.WriteLine("No processes was found.");
                StatusLabel.Text = "No process was found";
                StatusLabel.ForeColor = Color.Red;
                RetryButton.Visible = true;
            }
            else
                OpenProcess(id);
        }

        /// <summary>
        /// Attepmts to open process with specified id.
        /// </summary>
        /// <param name="id"></param>
        private void OpenProcess(int id)
        {
            // Open process
            bool open = memoryReader.OpenProcess(id);
            bool hooked = false;
            if (open)
                hooked = memoryReader.Init();

            if (open && hooked)
                SetReady();
            else
            {
                Console.WriteLine("Initialization failed!");
                StatusLabel.Text = "Please enter world and hit retry";
                StatusLabel.ForeColor = Color.Red;
                RetryButton.Visible = true;
                StartButton.Enabled = false;
                StopButton.Enabled = false;
            }
        }

        /// <summary>
        /// Tries to search for Wow process again or init the memory reader
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void RetryButton_Click(object sender, EventArgs e)
        {
            if (!processFound)
                FindWowProcess();
            else
            {
                bool status = memoryReader.Init();
                if (status)
                    SetReady();
            }
        }

        private void ConfirmProcessButton_Click(object sender, EventArgs e)
        {
            int index = WowIDList.SelectedIndex;
            if (index == -1)
                index = 0;
            int procId = wowList[index].Id;
            OpenProcess(procId);
        }

        private void SetReady()
        {
            StatusLabel.Text = "Ready";
            StatusLabel.ForeColor = Color.Green;
            Console.WriteLine("Bot is ready");
            WowIDList.Visible = false;
            ConfirmProcessButton.Visible = false;
            ProcIdLabel.Text = $"Process ID: {memoryReader.GetProcessId()}";
            ProcIdLabel.Visible = true;
            RetryButton.Visible = false;
            StartButton.Enabled = true;
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            bot.Start(out bool started);
            if(started)
            {
                UpdateStatus(true);
                CurrentSessionBox.Visible = true;
            }
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
        internal void UpdateStatus(bool running, bool memoryError = false)
        {
            if (memoryError)
            {
                StatusLabel.Text = "Please enter world before starting";
                StatusLabel.ForeColor = Color.Red;
                StopButton.Enabled = false;
                return;
            }

            if (running)
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

        internal string GetFishingPole()
        {
            return FishingPoleSelector.GetItemText(FishingPoleSelector.SelectedItem);
        }

        #region OPTIONS

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
            InventoryFullCheckbox.Checked = cfg.StopIfInventoryFull;
            NearybyPlayerCheckbox.Checked = cfg.NearbyPlayer;
            AutoEquipCheckbox.Checked = cfg.AutoEquip;
            FishingPoleSelector.SelectedIndex = cfg.FishingPole;
            WintergraspCheckbox.Checked = cfg.Wintergrasp;

            if (!AutoEquipCheckbox.Checked)
                FishingPoleSelector.Enabled = false;
            else
                FishingPoleSelector.Enabled = true;
        }

        private int GetCurrentOptionChecksum()
        {
            // see comment in ConfigHandler.cs for details
            return (EnableTimerCheckBox.Checked ? 1 : 0) << 0 |
                (LogoutWhenDoneCheckBox.Checked ? 1 : 0) << 1 |
                (LogoutWhenDeadCheckBox.Checked ? 1 : 0) << 2 |
                (HearthstoneCheckBox.Checked ? 1 : 0) << 3 |
                (InventoryFullCheckbox.Checked ? 1 : 0) << 4 |
                (AutoEquipCheckbox.Checked ? 1 : 0) << 5 |
                (FishingPoleSelector.SelectedIndex & 0xF) << 6 |
                (NearybyPlayerCheckbox.Checked ? 1 : 0) << 10 |
                (WintergraspCheckbox.Checked ? 1 : 0) << 11 |
                (int)TimerDuration.Value << 12;
        }

        /// <summary>
        /// Compares the saved checksum which comes from config file to the current state of options
        /// If they are not the same we enable the Save Button so user can overwrite the config file.
        /// </summary>
        private void CompareChecksum()
        {
            // Console.WriteLine($"Compaing {GetCurrentOptionChecksum():X} with {savedChecksum:X}");
            if (GetCurrentOptionChecksum() != savedChecksum)
                SaveOptions.Enabled = true;
            else
                SaveOptions.Enabled = false;
        }

        private void EnableTimerCheckBox_CheckedChanged(object sender, EventArgs e) { CompareChecksum(); }
        private void TimerDuration_ValueChanged(object sender, EventArgs e) { CompareChecksum(); }
        private void LogoutWhenDoneCheckBox_CheckedChanged(object sender, EventArgs e) { CompareChecksum(); }
        private void LogoutWhenDeadCheckBox_CheckedChanged(object sender, EventArgs e) { CompareChecksum(); }
        private void HearthstoneCheckBox_CheckedChanged(object sender, EventArgs e) { CompareChecksum(); }
        private void InventoryFullCheckbox_CheckedChanged(object sender, EventArgs e) { CompareChecksum(); }
        private void FishingPoleSelector_SelectedIndexChanged(object sender, EventArgs e) { CompareChecksum(); }
        private void NearybyPlayerCheckbox_CheckedChanged(object sender, EventArgs e) { CompareChecksum(); }
        private void WintergraspCheckbox_CheckedChanged(object sender, EventArgs e) { CompareChecksum(); }

        private void AutoEquipCheckbox_CheckedChanged(object sender, EventArgs e) 
        {
            CompareChecksum();

            // Toggle the fishing pole selector
            if(AutoEquipCheckbox.Checked)
            {
                FishingPoleSelector.Enabled = true;
                FishingPoleSelector.SelectedIndex = 0;
            }
            else
                FishingPoleSelector.Enabled = false;
        }

        /// <summary>
        /// Saves current option values to config file and sets current config to it.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveOptions_Click(object sender, EventArgs e)
        {
            Config config = ReadOptionValues();
            configHandler.SaveConfig(config);
            savedChecksum = configHandler.GetChecksum();
            CompareChecksum();
        }

        /// <summary>
        /// Reads current option values and creates a config out of it
        /// </summary>
        /// <returns>Config with current option values</returns>
        internal Config ReadOptionValues()
        {
            return new Config
            {
                EnableTimer = EnableTimerCheckBox.Checked,
                TimerDuration = (int)TimerDuration.Value,
                LogoutWhenDone = LogoutWhenDoneCheckBox.Checked,
                LogoutWhenDead = LogoutWhenDeadCheckBox.Checked,
                HearthstoneWhenDone = HearthstoneCheckBox.Checked,
                StopIfInventoryFull = InventoryFullCheckbox.Checked,
                NearbyPlayer = NearybyPlayerCheckbox.Checked,
                AutoEquip = AutoEquipCheckbox.Checked,
                FishingPole = FishingPoleSelector.SelectedIndex,
                Wintergrasp = WintergraspCheckbox.Checked
            };
        }

        #endregion

    }
}
