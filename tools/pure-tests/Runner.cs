using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// Minimal CLI test runner: finds every [Test] method in the compiled assembly,
// invokes it, and reports pass/fail. Exists because the Unity editor holds a
// lock that makes batch-mode -runTests unreliable while the editor is open.
public static class Runner
{
    public static int Main()
    {
        int pass = 0, fail = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            foreach (var m in type.GetMethods().Where(x => x.GetCustomAttributes(typeof(TestAttribute), true).Any()))
            {
                object instance = Activator.CreateInstance(type);
                try { m.Invoke(instance, null); pass++; Console.WriteLine("PASS " + type.Name + "." + m.Name); }
                catch (TargetInvocationException ex)
                {
                    fail++;
                    Console.WriteLine("FAIL " + type.Name + "." + m.Name + ": " + ex.InnerException.Message);
                }
            }
        }
        Console.WriteLine(string.Format("--- {0} passed, {1} failed ---", pass, fail));
        return fail == 0 ? 0 : 1;
    }
}
