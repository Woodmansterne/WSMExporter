using System.Collections.Generic;
using System.Linq;
using WSMExporter;

void PrintStuffOut(RoomData rd)
{
    string? ToDisplayable(object obj)
    {
        if (obj is string s)
            return $"\"{s}\"";
        else if (obj is Dictionary<object, object> table)
            return string.Format("{{{0}}}", string.Join(", ", from pair in table select $"{ToDisplayable(pair.Key)} : {ToDisplayable(pair.Value)}"));
        else
            return obj.ToString();
    }

    foreach (Entity entity in rd.Entities)
    {
        Console.WriteLine("ENTITY: {0}", entity.Name);
        foreach (KeyValuePair<object, object> entry in entity.Data)
        {
            Console.WriteLine("{0} : {1}", ToDisplayable(entry.Key), ToDisplayable(entry.Value));
        }
    }
}

string filename = "foster_network.wsm";
byte[] content = File.ReadAllBytes(filename);

RoomData? rd = RoomData.Load(content);
if (rd is null)
{
    Console.WriteLine("oh no!");
    return;
}

PrintStuffOut(rd);
