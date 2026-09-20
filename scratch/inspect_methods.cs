using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

class Program
{
    static void Main()
    {
        var asm = Assembly.Load("Npgsql.EntityFrameworkCore.PostgreSQL");
        var types = asm.GetTypes().Where(t => t.Name.Contains("DbFunctionsExtensions") || t.Name.Contains("Vector"));
        foreach (var type in types)
        {
            Console.WriteLine(type.FullName);
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (method.Name.Contains("Distance") || method.Name.Contains("Cosine"))
                {
                    var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                    Console.WriteLine($"  {method.Name}({parameters})");
                }
            }
        }
        
        var pgVectorAsm = Assembly.Load("Pgvector.EntityFrameworkCore");
        var pgTypes = pgVectorAsm.GetTypes().Where(t => t.Name.Contains("Functions") || t.Name.Contains("Vector"));
        foreach (var type in pgTypes)
        {
            Console.WriteLine(type.FullName);
            foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public))
            {
                if (method.Name.Contains("Distance") || method.Name.Contains("Cosine"))
                {
                    var parameters = string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name));
                    Console.WriteLine($"  {method.Name}({parameters})");
                }
            }
        }
    }
}
