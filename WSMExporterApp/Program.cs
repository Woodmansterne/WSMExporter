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

Console.WriteLine("WSMExporter Version 1.1");
Console.WriteLine("Written by Nick");
Console.WriteLine("Woodmansterne Audiovisual Department");
Console.WriteLine("====================");

if (args.Length == 0)
{
    Console.WriteLine("Include the name of the .wsm you are converting.");
}

foreach (string arg in args)
{
    string filename = arg;
    if (!filename.EndsWith(".wsm"))
    {
        Console.WriteLine($"Input file {filename} must be a .wsm format.");
    }

    Console.WriteLine($"Input file {filename}");
    string output_filename = filename.Substring(0, filename.Length - 4) + ".map";
    byte[] content = File.ReadAllBytes(filename);

    try
    {
        RoomData? rd = RoomData.Load(content);
        if (rd is null)
        {
            Console.WriteLine("oh no!");
            return;
        }

        using (FileStream stream = new(output_filename, FileMode.Create, FileAccess.Write))
        {
            MapExporter exporter = new();
            exporter.Stream = stream;
            exporter.RoomData = rd;
            exporter.TextureMap.Add("tools/bound_player", "dev/orange");
            exporter.DefaultTexture = "dev/grey";
            exporter.Export();
            stream.Flush();
        }

        Console.WriteLine($"Created {output_filename}");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Failed to convert {output_filename}: {e}");
    }
}