using Amicitia.IO.Binary;
using System.Text;
using System.Text.Json;

namespace SPD_Patcher
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Drag a .spd file to generate a patch, or input Three args to patch an spd file.\n[.spdp file path] [original .spd file path] [output .spd path]");
                Console.ReadKey();
                return;
            }
            FileInfo arg0 = new (args[0]);
            if (arg0.Extension == ".spd")
            {
                Console.WriteLine("Input Texture Indexes, seperated by spaces");
                string textureIndex = Console.ReadLine();
                ReadSPD(args[0], textureIndex);
            }
            else if ( arg0.Extension == ".spdp")
            {
                if ( args.Length != 3)
                {
                    Console.WriteLine("Three args are required to patch an spd file.\n[.spdp file path] [original .spd file path] [output .spd path]");
                }
                else
                {
                    ReadSPDPatch(args);
                }
            }
            else
            {
                Console.WriteLine("If you're trying to generate an .spdp patch file, drag an spd onto the .exe\nIf you're trying to patch an spd, three args are required.\n[.spdp path] [original .spd path] [output .spd path]");
                Console.ReadKey();
            }
        }

        static void ReadSPD(string spdFileName, string textureIndexStr)
        {
            string[] textureIndexArray = textureIndexStr.Split(' ');
            int length = textureIndexArray.Length;
            int textureIndex;
            List<uint> textureIDList = new();
            List<uint> textureOffsetList = new();
            List<uint> textureSizeList = new();
            List<string> textureStringList = new();
            string[] spriteIDLine = new string[length];
            string[] OutputTextureName = new string[length];

            for (int i = 0; i < textureIndexArray.Length; i++)
            {
                textureIndex = Convert.ToInt32(textureIndexArray[i]);
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                using (BinaryObjectReader SPDFile = new(spdFileName, Endianness.Little, Encoding.GetEncoding(932)))
                {
                    SPDFile.ReadInt32(); //Magic
                    SPDFile.ReadInt32(); //field04
                    uint fileSize = SPDFile.ReadUInt32();
                    SPDFile.ReadInt32(); //field0C
                    SPDFile.ReadInt32(); //field10
                    ushort textureCount = SPDFile.ReadUInt16();
                    ushort spriteCount = SPDFile.ReadUInt16();
                    uint textureOffset = SPDFile.ReadUInt32();
                    uint spriteOffset = SPDFile.ReadUInt32();

                    for (int j = 0; j < textureCount; j++)
                    {
                        textureIDList.Add(SPDFile.ReadUInt32());
                        SPDFile.ReadInt32(); //field04
                        textureOffsetList.Add(SPDFile.ReadUInt32());
                        textureSizeList.Add(SPDFile.ReadUInt32());
                        SPDFile.ReadUInt32(); //texture width
                        SPDFile.ReadUInt32(); //texture Height
                        SPDFile.ReadUInt32(); //field18
                        SPDFile.ReadUInt32(); //field1C
                        textureStringList.Add(SPDFile.ReadString(StringBinaryFormat.FixedLength, 16));
                    }

                    OutputTextureName[i] = $"{textureStringList[textureIndex]}_{textureIndex:D2}_patch";

                    for (int j = 0; j < spriteCount; j++)
                    {
                        uint spriteID = SPDFile.ReadUInt32();
                        uint textureID = SPDFile.ReadUInt32();
                        SPDFile.ReadUInt32(); //field08
                        SPDFile.ReadUInt32(); //field0C
                        SPDFile.ReadUInt32(); //field10
                        SPDFile.ReadUInt32(); //field14
                        SPDFile.ReadUInt32(); //field18
                        SPDFile.ReadUInt32(); //field1C
                        SPDFile.ReadUInt32(); //x1 position
                        SPDFile.ReadUInt32(); //y1 position
                        SPDFile.ReadUInt32(); //x2 position
                        SPDFile.ReadUInt32(); //y2 position
                        SPDFile.ReadUInt32(); //field30
                        SPDFile.ReadUInt32(); //field34
                        SPDFile.ReadUInt32(); //x scale
                        SPDFile.ReadUInt32(); //y scale

                        for (int k = 0; k < 24; k++)
                        {
                            SPDFile.ReadUInt32();
                        }

                        if (textureID == textureIDList[textureIndex])
                        {
                            spriteIDLine[i] += $"{spriteID} ";
                        }
                    }

                    Console.WriteLine($"{textureIDList[textureIndex]} {textureOffsetList[textureIndex]} {textureSizeList[textureIndex]} {textureStringList[textureIndex]}");

                    string spdTextureFile = Path.Combine(Path.GetDirectoryName(spdFileName), $"{OutputTextureName[i]}.dds");

                    using (BinaryObjectWriter NewTextureFile = new(spdTextureFile, Endianness.Little, Encoding.GetEncoding(932)))
                    {
                        SPDFile.AtOffset(textureOffsetList[textureIndex]);
                        for (int j = 0; j < textureSizeList[textureIndex]; j++)
                        {
                            NewTextureFile.Write(SPDFile.ReadByte());
                        }
                    }
                }
            }

            CreateSPDPatch(spriteIDLine, textureIDList, textureIndexArray, OutputTextureName, spdFileName);
        }

        public class SpdPatches
        {
            public int Version { get; set; }
            public List<SpdPatchData> Patches { get; set; }
        }
        public class SpdPatchData
        {
            public string SpdPath { get; set; }
            public string TextureName { get; set; }
            public uint TextureID { get; set; }
            public string SpriteIDs{ get; set; }
        }
        static void CreateSPDPatch(string[] spriteIDLine, List<uint> textureIDList, string[] textureIndexArray, string[] textureString, string spdFileName)
        {
            SpdPatches SpdPatches = new();
            List<SpdPatchData> patchData = new();

            SpdPatches.Version = 1;
            for (int i = 0; i < textureIndexArray.Length; i++)
            {
                int textureIndex = Convert.ToInt32(textureIndexArray[i]);

                SpdPatchData SpdPatchData = new()
                {
                    SpdPath = Path.GetFileName(spdFileName),
                    TextureName = $"{textureString[i]}.dds",
                    TextureID = textureIDList[textureIndex],
                    SpriteIDs = spriteIDLine[i]
                };

                patchData.Add(SpdPatchData);
            } 

            SpdPatches.Patches = patchData;
            var indent = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(SpdPatches, indent);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(spdFileName), $"{Path.GetFileNameWithoutExtension(spdFileName)}.spdp"), json);
        }

        static void ReadSPDPatch(string[] args)
        {
            string spdPatchFile = args[0];
            string originalSpd = args[1];
            string outputSpd = args[2];
            uint highestTextureID = 0;

           if (!File.Exists(outputSpd))
            {
                File.Copy(originalSpd, outputSpd, true);
            }
            SpdPatches inputspdp = JsonSerializer.Deserialize<SpdPatches>(File.ReadAllText(spdPatchFile))!;
            foreach (var patch in inputspdp.Patches)
            {
                string spdPath = patch.SpdPath;
                string textureName = patch.TextureName;
                uint textureId = patch.TextureID;
                string[] spriteIDList = patch.SpriteIDs.Split(' ');
                List<uint> textureParams = new();
                string texturePath = Path.GetDirectoryName(Path.GetFullPath(spdPatchFile)) + @"\" + textureName;

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                using (BinaryObjectReader SPDFile = new($"{outputSpd}", Endianness.Little, Encoding.GetEncoding(932)))
                {
                    using (BinaryObjectWriter NewSPDFile = new($"{outputSpd}_out", Endianness.Little, Encoding.GetEncoding(932)))
                    {
                        NewSPDFile.Write(SPDFile.ReadInt32()); //Magic
                        NewSPDFile.Write(SPDFile.ReadInt32()); //field04
                        NewSPDFile.Write(SPDFile.ReadUInt32()); //filesize
                        NewSPDFile.Write(SPDFile.ReadInt32()); //field0C
                        NewSPDFile.Write(SPDFile.ReadInt32()); //field10

                        ushort textureCount = (ushort)(SPDFile.ReadUInt16() + 1);
                        NewSPDFile.Write(textureCount); //texture count

                        ushort spriteCount = (SPDFile.ReadUInt16());
                        NewSPDFile.Write(spriteCount); //sprite count

                        NewSPDFile.Write(SPDFile.ReadUInt32()); //texture parameter offset
                        NewSPDFile.Write(SPDFile.ReadUInt32() + 48); //sprite offset

                        for (int i = 0; i < textureCount; i++)
                        {
                            if (textureCount - 1 != i)
                            {
                                uint textureID = SPDFile.ReadUInt32();

                                if (textureID > highestTextureID)
                                    highestTextureID = textureID;

                                if (textureID == textureId)
                                {
                                    textureParams.Add(textureID); //texture id
                                    textureParams.Add(SPDFile.ReadUInt32()); //field04
                                    textureParams.Add(SPDFile.ReadUInt32() + 48); //textureOffset
                                    textureParams.Add(SPDFile.ReadUInt32()); //textureSize
                                    textureParams.Add(SPDFile.ReadUInt32()); //texture width
                                    textureParams.Add(SPDFile.ReadUInt32()); //texture Height
                                    textureParams.Add(SPDFile.ReadUInt32()); //field18
                                    textureParams.Add(SPDFile.ReadUInt32()); //field1C
                                    for (int j = 0; j < 4; j++)
                                    {
                                        textureParams.Add(SPDFile.ReadUInt32()); //texture string
                                    }

                                    for (int k = 0; k < textureParams.Count; k++)
                                    {
                                        NewSPDFile.Write(textureParams[k]);
                                    }
                                }
                                else
                                {
                                    NewSPDFile.Write(textureID); //texture id

                                    NewSPDFile.Write(SPDFile.ReadInt32()); //field04
                                    NewSPDFile.Write(SPDFile.ReadUInt32() + 48); //textureOffset
                                    NewSPDFile.Write(SPDFile.ReadUInt32()); //textureSize
                                    NewSPDFile.Write(SPDFile.ReadUInt32()); //texture width
                                    NewSPDFile.Write(SPDFile.ReadUInt32()); //texture Height
                                    NewSPDFile.Write(SPDFile.ReadUInt32()); //field18
                                    NewSPDFile.Write(SPDFile.ReadUInt32()); //field1C
                                    for (int j = 0; j < 4; j++)
                                    {
                                        NewSPDFile.Write(SPDFile.ReadInt32()); //texture string
                                    }
                                }
                            }
                            else
                            {
                                NewSPDFile.Write(highestTextureID + 1); //texture ID
                                NewSPDFile.Write(textureParams[1]); //field04
                                NewSPDFile.Write((uint)SPDFile.Length + 48); //textureOffset
                                NewSPDFile.Write((uint)(new FileInfo(texturePath).Length)); //textureSize
                                NewSPDFile.Write(textureParams[4]); //texture width
                                NewSPDFile.Write(textureParams[5]); //texture Height
                                NewSPDFile.Write(textureParams[6]); //field18
                                NewSPDFile.Write(textureParams[7]); //field1C
                                for (int j = 0; j < 4; j++)
                                {
                                    NewSPDFile.Write(textureParams[8 + j]); //texture string
                                }
                                textureParams.Clear();
                            }
                        }

                        for (int i = 0; i < spriteCount; i++)
                        {
                            uint spriteID = SPDFile.ReadUInt32();
                            NewSPDFile.Write(spriteID);

                            if (spriteIDList.Contains($"{spriteID}"))
                            {
                                uint spriteTextureID = SPDFile.ReadUInt32();
                                NewSPDFile.Write(highestTextureID + 1);
                            }
                            else
                            {
                                uint spriteTextureID = SPDFile.ReadUInt32();
                                NewSPDFile.Write(spriteTextureID);
                            }

                            for (int j = 0; j < 38; j++)
                            {
                                NewSPDFile.Write(SPDFile.ReadUInt32());
                            }
                        }

                        long currentOffset = SPDFile.Length - SPDFile.Position;
                        for (int i = 0; i < currentOffset; i++)
                        {
                            NewSPDFile.Write(SPDFile.ReadByte());
                        }
                        Console.WriteLine($"texture path: {texturePath}");
                        using (BinaryObjectReader TextureFile = new(texturePath, Endianness.Little, Encoding.GetEncoding(932)))
                        {
                            for (int i = 0; i < TextureFile.Length; i++)
                            {
                                NewSPDFile.Write(TextureFile.ReadByte());
                            }
                        }
                    }
                }
                using (var stream = File.Open(outputSpd + "_out", FileMode.Open, FileAccess.Write, FileShare.Read))
                {
                    File.Copy(outputSpd + "_out", outputSpd, true);
                }
                File.Delete(outputSpd + "_out");
            }
        }
    }
}