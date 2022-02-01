using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitfish
{
    static class Offsets
    {
        public struct Player
        {
            public const uint POS_X = 0xADF4E4;
            public const uint POS_Y = 0xADF4E8;
            public const uint POS_Z = 0xADF4EC;
        }

        public struct ObjManager
        {
            public const uint CLIENT_CONNECTION = 0xC79CE0;
            public const uint OBJ_MANAGER = 0x2ED0; // [clientConn] + 2ed0
            public const uint LIST_START = 0xAC; // currentObject = [objManager] + AC

            // current object + ...
            public const uint NEXT = 0x3C;   // next object in list
            public const uint TYPE = 0x14;
            public const uint GUID = 0x30;
            public const uint POS_X = 0xE8;
            public const uint POS_Y = 0xEC;
            public const uint POS_Z = 0xF0;
            public const uint BOBBING = 0xBC;    // no bobbing = 0, bobbing = 1

            // player object from object manager list offsets
            public const int X_OFFSET = 0x798;
            public const int Y_OFFSET = 0x79C;
            public const int Z_OFFSET = 0x7A0;
            public const int HEALTH_OFFSET = 0xFB0;
        }

        public const uint MOUSEOVER = 0x00BD07A0;
        public const uint LUA_DO_STRING = 0x819210;
        public const uint DEVICE_PTR1 = 0xC5DF88;
        public const uint DEVICE_PTR2 = 0x397C;
        public const uint END_SCENE = 0xA8;
        public const uint ACTIVE_PLAYER_OBJ = 0x004038F0;
        public const uint GET_LOC_TEXT = 0x007225E0;
    }
}
