using System;
using System.Linq;
using Mono.Cecil;

internal static class VerifyIL
{
    private static void Main(string[] args)
    {
        string path = args.Length > 0 ? args[0] : "pcl_patched.exe";
        var asm = AssemblyDefinition.ReadAssembly(path);
        var mod = asm.MainModule;

        var m = mod.GetType("PCL.PageDownloadInstall").Methods.First(x => x.Name == "LoaderInit");
        Console.WriteLine("=== LoaderInit ===");
        foreach (var i in m.Body.Instructions)
            Console.WriteLine("IL_" + i.Offset.ToString("X4") + " " + i.OpCode + " " + i.Operand);

        var m2 = mod.GetType("PCL.PageDownloadModify").Methods.First(x => x.Name == "LoadInstanceFinished");
        Console.WriteLine("=== LoadInstanceFinished (CardMinecraft) ===");
        var ins = m2.Body.Instructions;
        for (int k = 0; k < ins.Count; k++)
        {
            if (ins[k].OpCode.ToString() == "callvirt" && ins[k].Operand != null && ins[k].Operand.ToString().Contains("get_CardMinecraft"))
            {
                for (int j = k; j < Math.Min(k + 4, ins.Count); j++)
                    Console.WriteLine("IL_" + ins[j].Offset.ToString("X4") + " " + ins[j].OpCode + " " + ins[j].Operand);
            }
        }
    }
}
