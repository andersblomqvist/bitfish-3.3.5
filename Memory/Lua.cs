using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Magic;

namespace Bitfish
{
    public class Lua
    {
        private readonly Hook hook;
        private readonly BlackMagic blackMagic;

        public Lua(Hook hook, BlackMagic blackMagic)
        {
            this.hook = hook;
            this.blackMagic = blackMagic;
        }

        internal void DoString(string command)
        {
            // Allocate memory
            uint doStringArgCodecave = blackMagic.AllocateMemory(Encoding.UTF8.GetBytes(command).Length + 1);

            // Write value:
            blackMagic.WriteBytes(doStringArgCodecave, Encoding.UTF8.GetBytes(command));

            // Write the asm stuff for Lua_DoString
            var asm = new[]
            {
                "mov eax, " + doStringArgCodecave,
                "push 0",
                "push eax",
                "push eax",
                $"call {(Offsets.LUA_DO_STRING)}",
                "add esp, 0xC",
                "retn"
            };

            // Inject
            hook.InjectAndExecute(asm, false, out bool success);

            if (!success)
                Console.WriteLine("Failed to DoLuaString");

            // Free memory allocated
            blackMagic.FreeMemory(doStringArgCodecave);
        }

        internal string GetLocalizedText(string localVar)
        {
            if(hook.isHooked)
            {
                uint space = blackMagic.AllocateMemory(Encoding.UTF8.GetBytes(localVar).Length + 1);
                
                blackMagic.WriteBytes(space, Encoding.UTF8.GetBytes(localVar));

                var asm = new[]
                {
                    "call " + Offsets.ACTIVE_PLAYER_OBJ,
                    "mov ecx, eax",
                    "push -1",
                    "mov edx, " + space + "",
                    "push edx",
                    "call " + ((uint) Offsets.GET_LOC_TEXT) ,
                    "retn",
                };

                string result = Encoding.UTF8.GetString(hook.InjectAndExecute(asm, true, out bool success));

                if (!success)
                    Console.WriteLine("Lua Localized text failed");

                // Free memory allocated
                blackMagic.FreeMemory(space);
                return result;
            }
            return "";
        }
    }
}

