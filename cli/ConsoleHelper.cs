using System;
using System.Text;

namespace WhatInsides.Cli;

public static class ConsoleHelper
{
    public static string ReadPassword(string prompt = "Enter Master Password: ")
    {
        Console.Write(prompt);
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Remove(password.Length - 1, 1);
                    Console.Write("\b \b");
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Write("*");
            }
        }
        return password.ToString();
    }

    public static void WriteSuccess(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[✓] {message}");
        Console.ForegroundColor = prev;
    }

    public static void WriteError(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"[x] {message}");
        Console.ForegroundColor = prev;
    }

    public static void WriteWarning(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[!] {message}");
        Console.ForegroundColor = prev;
    }

    public static void WriteInfo(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[i] {message}");
        Console.ForegroundColor = prev;
    }
}
