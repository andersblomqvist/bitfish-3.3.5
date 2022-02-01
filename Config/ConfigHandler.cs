using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace Bitfish
{
    public class Config
    {
        public bool EnableTimer { get; set; }
        public int TimerDuration { get; set; }
        public bool LogoutWhenDone { get; set; }
        public bool LogoutWhenDead { get; set; }
        public bool HearthstoneWhenDone { get; set; }
        public bool StopIfInventoryFull { get; set; }
        public bool AutoEquip { get; set; }
        public int FishingPole { get; set; }
    }

    public class ConfigHandler
    {
        public static ConfigHandler instance;
        private Config config;

        private readonly string path;

        // tracks current config values
        private int configChecksum;

        public ConfigHandler()
        {
            if (instance == null)
                instance = this;

            path = AppDomain.CurrentDomain.BaseDirectory + "fish-config.json";

            // Default config
            config = new Config();

            // look if there is a config file available
            ReadConfig(path);
            SetChecksum();
        }

        internal void ReadConfig(string fileName)
        {
            Console.WriteLine("Trying to read config: " + fileName);
            if(File.Exists(fileName))
            {
                string jsonString = File.ReadAllText(fileName);
                config = JsonSerializer.Deserialize<Config>(jsonString);
                Console.WriteLine("Loaded config from file");
            }
            else
                Console.WriteLine("Found no config file! Using default.");
        }

        internal void SaveConfig(Config cfg)
        {
            config = cfg;
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(cfg, options);
            File.WriteAllText(path, jsonString);
            SetChecksum();
        }

        internal void SetChecksum()
        {
            // 0000 0000 0000 0000 0000 0000 0000 0000
            //                           |    ||| |||^- [0] Enable timer
            //                           |    ||| ||^-- [1] Logout when done
            //                           |    ||| |^--- [2] Logout when dead
            //                           |    ||| ^---- [3] Hearthstone when done
            //                           |    ||^------ [4] Inventory full
            //                           |    |^------- [5] Auto Equip
            //                           |    ^-------- [6-9] Fishing Pole
            //                           ^------------- [10..] Timer duration
            configChecksum = (config.EnableTimer ? 1 : 0) |
                (config.LogoutWhenDone ? 1 : 0) << 1 |
                (config.LogoutWhenDead ? 1 : 0) << 2 |
                (config.HearthstoneWhenDone ? 1 : 0) << 3 |
                (config.StopIfInventoryFull ? 1 : 0) << 4 |
                (config.AutoEquip ? 1 : 0) << 5 |
                (config.FishingPole << 6) |
                config.TimerDuration << 10;
        }

        internal int GetChecksum()
        {
            return configChecksum;
        }

        internal Config GetConfig()
        {
            return config;
        }
    }
}
