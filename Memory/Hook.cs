using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using Magic;

namespace Bitfish
{
    public class Hook
    {
        private readonly BlackMagic blackMagic;

        private const uint ENDSCENE_HOOK_OFFSET = 0x2;
        private byte[] originalEndscene = new byte[] { 0xB8, 0x51, 0xD7, 0xCA, 0x64 };

        private uint returnAdress;
        private uint codeCave;
        private uint codeCaveForInjection;
        private uint codeToExecute;
        private uint endsceneReturnAddress;

        public bool isHooked = false;
        public bool isInjectionUsed = false;

        public Hook(BlackMagic blackMagic)
        {
            this.blackMagic = blackMagic;
        }

        internal void InitHook()
        {
            Console.WriteLine("Trying to hook ...");

            if (!blackMagic.IsProcessOpen)
            {
                Console.WriteLine("Can not hook, process is not open!");
                return;
            }   

            // get D3D9 Endscene Pointer
            uint endScene = GetEndScene();

            if(blackMagic.ReadByte(endScene) == 0xE9)
            {
                originalEndscene = new byte[] { 0xB8, 0x51, 0xD7, 0xCA, 0x64 };
                DisposeHooking();
            }

            try
            {
                if(blackMagic.ReadByte(endScene) != 0xE9)
                {
                    // first thing thats 5 bytes big is here
                    // we are going to replace this 5 bytes with
                    // our JMP instruction (JMP (1 byte) + Address (4 byte))
                    endScene += ENDSCENE_HOOK_OFFSET;

                    // the address that we will return to after 
                    // the jump wer'e going to inject
                    endsceneReturnAddress = endScene + 0x5;

                    // integer to check if there is code waiting to be executed
                    codeToExecute = blackMagic.AllocateMemory(4);
                    blackMagic.WriteInt(codeToExecute, 0);

                    // integer to save the address of the return value
                    returnAdress = blackMagic.AllocateMemory(4);
                    blackMagic.WriteInt(returnAdress, 0);

                    // codecave to check if we need to execute something
                    codeCave = blackMagic.AllocateMemory(64);

                    // codecave for the code we want to execute
                    codeCaveForInjection = blackMagic.AllocateMemory(256);

                    blackMagic.Asm.Clear();

                    // save registers
                    blackMagic.Asm.AddLine("PUSHFD");
                    blackMagic.Asm.AddLine("PUSHAD");

                    // check for code to be executed
                    blackMagic.Asm.AddLine($"MOV EBX, [{(codeToExecute)}]");
                    blackMagic.Asm.AddLine("TEST EBX, 1");
                    blackMagic.Asm.AddLine("JE @out");

                    // execute our stuff and get return address
                    blackMagic.Asm.AddLine($"MOV EDX, {(codeCaveForInjection)}");
                    blackMagic.Asm.AddLine("CALL EDX");
                    blackMagic.Asm.AddLine($"MOV [{(returnAdress)}], EAX");

                    // finish up our execution
                    blackMagic.Asm.AddLine("@out:");
                    blackMagic.Asm.AddLine("MOV EDX, 0");
                    blackMagic.Asm.AddLine($"MOV [{(codeToExecute)}], EDX");

                    // restore registers
                    blackMagic.Asm.AddLine("POPAD");
                    blackMagic.Asm.AddLine("POPFD");

                    // needed to determine the position where the original
                    // asm is going to be placed
                    int asmLenght = blackMagic.Asm.Assemble().Length;

                    // inject the instructions into our codecave
                    blackMagic.Asm.Inject(codeCave);
                    // ---------------------------------------------------
                    // End of the code that checks if there is asm to be
                    // executed on our hook
                    // ---------------------------------------------------

                    // Prepare to replace the instructions inside WoW
                    blackMagic.Asm.Clear();

                    // do the original EndScene stuff after we restored the registers
                    // and insert it after our code
                    blackMagic.WriteBytes(codeCave + (uint)asmLenght, originalEndscene);

                    // return to original function after we're done with our stuff
                    blackMagic.Asm.AddLine($"JMP {(endsceneReturnAddress)}");
                    blackMagic.Asm.Inject((codeCave + (uint)asmLenght) + 5);
                    blackMagic.Asm.Clear();
                    // ---------------------------------------------------
                    // End of doing the original stuff and returning to
                    // the original instruction
                    // ---------------------------------------------------

                    // modify original EndScene instructions to start the hook
                    blackMagic.Asm.AddLine($"JMP {(codeCave)}");
                    blackMagic.Asm.Inject(endScene);
                    // we should've hooked WoW now
                }
                isHooked = true;
                Console.WriteLine("Successfully hooked!");
            } 
            catch(Exception e)
            {
                isHooked = false;
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Inject assembly code on our hook
        /// </summary>
        /// <param name="asm">assembly to execute</param>
        /// <param name="readReturnBytes">should the return bytes get read</param>
        /// <param name="successful">if the reading of return bytes was successful</param>
        /// <returns></returns>
        internal byte[] InjectAndExecute(string[] asm, bool readReturnBytes, out bool successful)
        {
            List<byte> returnBytes = new List<byte>();

            try
            {
                // wait for the code to be executed
                while (blackMagic.ReadInt(codeToExecute) > 0 || isInjectionUsed)
                    Thread.Sleep(5);

                isInjectionUsed = true;

                // preparing to inject the given ASM
                blackMagic.Asm.Clear();

                // add all lines
                foreach (string s in asm)
                    blackMagic.Asm.AddLine(s);

                // now there is code to be executed
                blackMagic.WriteInt(codeToExecute, 1);

                // inject it
                blackMagic.Asm.Inject(codeCaveForInjection);

                // wait for the code to be executed
                while (blackMagic.ReadInt(codeToExecute) > 0)
                    Thread.Sleep(1);

                // if we want to read the return value do it otherwise we're done
                if (readReturnBytes)
                {
                    byte buffer = new byte();
                    try
                    {
                        // get our return parameter address
                        uint dwAddress = blackMagic.ReadUInt(returnAdress);

                        // read all parameter-bytes until we the buffer is 0
                        buffer = blackMagic.ReadByte(dwAddress);
                        while (buffer != 0)
                        {
                            returnBytes.Add(buffer);
                            dwAddress = dwAddress + 1;
                            buffer = blackMagic.ReadByte(dwAddress);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("InjectAndExecute failed. Crash at reading return address: {0}", e);
                    }
                }
            }
            catch (Exception e)
            {
                // now there is no more code to be executed
                blackMagic.WriteInt(codeToExecute, 0);
                successful = false;

                Console.WriteLine("Crash at InjectAndExecute: {0}", e);

                foreach (string s in asm)
                    Console.WriteLine("ASM content: {0}", s);

                Console.WriteLine("ReadReturnBytes: {0}", readReturnBytes);
            }

            // now we can use the hook again
            isInjectionUsed = false;
            successful = true;

            return returnBytes.ToArray();
        }


        public void DisposeHooking()
        {
            // get D3D9 Endscene Pointer
            uint endscene = GetEndScene();
            endscene += ENDSCENE_HOOK_OFFSET;

            // check if WoW is hooked
            if (blackMagic.ReadByte(endscene) == 0xE9)
            {
                blackMagic.WriteBytes(endscene, originalEndscene);

                blackMagic.FreeMemory(codeCave);
                blackMagic.FreeMemory(codeToExecute);
                blackMagic.FreeMemory(codeCaveForInjection);
            }

            isHooked = false;
        }

        private uint GetEndScene()
        {
            uint pDevice = blackMagic.ReadUInt(Offsets.DEVICE_PTR1);
            uint pEnd = blackMagic.ReadUInt(pDevice + Offsets.DEVICE_PTR2);
            uint pScene = blackMagic.ReadUInt(pEnd);
            return blackMagic.ReadUInt(pScene + Offsets.END_SCENE);
        }
    }
}
