using System;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

internal static class DnlibPatcher
{
    private static void Main(string[] args)
    {
        string src = args.Length > 0 ? args[0] : "pcl.exe";
        string dst = args.Length > 1 ? args[1] : "pcl_patched.exe";

        ModuleDefMD mod = ModuleDefMD.Load(src);

        TypeDef t = mod.Find("PCL.PageDownloadInstall", false);
        if (t == null) throw new Exception("PageDownloadInstall not found");
        MethodDef m = t.FindMethod("LoaderInit");
        if (m == null || !m.HasBody) throw new Exception("LoaderInit not found");

        var ins = m.Body.Instructions;
        Console.WriteLine("LoaderInit instructions: " + ins.Count);

        Instruction ldstr = ins.First(x => x.OpCode == OpCodes.Ldstr);
        Instruction newobj = ins
            .SkipWhile(x => x != ldstr)
            .Skip(1)
            .First(x => x.OpCode == OpCodes.Newobj && x.Operand != null && x.Operand.ToString().Contains("LoaderTask"));

        TypeDef md = mod.Find("PCL.ModDownload", false);
        FieldDef field = md.FindField("DlClientListLoader");

        int start = ins.IndexOf(ldstr);
        int end = ins.IndexOf(newobj);
        Console.WriteLine("ldstr index " + start + ", newobj index " + end);

        ins.Insert(start, new Instruction(OpCodes.Ldsfld, field));
        for (int i = end + 1; i >= start + 1; i--) ins.RemoveAt(i);

        TypeDef t2 = mod.Find("PCL.PageDownloadModify", false);
        if (t2 != null)
        {
            MethodDef m2 = t2.FindMethod("LoadInstanceFinished");
            if (m2 != null && m2.HasBody)
            {
                var ins2 = m2.Body.Instructions;
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
                                Console.WriteLine("CardMinecraft: Collapsed -> Visible");
                                break;
                            }
                        }
                    }
                }
            }
        }

        mod.Write(dst);
        Console.WriteLine("written " + dst + " (" + new System.IO.FileInfo(dst).Length + " bytes)");
    }
}
