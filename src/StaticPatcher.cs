using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static class StaticPatcher
{
    private static void Main(string[] args)
    {
        string src = args.Length > 0 ? args[0] : "pcl.exe";
        string dst = args.Length > 1 ? args[1] : "pcl_patched.exe";

        AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(src);
        ModuleDefinition mod = asm.MainModule;

        // ---------- 1. PageDownloadInstall::LoaderInit ----------
        TypeDefinition type = mod.GetType("PCL.PageDownloadInstall");
        if (type == null) throw new Exception("PageDownloadInstall not found");
        MethodDefinition m = type.Methods.First(x => x.Name == "LoaderInit");
        Console.WriteLine("LoaderInit instructions: " + m.Body.Instructions.Count);

        ILProcessor il = m.Body.GetILProcessor();
        Mono.Collections.Generic.Collection<Instruction> ins = m.Body.Instructions;

        Instruction ldstr = ins.First(x => x.OpCode == OpCodes.Ldstr);
        Instruction newobj = ins
            .SkipWhile(x => x != ldstr)
            .Skip(1)
            .First(x => x.OpCode == OpCodes.Newobj && x.Operand != null && x.Operand.ToString().Contains("LoaderTask"));

        FieldDefinition field = mod.GetType("PCL.ModDownload").Fields.First(x => x.Name == "DlClientListLoader");

        int start = ins.IndexOf(ldstr);
        int end = ins.IndexOf(newobj);
        Console.WriteLine("ldstr index " + start + ", newobj index " + end);

        il.InsertBefore(ldstr, il.Create(OpCodes.Ldsfld, field));
        for (int i = end; i >= start; i--) il.Remove(ins[i]);

        // ---------- 2. PageDownloadModify::LoadInstanceFinished ----------
        TypeDefinition t2 = mod.GetType("PCL.PageDownloadModify");
        if (t2 != null)
        {
            MethodDefinition m2 = t2.Methods.FirstOrDefault(x => x.Name == "LoadInstanceFinished");
            if (m2 != null && m2.HasBody)
            {
                Mono.Collections.Generic.Collection<Instruction> ins2 = m2.Body.Instructions;
                for (int i = 0; i < ins2.Count; i++)
                {
                    if (ins2[i].OpCode == OpCodes.Callvirt
                        && ins2[i].Operand != null
                        && ins2[i].Operand.ToString().Contains("get_CardMinecraft"))
                    {
                        for (int j = i + 1; j < Math.Min(i + 6, ins2.Count); j++)
                        {
                            if (ins2[j].OpCode == OpCodes.Ldc_I4_2)
                            {
                                ins2[j].OpCode = OpCodes.Ldc_I4_0;
                                Console.WriteLine("CardMinecraft: Collapsed -> Visible at " + ins2[j].Offset);
                                break;
                            }
                        }
                    }
                }
            }
        }

        try
        {
            mod.Write(dst);
            Console.WriteLine("written: " + dst + " (" + new System.IO.FileInfo(dst).Length + " bytes)");
        }
        catch (Exception ex)
        {
            string errPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patcher-error.txt");
            try
            {
                System.IO.File.WriteAllText(errPath,
                    ex.GetType().FullName + Environment.NewLine + ex.Message + Environment.NewLine + ex.StackTrace);
            }
            catch { }
            Console.WriteLine("WRITE ERROR: " + errPath);
            Environment.Exit(2);
        }
    }
}
