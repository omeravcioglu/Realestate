using System.Collections.Generic;
using UnityEngine;

public class ScaryElementLibrary : MonoBehaviour
{
    public List<string> elements = new()
    {
        "Banging Door","Cold Spot","Flickering Light","Static TV","Moving Curtains",
        "Footsteps","Whispering","Shadow Figure","Mirror Breath","Slamming Window",
        "Crawling Noise","Clock Stopped","Candles Extinguish","Keys Rattling",
        "Pages Turning","Phone Ring","Knocking","Wind Chime Inside","Toy Rolls","Water Dripping"
    };

    public string GetName(int id) =>
        (id >= 0 && id < elements.Count) ? elements[id] : $"Element#{id}";
}
