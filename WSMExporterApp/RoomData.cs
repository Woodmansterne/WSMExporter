using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Numerics;
using System.Text.RegularExpressions;

namespace WSMExporter
{
    using Table = Dictionary<object, object>;

    public class Entity
    {
        public string EntityType = "";
        public string Name = "";
        public Vector2 Position = new(0.0f);
        public Table Data = new();
    }

    public enum Layer
    {
        Floor, LowWall, Wall, Ceiling
    }

    public class Brush
    {
        public Vector2[] Vertices = Array.Empty<Vector2>();
        public float VerticalPosition = 0.0f;
        public float Height = 0.0f;

        public Layer Layer = Layer.Floor;
        public int EntityIndex = -1;
        public int MaterialIndex = -1;

        public Vector2 TextureTransformOffset;
        public float TextureTransformAngle = 0.0f;
        public Vector2 TextureTransformScale;
    }

    public class Params
    {
        //empty for now
    }

    public class RoomData
    {
        public Brush[] Brushes;
        public Entity[] Entities;
        public string[] Materials;

        public static RoomData? Load(byte[] data, Params? room_params = null)
        {
            Params @params = room_params ?? new();
            BinaryReader bin = new BinaryReader(new MemoryStream(data), Encoding.UTF8);

            string head_id = bin.ReadBytes(3).ToString() ?? "";
            if (head_id.Equals("WSM", StringComparison.Ordinal))
                throw new Exception("Failed to load WSM room: bad header");

            ushort[] version = new ushort[4];
            for (int v_idx = 0; v_idx < version.Length; v_idx++)
                version[v_idx] = bin.ReadUInt16();

            Console.WriteLine("[WS1 Room] version {0}", version[0]);


            switch (version[0])
            {
                case 5: Console.WriteLine("[WS1 Room] format: PIC20 (5)"); break;
                case 6: Console.WriteLine("[WS1 Room] format: PIC20/CRX (6)"); break;
                case 7: Console.WriteLine("[WS1 Room] format: CRX3D (7)"); break;
                default: Console.WriteLine("[WS1 Room] format: Legacy ({0})", version[0]); break;
            }

            //What version?
            if (version[0] > 7)
                throw new Exception("Failed to load WS1 room: version is unknown");
            
            string ReadString(BinaryReader reader)
            {
                StringBuilder string_builder = new();
                string_builder.Clear();
                while (true)
                {
                    char c = bin.ReadChar();
                    if (c == '\0') break;
                    string_builder.Append(c);
                }
            
                return string_builder.ToString();
            }

            object? ReadVariant(BinaryReader bin)
            {
                int type_n = bin.ReadByte();
                object? value;
                switch (type_n)
                {
                    case 0: value = null; break;
                    case 1: value = bin.ReadDouble(); break;
                    case 2: value = ReadString(bin); break;
                    case 3: value = ReadTable(bin); break;
                    case 4: value = bin.ReadBoolean(); break;
                    default: throw new Exception("unknown variant type");
                }

                return value;
            }

            Table ReadTable(BinaryReader bin)
            {
                Table dict = new();

                while (true)
                {
                    object? key = ReadVariant(bin);
                    if (key is null) break;

                    object? value = ReadVariant(bin);
                    if (value is null)
                        throw new Exception("Failed to load WS1 room: illegal nil value encountered in entity data");

                    dict.Add(key, value);
                }

                return dict;
            }

            string[] materials = Array.Empty<string>();
            {
                List<string> list = new();
                while (true)
                {
                    string mat_name = ReadString(bin);
                    if (mat_name.Length == 0) break;
                    list.Add(mat_name);
                }
                materials = list.ToArray();
            }

            Console.WriteLine("[WS1 Room] material count: {0}", materials.Length);

            float default_wall_height = 0.0f;
            if (version[0] > 3)
            {
                default_wall_height = bin.ReadSingle();
                Console.WriteLine("[WS1 Room] default wall height: {0}", default_wall_height);
            }

            int brush_count = bin.ReadInt32();
            Brush[] brushes = new Brush[brush_count];
            
            Console.WriteLine("[WS1 Room] brush count: {0}", brush_count);

            for (int i = 0; i < brush_count; i++)
            {
                Brush brush = brushes[i] = new();

                byte point_count = bin.ReadByte();
                if (point_count == 0xFF) //circle brush
                {
                    Console.WriteLine("[WS1 Room] ignoring circle brush: #{0}", i);
                    bin.ReadBytes(12);
                }
                else
                {
                    Vector2[] vertices_2d = new Vector2[point_count];
                    //vertices
                    for (int vertex_idx = 0; vertex_idx < point_count; vertex_idx++)
                    {
                        float x = bin.ReadSingle();
                        float y = bin.ReadSingle();
                        vertices_2d[vertex_idx] = new Vector2(x, y);
                    }
                    brush.Vertices = vertices_2d;

                    //matn
                    int material_idx = bin.ReadUInt16() - 1;
                    brush.MaterialIndex = material_idx;
                    //MeshSetup mesh = meshes[material_idx];

                    //tex transform
                    float tt_x = bin.ReadSingle();
                    float tt_y = bin.ReadSingle();
                    float tt_r = bin.ReadSingle();
                    float tt_sx = bin.ReadSingle();
                    float tt_sy = bin.ReadSingle();
                    brush.TextureTransformOffset = new(tt_x, tt_y);
                    brush.TextureTransformAngle = tt_r;
                    brush.TextureTransformScale = new(tt_sx, tt_sy);

                    //this is how you apply texture transforms to uv
                    //Transform2D tex_trans = Transform2D.Identity
                    //    .Scaled(new Vector2(tt_sx, tt_sy))
                    //    .Scaled(new Vector2(128.0f, 128.0f))
                    //    .Rotated(tt_r)
                    //    .Translated(new Vector2(tt_x, tt_y))
                    //    .AffineInverse()
                    //   ;

                    //layer n
                    int layer_idx = bin.ReadByte();
                    brush.Layer = (Layer)layer_idx;

                    //attached entity
                    int entity_idx = bin.ReadUInt16();
                    brush.EntityIndex = entity_idx;

                    //unused?
                    bin.ReadUInt16();

                    float wall_base = 0.0f;
                    float wall_height = 0.0f;

                    float fake2d_base = 0.0f;
                    float fake2d_height = 0.0f;
                    float fake2d_z = 0.0f;

                    if (version[0] > 3)
                    {
                        //wall height
                        fake2d_height = bin.ReadSingle();
                    }

                    if (version[0] > 4)
                    {
                        //wall bottom
                        fake2d_base = bin.ReadSingle();
                    }

                    if (version[0] > 5)
                    {
                        //z offset
                        fake2d_z = bin.ReadSingle();
                    }

                    //Console.WriteLine("fake z values: {0} {1} {2}", fake2d_base, fake2d_height, fake2d_z);

                    if (version[0] > 6)
                    {
                        //brush z
                        wall_base = bin.ReadSingle();

                        //height
                        wall_height = bin.ReadSingle();
                    }
                    else
                    {
                        wall_base = 0.0f;
                        if (layer_idx == 2)
                            wall_height = 8.0f * default_wall_height;
                        else if (layer_idx == 3)
                            wall_height = 32.0f * default_wall_height;
                        else
                            wall_height = 0.0f;
                    }

                    //process brush
                    if (layer_idx > 3)
                        continue;

                    if (layer_idx == 3 && wall_height == 0.0f)
                        wall_height = 128.0f;

                    if (layer_idx == 2 && wall_height == 0.0f)
                        wall_height = 32.0f;

                    brush.VerticalPosition = wall_base;
                    brush.Height = wall_height;

                    //if (@params.EnableCRX3DZeroFloorUV && layer_idx == 1 && (wall_base == 0.0f && wall_height == 0.0f))
                    //  special_floor_uv = true;
                }
            }

            //Console.WriteLine("[WS1 Room] total vertices: {0}", total_vertex_count);
            //Console.WriteLine("[WS1 Room] total indices: {0}", total_index_count);

            //entities
            int entity_count = bin.ReadInt32();
            Console.WriteLine("[WS1 Room] entity count: {0}", entity_count);
            Entity[] entities = new Entity[entity_count];

            for (int entity_idx = 0; entity_idx < entity_count; entity_idx++)
            {
                Entity ent = entities[entity_idx] = new();
                ent.EntityType = ReadString(bin);
                float x = bin.ReadSingle();
                float y = bin.ReadSingle();
                float angle = bin.ReadSingle();
                ent.Name = ReadString(bin);
                ent.Data = ReadTable(bin);
                ent.Position = new Vector2(x, y);
            }

            RoomData output = new();
            output.Brushes = brushes;
            output.Entities = entities;
            output.Materials = materials;
            return output;
        }

        private RoomData()
        {
            Brushes = Array.Empty<Brush>();
            Entities = Array.Empty<Entity>();
            Materials = Array.Empty<string>();
        }
    }
}