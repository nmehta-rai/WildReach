using System;
using System.Collections.Generic;
using UnityEngine;

// The player's actual item list (name + icon). Pickups add to it; the inventory menu reads from it. Stacks by
// name so picking up two sticks shows "Stick x2".
public class Inventory : MonoBehaviour
{
    public class Entry { public string name; public Sprite icon; public int count; }

    readonly List<Entry> items = new List<Entry>();
    public IReadOnlyList<Entry> Items => items;
    public event Action Changed;

    public void Add(string name, Sprite icon, int n = 1)
    {
        var e = items.Find(x => x.name == name);
        if (e != null) e.count += n;
        else items.Add(new Entry { name = name, icon = icon, count = n });
        Changed?.Invoke();
    }

    public bool Remove(string name, int n = 1)
    {
        var e = items.Find(x => x.name == name);
        if (e == null) return false;
        e.count -= n;
        if (e.count <= 0) items.Remove(e);
        Changed?.Invoke();
        return true;
    }

    public bool Has(string name) => items.Exists(x => x.name == name);
}
