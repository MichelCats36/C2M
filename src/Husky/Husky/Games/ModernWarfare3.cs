﻿// ------------------------------------------------------------------------
// Husky - Call of Duty BSP Extractor
// Copyright (C) 2018 Philip/Scobalula
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.

// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
// ------------------------------------------------------------------------
using PhilLibX;
using PhilLibX.IO;
using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;
using System.Globalization;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Husky
{
    /// <summary>
    /// MW3 Logic
    /// </summary>
    class ModernWarfare3
    {
        /// <summary>
        /// MW3 GfxMap Asset (some pointers we skip over point to DirectX routines, etc. if that means anything to anyone)
        /// </summary>


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct GfxMap
        {
            /// <summary>
            /// A pointer to the name of this GfxMap Asset
            /// </summary>
            public int NamePointer { get; set; }

            /// <summary>
            /// A pointer to the name of the map 
            /// </summary>
            public int MapNamePointer { get; set; }

            /// <summary>
            /// Unknown Bytes (Possibly counts for other data we don't care about)
            /// </summary>
            public fixed byte Padding[8];

            /// <summary>
            /// Number of Surfaces
            /// </summary>
            public int SurfaceCount { get; set; }

            /// <summary>
            /// Unknown Bytes (Possibly counts, pointers, etc. for other data we don't care about)
            /// </summary>
            public fixed byte Padding1[0x70];

            /// <summary>
            /// Number of Gfx Vertices (XYZ, etc.)
            /// </summary>
            public int GfxVertexCount { get; set; }

            /// <summary>
            /// Pointer to the Gfx Vertex Data
            /// </summary>
            public int GfxVerticesPointer { get; set; }

            /// <summary>
            /// Unknown Bytes (more BSP data we probably don't care for)
            /// </summary>
            public fixed byte Padding2[0x10];

            /// <summary>
            /// Number of Gfx Indices (for Faces)
            /// </summary>
            public int GfxIndicesCount { get; set; }

            /// <summary>
            /// Pointer to the Gfx Index Data
            /// </summary>
            public int GfxIndicesPointer { get; set; }

            /// <summary>
            /// Unknown Bytes (more BSP data we probably don't care for)
            /// </summary>
            public fixed byte Padding3[0x130];

            /// <summary>
            /// Number of Static Models
            /// </summary>
            public int GfxStaticModelsCount { get; set; }

            /// <summary>
            /// Unknown Bytes (more BSP data we probably don't care for)
            /// </summary>
            public fixed byte Padding4[0x50];

            /// <summary>
            /// Pointer to the Gfx Index Data
            /// </summary>
            public int GfxSurfacesPointer { get; set; }

            /// <summary>
            /// Null Padding
            /// </summary>
            public int Padding5 { get; set; }

            /// <summary>
            /// Pointer to the Gfx Static Models
            /// </summary>
            public int GfxStaticModelsPointer { get; set; }
        }

        /// <summary>
        /// Call of Duty: Modern Warfare 3 Material Asset
        /// </summary>
        public unsafe struct Material
        {
            /// <summary>
            /// A pointer to the name of this material
            /// </summary>
            public int NamePointer { get; set; }

            /// <summary>
            /// Unknown Bytes (Flags, settings, etc.)
            /// </summary>
            public fixed byte UnknownBytes[0x4A];

            /// <summary>
            /// Number of Images this Material has
            /// </summary>
            public byte ImageCount { get; set; }

            /// <summary>
            /// Unknown Bytes (Flags, settings, etc.)
            /// </summary>
            public fixed byte UnknownBytes1[9];

            /// <summary>
            /// A pointer to this Material's Image table
            /// </summary>
            public int ImageTablePointer { get; set; }
        }

        /// <summary>
        /// Gfx Static Model
        /// </summary>
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct GfxStaticModel
        {
            /// <summary>
            /// X Origin
            /// </summary>
            public float X { get; set; }

            /// <summary>
            /// Y Origin
            /// </summary>
            public float Y { get; set; }

            /// <summary>
            /// Z Origin
            /// </summary>
            public float Z { get; set; }

            /// <summary>
            /// 3x3 Rotation Matrix
            /// </summary>
            public fixed float Matrix[9];

            /// <summary>
            /// Model Scale 
            /// </summary>
            public float ModelScale { get; set; }

            /// <summary>
            /// Pointer to the XModel Asset
            /// </summary>
            public int ModelPointer { get; set; }

            /// <summary>
            /// Unknown Bytes
            /// </summary>
            public fixed byte UnknownBytes2[0x14];
        }

        /// <summary>
        /// Reads BSP Data
        /// </summary>
        public static void ExportBSPData(ProcessReader reader, long assetPoolsAddress, long assetSizesAddress, string gameType, Action<object> printCallback = null)
        {
            // Found her
            printCallback?.Invoke("Found supported game: Call of Duty: Modern Warfare 3");
            // Validate by XModel Name
            if (reader.ReadNullTerminatedString(reader.ReadInt32(reader.ReadInt32(assetPoolsAddress + 0x10) + 4)) == "void")
            {
                // Load BSP Pools (they only have a size of 1 so we have no free header)
                var gfxMapAsset = reader.ReadStruct<GfxMap>(reader.ReadInt32(assetPoolsAddress + 4 * 0x15));
                var mapEntsAsset = reader.ReadStruct<MapEntsMW2>(reader.ReadInt32(assetPoolsAddress + 4 * 0x13));

                // Name
                string gfxMapName = reader.ReadNullTerminatedString(gfxMapAsset.NamePointer);
                string mapName = reader.ReadNullTerminatedString(gfxMapAsset.MapNamePointer);
                string mapEnt = reader.ReadNullTerminatedString(mapEntsAsset.MapData);

                // Verify a BSP is actually loaded (if in base menu, etc, no map is loaded)
                if (String.IsNullOrWhiteSpace(gfxMapName))
                {
                    printCallback?.Invoke("No BSP loaded. Enter Main Menu or a Map to load in the required assets.");
                }
                else
                {
                    // New IW Map
                    var mapFile = new IWMap();
                    // Print Info
                    printCallback?.Invoke(String.Format("Loaded Gfx Map     -   {0}", gfxMapName));
                    printCallback?.Invoke(String.Format("Loaded Map         -   {0}", mapName));
                    printCallback?.Invoke(String.Format("Vertex Count       -   {0}", gfxMapAsset.GfxVertexCount));
                    printCallback?.Invoke(String.Format("Indices Count      -   {0}", gfxMapAsset.GfxIndicesCount));
                    printCallback?.Invoke(String.Format("Surface Count      -   {0}", gfxMapAsset.SurfaceCount));
                    printCallback?.Invoke(String.Format("Model Count        -   {0}", gfxMapAsset.GfxStaticModelsCount));

                    // Build output Folder
                    string outputName = Path.Combine("exported_maps", "modern_warfare_3", gameType, mapName, mapName);
                    Directory.CreateDirectory(Path.GetDirectoryName(outputName));

                    // Stop watch
                    var stopWatch = Stopwatch.StartNew();

                    // Read Vertices
                    printCallback?.Invoke("Parsing vertex data....");
                    var vertices = ReadGfxVertices(reader, gfxMapAsset.GfxVerticesPointer, gfxMapAsset.GfxVertexCount);
                    printCallback?.Invoke(String.Format("Parsed vertex data in {0:0.00} seconds.", stopWatch.ElapsedMilliseconds / 1000.0));

                    // Reset timer
                    stopWatch.Restart();

                    // Read Indices
                    printCallback?.Invoke("Parsing surface indices....");
                    var indices = ReadGfxIndices(reader, gfxMapAsset.GfxIndicesPointer, gfxMapAsset.GfxIndicesCount);
                    printCallback?.Invoke(String.Format("Parsed indices in {0:0.00} seconds.", stopWatch.ElapsedMilliseconds / 1000.0));

                    // Reset timer
                    stopWatch.Restart();

                    // Read Indices
                    printCallback?.Invoke("Parsing surfaces....");
                    var surfaces = ReadGfxSufaces(reader, gfxMapAsset.GfxSurfacesPointer, gfxMapAsset.SurfaceCount);
                    printCallback?.Invoke(String.Format("Parsed surfaces in {0:0.00} seconds.", stopWatch.ElapsedMilliseconds / 1000.0));

                    // Reset timer
                    stopWatch.Restart();

                    // Write OBJ
                    printCallback?.Invoke("Converting to OBJ....");

                    // Create new OBJ
                    var obj = new WavefrontOBJ();

                    // Append Vertex Data
                    foreach (var vertex in vertices)
                    {
                        obj.Vertices.Add(vertex.Position);
                        obj.Normals.Add(vertex.Normal);
                        obj.UVs.Add(vertex.UV);
                    }

                    // Image Names (for Search String)
                    HashSet<string> imageNames = new HashSet<string>();

                    // Append Faces
                    foreach (var surface in surfaces)
                    {
                        // Create new Material
                        var material = ReadMaterial(reader, surface.MaterialPointer);
                        // Add to images
                        imageNames.Add(material.DiffuseMap);
                        //imageNames.Add(material.NormalMap);
                        imageNames.Add(material.SpecularMap);
                        // Add it
                        obj.AddMaterial(material);
                        // Add points
                        for (ushort i = 0; i < surface.FaceCount; i++)
                        {
                            // Face Indices
                            var faceIndex1 = indices[i * 3 + surface.FaceIndex] + surface.VertexIndex;
                            var faceIndex2 = indices[i * 3 + surface.FaceIndex + 1] + surface.VertexIndex;
                            var faceIndex3 = indices[i * 3 + surface.FaceIndex + 2] + surface.VertexIndex;

                            // Validate unique points, and write to OBJ
                            if (faceIndex1 != faceIndex2 && faceIndex1 != faceIndex3 && faceIndex2 != faceIndex3)
                            {
                                // new Obj Face
                                var objFace = new WavefrontOBJ.Face(material.Name);

                                // Add points
                                objFace.Vertices[0] = new WavefrontOBJ.Face.Vertex(faceIndex1, faceIndex1, faceIndex1);
                                objFace.Vertices[2] = new WavefrontOBJ.Face.Vertex(faceIndex2, faceIndex2, faceIndex2);
                                objFace.Vertices[1] = new WavefrontOBJ.Face.Vertex(faceIndex3, faceIndex3, faceIndex3);

                                // Add to OBJ
                                obj.Faces.Add(objFace);
                            }
                        }
                    }

                    // Save it
                    obj.Save(outputName + ".obj");

                    // Build search strinmg
                    string searchString = "";

                    // Loop through images, and append each to the search string (for Wraith/Greyhound)
                    foreach (string imageName in imageNames)
                        searchString += String.Format("{0},", Path.GetFileNameWithoutExtension(imageName));

                    // Create .JSON with XModel Data
                    List<IDictionary> ModelData = CreateXModelDictionary(reader, gfxMapAsset.GfxStaticModelsPointer, (int)gfxMapAsset.GfxStaticModelsCount);
                    string xmodeljson = JToken.FromObject(ModelData).ToString(Formatting.Indented);
                    File.WriteAllText(outputName + "_xmodels.json", xmodeljson);

                    // Loop through xmodels, and append each to the search string (for Wraith/Greyhound)
                    List<string> xmodelList = CreateXModelList(ModelData);

                    // Create .JSON with World settings

                    Dictionary<string, string> world_settings = ParseWorldSettings(mapEnt);
                    string worldsettingsjson = JToken.FromObject(world_settings).ToString(Formatting.Indented);
                    File.WriteAllText(outputName + "_worldsettings.json", worldsettingsjson);


                    // Dump it
                    File.WriteAllText(outputName + "_search_string.txt", searchString);
                    File.WriteAllText(outputName + "_mapEnts.txt", mapEnt);
                    File.WriteAllText(outputName + "_xmodelList.txt", String.Join(",", xmodelList.ToArray()));

                    // Read entities and dump to map
                    mapFile.Entities.AddRange(ReadStaticModels(reader, gfxMapAsset.GfxStaticModelsPointer, (int)gfxMapAsset.GfxStaticModelsCount));
                    mapFile.DumpToMap(outputName + ".map");

                    // Done
                    printCallback?.Invoke(String.Format("Converted to OBJ in {0:0.00} seconds.", stopWatch.ElapsedMilliseconds / 1000.0));
                }

            }
            else
            {
                printCallback?.Invoke("Call of Duty: Modern Warfare 3 is supported, but this EXE is not.");
            }
        }

        /// <summary>
        /// Reads Gfx Surfaces
        /// </summary>
        public static ModernWarfare2.GfxSurface[] ReadGfxSufaces(ProcessReader reader, long address, int count)
        {
            // Preallocate short array
            ModernWarfare2.GfxSurface[] surfaces = new ModernWarfare2.GfxSurface[count];
            // Loop number of indices we have
            for (int i = 0; i < count; i++)
                // Add it
                surfaces[i] = reader.ReadStruct<ModernWarfare2.GfxSurface>(address + i * 24);
            // Done
            return surfaces;
        }


        /// <summary>
        /// Reads Gfx Vertex Indices
        /// </summary>
        public static ushort[] ReadGfxIndices(ProcessReader reader, long address, int count)
        {
            // Preallocate short array
            ushort[] indices = new ushort[count];
            // Read buffer
            var byteBuffer = reader.ReadBytes(address, count * 2);
            // Copy buffer 
            Buffer.BlockCopy(byteBuffer, 0, indices, 0, byteBuffer.Length);
            // Done
            return indices;
        }

        /// <summary>
        /// Reads Gfx Vertices
        /// </summary>
        public static Vertex[] ReadGfxVertices(ProcessReader reader, long address, int count)
        {
            // Preallocate vertex array
            Vertex[] vertices = new Vertex[count];
            // Read buffer
            var byteBuffer = reader.ReadBytes(address, count * 44);
            // Loop number of vertices we have
            for (int i = 0; i < count; i++)
            {
                // Read Struct
                var gfxVertex = ByteUtil.BytesToStruct<GfxVertex>(byteBuffer, i * 44);

                // Create new SEModel Vertex
                vertices[i] = new Vertex()
                {
                    // Set offset
                    Position = new Vector3(
                        gfxVertex.X * 2.54,
                        gfxVertex.Y * 2.54,
                        gfxVertex.Z * 2.54),
                    // Decode and set normal (from DTZxPorter - Wraith, same as XModels)
                    Normal = VertexNormalUnpacking.MethodA(gfxVertex.Normal),
                    // Set UV
                    UV = new Vector2(gfxVertex.U, 1 - gfxVertex.V)
                };
            }

            // Done
            return vertices;
        }

        /// <summary>
        /// Reads a material for the given surface and its associated images
        /// </summary>
        public static WavefrontOBJ.Material ReadMaterial(ProcessReader reader, long address)
        {
            // Read Material
            var material = reader.ReadStruct<Material>(address);
            // Create new OBJ Image
            var objMaterial = new WavefrontOBJ.Material(Path.GetFileNameWithoutExtension(reader.ReadNullTerminatedString(reader.ReadInt32(address)).Replace("*", "")));
            // Loop over images
            for (byte i = 0; i < material.ImageCount; i++)
            {
                // Read Material Image
                var materialImage = reader.ReadStruct<MaterialImage32B>(material.ImageTablePointer + i * Marshal.SizeOf<MaterialImage32B>());
                // Check for color map for now
                if (materialImage.SemanticHash == 0xA0AB1041)
                    objMaterial.DiffuseMap = "_images\\\\" + reader.ReadNullTerminatedString(reader.ReadInt32(materialImage.ImagePointer + 0x1C));
            }
            // Done
            return objMaterial;
        }

        /// <summary>
        /// Reads Static Models
        /// </summary>
        public unsafe static List<IWMap.Entity> ReadStaticModels(ProcessReader reader, long address, int count)
        {
            // Resulting Entities
            List<IWMap.Entity> entities = new List<IWMap.Entity>(count);
            // Read buffer
            var byteBuffer = reader.ReadBytes(address, count * Marshal.SizeOf<GfxStaticModel>());
            // Loop number of models we have
            for (int i = 0; i < count; i++)
            {
                // Read Struct
                var staticModel = ByteUtil.BytesToStruct<GfxStaticModel>(byteBuffer, i * Marshal.SizeOf<GfxStaticModel>());
                // Model Name
                var modelName = reader.ReadNullTerminatedString(reader.ReadInt32(staticModel.ModelPointer));
                // New Matrix
                var matrix = new Rotation.Matrix();
                // Copy X Values
                matrix.Values[0] = staticModel.Matrix[0];
                matrix.Values[1] = staticModel.Matrix[1];
                matrix.Values[2] = staticModel.Matrix[2];
                // Copy Y Values
                matrix.Values[4] = staticModel.Matrix[3];
                matrix.Values[5] = staticModel.Matrix[4];
                matrix.Values[6] = staticModel.Matrix[5];
                // Copy Z Values
                matrix.Values[8] = staticModel.Matrix[6];
                matrix.Values[9] = staticModel.Matrix[7];
                matrix.Values[10] = staticModel.Matrix[8];
                // Convert to Euler
                var euler = matrix.ToEuler();
                // Add it
                if (string.IsNullOrEmpty(modelName) == true || modelName.Contains("?") == true || modelName.Contains("'") == true || modelName.Contains("\\") == true || modelName.Contains("fx") == true || modelName.Contains("viewmodel") == true || staticModel.ModelScale < 0.001 || staticModel.ModelScale > 10)
                {

                }
                else
                {
                    entities.Add(IWMap.Entity.CreateMiscModel(modelName, new Vector3(staticModel.X, staticModel.Y, staticModel.Z), Rotation.ToDegrees(euler), staticModel.ModelScale));
                }
            }
            // Done
            return entities;
        }


        public unsafe static List<IDictionary> CreateXModelDictionary(ProcessReader reader, long address, int count)
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("en-US");
            // Read buffer
            var byteBuffer = reader.ReadBytes(address, count * Marshal.SizeOf<GfxStaticModel>());
            // Loop number of models we have
            List<IDictionary> MapModels = new List<IDictionary>(count);
            for (int i = 0; i < count; i++)
            {
                Dictionary<string, string> ModelData = new Dictionary<string, string>();
                List<double> Position = new List<double>();
                List<double> angles = new List<double>();
                // Read Struct
                var staticModel = ByteUtil.BytesToStruct<GfxStaticModel>(byteBuffer, i * Marshal.SizeOf<GfxStaticModel>());
                // Model Name
                var modelName = reader.ReadNullTerminatedString(reader.ReadInt32(staticModel.ModelPointer));

                var matrix = new Rotation.Matrix();
                // Copy X Values
                matrix.Values[0] = staticModel.Matrix[0];
                matrix.Values[1] = staticModel.Matrix[1];
                matrix.Values[2] = staticModel.Matrix[2];
                // Copy Y Values
                matrix.Values[4] = staticModel.Matrix[3];
                matrix.Values[5] = staticModel.Matrix[4];
                matrix.Values[6] = staticModel.Matrix[5];
                // Copy Z Values
                matrix.Values[8] = staticModel.Matrix[6];
                matrix.Values[9] = staticModel.Matrix[7];
                matrix.Values[10] = staticModel.Matrix[8];
                // Convert to Euler
                var euler = matrix.ToEuler();
                // Add it
                if (string.IsNullOrEmpty(modelName) == true || modelName.Contains("?") == true || modelName.Contains("'") == true || modelName.Contains("\\") == true || modelName.Contains("fx") == true || modelName.Contains("viewmodel") == true || staticModel.ModelScale < 0.001 || staticModel.ModelScale > 10)
                {

                }
                else
                {
                    ModelData.Add("Name", CleanInput(modelName));
                    ModelData.Add("PosX", string.Format("{0:0.0000}", staticModel.X));
                    ModelData.Add("PosY", string.Format("{0:0.0000}", staticModel.Y));
                    ModelData.Add("PosZ", string.Format("{0:0.0000}", staticModel.Z));
                    ModelData.Add("RotX", string.Format("{0:0.0000}", (float)Rotation.ToDegrees(euler).X).ToString(CultureInfo.InvariantCulture));
                    ModelData.Add("RotY", string.Format("{0:0.0000}", (float)Rotation.ToDegrees(euler).Y).ToString(CultureInfo.InvariantCulture));
                    ModelData.Add("RotZ", string.Format("{0:0.0000}", (float)Rotation.ToDegrees(euler).Z).ToString(CultureInfo.InvariantCulture));
                    ModelData.Add("Scale", string.Format("{0:0.0000}", staticModel.ModelScale).ToString(CultureInfo.InvariantCulture));
                    MapModels.Add(new Dictionary<string, string>(ModelData));
                }
            }

            // Done
            return MapModels;
        }

        public unsafe static List<string> CreateXModelList(List<IDictionary> ModelData)
        {
            List<string> xmodel_list = new List<string>();

            foreach (Dictionary<string, string> model_dict in ModelData)
            {
                foreach (KeyValuePair<string, string> kvp in model_dict)
                {
                    if (kvp.Key == "Name" && xmodel_list.Contains(kvp.Value) == false)
                    {
                        xmodel_list.Add(kvp.Value);
                    }
                }
            }

            // Done
            return xmodel_list;
        }

        public unsafe static List<IDictionary> ParseMapEnts(string mapEnts)
        {
            List<string> DynModels = new List<string>();
            string[] Entities = mapEnts.Split(new[] { "\n}\n{" }, StringSplitOptions.None);
            foreach (string i in Entities)
            {
                if (i.Contains("script_model") && i.Contains("\"model\""))
                {
                    if (i.Contains("\"hq\"") == false && i.Contains("\"sab\"") == false && i.Contains("\"ctf\"") == false && i.Contains("\"sd\"") == false && i.Contains("\"special") == false)
                    {
                        DynModels.Add(i);
                    }
                }
            }

            List<IDictionary> ParsedList = new List<IDictionary>();
            Regex reg = new Regex(@"""(.*?)""\s""(.*?)""");

            foreach (string entity in DynModels)
            {
                string[] entity_properties = entity.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                Dictionary<string, string> model_data = new Dictionary<string, string>();
                foreach (String line in entity_properties)
                {
                    MatchCollection matches = reg.Matches(line);
                    foreach (Match m in matches)
                    {
                        if (m.Groups[1].Value == "model")
                        {
                            model_data.Add("Name", m.Groups[2].Value);
                        }
                        else if (m.Groups[1].Value == "origin")
                        {
                            string[] vec3 = m.Groups[2].Value.Split(new[] { " " }, StringSplitOptions.None);
                            model_data.Add("PosX", vec3[0]);
                            model_data.Add("PosY", vec3[1]);
                            model_data.Add("PosZ", vec3[2]);
                        }
                        else if (m.Groups[1].Value == "angles")
                        {
                            string[] vec3 = m.Groups[2].Value.Split(new[] { " " }, StringSplitOptions.None);
                            model_data.Add("RotX", vec3[2]);
                            model_data.Add("RotY", vec3[0]);
                            model_data.Add("RotZ", vec3[1]);
                        }
                    }
                }
                model_data.Add("Scale", "1.0000");
                ParsedList.Add(model_data);
            }

            return ParsedList;
        }

        public unsafe static Dictionary<string, string> ParseWorldSettings(string mapEnts)
        {
            Regex reg = new Regex(@"""(.*?)""\s""(.*?)""");
            string world = mapEnts.Split(new[] { "\n}\n{" }, StringSplitOptions.None)[0];
            string[] world_settings = world.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            Dictionary<string, string> world_data = new Dictionary<string, string>();
            foreach (String line in world_settings)
            {
                MatchCollection matches = reg.Matches(line);
                foreach (Match m in matches)
                {
                    if (world_data.ContainsKey(m.Groups[1].Value) == false)
                    {
                        world_data.Add(m.Groups[1].Value, m.Groups[2].Value);
                    }
                }
            }
            return world_data;
        }

        public unsafe static string CleanInput(string strIn)
        {
            // Replace invalid characters with empty strings.
            try
            {
                return Regex.Replace(strIn, @"[^\w\.@-]", "",
                                     RegexOptions.None, TimeSpan.FromSeconds(1.5));
            }
            // If we timeout when replacing invalid characters, 
            // we should return Empty.
            catch (RegexMatchTimeoutException)
            {
                return String.Empty;
            }
        }
    }
}