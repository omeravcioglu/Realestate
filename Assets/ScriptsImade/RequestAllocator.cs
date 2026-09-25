using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class RequestAllocator : NetworkBehaviour
{
    public ScaryElementLibrary library;
    private readonly List<int> bag = new();
    private int cursor;

    public override void Spawned() { if (Object.HasStateAuthority) Refill(); }

    private void Refill()
    {
        bag.Clear();
        int n = library ? library.elements.Count : 0;
        for (int i = 0; i < n; i++) bag.Add(i);
        for (int i = n - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (bag[i], bag[j]) = (bag[j], bag[i]); }
        cursor = 0;
    }

    public int NextElementId()
    {
        if (!Object.HasStateAuthority) return -1;
        if (bag.Count == 0 || cursor >= bag.Count) Refill();
        return bag[cursor++];
    }
}
