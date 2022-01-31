using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Magic;
using System.Windows.Forms;
using System.Diagnostics;

namespace Bitfish
{
    public class MemoryReader
    {
        private readonly BlackMagic blackMagic;
        private ObjectManager objManager;

        private Hook hook;
        private Lua lua;

        private int pId;
        private bool ready;

        public MemoryReader() 
        {
            blackMagic = new BlackMagic();
        }

        internal bool OpenProcess()
        {
            List<Process> procList = new List<Process>(Process.GetProcessesByName("Wow"));
            foreach(Process p in procList)
            {
                Console.WriteLine("Found Wow process! pid={0}", p.Id);
                pId = p.Id;
                objManager = new ObjectManager(blackMagic);
            }

            return blackMagic.Open(pId);
        }

        internal bool Init()
        {
            // find the object manager list
            ready = objManager.Init();

            // hook
            hook = new Hook(blackMagic);
            hook.InitHook();
            lua = new Lua(hook, blackMagic);

            ready = ready && hook.isHooked;
            return ready;
        }

        /// <summary>
        /// Tries to find the Fishing Bobber object in the object list
        /// </summary>
        /// <returns>GameObject with guid and pos if found, otherwise null</returns>
        internal GameObject FindBobber(Queue<ulong> blacklist)
        {
            return objManager.Dump(blacklist);
        }

        /// <summary>
        /// Reads the bobbing status of the bobber.
        /// </summary>
        /// <param name="bobber"></param>
        /// <returns>true if fish is hooked, otherwise false</returns>
        internal bool ReadBobberStatus(GameObject bobber)
        {
            byte bobbing = blackMagic.ReadByte(bobber.address + Offsets.ObjManager.BOBBING);
            if (bobbing == 1)
                return true;
            else
                return false;
        }

        internal void LuaDoString(string command)
        {
            lua.DoString(command);
        }

        internal void LuaMouseoverInteract(GameObject obj)
        {
            blackMagic.WriteUInt64(Offsets.MOUSEOVER, obj.guid);
            lua.DoString("InteractUnit(\'mouseover\')");
        }

        internal Point ReadPlayerPosition()
        {
            return new Point(
                blackMagic.ReadFloat(Offsets.Player.POS_X),
                blackMagic.ReadFloat(Offsets.Player.POS_Y),
                blackMagic.ReadFloat(Offsets.Player.POS_Z));
        }

        /// <summary>
        /// Returns the player health. If it failed return -1
        /// </summary>
        /// <returns></returns>
        internal int ReadPlayerHealth()
        {
            if (objManager.GetPlayerPointer() == 0)
                objManager.Dump(null);

            int health = blackMagic.ReadInt(objManager.GetPlayerPointer() + Offsets.ObjManager.HEALTH_OFFSET);
            if (health < 0 && health > 60000)
                return -1;
            else
                return health;
        }

        internal bool IsReady() { return ready; }
    }
}
