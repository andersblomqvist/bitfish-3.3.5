using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Magic;

namespace Bitfish
{
    public class ObjectManager
    {
        public enum WowObjectType
        {
            None = 0,
            Item = 1,
            Container = 2,
            Unit = 3,
            Player = 4,
            GameObject = 5,
            DynamicObject = 6,
            Corpse = 7,
            AiGroup = 8,
            AreaTrigger = 9
        }

        public const int SIZE = 4096;

        private readonly BlackMagic blackMagic;
        private uint listStart;
        private uint playerPtr;

        public ObjectManager(BlackMagic mem)
        {
            this.blackMagic = mem;
        }

        internal bool Init()
        {
            uint clientConnection = blackMagic.ReadUInt(Offsets.ObjManager.CLIENT_CONNECTION);
            uint objectManagerPtr = clientConnection + Offsets.ObjManager.OBJ_MANAGER;
            uint objectManager = blackMagic.ReadUInt(objectManagerPtr);
            listStart = objectManager + Offsets.ObjManager.LIST_START;

            Console.WriteLine("Object Manager: {0}, List start at: {1}",
                objectManager.ToString("X"),
                listStart.ToString("X"));

            if (objectManager == 0)
                return false;
            else
                return true;
        }

        internal GameObject Dump(Queue<ulong> blacklist)
        {
            uint curr = blackMagic.ReadUInt(listStart);

            for(int i = 0; i < SIZE; i++)
            {
                int type = blackMagic.ReadInt(curr + Offsets.ObjManager.TYPE);

                if (type < 0 || type > 40)
                    break;

                if(type == (int)WowObjectType.GameObject)
                {
                    Point pos = new Point(
                        blackMagic.ReadFloat(curr + Offsets.ObjManager.POS_X),
                        blackMagic.ReadFloat(curr + Offsets.ObjManager.POS_Y),
                        blackMagic.ReadFloat(curr + Offsets.ObjManager.POS_Z));

                    // Read the object name
                    uint pName = blackMagic.ReadUInt(curr + 0x1A4);
                    uint pStr = blackMagic.ReadUInt(pName + 0x90);
                    string objectName = blackMagic.ReadASCIIString(pStr, 14);

                    // Console.WriteLine($"i={i} [{curr.ToString("X")}] name={objectName}");

                    if (objectName == "Fishing Bobber")
                    {
                        ulong guid = blackMagic.ReadUInt64(curr + Offsets.ObjManager.GUID);
                        if(!blacklist.Contains(guid))
                        {
                            GameObject bobber = new GameObject(curr, guid, pos);
                            // Console.WriteLine("Found bobber: [{0}] ({1}) {2}", curr.ToString("X"), pos, guid.ToString("X"));
                            return bobber;
                        }
                    }
                }

                else if (type == (int)WowObjectType.Player)
                {
                    float px = blackMagic.ReadFloat(Offsets.Player.POS_X);
                    float py = blackMagic.ReadFloat(Offsets.Player.POS_Y);
                    float pz = blackMagic.ReadFloat(Offsets.Player.POS_Z);

                    float x = blackMagic.ReadFloat(curr + Offsets.ObjManager.X_OFFSET);
                    float y = blackMagic.ReadFloat(curr + Offsets.ObjManager.Y_OFFSET);
                    float z = blackMagic.ReadFloat(curr + Offsets.ObjManager.Z_OFFSET);

                    // check if this player have the same coords as us
                    if (px.Equals(x) && py.Equals(y) && pz.Equals(z))
                    {
                        playerPtr = curr;
                        Console.WriteLine("Found player pointer");
                        return null;
                    }
                        
                }

                curr += Offsets.ObjManager.NEXT;
                curr = blackMagic.ReadUInt(curr);
            }
            Console.WriteLine("Could not find any objects!");
            return null;
        }

        /// <summary>
        /// Returns the address to player object which comes from the object manager list
        /// </summary>
        /// <returns>address, otherwise 0</returns>
        internal uint GetPlayerPointer()
        {
            if (playerPtr != 0)
                return playerPtr;
            else
                return 0;
        }
    }
}
