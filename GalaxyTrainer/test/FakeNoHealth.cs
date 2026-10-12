// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
// Fake game with NO health/ammo/money: only the kind of noise real engines produce.
using System;
using System.Collections.Generic;
using System.Threading;

class FakeNoHealth
{
    static void Main()
    {
        var rng = new Random(7);
        var slowInts = new int[5000];        // rarely-changing ints (ids, states, cooldowns)
        var slowFloats = new float[5000];
        for (int i = 0; i < slowInts.Length; i++) { slowInts[i] = rng.Next(2, 50000); slowFloats[i] = (float)(rng.NextDouble() * 1000); }
        var garbage = new List<int[]>();
        int frame = 0;
        while (true)
        {
            Thread.Sleep(16);
            frame++;
            // a few slow values change at random, both directions, random sizes
            for (int n = 0; n < 3; n++)
            {
                int i = rng.Next(slowInts.Length);
                slowInts[i] += rng.Next(-300, 300);
                if (slowInts[i] < 2) slowInts[i] = 2;
                int j = rng.Next(slowFloats.Length);
                slowFloats[j] += (float)(rng.NextDouble() * 200 - 100);
                if (slowFloats[j] < 1) slowFloats[j] = 1;
            }
            // allocation churn so the GC moves memory around
            garbage.Add(new int[rng.Next(100, 5000)]);
            if (garbage.Count > 400) garbage.RemoveRange(0, 200);
            // occasional "level load": rewrite a big block at once
            if (frame % 900 == 0) for (int i = 0; i < slowInts.Length; i++) slowInts[i] = rng.Next(2, 50000);
        }
    }
}
