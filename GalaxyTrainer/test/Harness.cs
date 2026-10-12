// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
// Runs the smart detector headless against a pid and prints what it finds.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

class Harness
{
    static void Main(string[] args)
    {
        int pid = int.Parse(args[0]);
        int seconds = int.Parse(args[1]);
        var h = Native.OpenProcess(Native.PROCESS_ACCESS, false, pid);
        var det = new AutoDetector(new Scanner(), new List<IntPtr> { h });
        List<Track> last = new List<Track>();
        string lastMsg = "";
        det.OnUpdate = (m, top) => { lastMsg = m; last = top; };
        det.OnFound = f => { foreach (var x in f) Console.WriteLine("FOUND {0} {1:X} {2} = {3} ({4}%) first={5} max={6} min={7} twinOff={8} streak={9} drops={10} sizes={11}", x.Guess, x.Addr, Values.TypeNames[x.Type], x.Cur, x.Score, x.First, x.Max, x.Min, x.TwinOff, x.TwinStreak, x.Drops, string.Join("/", x.DropSizes)); };
        det.Start(new[] { true, true, false, false });
        for (int i = 0; i < seconds; i += 5)
        {
            Thread.Sleep(5000);
            Console.WriteLine("[{0}s] {1}", i + 5, lastMsg);
        }
        Console.WriteLine("Top candidates:");
        foreach (var x in last.Take(10))
            Console.WriteLine("  {0:X} {1,-6} val={2,-8} {3,-7} {4}% drops={5} rises={6} twin={7}", x.Addr, Values.TypeNames[x.Type], x.Cur, x.Guess, x.Score, x.Drops, x.Rises, x.Twin);
        det.Stop();
        Thread.Sleep(1000);
    }
}

