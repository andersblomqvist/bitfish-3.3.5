using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bitfish
{
    public class GameObject
    {
        public uint address; // current memory address
        public ulong guid;
        public Point position;

        public GameObject() { }
        public GameObject(Point p) { position = p; }
        public GameObject(ulong guid) { this.guid = guid; }
        public GameObject(ulong guid, Point p)
        {
            this.guid = guid;
            position = p;
        }

        public GameObject(uint address, ulong guid, Point p)
        {
            this.address = address;
            this.guid = guid;
            position = p;
        }
    }
}
