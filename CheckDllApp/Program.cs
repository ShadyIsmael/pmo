using System;
using System.IO;
using System.Text;

class Program
{
    static void Main()
    {
        var bytes = File.ReadAllBytes(@"C:\Projects\pmo\apps\api\src\NEXUS.Modules.Documents\bin\Debug\net10.0\NEXUS.Modules.Documents.dll");
        var text16 = Encoding.Unicode.GetString(bytes);
        
        string arabic1 = "مرجع كامل";
        string arabic2 = "القالب المصدري";
        
        int idx = text16.IndexOf(arabic1);
        if (idx >= 0)
        {
            // Get the exact substring from the DLL
            string context = text16.Substring(idx, 80);
            Console.WriteLine("Context from DLL (with char codes):");
            for (int i = 0; i < context.Length; i++)
            {
                Console.Write($"U+{(int)context[i]:X4} ");
            }
            Console.WriteLine();
            Console.WriteLine("As string: " + context);
        }
        
        // Now check the test's expected string
        Console.WriteLine("\nTest string 'القالب المصدري' char codes:");
        for (int i = 0; i < arabic2.Length; i++)
        {
            Console.Write($"U+{(int)arabic2[i]:X4} ");
        }
        Console.WriteLine();
        
        // Check if the DLL contains the exact test string bytes
        var testBytes = Encoding.Unicode.GetBytes(arabic2);
        Console.WriteLine("\nSearching for test bytes in DLL...");
        bool found = ContainsBytes(bytes, testBytes);
        Console.WriteLine("Found: " + found);
        
        // Also check with UTF-8 bytes (what the test might be comparing)
        var testBytes8 = Encoding.UTF8.GetBytes(arabic2);
        Console.WriteLine("Searching for UTF-8 test bytes in DLL...");
        found = ContainsBytes(bytes, testBytes8);
        Console.WriteLine("Found: " + found);
    }
    
    static bool ContainsBytes(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return true;
        }
        return false;
    }
}