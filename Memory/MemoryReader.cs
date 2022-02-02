using System;
using System.Collections.Generic;
using Magic;
using System.Diagnostics;

namespace Bitfish
{
    public class MemoryReader
    {
        private readonly BlackMagic blackMagic;
        private ObjectManager objManager;

        private Hook hook;
        private Lua lua;

        private bool ready;
        private int processId;

        public MemoryReader() 
        {
            blackMagic = new BlackMagic();
        }

        internal bool OpenProcess(int id)
        {
            processId = id;
            objManager = new ObjectManager(blackMagic);
            return blackMagic.Open(id);
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
            Point player = ReadPlayerPosition();
            return objManager.Dump(blacklist, player);
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

        internal string LuaGetLocalizedText(string localVar)
        {
            return lua.GetLocalizedText(localVar);
        }

        internal void LuaMouseoverInteract(GameObject obj)
        {
            blackMagic.WriteUInt64(Offsets.MOUSEOVER, obj.guid);
            lua.DoString("InteractUnit(\'mouseover\')");
        }

        /// <summary>
        /// Reads and returns local player position
        /// </summary>
        /// <returns></returns>
        internal Point ReadPlayerPosition()
        {
            return new Point(
                blackMagic.ReadFloat(Offsets.Player.POS_X),
                blackMagic.ReadFloat(Offsets.Player.POS_Y),
                blackMagic.ReadFloat(Offsets.Player.POS_Z));
        }

        /// <summary>
        /// Reads and returns the local player health
        /// </summary>
        /// <returns></returns>
        internal int ReadPlayerHealth()
        {
            uint ptr = objManager.GetPlayerPointer();
            int health = blackMagic.ReadInt(ptr + Offsets.ObjManager.HEALTH_OFFSET);
            if(health < 0 || health > 55000 || ptr == 0)
            {
                Console.WriteLine("Player health seems off, getting new player pointer.");
                objManager.FindPlayerPointer();
                return ReadPlayerHealth();
            }
            return health;
        }

        /// <summary>
        /// Searches for nearby players which are within a specified radius. Each player is a GameObject
        /// in the array which is returned. If array is empty there is no close players.
        /// </summary>
        /// <param name="radius"></param>
        /// <returns>An array of GameObjects which are nearby players</returns>
        internal GameObject[] GetNearbyPlayers(double radius)
        {
            Point p = ReadPlayerPosition();
            return objManager.NearbyPlayers(p, radius);
        }

        internal bool IsReady() { return ready; }
    }
}
