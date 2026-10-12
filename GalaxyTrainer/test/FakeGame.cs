// Copyright (c) 2026 drdonut54. All rights reserved. See LICENSE.
// Fake single-player game used to test Galaxy Trainer's smart detector.
using System;
using System.Threading;

class Player
{
    public float Health = 100f, MaxHealth = 100f;
    public int Ammo = 30, MaxAmmo = 30;
    public int Money = 500;
}

class FakeGame
{
    static void Main()
    {
        var p = new Player();
        var rng = new Random(1);
        var noise = new float[4096];      // positions, animation, physics - changes every frame
        int frame = 0;
        double t = 0, nextHit = 6, nextShot = 7, nextCash = 8;
        while (true)
        {
            Thread.Sleep(16);
            t += 0.016;
            frame++;
            for (int i = 0; i < noise.Length; i++) noise[i] += (float)(rng.NextDouble() - 0.5);
            if (t > nextHit)
            {
                nextHit = t + 3 + rng.NextDouble() * 2;
                p.Health -= 10 + rng.Next(15);
                if (p.Health < 30) p.Health = p.MaxHealth;     // medkit
            }
            if (t > nextShot)
            {
                nextShot = t + 1.2;
                p.Ammo--;
                if (p.Ammo == 0) p.Ammo = p.MaxAmmo;               // reload
            }
            if (t > nextCash)
            {
                nextCash = t + 4 + rng.NextDouble() * 3;
                p.Money += rng.Next(3) == 0 ? -40 : 25 + rng.Next(200);
            }
            if (frame % 120 == 0)
                Console.WriteLine("t={0:0} health={1} ammo={2} money={3}", t, p.Health, p.Ammo, p.Money);
        }
    }
}
