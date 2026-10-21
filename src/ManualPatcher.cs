using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class ManualPatcher
{
    private static int RvaToOffset(byte[] pe, int rva)
    {
        int peOff = BitConverter.ToInt32(pe, 0x3c);
        int numSec = BitConverter.ToInt16(pe, peOff + 6);
        int optSize = BitConverter.ToInt16(pe, peOff + 20);
        int secOff = peOff + 24 + optSize;
        for (int i = 0; i < numSec; i++)
        {
            int o = secOff + i * 40;
            int vs = BitConverter.ToInt32(pe, o + 8);
            int va = BitConverter.ToInt32(pe, o + 12);
            int raw = BitConverter.ToInt32(pe, o + 20);
            if (rva >= va && rva < va + vs) return raw + (rva - va);
        }
        return -1;
    }

    private static void Main(string[] args)
    {
        string src = args.Length > 0 ? args[0] : "pcl.exe";
        string dst = args.Length > 1 ? args[1] : "pcl_patched.exe";

        // 只读解析元数据，拿 token / RVA / IL 偏移；写回完全用字节操作
        AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(src);
        ModuleDefinition mod = asm.MainModule;

        TypeDefinition t = mod.GetType("PCL.PageDownloadInstall");
        MethodDefinition m = t.Methods.First(x => x.Name == "LoaderInit");
        FieldDefinition field = mod.GetType("PCL.ModDownload").Fields.First(x => x.Name == "DlClientListLoader");
        int fieldToken = field.MetadataToken.ToInt32();

        var ins = m.Body.Instructions;
        Instruction ldstr = ins.First(x => x.OpCode == OpCodes.Ldstr);
        // LoaderTask 构造前一条是 ldc.i4.2（ThreadPriority），不能误选到
        // 更早的 Action<LoaderTask> 委托构造
        Instruction newobj = ins
            .SkipWhile(x => x != ldstr)
            .SkipWhile(x => x.OpCode != OpCodes.Ldc_I4_2)
            .Skip(1)
            .First(x => x.OpCode == OpCodes.Newobj);

        int methodIlStart = RvaToOffset(asmImage(src), m.RVA + 12);
        Console.WriteLine("fieldToken=0x" + fieldToken.ToString("X8"));
        Console.WriteLine("LoaderInit RVA=0x" + m.RVA.ToString("X") + " ilFile=0x" + methodIlStart.ToString("X"));
        Console.WriteLine("ldstr IL=0x" + ldstr.Offset.ToString("X") + " newobj IL=0x" + newobj.Offset.ToString("X") + " newobjSize=" + newobj.GetSize());

        byte[] pe = File.ReadAllBytes(src);
        int ilStart = RvaToOffset(pe, m.RVA + 12);
        int startPos = ilStart + ldstr.Offset;
        int fillEnd = ilStart + newobj.Offset + newobj.GetSize();

        // ldsfld <DlClientListLoader>
        pe[startPos] = 0x7E;
        Array.Copy(BitConverter.GetBytes(fieldToken), 0, pe, startPos + 1, 4);
        for (int i = startPos + 5; i < fillEnd; i++) pe[i] = 0x00; // nop

        // PageDownloadModify::LoadInstanceFinished: ldc.i4.2 -> ldc.i4.0
        TypeDefinition t2 = mod.GetType("PCL.PageDownloadModify");
        if (t2 != null)
        {
            MethodDefinition m2 = t2.Methods.FirstOrDefault(x => x.Name == "LoadInstanceFinished");
            if (m2 != null && m2.HasBody)
            {
                var ins2 = m2.Body.Instructions;
                Instruction call = ins2.First(x => x.OpCode == OpCodes.Callvirt
                    && x.Operand != null && x.Operand.ToString().Contains("get_CardMinecraft"));
                Instruction ldc = ins2.SkipWhile(x => x != call).Skip(1).First(x => x.OpCode == OpCodes.Ldc_I4_2);
                int pos2 = RvaToOffset(pe, m2.RVA + 12 + ldc.Offset);
                Console.WriteLine("CardMinecraft ldc.i4.2 file=0x" + pos2.ToString("X") + " (old byte 0x" + pe[pos2].ToString("X2") + ")");
                pe[pos2] = 0x16; // ldc.i4.0
            }
        }

        File.WriteAllBytes(dst, pe);
        Console.WriteLine("written " + dst + " (" + new FileInfo(dst).Length + " bytes)");
    }

    private static byte[] asmImage(string path)
    {
        return File.ReadAllBytes(path);
    }
}
