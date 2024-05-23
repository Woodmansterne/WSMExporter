using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace WSMExporter
{
    public class MapExporter
    {
        public Stream? Stream {
            get { return stream; }
            set {
                stream = value;
                if (stream != null)
                    writer = new(stream, new UTF8Encoding(false));
                else
                    writer = null;
            }
        }

        public RoomData? RoomData { get; set; }
        public string Game = "POTS";
        public Dictionary<string, string> TextureMap = new();
        public string DefaultTexture = "dev/grey";

        public void Export()
        {
            if (writer is null || RoomData is null)
            {
                Console.WriteLine("Set stream and room data before exporting.");
                return;
            }

            entity_n = 0;

            writer.Write($"// Game: {Game}\n");
            writer.Write($"// Format: Standard\n");
            for (int i = -1; i < RoomData.Entities.Length; i++) {
                WriteEnt(i);
            }
        }

        private static readonly HashSet<string> accepted_types = new()
        {
            "worldspawn",
            "light"
        };

        //use -1 for worldspawn
        private void WriteEnt(int eidx)
        {
            bool is_world = (eidx < 0);
            Entity? entity = !is_world ? RoomData.Entities[eidx] : null;
            string type = is_world ? "worldspawn" : entity.EntityType;

            if (!accepted_types.Contains(type)) return;
            writer.Write($"// entity {entity_n}\n");
            entity_n++;
            writer.Write($"{{\n");
            writer.Write($"\"classname\" \"{type}\"\n");
            if (entity is not null)
            {
                writer.Write($"\"origin\" \"{-entity.Position.X} {entity.Position.Y} 64\"\n");
                if (entity.Data.ContainsKey("radius")) writer.Write($"\"light\" \"{entity.Data["radius"]}\"\n");
                if (entity.Data.ContainsKey("color_r") && entity.Data.ContainsKey("color_g") && entity.Data.ContainsKey("color_b"))
                    writer.Write($"\"_color\" \"{(int)((double)entity.Data["color_r"] * 255)} {(int)((double)entity.Data["color_g"] * 255)} {(int)((double)entity.Data["color_b"] * 255)}\"\n");
            }
            //if (entity != null) WriteData(entity);
            WriteBrushes(eidx);
            writer.Write($"}}\n");
        }

        private void WriteBrushes(int eidx)
        {
            int bn = 0;
            foreach (Brush brush in RoomData.Brushes)
            {
                int entity_idx = brush.EntityIndex - 1;
                if (entity_idx != eidx) continue;

                writer.Write($"// brush {bn}\n");
                writer.Write($"{{\n");
                bn++;
                //floor/ceiling
                float floorz = brush.VerticalPosition + brush.Height;
                float ceilz = brush.VerticalPosition;
                if (floorz == ceilz) ceilz = floorz - 32.0f;
                string texture = RoomData.Materials[brush.MaterialIndex];
                if (TextureMap.ContainsKey(texture)) {
                    texture = TextureMap[texture];
                } else if (DefaultTexture.Length > 0)
                {
                    texture = DefaultTexture;
                }
                string transf = $"{brush.TextureTransformOffset.X} {brush.TextureTransformOffset.Y} {brush.TextureTransformAngle} {brush.TextureTransformScale.X} {brush.TextureTransformScale.Y}";
                for (int i = 0; i < brush.Vertices.Length; i++)
                {
                    Vector2 v0 = brush.Vertices[i];
                    Vector2 v1 = brush.Vertices[i == brush.Vertices.Length - 1 ? 0 : i + 1];
                    writer.Write($"( {-v0.X} {v0.Y} {ceilz} ) ( {-v1.X} {v1.Y} {ceilz} ) ( {-v0.X} {v0.Y} {ceilz + 1} ) {texture} -0 -0 -0 1 1\n");
                }
                writer.Write($"( 0 0 {ceilz} ) ( 1 0 {ceilz} ) ( 0 1 {ceilz} ) {texture} 0 0 0 1 1\n");
                writer.Write($"( 0 0 {floorz} ) ( 0 1 {floorz} ) ( 1 0 {floorz} ) {texture} 0 0 0 1 1\n");
                writer.Write($"}}\n");
            }
        }

        private void WriteData(Entity entity)
        {
            foreach (KeyValuePair<object, object> pair in entity.Data)
            {
                string key = "";
                string value = "";

                switch (pair.Key)
                {
                    case string s:
                        key = $"wsm_{s}";
                        break;
                    case double d:
                        key = $"wsm_{d}";
                        break;
                    default:
                        continue;
                }

                switch (pair.Value)
                {
                    case string s:
                        value = s;
                        break;
                    case double d:
                        value = $"{d}";
                        break;
                    case bool b:
                        value = b ? "1" : "0";
                        break;
                    default:
                        continue;
                }

                writer.Write($"\"{key}\" \"{value}\"");
            }
        }

        private Stream? stream = null;
        private StreamWriter? writer = null;
        private int entity_n = 0;
    }
}