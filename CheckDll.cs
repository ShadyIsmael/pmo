using System;
using System.IO;
using System.Text;

class CheckDll
{
    static void Main()
    {
        var bytes = File.ReadAllBytes(@"apps/api/src/NEXUS.Modules.Documents/bin/Debug/net10.0/NEXUS.Modules.Documents.dll");
        var text = Encoding.UTF8.GetString(bytes);
        Console.WriteLine("Contains TemplateIdentityNotVerified: " + text.Contains("TemplateIdentityNotVerified"));
        
        string arabic1 = "مرجع كامل";
        int idx = text.IndexOf(arabic1);
        if (idx >= 0)
        {
            Console.WriteLine("Found 'مرجع كامل' at index: " + idx);
            Console.WriteLine("Context: " + text.Substring(idx, Math.Min(100, text.Length - idx)));
        }
        else
        {
            Console.WriteLine("'مرجع كامل' NOT FOUND");
        }
        
        string arabic2 = "القالب المصدري";
        int idx2 = text.IndexOf(arabic2);
        if (idx2 >= 0)
        {
            Console.WriteLine("Found 'القالب المصدري' at index: " + idx2);
            Console.WriteLine("Context: " + text.Substring(idx2, Math.Min(100, text.Length - idx2)));
        }
        else
        {
            Console.WriteLine("'القالب المصدري' NOT FOUND");
        }
    }
}