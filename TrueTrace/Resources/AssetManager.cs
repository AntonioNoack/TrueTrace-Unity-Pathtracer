using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CommonVars;
using System.Threading.Tasks;
using System.Threading;
using RectpackSharp;
using System.IO;
using System.Xml.Serialization;
using System.Linq;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
#pragma warning disable 4014

namespace TrueTrace {
    [System.Serializable]
    public class AssetManager : MonoBehaviour
    {//This handles all the data
        public int TotalParentObjectSize;
        public float LightEnergyScale = 1.0f;
        //emissive, alpha, metallic, roughness
        [System.NonSerialized] public Texture2D AlbedoAtlas;
        [System.NonSerialized] public RenderTexture NormalAtlas;
        [System.NonSerialized] public RenderTexture EmissiveAtlas;
        [System.NonSerialized] public RenderTexture AlphaAtlas;
        [System.NonSerialized] public RenderTexture MetallicAtlas;
        [System.NonSerialized] public RenderTexture RoughnessAtlas;
        private RenderTexture TempTex;
        private RenderTexture s_Prop_EncodeBCn_Temp;
        private ComputeShader CopyShader;
        private ComputeShader Refitter;
        private int RefitLayer;
        private int NodeUpdater;
        private int NodeCompress;
        private int NodeInitializerKernel;

        [HideInInspector] public int AlbedoAtlasSize;

        [HideInInspector] public VideoObject VideoPlayerObject;
        [HideInInspector] public RenderTexture VideoTexture;

        [HideInInspector] public List<RayTracingObject> MaterialsChanged;
        [HideInInspector] public List<MaterialData> _Materials;
        [HideInInspector] public ComputeBuffer BVH8AggregatedBuffer;
        [HideInInspector] public ComputeBuffer AggTriBuffer;
        [HideInInspector] public ComputeBuffer LightTriBuffer;
        [HideInInspector] public List<MyMeshDataCompacted> MyMeshesCompacted;
        [HideInInspector] public List<LightData> UnityLights;
        [HideInInspector] public InstancedManager InstanceData;
        [HideInInspector] public List<InstancedObject> Instances;
        [HideInInspector] public ComputeBuffer TLASCWBVHIndexes;

        [HideInInspector] public List<TerrainObject> Terrains;
        [HideInInspector] public List<TerrainDat> TerrainInfos;
        [HideInInspector] public ComputeBuffer TerrainBuffer;
        [HideInInspector] public Texture2D HeightMap;
        [HideInInspector] public Texture2D AlphaMap;
        [HideInInspector] public bool DoHeightmap;

        private ComputeShader MeshFunctions;
        private int TriangleBufferKernel;
        private int NodeBufferKernel;
        private int LightBufferKernel;

        [HideInInspector] public List<ParentObject> RenderQueue;
        [HideInInspector] public List<Transform> RenderTransforms;
        [HideInInspector] public List<ParentObject> BuildQueue;
        [HideInInspector] public List<ParentObject> AddQueue;
        [HideInInspector] public List<ParentObject> RemoveQueue;
        [HideInInspector] public List<ParentObject> UpdateQueue;

        [HideInInspector] public List<InstancedObject> InstanceRenderQueue;
        [HideInInspector] public List<InstancedObject> InstanceUpdateQueue;
        [HideInInspector] public List<Transform> InstanceRenderTransforms;
        [HideInInspector] public List<InstancedObject> InstanceBuildQueue;
        [HideInInspector] public List<InstancedObject> InstanceAddQueue;
        [HideInInspector] public List<InstancedObject> InstanceRemoveQueue;

        private bool OnlyInstanceUpdated;
        [HideInInspector] public List<Transform> LightTransforms;
        [HideInInspector] public int DesiredRes = 16300;

        [HideInInspector] public int MatCount;

        [HideInInspector] public int NormalSize;
        [HideInInspector] public int EmissiveSize;

        [HideInInspector] public List<LightMeshData> LightMeshes;

        [HideInInspector] public AABB[] MeshAABBs;

        [HideInInspector] public bool ParentCountHasChanged;

        [HideInInspector] public int LightMeshCount;
        [HideInInspector] public int UnityLightCount;

        private int PrevLightCount;
        [HideInInspector] public BVH2Builder BVH2;

        [HideInInspector] public bool UseSkinning = true;
        [HideInInspector] public bool HasStart = false;
        [HideInInspector] public bool didstart = false;
        [HideInInspector] public bool ChildrenUpdated;

        [HideInInspector] public Vector3 SunDirection;

        private BVHNode8DataCompressed[] TempBVHArray;

        [SerializeField] public int RunningTasks;
        #if HardwareRT
            public List<Vector2> MeshOffsets;
            public List<int> SubMeshOffsets;
            public UnityEngine.Rendering.RayTracingAccelerationStructure AccelStruct;
        #endif
        public void ClearAll()
        {//My attempt at clearing memory
            RunningTasks = 0;
            ParentObject[] ChildrenObjects = this.GetComponentsInChildren<ParentObject>();
            foreach (ParentObject obj in ChildrenObjects)
                obj.ClearAll();
            CommonFunctions.DeepClean(ref _Materials);
            CommonFunctions.DeepClean(ref LightTransforms);
            CommonFunctions.DeepClean(ref LightMeshes);
            CommonFunctions.DeepClean(ref MyMeshesCompacted);
            CommonFunctions.DeepClean(ref UnityLights);

            DestroyImmediate(AlbedoAtlas);
            DestroyImmediate(NormalAtlas);
            DestroyImmediate(EmissiveAtlas);
            DestroyImmediate(AlphaAtlas);
            DestroyImmediate(MetallicAtlas);
            DestroyImmediate(RoughnessAtlas);
            if (BVH8AggregatedBuffer != null)
            {
                LightTriBuffer.Release();
                LightTriBuffer = null;
                BVH8AggregatedBuffer.Release();
                BVH8AggregatedBuffer = null;
                AggTriBuffer.Release();
                AggTriBuffer = null;
            }
            if(TLASCWBVHIndexes != null) TLASCWBVHIndexes.Release();

            Resources.UnloadUnusedAssets();
            System.GC.Collect();
        }

        private List<Texture2D> AlbedoTexs;
        private List<RayObjects> AlbedoIndexes;
        private List<Texture2D> NormalTexs;
        private List<RayObjects> NormalIndexes;
        private List<Texture2D> MetallicTexs;
        private List<RayObjects> MetallicIndexes;
        private List<int> MetallicTexChannelIndex;
        private List<Texture2D> RoughnessTexs;
        private List<RayObjects> RoughnessIndexes;
        private List<int> RoughnessTexChannelIndex;
        private List<Texture2D> EmissiveTexs;
        private List<RayObjects> EmissiveIndexes;
        public static AssetManager Assets;

        private void AddTextures(ref List<Texture2D> Texs, ref List<RayObjects> Indexes, ref List<RayObjects> ObjIndexes, ref List<Texture> ObjTexs, ref List<int> ReadIndex, List<int> ObjReadIndex = null) {
            int NewLength = ObjTexs.Count;
            int PrevLength = Texs.Count;
            for (int i = 0; i < NewLength; i++) {
                int Index = Texs.IndexOf((Texture2D)ObjTexs[i], 0, PrevLength);
                if (Index == -1) {
                    Texs.Add((Texture2D)ObjTexs[i]);
                    if(ObjReadIndex != null) ReadIndex.Add(ObjReadIndex[i]);
                    var E = new RayObjects();
                    E.RayObjectList = new List<RayObjectTextureIndex>(ObjIndexes[i].RayObjectList);
                    Indexes.Add(E);
                } else {
                    Indexes[Index].RayObjectList.AddRange(ObjIndexes[i].RayObjectList);
                }
            }
        }

        private void ModifyTextureBounds(ref Rect[] Rects, int TexLength, ref List<RayObjects> Indexes, int TargetTex) {
            int TerrainIndexOffset = 0;
            for (int i = 0; i < TexLength; i++) {
                int SecondaryLength = Indexes[i].RayObjectList.Count;
                for (int i2 = 0; i2 < SecondaryLength; i2++) {
                    MaterialData TempMat = Indexes[i].RayObjectList[i2].Obj == null ?
                    _Materials[Indexes[i].RayObjectList[i2].Terrain.MaterialIndex[Indexes[i].RayObjectList[i2].ObjIndex] + MatCount + TerrainIndexOffset]
                    : _Materials[Indexes[i].RayObjectList[i2].Obj.MaterialIndex[Indexes[i].RayObjectList[i2].ObjIndex]];
                    switch (TargetTex) {
                        case 0: TempMat.AlbedoTex = new Vector4(Rects[i].xMax, Rects[i].yMax, Rects[i].xMin, Rects[i].yMin); break;
                        case 1: TempMat.NormalTex = new Vector4(Rects[i].xMax, Rects[i].yMax, Rects[i].xMin, Rects[i].yMin); break;
                        case 2: TempMat.MetallicTex = new Vector4(Rects[i].xMax, Rects[i].yMax, Rects[i].xMin, Rects[i].yMin); break;
                        case 3: TempMat.RoughnessTex = new Vector4(Rects[i].xMax, Rects[i].yMax, Rects[i].xMin, Rects[i].yMin); break;
                        case 4: TempMat.EmissiveTex = new Vector4(Rects[i].xMax, Rects[i].yMax, Rects[i].xMin, Rects[i].yMin); break;
                        default: Debug.Log("Materials Broke"); break;
                    }
                    _Materials[Indexes[i].RayObjectList[i2].Obj == null ? (Indexes[i].RayObjectList[i2].Terrain.MaterialIndex[Indexes[i].RayObjectList[i2].ObjIndex] + MatCount + TerrainIndexOffset) : Indexes[i].RayObjectList[i2].Obj.MaterialIndex[Indexes[i].RayObjectList[i2].ObjIndex]] = TempMat;
                }
            }
        }

        private void ConstructAtlas(List<Texture2D> Texs, ref RenderTexture Atlas, out Rect[] Rects, int DesiredRes, bool IsNormalMap, bool IsAlbedo = false, int ReadIndex = -1, List<int> TexChannelIndex = null) {
            if(Texs.Count == 0) {
                Rects = new Rect[0];
                if(TexChannelIndex != null || IsNormalMap) {
                    if(IsNormalMap) CreateRenderTexture(ref Atlas, 1, 1, RenderTextureFormat.RG16, true);
                    else CreateRenderTexture(ref Atlas, 1, 1, RenderTextureFormat.R8, true);
                } else CreateRenderTexture(ref Atlas, IsAlbedo ? DesiredRes : 1, IsAlbedo ? DesiredRes : 1, RenderTextureFormat.ARGB32, false);
                return;
            }
            PackingRectangle[] rectangles = new PackingRectangle[Texs.Count];
            for (int i = 0; i < Texs.Count; i++) {
                rectangles[i].Width = (uint)Texs[i].width + 1;
                rectangles[i].Height = (uint)Texs[i].height + 1;
                rectangles[i].Id = i;
            }
            PackingRectangle BoundRects;
            RectanglePacker.Pack(rectangles, out BoundRects);
            if(TexChannelIndex != null || IsNormalMap) {
                if(IsNormalMap) {
                    DesiredRes = (int)Mathf.Min(Mathf.Max(BoundRects.Width, BoundRects.Height), DesiredRes);
                    CreateRenderTexture(ref Atlas, DesiredRes, DesiredRes, RenderTextureFormat.RG16, true);
                } else CreateRenderTexture(ref Atlas, DesiredRes, DesiredRes, RenderTextureFormat.R8, true);
            } else {
                if(ReadIndex == -1) {
                    // DesiredRes = (int)Mathf.Min(Mathf.Max(BoundRects.Width, BoundRects.Height), DesiredRes);
                    CreateRenderTexture(ref Atlas, DesiredRes, DesiredRes, RenderTextureFormat.ARGB32, false);
                } else CreateRenderTexture(ref Atlas, DesiredRes, DesiredRes, RenderTextureFormat.R8, true);
            }

            Rects = new Rect[Texs.Count];
            for (int i = 0; i < Texs.Count; i++) {
                Rects[rectangles[i].Id].width = rectangles[i].Width;
                Rects[rectangles[i].Id].height = rectangles[i].Height;
                Rects[rectangles[i].Id].x = rectangles[i].X;
                Rects[rectangles[i].Id].y = rectangles[i].Y;
            }

            Vector2 Scale = new Vector2(Mathf.Min((float)DesiredRes / BoundRects.Width, 1), Mathf.Min((float)DesiredRes / BoundRects.Height, 1));
            CopyShader.SetBool("IsNormalMap", IsNormalMap);
            for (int i = 0; i < Texs.Count; i++) {
                CopyShader.SetVector("InputSize", new Vector2(Rects[i].width, Rects[i].height));
                CopyShader.SetVector("OutputSize", new Vector2(Atlas.width, Atlas.height));
                CopyShader.SetVector("Scale", Scale);
                CopyShader.SetVector("Offset", new Vector2(Rects[i].x, Rects[i].y));

                if(ReadIndex != -1) {
                    CopyShader.SetInt("OutputRead", ((TexChannelIndex == null) ? ReadIndex : TexChannelIndex[i]));
                    if(!IsNormalMap) {
                        CopyShader.SetTexture(2, "AdditionTex", Texs[i]);
                        CopyShader.SetTexture(2, "ResultSingle", Atlas);
                        CopyShader.Dispatch(2, (int)Mathf.CeilToInt(Rects[i].width * Scale.x / 32.0f), (int)Mathf.CeilToInt(Rects[i].height * Scale.y / 32.0f), 1);
                    }
                } else {
                    if(IsNormalMap) {
                        CopyShader.SetTexture(3, "AdditionTex", Texs[i]);
                        CopyShader.SetTexture(3, "ResultDouble", Atlas);
                        CopyShader.Dispatch(3, (int)Mathf.CeilToInt(Rects[i].width * Scale.x / 32.0f), (int)Mathf.CeilToInt(Rects[i].height * Scale.y / 32.0f), 1);
                    } else {
                        CopyShader.SetTexture(0, "AdditionTex", Texs[i]);
                        CopyShader.SetTexture(0, "Result", Atlas);
                        CopyShader.Dispatch(0, (int)Mathf.CeilToInt(Rects[i].width * Scale.x / 32.0f), (int)Mathf.CeilToInt(Rects[i].height * Scale.y / 32.0f), 1);
                    }
                }

                Rects[i].width = Mathf.Floor((Rects[i].width - 1) * Scale.x) / Atlas.width;
                Rects[i].x = Mathf.Floor(Rects[i].x * Scale.x) / Atlas.width;
                Rects[i].height = Mathf.Floor((Rects[i].height - 1) * Scale.y) / Atlas.height;
                Rects[i].y = Mathf.Floor(Rects[i].y * Scale.y) / Atlas.height;
            }
        }

        private void CreateRenderTexture(ref RenderTexture ThisTex, int Width, int Height, RenderTextureFormat Form, bool UseMip) {
            ThisTex = new RenderTexture(Width, Height, 0, Form, RenderTextureReadWrite.sRGB);
            ThisTex.enableRandomWrite = true;
            if(UseMip) {
                ThisTex.useMipMap = true;
                ThisTex.autoGenerateMips = false;
            } else ThisTex.useMipMap = false;
            ThisTex.Create();
        }

        private void CreateAtlas() {//Creates texture atlas
            _Materials = new List<MaterialData>();
            AlbedoIndexes = new List<RayObjects>();
            AlbedoTexs = new List<Texture2D>();
            NormalTexs = new List<Texture2D>();
            NormalIndexes = new List<RayObjects>();
            MetallicTexs = new List<Texture2D>();
            MetallicIndexes = new List<RayObjects>();
            MetallicTexChannelIndex = new List<int>();
            RoughnessTexs = new List<Texture2D>();
            RoughnessIndexes = new List<RayObjects>();
            RoughnessTexChannelIndex = new List<int>();
            EmissiveTexs = new List<Texture2D>();
            EmissiveIndexes = new List<RayObjects>();
            List<Texture2D> HeightMaps = new List<Texture2D>();
            List<Texture2D> AlphaMaps = new List<Texture2D>();

            int TerrainCount = Terrains.Count;
            int MaterialOffset = 0;
            List<Vector2> Sizes = new List<Vector2>();
            TerrainInfos = new List<TerrainDat>();
            DoHeightmap = false;
            for (int i = 0; i < TerrainCount; i++) {
                TerrainDat TempTerrain = new TerrainDat();
                TempTerrain.PositionOffset = Terrains[i].transform.position;
                AlphaMaps.Add(Terrains[i].AlphaMap);
                HeightMaps.Add(Terrains[i].HeightMap);
                Sizes.Add(new Vector2(Terrains[i].HeightMap.width, Terrains[i].HeightMap.height));
                TempTerrain.TerrainDim = Terrains[i].TerrainDim;
                TempTerrain.HeightScale = Terrains[i].HeightScale;
                TempTerrain.MatOffset = MaterialOffset;
                MaterialOffset += Terrains[i].Materials.Count;
                TerrainInfos.Add(TempTerrain);
            }
            if (TerrainCount != 0) {
                DoHeightmap = true;
                Rect[] AlphaRects;
                List<Rect> HeightRects = new List<Rect>();
                float MinX = 0;
                float MinY = 0;
                for (int i = 0; i < Sizes.Count; i++) {
                    MinX = Mathf.Max(Sizes[i].x, MinX);
                    MinY += Sizes[i].y * 2;
                }
                int Size = (int)Mathf.Min((MinY) / Mathf.Ceil(Mathf.Sqrt(Sizes.Count)), 16300);

                Texture2D.GenerateAtlas(Sizes.ToArray(), 0, Size, HeightRects);
                HeightMap = new Texture2D(Size, Size, UnityEngine.Experimental.Rendering.GraphicsFormat.R16_UNorm, UnityEngine.Experimental.Rendering.TextureCreationFlags.None);
                Color32[] Colors = HeightMap.GetPixels32(0);
                System.Array.Fill(Colors, Color.black);
                HeightMap.SetPixels32(Colors);
                HeightMap.Apply();
                AlphaRects = AlphaMap.PackTextures(AlphaMaps.ToArray(), 1, 16300, true);
                for (int i = 0; i < TerrainCount; i++) {
                    Graphics.CopyTexture(HeightMaps[i], 0, 0, 0, 0, HeightMaps[i].width, HeightMaps[i].height, HeightMap, 0, 0, (int)HeightRects[i].xMin, (int)HeightRects[i].yMin);
                    TerrainDat TempTerrain = TerrainInfos[i];
                    TempTerrain.HeightMap = new Vector4(HeightRects[i].xMax / Size, HeightRects[i].yMax / Size, HeightRects[i].xMin / Size, HeightRects[i].yMin / Size);
                    TempTerrain.AlphaMap = new Vector4(AlphaRects[i].xMax, AlphaRects[i].yMax, AlphaRects[i].xMin, AlphaRects[i].yMin);
                    TerrainInfos[i] = TempTerrain;
                }
            }
            if(RenderQueue.Count == 0) return;
            int CurCount = RenderQueue[0].AlbedoTexs.Count;
            foreach (ParentObject Obj in RenderQueue) {
                AddTextures(ref AlbedoTexs, ref AlbedoIndexes, ref Obj.AlbedoIndexes, ref Obj.AlbedoTexs, ref MetallicTexChannelIndex);
                AddTextures(ref NormalTexs, ref NormalIndexes, ref Obj.NormalIndexes, ref Obj.NormalTexs, ref MetallicTexChannelIndex);
                AddTextures(ref MetallicTexs, ref MetallicIndexes, ref Obj.MetallicIndexes, ref Obj.MetallicTexs, ref MetallicTexChannelIndex, Obj.MetallicTexChannelIndex);
                AddTextures(ref RoughnessTexs, ref RoughnessIndexes, ref Obj.RoughnessIndexes, ref Obj.RoughnessTexs, ref RoughnessTexChannelIndex, Obj.RoughnessTexChannelIndex);
                AddTextures(ref EmissiveTexs, ref EmissiveIndexes, ref Obj.EmissionIndexes, ref Obj.EmissionTexs, ref MetallicTexChannelIndex);
            }
            foreach (ParentObject Obj in InstanceData.RenderQueue) {
                AddTextures(ref AlbedoTexs, ref AlbedoIndexes, ref Obj.AlbedoIndexes, ref Obj.AlbedoTexs, ref MetallicTexChannelIndex);
                AddTextures(ref NormalTexs, ref NormalIndexes, ref Obj.NormalIndexes, ref Obj.NormalTexs, ref MetallicTexChannelIndex);
                AddTextures(ref MetallicTexs, ref MetallicIndexes, ref Obj.MetallicIndexes, ref Obj.MetallicTexs, ref MetallicTexChannelIndex, Obj.MetallicTexChannelIndex);
                AddTextures(ref RoughnessTexs, ref RoughnessIndexes, ref Obj.RoughnessIndexes, ref Obj.RoughnessTexs, ref RoughnessTexChannelIndex, Obj.RoughnessTexChannelIndex);
                AddTextures(ref EmissiveTexs, ref EmissiveIndexes, ref Obj.EmissionIndexes, ref Obj.EmissionTexs, ref MetallicTexChannelIndex);
            }
            if (TerrainCount != 0) {
                for (int i = 0; i < TerrainCount; i++) {
                    AddTextures(ref AlbedoTexs, ref AlbedoIndexes, ref Terrains[i].AlbedoIndexes, ref Terrains[i].AlbedoTexs, ref MetallicTexChannelIndex);
                    AddTextures(ref NormalTexs, ref NormalIndexes, ref Terrains[i].NormalIndexes, ref Terrains[i].NormalTexs, ref MetallicTexChannelIndex);
                    AddTextures(ref MetallicTexs, ref MetallicIndexes, ref Terrains[i].MetallicIndexes, ref Terrains[i].MetallicTexs, ref MetallicTexChannelIndex);
                }
            }

            if (!RenderQueue.Any())
                return;

            Rect[] AlbedoRects, NormalRects, EmissiveRects, MetallicRects, RoughnessRects;
            if (CopyShader == null) CopyShader = Resources.Load<ComputeShader>("Utility/CopyTextureShader");
            if(NormalAtlas != null) NormalAtlas?.Release();
            if(AlphaAtlas != null) AlphaAtlas?.Release();
            if(RoughnessAtlas != null) RoughnessAtlas?.Release();
            if(MetallicAtlas != null) MetallicAtlas?.Release();
            if(EmissiveAtlas != null) EmissiveAtlas?.Release();
            ConstructAtlas(AlbedoTexs, ref TempTex, out AlbedoRects, DesiredRes, false, true);
            int tempWidth = (TempTex.width + 3) / 4;
            int tempHeight = (TempTex.height + 3) / 4;
            var desc = new RenderTextureDescriptor
            {
                width = tempWidth,
                height = tempHeight,
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                msaaSamples = 1,
                volumeDepth = 1
            };
            desc.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SInt;

            s_Prop_EncodeBCn_Temp = new RenderTexture(desc);
            ConstructAtlas(AlbedoTexs, ref AlphaAtlas, out AlbedoRects, AlbedoAtlas.width, false, false, 3, null);
            CopyShader.SetTexture(4, "_Source", TempTex);
            CopyShader.SetTexture(4, "Alpha", AlphaAtlas);
            CopyShader.SetTexture(4, "_Target", s_Prop_EncodeBCn_Temp);
            CopyShader.Dispatch(4, (int)((tempWidth + 8 - 1) / 8), (int)((tempHeight + 8 - 1) / 8),1);
            Graphics.CopyTexture(s_Prop_EncodeBCn_Temp, 0, AlbedoAtlas, 0);
            TempTex.Release();
            s_Prop_EncodeBCn_Temp.Release();
            AlphaAtlas.Release();
            ConstructAtlas(NormalTexs, ref NormalAtlas, out NormalRects, DesiredRes, true, false);
            ConstructAtlas(EmissiveTexs, ref EmissiveAtlas, out EmissiveRects, DesiredRes, false, false);
            ConstructAtlas(MetallicTexs, ref MetallicAtlas, out MetallicRects, AlbedoAtlas.width, false, false, 0, MetallicTexChannelIndex);
            ConstructAtlas(RoughnessTexs, ref RoughnessAtlas, out RoughnessRects, AlbedoAtlas.width, false, false, 0, RoughnessTexChannelIndex);
            AlbedoAtlasSize = AlbedoAtlas.width;
            NormalAtlas.anisoLevel = 3;
            NormalAtlas.GenerateMips();
            MetallicAtlas.GenerateMips();
            RoughnessAtlas.GenerateMips();
            MatCount = 0;

            foreach (ParentObject Obj in RenderQueue) {
                foreach (RayTracingObject Obj2 in Obj.ChildObjects) {
                    MaterialsChanged.Add(Obj2);
                    for (int i = 0; i < Obj2.MaterialIndex.Length; i++) Obj2.MaterialIndex[i] = Obj2.LocalMaterialIndex[i] + MatCount;
                }
                _Materials.AddRange(Obj._Materials);
                MatCount += Obj._Materials.Count;
            }
            foreach (ParentObject Obj in InstanceData.RenderQueue) {
                foreach (RayTracingObject Obj2 in Obj.ChildObjects) {
                    MaterialsChanged.Add(Obj2);
                    for (int i = 0; i < Obj2.MaterialIndex.Length; i++) Obj2.MaterialIndex[i] = Obj2.LocalMaterialIndex[i] + MatCount;
                }
                _Materials.AddRange(Obj._Materials);
                MatCount += Obj._Materials.Count;
            }
            int TerrainMaterials = 0;
            if (TerrainCount != 0) {
                foreach (TerrainObject Obj2 in Terrains) {
                    for (int i = 0; i < Obj2.MaterialIndex.Length; i++) Obj2.MaterialIndex[i] += TerrainMaterials;
                    _Materials.AddRange(Obj2.Materials);
                    TerrainMaterials += Obj2.Materials.Count;
                }
            }

            ModifyTextureBounds(ref AlbedoRects, AlbedoTexs.Count, ref AlbedoIndexes, 0);
            ModifyTextureBounds(ref NormalRects, NormalTexs.Count, ref NormalIndexes, 1);
            ModifyTextureBounds(ref MetallicRects, MetallicTexs.Count, ref MetallicIndexes, 2);
            ModifyTextureBounds(ref RoughnessRects, RoughnessTexs.Count, ref RoughnessIndexes, 3);
            ModifyTextureBounds(ref EmissiveRects, EmissiveTexs.Count, ref EmissiveIndexes, 4);

            CommonFunctions.DeepClean(ref AlbedoTexs);
            CommonFunctions.DeepClean(ref NormalTexs);
            CommonFunctions.DeepClean(ref MetallicTexs);
            CommonFunctions.DeepClean(ref RoughnessTexs);
            CommonFunctions.DeepClean(ref EmissiveTexs);

            if (TerrainCount != 0) {
                if (TerrainBuffer != null) TerrainBuffer.Release();
                TerrainBuffer = new ComputeBuffer(TerrainCount, 56);
                TerrainBuffer.SetData(TerrainInfos);
            }
            NormalSize = NormalAtlas.width;
            EmissiveSize = EmissiveAtlas.width;

        }
        public void Start() {
            Assets = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if(this != null) gameObject.GetComponent<RayTracingMaster>().Start2();
        }

        public void ForceUpdateAtlas() {
            for(int i = 0; i < RenderQueue.Count; i++) RenderQueue[i].CreateAtlas();
            CreateAtlas();
        }

        public void OnApplicationQuit() {
            RunningTasks = 0;
            #if HardwareRT
                AccelStruct.Release();
            #endif
            if (BVH8AggregatedBuffer != null)
            {
                BVH8AggregatedBuffer.Release();
                BVH8AggregatedBuffer = null;
                LightTriBuffer.Release();
                LightTriBuffer = null;
                AggTriBuffer.Release();
                AggTriBuffer = null;
            }
            if(WorkingBuffer != null) for(int i = 0; i < WorkingBuffer.Length; i++) WorkingBuffer[i]?.Release();
            if (NodeBuffer != null) NodeBuffer.Release();
            if (StackBuffer != null) StackBuffer.Release();
            if (ToBVHIndexBuffer != null) ToBVHIndexBuffer.Release();
            if (BVHDataBuffer != null) BVHDataBuffer.Release();
            if (BVHBuffer != null) BVHBuffer.Release();
            if (BoxesBuffer != null) BoxesBuffer.Release();
            if (TerrainBuffer != null) TerrainBuffer.Release();
            if(TLASCWBVHIndexes != null) TLASCWBVHIndexes.Release();
            if(TerrainBuffer != null) TerrainBuffer.Release();
        }

        void OnDisable() {
            ClearAll();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }


        private TextAsset XMLObject;
        [HideInInspector] public static List<string> ShaderNames;
        [HideInInspector] public static Materials data = new Materials();
        [HideInInspector] public bool NeedsToUpdateXML;
        public void AddMaterial(Shader shader) {
            int ShaderPropertyCount = shader.GetPropertyCount();
            string NormalTex, EmissionTex, MetallicTex, RoughnessTex, MetallicRange, RoughnessRange, MetallicRemapMin, MetallicRemapMax, RoughnessRemapMin, RoughnessRemapMax;
            NormalTex = EmissionTex = MetallicTex = RoughnessTex = MetallicRange = RoughnessRange = MetallicRemapMin = MetallicRemapMax = RoughnessRemapMin = RoughnessRemapMax = "null";
            int MetallicIndex, RoughnessIndex;
            MetallicIndex = RoughnessIndex = 0;
            data.Material.Add(new MaterialShader() {
                Name = shader.name,
                BaseColorTex = "_MainTex",
                NormalTex = NormalTex,
                EmissionTex = EmissionTex,
                MetallicTex = MetallicTex,
                MetallicTexChannel = MetallicIndex,
                MetallicRange = MetallicRange,
                RoughnessTex = RoughnessTex,
                RoughnessTexChannel = RoughnessIndex,
                RoughnessRange = RoughnessRange,
                IsGlass = false,
                IsCutout = false,
                UsesSmoothness = false,
                BaseColorValue = "null",
                MetallicRemapMin = MetallicRemapMin,
                MetallicRemapMax = MetallicRemapMax,
                RoughnessRemapMin = RoughnessRemapMin,
                RoughnessRemapMax = RoughnessRemapMax
            });
            ShaderNames.Add(shader.name);
            NeedsToUpdateXML = true;
        }
        public void UpdateMaterialDefinition() {
            XMLObject = Resources.Load<TextAsset>("Utility/MaterialMappings");
            #if UNITY_EDITOR
                if(XMLObject == null) {
                    // XMLObject = new TextAsset();
                    // var g = UnityEditor.AssetDatabase.FindAssets ( $"t:Script {nameof(AssetManager)}" );
                    // var Path = UnityEditor.AssetDatabase.GUIDToAssetPath ( g [ 0 ] );
                    // Path = Path.Replace("AssetManager.cs", "Utility/MaterialMappings.xml").Replace("Assets", Application.dataPath);
                    // using(var A = File.OpenWrite(Path)) {
                    //     byte[] info = new System.Text.UTF8Encoding(true).GetBytes("<?xml version=\"1.0\"?>\n");
                    //     A.Write(info, 0, info.Length);
                    //     info = new System.Text.UTF8Encoding(true).GetBytes("<Materials xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">\n");
                    //     A.Write(info, 0, info.Length);
                    //     info = new System.Text.UTF8Encoding(true).GetBytes("</Materials>\n");
                    //     A.Write(info, 0, info.Length);
                    // }
                    // UnityEditor.AssetDatabase.Refresh();
                    // XMLObject = Resources.Load<TextAsset>("Utility/MaterialMappings");
                    Debug.Log("Missing XML Object");
                    return;
                }
            #endif
            ShaderNames = new List<string>();
            using (var A = new StringReader(XMLObject.text)) {
                var serializer = new XmlSerializer(typeof(Materials));
                data = serializer.Deserialize(A) as Materials;
            }
            foreach (var Mat in data.Material) {
                ShaderNames.Add(Mat.Name);
            }
        }

        private void init() {
            #if HardwareRT
                AccelStruct = new UnityEngine.Rendering.RayTracingAccelerationStructure();
            #endif
            if(AlbedoAtlas == null || AlbedoAtlas.width != DesiredRes) AlbedoAtlas = new Texture2D(DesiredRes,DesiredRes, TextureFormat.DXT5, false);
            // AlbedoAtlas.Apply(false, true);
            UpdateMaterialDefinition();
            UnityEngine.Video.VideoPlayer[] VideoObjects = GameObject.FindObjectsOfType<UnityEngine.Video.VideoPlayer>();
            if (VideoTexture != null) VideoTexture.Release();
            if (VideoObjects.Length == 0) CreateRenderTexture(ref VideoTexture, 1, 1, RenderTextureFormat.ARGB32, false);
            else {
                GameObject VideoAttatchedObject = VideoObjects[0].gameObject;
                VideoPlayerObject = (VideoAttatchedObject.GetComponent<VideoObject>() == null) ? VideoAttatchedObject.AddComponent<VideoObject>() : VideoAttatchedObject.GetComponent<VideoObject>();
                VideoTexture = VideoPlayerObject.VideoTexture;
            }
            Refitter = Resources.Load<ComputeShader>("Utility/BVHRefitter");
            RefitLayer = Refitter.FindKernel("RefitBVHLayer");
            NodeUpdater = Refitter.FindKernel("NodeUpdate");
            NodeCompress = Refitter.FindKernel("NodeCompress");
            NodeInitializerKernel = Refitter.FindKernel("NodeInitializer");

            MaterialsChanged = new List<RayTracingObject>();
            InstanceData = GameObject.Find("InstancedStorage").GetComponent<InstancedManager>();
            Instances = new List<InstancedObject>();
            SunDirection = new Vector3(0, -1, 0);
            {
                AddQueue = new List<ParentObject>();
                RemoveQueue = new List<ParentObject>();
                RenderQueue = new List<ParentObject>();
                RenderTransforms = new List<Transform>();
                UpdateQueue = new List<ParentObject>();
                BuildQueue = new List<ParentObject>();
            }
            {
                InstanceRenderQueue = new List<InstancedObject>();
                InstanceUpdateQueue = new List<InstancedObject>();
                InstanceRenderTransforms = new List<Transform>();
                InstanceBuildQueue = new List<InstancedObject>();
                InstanceAddQueue = new List<InstancedObject>();
                InstanceRemoveQueue = new List<InstancedObject>();
            }
            MyMeshesCompacted = new List<MyMeshDataCompacted>();
            UnityLights = new List<LightData>();
            LightMeshes = new List<LightMeshData>();
            LightTransforms = new List<Transform>();
            LightMeshCount = 0;
            UnityLightCount = 0;
            TerrainInfos = new List<TerrainDat>();
            if (BVH8AggregatedBuffer != null) {
                BVH8AggregatedBuffer.Release();
                BVH8AggregatedBuffer = null;
                LightTriBuffer.Release();
                LightTriBuffer = null;
                AggTriBuffer.Release();
                AggTriBuffer = null;
            }
            MeshFunctions = Resources.Load<ComputeShader>("Utility/GeneralMeshFunctions");
            TriangleBufferKernel = MeshFunctions.FindKernel("CombineTriBuffers");
            NodeBufferKernel = MeshFunctions.FindKernel("CombineNodeBuffers");
            LightBufferKernel = MeshFunctions.FindKernel("CombineLightBuffers");
            AlphaMap = Texture2D.blackTexture;
            HeightMap = Texture2D.blackTexture;
            if (TerrainBuffer != null) TerrainBuffer.Release();
            TerrainBuffer = new ComputeBuffer(1, 56);

            if (Terrains.Count != 0) for (int i = 0; i < Terrains.Count; i++) Terrains[i].Load();
        }

        private void UpdateRenderAndBuildQueues() {
            ChildrenUpdated = false;
            OnlyInstanceUpdated = false;

            int QueCount = RemoveQueue.Count;
            {//Main Object Data Handling
             // UnityEngine.Profiling.Profiler.BeginSample("Remove");
                for (int i = QueCount - 1; i >= 0; i--) {
                    switch(RemoveQueue[i].ExistsInQueue) {
                        case 0: {int Index = RenderQueue.IndexOf(RemoveQueue[i]); RenderQueue.RemoveAt(Index); RenderTransforms.RemoveAt(Index);}; break;
                        case 1: BuildQueue.Remove(RemoveQueue[i]); break;
                        case 2: UpdateQueue.Remove(RemoveQueue[i]); break;
                        case 3: AddQueue.Remove(RemoveQueue[i]); break;
                    }
                    RemoveQueue[i].ExistsInQueue = -1;
                    ChildrenUpdated = true;
                }
                RemoveQueue.Clear();
            // UnityEngine.Profiling.Profiler.EndSample();
             // UnityEngine.Profiling.Profiler.BeginSample("Update");
                QueCount = UpdateQueue.Count;
                for (int i = QueCount - 1; i >= 0; i--) {
                    if(UpdateQueue[i] == null) continue;
                    switch(UpdateQueue[i].ExistsInQueue) {
                        case 0: {int Index = RenderQueue.IndexOf(UpdateQueue[i]); RenderQueue.RemoveAt(Index); RenderTransforms.RemoveAt(Index);}; break;
                        case 1: BuildQueue.Remove(UpdateQueue[i]); break;
                    }
                    UpdateQueue[i].ExistsInQueue = -1;
                    ChildrenUpdated = true;
                }
            // UnityEngine.Profiling.Profiler.EndSample();
             // UnityEngine.Profiling.Profiler.BeginSample("Add");
                QueCount = AddQueue.Count;
                for (int i = QueCount - 1; i >= 0; i--) {
                    bool Contained = AddQueue[i].ExistsInQueue != 3;
                    if(!Contained || (AddQueue[i].AsyncTask == null || AddQueue[i].AsyncTask.Status != TaskStatus.RanToCompletion)) {
                        AddQueue[i].Reset(1);
                        BuildQueue.Add(AddQueue[i]);
                        ChildrenUpdated = true;
                    }
                }
                AddQueue.Clear();
            // UnityEngine.Profiling.Profiler.EndSample();
             // UnityEngine.Profiling.Profiler.BeginSample("Build");
                QueCount = BuildQueue.Count;
                for (int i = QueCount - 1; i >= 0; i--) {//Promotes from Build Que to Render Que
                    if (BuildQueue[i].AsyncTask.IsFaulted) {//Fuck, something fucked up
                        Debug.Log(BuildQueue[i].AsyncTask.Exception + ", " + BuildQueue[i].Name);
                        BuildQueue[i].FailureCount++;
                        if(BuildQueue[i].FailureCount > 6) {
                            BuildQueue[i].ExistsInQueue = -1;
                        } else {
                            BuildQueue[i].ExistsInQueue = 3;
                            AddQueue.Add(BuildQueue[i]);
                        }
                        BuildQueue.RemoveAt(i);
                    } else {
                        if (BuildQueue[i].AsyncTask.Status == TaskStatus.RanToCompletion) {
                            if (BuildQueue[i].AggTriangles == null || BuildQueue[i].AggNodes == null) {
                                BuildQueue[i].ExistsInQueue = 3;
                                AddQueue.Add(BuildQueue[i]);
                            } else {
                                BuildQueue[i].SetUpBuffers();
                                BuildQueue[i].ExistsInQueue = 0;
                                RenderTransforms.Add(BuildQueue[i].gameObject.transform);
                                RenderQueue.Add(BuildQueue[i]);
                                ChildrenUpdated = true;
                            }
                            BuildQueue.RemoveAt(i);
                        }
                    }
                }
            // UnityEngine.Profiling.Profiler.EndSample();
             // UnityEngine.Profiling.Profiler.BeginSample("Update 2");
                QueCount = UpdateQueue.Count;
                for (int i = QueCount - 1; i >= 0; i--) {//Demotes from Render Que to Build Que in case mesh has changed
                    if (UpdateQueue[i] != null && UpdateQueue[i].gameObject.activeInHierarchy) {
                        if (UpdateQueue[i].ExistsInQueue != 1) {
                            UpdateQueue[i].Reset(1);
                            BuildQueue.Add(UpdateQueue[i]);
                        }
                        ChildrenUpdated = true;
                    }
                    UpdateQueue.RemoveAt(i);
                }
            // UnityEngine.Profiling.Profiler.EndSample();
            }
            {//Instanced Models Data Handling
                InstanceData.UpdateRenderAndBuildQueues(ref ChildrenUpdated);
             // UnityEngine.Profiling.Profiler.BeginSample("Asset Instance Remove");
                QueCount = InstanceUpdateQueue.Count;
                for (int i = QueCount - 1; i >= 0; i--) {//Demotes from Render Que to Build Que in case mesh has changed
                    if(InstanceRenderQueue.Contains(InstanceUpdateQueue[i])) {InstanceRenderTransforms.RemoveAt(InstanceRenderQueue.IndexOf(InstanceUpdateQueue[i])); InstanceRenderQueue.Remove(InstanceUpdateQueue[i]);}
                    OnlyInstanceUpdated = true;
                }
                InstanceUpdateQueue.Clear();
                QueCount = InstanceRemoveQueue.Count;
                 for (int i = QueCount - 1; i >= 0; i--) {
                    switch(InstanceRemoveQueue[i].ExistsInQueue) {
                        default: Debug.Log("INSTANCES BROKE!"); break;
                        case 0: {InstanceRenderTransforms.RemoveAt(InstanceRenderQueue.IndexOf(InstanceRemoveQueue[i])); InstanceRenderQueue.Remove(InstanceRemoveQueue[i]);} break;
                        case 1: InstanceBuildQueue.Remove(InstanceRemoveQueue[i]); break;
                        case 3: InstanceAddQueue.Remove(InstanceRemoveQueue[i]); break;
                    }
                    InstanceRemoveQueue[i].ExistsInQueue = -1;
                    OnlyInstanceUpdated = true;
                }
                InstanceRemoveQueue.Clear();
            // UnityEngine.Profiling.Profiler.EndSample();

             // UnityEngine.Profiling.Profiler.BeginSample("Asset Instance Add");
                QueCount = InstanceAddQueue.Count;
                for (int i = QueCount - 1; i >= 0; i--) {
                    if (InstanceAddQueue[i].InstanceParent != null && InstanceAddQueue[i].InstanceParent.gameObject.activeInHierarchy) {
                        InstanceAddQueue[i].ExistsInQueue = 1;
                        InstanceBuildQueue.Add(InstanceAddQueue[i]);
                        InstanceAddQueue.RemoveAt(i);
                    }
                }
            // UnityEngine.Profiling.Profiler.EndSample();
             // UnityEngine.Profiling.Profiler.BeginSample("Asset Instance Build");
                QueCount = InstanceBuildQueue.Count;
                for (int i = QueCount - 1; i >= 0; i--) {//Promotes from Build Que to Render Que
                    if (InstanceBuildQueue[i].InstanceParent.HasCompleted == true) {
                        InstanceBuildQueue[i].ExistsInQueue = 0;
                        InstanceRenderTransforms.Add(InstanceBuildQueue[i].transform);
                        InstanceRenderQueue.Add(InstanceBuildQueue[i]);
                        InstanceBuildQueue.RemoveAt(i);
                        OnlyInstanceUpdated = true;
                    }
                }
            // UnityEngine.Profiling.Profiler.EndSample();
            }


            if (OnlyInstanceUpdated && !ChildrenUpdated)
            {
                ChildrenUpdated = true;
            }
            else
            {
                OnlyInstanceUpdated = false;
            }
            if (ChildrenUpdated || ParentCountHasChanged) MeshAABBs = new AABB[RenderQueue.Count + InstanceRenderQueue.Count];
        }
        public void EditorBuild()
        {//Forces all to rebuild
            ClearAll();
            Terrains = new List<TerrainObject>();
            Terrains = new List<TerrainObject>(GetComponentsInChildren<TerrainObject>());
            init();
            AddQueue = new List<ParentObject>();
            RemoveQueue = new List<ParentObject>();
            RenderQueue = new List<ParentObject>();
            RenderTransforms = new List<Transform>();
            BuildQueue = new List<ParentObject>(GetComponentsInChildren<ParentObject>());
            RunningTasks = 0;
            InstanceData.EditorBuild();
            for (int i = 0; i < BuildQueue.Count; i++)
            {
                var CurrentRep = i;
                BuildQueue[CurrentRep].LoadData();
                BuildQueue[CurrentRep].AsyncTask = Task.Run(() => { BuildQueue[CurrentRep].BuildTotal(); RunningTasks--; });
                BuildQueue[CurrentRep].ExistsInQueue = 1;
                RunningTasks++;
            }
            didstart = false;
        }
        public void BuildCombined()
        {//Only has unbuilt be built
            HasToDo = false;
            Terrains = new List<TerrainObject>(GetComponentsInChildren<TerrainObject>());
            init();
            List<ParentObject> TempQueue = new List<ParentObject>(GetComponentsInChildren<ParentObject>());
            InstanceAddQueue = new List<InstancedObject>(GetComponentsInChildren<InstancedObject>());
            InstanceData.BuildCombined();
            RunningTasks = 0;
            for (int i = 0; i < TempQueue.Count; i++)
            {
                if (TempQueue[i].HasCompleted && !TempQueue[i].NeedsToUpdate) {
                    TempQueue[i].ExistsInQueue = 0;
                    RenderQueue.Add(TempQueue[i]);
                    RenderTransforms.Add(TempQueue[i].gameObject.transform);
                }
                else {TempQueue[i].ExistsInQueue = 1; BuildQueue.Add(TempQueue[i]); RunningTasks++;}
            }
            for (int i = 0; i < BuildQueue.Count; i++)
            {
                var CurrentRep = i;
                BuildQueue[CurrentRep].LoadData();
                BuildQueue[CurrentRep].AsyncTask = Task.Run(() => {BuildQueue[CurrentRep].BuildTotal(); RunningTasks--;});
            }
            ParentCountHasChanged = true;
            if (RenderQueue.Count != 0) {CommandBuffer cmd = new CommandBuffer(); int throwaway = UpdateTLAS(cmd);  Graphics.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        cmd.Release();}
        }
        [HideInInspector] public bool HasToDo;

        private void AccumulateData(CommandBuffer cmd)
        {
            // UnityEngine.Profiling.Profiler.BeginSample("Update Object Lists");
            UpdateRenderAndBuildQueues();
            // UnityEngine.Profiling.Profiler.EndSample();

            int ParentsLength = RenderQueue.Count;
            int nodes = 2 * (ParentsLength + InstanceRenderQueue.Count);
            if (ChildrenUpdated || ParentCountHasChanged)
            {
                TotalParentObjectSize = 0;
                HasToDo = false;
                int CurNodeOffset = nodes;
                int CurTriOffset = 0;
                int CurLightTriOffset = 0;
                LightMeshCount = 0;
                LightMeshes.Clear();
                LightTransforms.Clear();
                int AggTriCount = 0;
                int AggNodeCount = nodes;
                int LightTriCount = 0;
                if (BVH8AggregatedBuffer != null)
                {
                    BVH8AggregatedBuffer.Release();
                    AggTriBuffer.Release();
                    LightTriBuffer.Release();
                }
                for (int i = 0; i < ParentsLength; i++)
                {
                    AggNodeCount += RenderQueue[i].AggNodes.Length;
                    AggTriCount += RenderQueue[i].AggTriangles.Length;
                    LightTriCount += RenderQueue[i].LightTriangles.Count;
                }
                for (int i = 0; i < InstanceData.RenderQueue.Count; i++)
                {
                    AggNodeCount += InstanceData.RenderQueue[i].AggNodes.Length;
                    AggTriCount += InstanceData.RenderQueue[i].AggTriangles.Length;
                    LightTriCount += InstanceData.RenderQueue[i].LightTriangles.Count;
                }
                Debug.Log("Total Tri Count: " + AggTriCount);
                Debug.Log("Total Light Tri Count: " + LightTriCount);
                if(LightTriCount == 0) LightTriCount++;
                if (AggNodeCount != 0)
                {//Accumulate the BVH nodes and triangles for all normal models
                    BVH8AggregatedBuffer = new ComputeBuffer(AggNodeCount, 80);
                    AggTriBuffer = new ComputeBuffer(AggTriCount, 88);
                    LightTriBuffer = new ComputeBuffer(LightTriCount, 40);
                    MeshFunctions.SetBuffer(TriangleBufferKernel, "OutCudaTriArray", AggTriBuffer);
                    MeshFunctions.SetBuffer(NodeBufferKernel, "OutAggNodes", BVH8AggregatedBuffer);
                    MeshFunctions.SetBuffer(LightBufferKernel, "LightTrianglesOut", LightTriBuffer);
                    int MatOffset = 0;
                    for (int i = 0; i < ParentsLength; i++)
                    {
                        RenderQueue[i].UpdateData();
                        // cmd.BeginSample("AccumBufferTri");
                        cmd.SetComputeIntParam(MeshFunctions, "Offset", CurTriOffset);
                        cmd.SetComputeIntParam(MeshFunctions, "Count", RenderQueue[i].TriBuffer.count);
                        cmd.SetComputeBufferParam(MeshFunctions, TriangleBufferKernel, "InCudaTriArray", RenderQueue[i].TriBuffer);
                        cmd.DispatchCompute(MeshFunctions, TriangleBufferKernel, (int)Mathf.Ceil(RenderQueue[i].TriBuffer.count / 372.0f), 1, 1);
                        // cmd.EndSample("AccumBufferTri");

                        // cmd.BeginSample("AccumBufferNode");
                        cmd.SetComputeIntParam(MeshFunctions, "Offset", CurNodeOffset);
                        cmd.SetComputeIntParam(MeshFunctions, "Count", RenderQueue[i].BVHBuffer.count);
                        cmd.SetComputeBufferParam(MeshFunctions, NodeBufferKernel, "InAggNodes", RenderQueue[i].BVHBuffer);
                        cmd.DispatchCompute(MeshFunctions, NodeBufferKernel, (int)Mathf.Ceil(RenderQueue[i].BVHBuffer.count / 372.0f), 1, 1);
                        // cmd.EndSample("AccumBufferNode");

                        if (RenderQueue[i].LightTriangles.Count != 0)
                        {
                            cmd.SetComputeIntParam(MeshFunctions, "Offset", CurLightTriOffset);
                            cmd.SetComputeIntParam(MeshFunctions, "Count", RenderQueue[i].LightTriBuffer.count);
                            cmd.SetComputeBufferParam(MeshFunctions, LightBufferKernel, "LightTrianglesIn", RenderQueue[i].LightTriBuffer);
                            cmd.DispatchCompute(MeshFunctions, LightBufferKernel, (int)Mathf.Ceil(RenderQueue[i].LightTriBuffer.count / 372.0f), 1, 1);
                            LightMeshCount++;
                            LightTransforms.Add(RenderTransforms[i]);
                            LightMeshes.Add(new LightMeshData()
                            {
                                StartIndex = CurLightTriOffset,
                                IndexEnd = RenderQueue[i].LightTriangles.Count + CurLightTriOffset,
                                MatOffset = MatOffset,
                                LockedMeshIndex = i
                            });


                            RenderQueue[i].LightTriOffset = CurLightTriOffset;
                            CurLightTriOffset += RenderQueue[i].LightTriangles.Count;
                            TotalParentObjectSize += RenderQueue[i].LightTriBuffer.count * RenderQueue[i].LightTriBuffer.stride;
                        }
                        TotalParentObjectSize += RenderQueue[i].TriBuffer.count * RenderQueue[i].TriBuffer.stride;
                        TotalParentObjectSize += RenderQueue[i].BVHBuffer.count * RenderQueue[i].BVHBuffer.stride;
                        RenderQueue[i].NodeOffset = CurNodeOffset;
                        RenderQueue[i].TriOffset = CurTriOffset;
                        CurNodeOffset += RenderQueue[i].AggNodes.Length;
                        CurTriOffset += RenderQueue[i].AggTriangles.Length;
                        MatOffset += RenderQueue[i]._Materials.Count;
                    }
                    for (int i = 0; i < InstanceData.RenderQueue.Count; i++)
                    {//Accumulate the BVH nodes and triangles for all instanced models
                        InstanceData.RenderQueue[i].UpdateData();
                        InstanceData.RenderQueue[i].InstanceMeshIndex = i + ParentsLength;

                        // cmd.BeginSample("AccumBufferInstanceTri");
                        cmd.SetComputeIntParam(MeshFunctions, "Offset", CurTriOffset);
                        cmd.SetComputeIntParam(MeshFunctions, "Count", InstanceData.RenderQueue[i].TriBuffer.count);
                        cmd.SetComputeBufferParam(MeshFunctions, TriangleBufferKernel, "InCudaTriArray", InstanceData.RenderQueue[i].TriBuffer);
                        cmd.DispatchCompute(MeshFunctions, TriangleBufferKernel, (int)Mathf.Ceil(InstanceData.RenderQueue[i].TriBuffer.count / 372.0f), 1, 1);
                        // cmd.EndSample("AccumBufferInstanceTri");

                        // cmd.BeginSample("AccumBufferInstanceNode");
                        cmd.SetComputeIntParam(MeshFunctions, "Offset", CurNodeOffset);
                        cmd.SetComputeIntParam(MeshFunctions, "Count", InstanceData.RenderQueue[i].BVHBuffer.count);
                        cmd.SetComputeBufferParam(MeshFunctions, NodeBufferKernel, "InAggNodes", InstanceData.RenderQueue[i].BVHBuffer);
                        cmd.DispatchCompute(MeshFunctions, NodeBufferKernel, (int)Mathf.Ceil(InstanceData.RenderQueue[i].BVHBuffer.count / 372.0f), 1, 1);
                        // cmd.EndSample("AccumBufferInstanceNode");
                        if (InstanceData.RenderQueue[i].LightTriangles.Count != 0)
                        {
                            cmd.SetComputeIntParam(MeshFunctions, "Offset", CurLightTriOffset);
                            cmd.SetComputeIntParam(MeshFunctions, "Count", InstanceData.RenderQueue[i].LightTriBuffer.count);
                            cmd.SetComputeBufferParam(MeshFunctions, LightBufferKernel, "LightTrianglesIn", InstanceData.RenderQueue[i].LightTriBuffer);
                            cmd.DispatchCompute(MeshFunctions, LightBufferKernel, (int)Mathf.Ceil(InstanceData.RenderQueue[i].LightTriBuffer.count / 372.0f), 1, 1);
                            
                            InstanceData.RenderQueue[i].LightTriOffset = CurLightTriOffset;
                            CurLightTriOffset += InstanceData.RenderQueue[i].LightTriangles.Count;
                            InstanceData.RenderQueue[i].LightEndIndex = CurLightTriOffset;
                        }

                        InstanceData.RenderQueue[i].NodeOffset = CurNodeOffset;
                        InstanceData.RenderQueue[i].TriOffset = CurTriOffset;
                        CurNodeOffset += InstanceData.RenderQueue[i].AggNodes.Length;
                        CurTriOffset += InstanceData.RenderQueue[i].AggTriangles.Length;

                    }
                    for (int i = 0; i < InstanceRenderQueue.Count; i++)
                    {
                        if (InstanceRenderQueue[i].InstanceParent.LightTriangles.Count != 0)
                        {
                            LightMeshCount++;
                            LightTransforms.Add(InstanceRenderTransforms[i]);
                            LightMeshes.Add(new LightMeshData()
                            {
                                LockedMeshIndex = i + ParentsLength,
                                StartIndex = InstanceRenderQueue[i].InstanceParent.LightTriOffset,
                                IndexEnd = InstanceRenderQueue[i].InstanceParent.LightEndIndex
                            });
                        }
                    }
                }

                if (LightMeshCount == 0) { LightMeshes.Add(new LightMeshData() { }); }


                if (!OnlyInstanceUpdated || _Materials.Count == 0) CreateAtlas();
            }
            ParentCountHasChanged = false;
            #if !HardwareRT
                if (UseSkinning && didstart)
                {
                    MyMeshDataCompacted TempMesh2;
                    for (int i = 0; i < ParentsLength; i++)
                    {//Refit BVH's of skinned meshes
                        if (RenderQueue[i].IsSkinnedGroup)//this can be optimized to operate directly on the triangle buffer instead of needing to copy it
                        {
                            cmd.SetComputeIntParam(RenderQueue[i].MeshRefit, "TriBuffOffset", RenderQueue[i].TriOffset);
                            cmd.SetComputeIntParam(RenderQueue[i].MeshRefit, "LightTriBuffOffset", RenderQueue[i].LightTriOffset);
                            RenderQueue[i].RefitMesh(ref BVH8AggregatedBuffer, ref AggTriBuffer, ref LightTriBuffer, cmd);
                            if(i < MyMeshesCompacted.Count) {
                                TempMesh2 = MyMeshesCompacted[i];
                                TempMesh2.Transform = RenderTransforms[i].worldToLocalMatrix;
                                MyMeshesCompacted[i] = TempMesh2;
                                MeshAABBs[i] = RenderQueue[i].aabb;
                            }
                        }
                    }
                }
            #else
                if (UseSkinning && didstart)
                {
                    MyMeshDataCompacted TempMesh2;
                    for (int i = 0; i < ParentsLength; i++)
                    {//Refit BVH's of skinned meshes
                        if (RenderQueue[i].IsSkinnedGroup)//this can be optimized to operate directly on the triangle buffer instead of needing to copy it
                        {
                             cmd.SetComputeIntParam(RenderQueue[i].MeshRefit, "TriBuffOffset", RenderQueue[i].TriOffset);
                            cmd.SetComputeIntParam(RenderQueue[i].MeshRefit, "LightTriBuffOffset", RenderQueue[i].LightTriOffset);
                            RenderQueue[i].RefitMesh(ref BVH8AggregatedBuffer, ref AggTriBuffer, ref LightTriBuffer, cmd);
                            if(i < MyMeshesCompacted.Count) {
                                TempMesh2 = MyMeshesCompacted[i];
                                TempMesh2.Transform = RenderTransforms[i].worldToLocalMatrix;
                                MyMeshesCompacted[i] = TempMesh2;
                                MeshAABBs[i] = RenderQueue[i].aabb;
                            }
                        }
                    }
                }


            #endif
        }

        public struct AggData
        {
            public int AggIndexCount;
            public int AggNodeCount;
            public int MaterialOffset;
            public int mesh_data_bvh_offsets;
            public int LightTriCount;
        }


        public void CreateAABB(Transform transform, ref AABB aabb)
        {//Update the Transformed AABB by getting the new Max/Min of the untransformed AABB after transforming it
            Vector3 center = 0.5f * (aabb.BBMin + aabb.BBMax);
            Vector3 extent = 0.5f * (aabb.BBMax - aabb.BBMin);
            Matrix4x4 Mat = transform.localToWorldMatrix;
            Vector3 new_center = CommonFunctions.transform_position(Mat, center);
            Vector3 new_extent = CommonFunctions.transform_direction(Mat, extent);

            aabb.BBMin = new_center - new_extent;
            aabb.BBMax = new_center + new_extent;
        }

        AABB tempAABB;
        int[] ToBVHIndex;
        [HideInInspector] public BVH8Builder TLASBVH8;

        int MaxRecur = 0;
        int TempRecur = 0;
          private List<Vector3Int> IsLeafList;
    unsafe public void DocumentNodes(int CurrentNode, int ParentNode, int NextNode, int NextBVH8Node, bool IsLeafRecur, int CurRecur) {
        NodeIndexPairData CurrentPair = NodePair[CurrentNode];
        TempRecur = Mathf.Max(TempRecur, CurRecur);
        IsLeafList[CurrentNode] = new Vector3Int(IsLeafList[CurrentNode].x, CurRecur, ParentNode);
        if (!IsLeafRecur) {
            ToBVHIndex[NextBVH8Node] = CurrentNode;
            IsLeafList[CurrentNode] = new Vector3Int(0, IsLeafList[CurrentNode].y, IsLeafList[CurrentNode].z);
            BVHNode8Data node = TLASBVH8.BVH8Nodes[NextBVH8Node];
            NodeIndexPairData IndexPair = new NodeIndexPairData();
            IndexPair.AABB = new AABB();

            Vector3 e = Vector3.zero;
            for(int i = 0; i < 3; i++) e[i] = (float)(System.Convert.ToSingle((int)(System.Convert.ToUInt32(node.e[i]) << 23)));
            for (int i = 0; i < 8; i++) {
                IndexPair.InNodeOffset = i;
                float maxFactorX = node.quantized_max_x[i] * e.x;
                float maxFactorY = node.quantized_max_y[i] * e.y;
                float maxFactorZ = node.quantized_max_z[i] * e.z;
                float minFactorX = node.quantized_min_x[i] * e.x;
                float minFactorY = node.quantized_min_y[i] * e.y;
                float minFactorZ = node.quantized_min_z[i] * e.z;

                IndexPair.AABB.Create(new Vector3(maxFactorX, maxFactorY, maxFactorZ) + node.p, new Vector3(minFactorX, minFactorY, minFactorZ) + node.p);

                NextNode++;
                IndexPair.BVHNode = NextBVH8Node;
                NodePair.Add(IndexPair);
                IsLeafList.Add(new Vector3Int(0,0,0));
                if ((node.meta[i] & 0b11111) < 24) {
                    DocumentNodes(NodePair.Count - 1, CurrentNode, NextNode, -1, true, CurRecur + 1);
                } else {
                    int child_offset = (byte)node.meta[i] & 0b11111;
                    int child_index = (int)node.base_index_child + child_offset - 24;
                    DocumentNodes(NodePair.Count - 1, CurrentNode, NextNode, child_index, false, CurRecur + 1);
                }
            }
        } else IsLeafList[CurrentNode] = new Vector3Int(1, IsLeafList[CurrentNode].y, IsLeafList[CurrentNode].z);

        NodePair[CurrentNode] = CurrentPair;
    }
        List<BVHNode8DataFixed> SplitNodes;
        List<NodeIndexPairData> NodePair;
        Layer[] ForwardStack;
        Layer2[] LayerStack;
        int[] CWBVHIndicesBufferInverted;
        ComputeBuffer[] WorkingBuffer;
        ComputeBuffer NodeBuffer;
        ComputeBuffer StackBuffer;
        ComputeBuffer ToBVHIndexBuffer;
        ComputeBuffer BVHDataBuffer;
        ComputeBuffer BVHBuffer;

        unsafe public void ConstructNewTLAS() {
            #if HardwareRT
                int TotLength = 0;
                int MeshOffset = 0;
                SubMeshOffsets = new List<int>();
                MeshOffsets = new List<Vector2>();
                AccelStruct.ClearInstances();
                for(int i = 0; i < RenderQueue.Count; i++) {
                    RenderQueue[i].HWRTIndex = new List<int>();
                    foreach(var A in RenderQueue[i].Renderers) {
                        MeshOffsets.Add(new Vector2(SubMeshOffsets.Count, i));
                        Mesh mesh = ((A.gameObject.GetComponent<MeshFilter>() != null) ? A.gameObject.GetComponent<MeshFilter>().sharedMesh : A.gameObject.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                        for (int i2 = 0; i2 < mesh.subMeshCount; ++i2)
                        {//Add together all the submeshes in the mesh to consider it as one object
                            SubMeshOffsets.Add(TotLength);
                            int IndiceLength = (int)mesh.GetIndexCount(i2) / 3;
                            TotLength += IndiceLength;
                        }
                        RayTracingSubMeshFlags[] B = new RayTracingSubMeshFlags[mesh.subMeshCount];
                        for(int i2 = 0; i2 < B.Length; i2++) {
                            B[i2] = RayTracingSubMeshFlags.Enabled | RayTracingSubMeshFlags.ClosestHitOnly;
                        }
                        AccelStruct.AddInstance(A, B, true, false, (uint)((A.gameObject.GetComponent<RayTracingObject>().SpecTrans[0] == 1) ? 0x2 : 0x1), (uint)MeshOffset);
                        MeshOffset++;
                    }
                }
            AccelStruct.Build();
            #else
                BVH2Builder BVH = new BVH2Builder(MeshAABBs);
                TLASBVH8 = new BVH8Builder(BVH);
                System.Array.Resize(ref TLASBVH8.BVH8Nodes, TLASBVH8.cwbvhnode_count);
                ToBVHIndex = new int[TLASBVH8.cwbvhnode_count];
                if (TempBVHArray == null || TLASBVH8.BVH8Nodes.Length != TempBVHArray.Length) TempBVHArray = new BVHNode8DataCompressed[TLASBVH8.BVH8Nodes.Length];
                CommonFunctions.Aggregate(ref TempBVHArray, ref TLASBVH8.BVH8Nodes);
                BVHNodeCount = TLASBVH8.BVH8Nodes.Length;
                NodePair = new List<NodeIndexPairData>();
                NodePair.Add(new NodeIndexPairData());
                IsLeafList = new List<Vector3Int>();
                IsLeafList.Add(new Vector3Int(0,0,0));
                DocumentNodes(0, 0, 1, 0, false, 0);
                TempRecur++;
                MaxRecur = TempRecur;
                int NodeCount = NodePair.Count;
                ForwardStack = new Layer[NodeCount];
                LayerStack = new Layer2[MaxRecur];
                Layer PresetLayer = new Layer();
                Layer2 TempSlab = new Layer2();
                TempSlab.Slab = new List<int>();

                for (int i = 0; i < MaxRecur; i++) LayerStack[i] = TempSlab;
                for(int i = 0; i < 8; i++) PresetLayer.Leaf[i] = PresetLayer.Children[i] = -1;

                for (int i = 0; i < NodePair.Count; i++) {
                    ForwardStack[i] = PresetLayer;
                    if (IsLeafList[i].x == 1) {
                        int first_triangle = (byte)TLASBVH8.BVH8Nodes[NodePair[i].BVHNode].meta[NodePair[i].InNodeOffset] & 0b11111;
                        int NumBits = CommonFunctions.NumberOfSetBits((byte)TLASBVH8.BVH8Nodes[NodePair[i].BVHNode].meta[NodePair[i].InNodeOffset] >> 5);
                        ForwardStack[i].Children[NodePair[i].InNodeOffset] = NumBits;
                        ForwardStack[i].Leaf[NodePair[i].InNodeOffset] = (int)TLASBVH8.BVH8Nodes[NodePair[i].BVHNode].base_index_triangle + first_triangle + 1;
                    } else {
                        ForwardStack[i].Children[NodePair[i].InNodeOffset] = i;
                        ForwardStack[i].Leaf[NodePair[i].InNodeOffset] = 0;
                    }
                    ForwardStack[IsLeafList[i].z].Children[NodePair[i].InNodeOffset] = i;
                    ForwardStack[IsLeafList[i].z].Leaf[NodePair[i].InNodeOffset] = 0;
                    
                    var TempLayer = LayerStack[IsLeafList[i].y];
                    TempLayer.Slab.Add(i);
                    LayerStack[IsLeafList[i].y] = TempLayer;
                }
                CommonFunctions.ConvertToSplitNodes(TLASBVH8, ref SplitNodes);
                List<Layer2> TempStack = new List<Layer2>();
                for (int i = 0; i < LayerStack.Length; i++) if(LayerStack[i].Slab.Count != 0) TempStack.Add(LayerStack[i]);
                MaxRecur = TempStack.Count;

                if(WorkingBuffer != null) for(int i = 0; i < WorkingBuffer.Length; i++) WorkingBuffer[i]?.Release();
                if (NodeBuffer != null) NodeBuffer.Release();
                if (StackBuffer != null) StackBuffer.Release();
                if (ToBVHIndexBuffer != null) ToBVHIndexBuffer.Release();
                if (BVHDataBuffer != null) BVHDataBuffer.Release();
                if (BVHBuffer != null) BVHBuffer.Release();
                if (BoxesBuffer != null) BoxesBuffer.Release();
                if (TLASCWBVHIndexes != null) TLASCWBVHIndexes.Release();
                WorkingBuffer = new ComputeBuffer[LayerStack.Length];
                for (int i = 0; i < MaxRecur; i++) {
                    WorkingBuffer[i] = new ComputeBuffer(LayerStack[i].Slab.Count, 4);
                    WorkingBuffer[i].SetData(LayerStack[i].Slab);
                }
                NodeBuffer = new ComputeBuffer(NodePair.Count, 32);
                NodeBuffer.SetData(NodePair);
                StackBuffer = new ComputeBuffer(ForwardStack.Length, 64);
                StackBuffer.SetData(ForwardStack);
                ToBVHIndexBuffer = new ComputeBuffer(ToBVHIndex.Length, 4);
                ToBVHIndexBuffer.SetData(ToBVHIndex);
                BVHDataBuffer = new ComputeBuffer(TempBVHArray.Length, 260);
                BVHDataBuffer.SetData(SplitNodes);
                BVHBuffer = new ComputeBuffer(TempBVHArray.Length, 80);
                BVHBuffer.SetData(TempBVHArray);
                BoxesBuffer = new ComputeBuffer(MeshAABBs.Length, 24);
                TLASCWBVHIndexes = new ComputeBuffer(MeshAABBs.Length, 4);
                TLASCWBVHIndexes.SetData(TLASBVH8.cwbvh_indices);
                CurFrame = 0;
            #endif
        }
        Task TLASTask;
        unsafe async void CorrectRefit(AABB[] Boxes) {
            TempRecur = 0;
            BVH2Builder BVH = new BVH2Builder(Boxes);
            TLASBVH8 = new BVH8Builder(BVH);
                System.Array.Resize(ref TLASBVH8.BVH8Nodes, TLASBVH8.cwbvhnode_count);
                ToBVHIndex = new int[TLASBVH8.cwbvhnode_count];
                if (TempBVHArray == null || TLASBVH8.BVH8Nodes.Length != TempBVHArray.Length) TempBVHArray = new BVHNode8DataCompressed[TLASBVH8.BVH8Nodes.Length];
                CommonFunctions.Aggregate(ref TempBVHArray, ref TLASBVH8.BVH8Nodes);

                NodePair = new List<NodeIndexPairData>();
                NodePair.Add(new NodeIndexPairData());
                IsLeafList = new List<Vector3Int>();
                IsLeafList.Add(new Vector3Int(0,0,0));
                DocumentNodes(0, 0, 1, 0, false, 0);
                TempRecur++;
                int NodeCount = NodePair.Count;
                ForwardStack = new Layer[NodeCount];
                LayerStack = new Layer2[TempRecur];
                Layer PresetLayer = new Layer();
                Layer2 TempSlab = new Layer2();
                TempSlab.Slab = new List<int>();

                for (int i = 0; i < TempRecur; i++) LayerStack[i] = TempSlab;
                for(int i = 0; i < 8; i++) PresetLayer.Leaf[i] = PresetLayer.Children[i] = -1;

                for (int i = 0; i < NodePair.Count; i++) {
                    ForwardStack[i] = PresetLayer;
                    if (IsLeafList[i].x == 1) {
                        int first_triangle = (byte)TLASBVH8.BVH8Nodes[NodePair[i].BVHNode].meta[NodePair[i].InNodeOffset] & 0b11111;
                        int NumBits = CommonFunctions.NumberOfSetBits((byte)TLASBVH8.BVH8Nodes[NodePair[i].BVHNode].meta[NodePair[i].InNodeOffset] >> 5);
                        ForwardStack[i].Children[NodePair[i].InNodeOffset] = NumBits;
                        ForwardStack[i].Leaf[NodePair[i].InNodeOffset] = (int)TLASBVH8.BVH8Nodes[NodePair[i].BVHNode].base_index_triangle + first_triangle + 1;
                    } else {
                        ForwardStack[i].Children[NodePair[i].InNodeOffset] = i;
                        ForwardStack[i].Leaf[NodePair[i].InNodeOffset] = 0;
                    }
                    ForwardStack[IsLeafList[i].z].Children[NodePair[i].InNodeOffset] = i;
                    ForwardStack[IsLeafList[i].z].Leaf[NodePair[i].InNodeOffset] = 0;
                    
                    var TempLayer = LayerStack[IsLeafList[i].y];
                    TempLayer.Slab.Add(i);
                    LayerStack[IsLeafList[i].y] = TempLayer;
                }
                CommonFunctions.ConvertToSplitNodes(TLASBVH8, ref SplitNodes);
                List<Layer2> TempStack = new List<Layer2>();
                for (int i = 0; i < LayerStack.Length; i++) if(LayerStack[i].Slab.Count != 0) TempStack.Add(LayerStack[i]);

                TempRecur = TempStack.Count;
                LayerStack = TempStack.ToArray();
            return;
        }
        ComputeBuffer BoxesBuffer;
        int CurFrame = 0;
        int BVHNodeCount = 0;
        public unsafe void RefitTLAS(AABB[] Boxes, CommandBuffer cmd)
        {
            #if !HardwareRT
            if(TLASTask == null) TLASTask = Task.Run(() => CorrectRefit(Boxes));

            CurFrame++;
             if(TLASTask.Status == TaskStatus.RanToCompletion && CurFrame % 25 == 24) {
                MaxRecur = TempRecur; 

                if (WorkingBuffer != null) for(int i = 0; i < WorkingBuffer.Length; i++) WorkingBuffer[i]?.Release();
                if (NodeBuffer != null) NodeBuffer.Release();
                if (StackBuffer != null) StackBuffer.Release();
                if (ToBVHIndexBuffer != null) ToBVHIndexBuffer.Release();
                if (BVHDataBuffer != null) BVHDataBuffer.Release();
                if (BVHBuffer != null) BVHBuffer.Release();
                if (BoxesBuffer != null) BoxesBuffer.Release();
                if (TLASCWBVHIndexes != null) TLASCWBVHIndexes.Release();
                WorkingBuffer = new ComputeBuffer[LayerStack.Length];
                for (int i = 0; i < MaxRecur; i++) {
                    WorkingBuffer[i] = new ComputeBuffer(LayerStack[i].Slab.Count, 4);
                    WorkingBuffer[i].SetData(LayerStack[i].Slab);
                }
                NodeBuffer = new ComputeBuffer(NodePair.Count, 32);
                NodeBuffer.SetData(NodePair);
                StackBuffer = new ComputeBuffer(ForwardStack.Length, 64);
                StackBuffer.SetData(ForwardStack);
                ToBVHIndexBuffer = new ComputeBuffer(ToBVHIndex.Length, 4);
                ToBVHIndexBuffer.SetData(ToBVHIndex);
                BVHDataBuffer = new ComputeBuffer(TempBVHArray.Length, 260);
                BVHDataBuffer.SetData(SplitNodes);
                BVHBuffer = new ComputeBuffer(TempBVHArray.Length, 80);
                BVHBuffer.SetData(TempBVHArray);
                BoxesBuffer = new ComputeBuffer(MeshAABBs.Length, 24);
                TLASCWBVHIndexes = new ComputeBuffer(MeshAABBs.Length, 4);
                TLASCWBVHIndexes.SetData(TLASBVH8.cwbvh_indices);
                 for (int i = 0; i < RenderQueue.Count; i++) {
                    ParentObject TargetParent = RenderQueue[i];
                    MeshAABBs[TargetParent.CompactedMeshData] = TargetParent.aabb;
                }
                Boxes = MeshAABBs;
                #if !HardwareRT
                    BVH8AggregatedBuffer.SetData(TempBVHArray, 0, 0, TempBVHArray.Length);
                #endif
                BVHNodeCount = TLASBVH8.BVH8Nodes.Length;

                TLASTask = Task.Run(() => CorrectRefit(Boxes));
            }
                // cmd.BeginSample("TLAS Refit Init");
                cmd.SetComputeIntParam(Refitter, "NodeCount", NodeBuffer.count);
                cmd.SetComputeBufferParam(Refitter, NodeInitializerKernel, "AllNodes", NodeBuffer);

                cmd.DispatchCompute(Refitter, NodeInitializerKernel, (int)Mathf.Ceil(NodeBuffer.count / (float)256), 1, 1);
                // cmd.EndSample("TLAS Refit Init");

                // cmd.BeginSample("TLAS Refit Refit");
                BoxesBuffer.SetData(Boxes);
                cmd.SetComputeBufferParam(Refitter, RefitLayer, "TLASCWBVHIndices", TLASCWBVHIndexes);
                cmd.SetComputeBufferParam(Refitter, RefitLayer, "Boxs", BoxesBuffer);
                cmd.SetComputeBufferParam(Refitter, RefitLayer, "ReverseStack", StackBuffer);
                cmd.SetComputeBufferParam(Refitter, RefitLayer, "AllNodes", NodeBuffer);
                cmd.SetComputeBufferParam(Refitter, NodeInitializerKernel, "AllNodes", NodeBuffer);
                for (int i = MaxRecur - 1; i >= 0; i--)
                {
                    var NodeCount2 = WorkingBuffer[i].count;
                    cmd.SetComputeIntParam(Refitter, "NodeCount", NodeCount2);
                    cmd.SetComputeBufferParam(Refitter, RefitLayer, "NodesToWork", WorkingBuffer[i]);
                    cmd.DispatchCompute(Refitter, RefitLayer, (int)Mathf.Ceil(WorkingBuffer[i].count / (float)256), 1, 1);
                }
                // cmd.EndSample("TLAS Refit Refit");

                // cmd.BeginSample("TLAS Refit Node Update");
                cmd.SetComputeIntParam(Refitter, "NodeCount", NodeBuffer.count);
                cmd.SetComputeBufferParam(Refitter, NodeUpdater, "AllNodes", NodeBuffer);
                cmd.SetComputeBufferParam(Refitter, NodeUpdater, "BVHNodes", BVHDataBuffer);
                cmd.SetComputeBufferParam(Refitter, NodeUpdater, "ToBVHIndex", ToBVHIndexBuffer);
                cmd.DispatchCompute(Refitter, NodeUpdater, (int)Mathf.Ceil(NodeBuffer.count / (float)256), 1, 1);
                // cmd.EndSample("TLAS Refit Node Update");

                // cmd.BeginSample("TLAS Refit Node Compress");
                cmd.SetComputeIntParam(Refitter, "NodeCount", BVHNodeCount);
                cmd.SetComputeIntParam(Refitter, "NodeOffset", 0);
                cmd.SetComputeBufferParam(Refitter, NodeCompress, "BVHNodes", BVHDataBuffer);
                try {
                    cmd.SetComputeBufferParam(Refitter, NodeCompress, "AggNodes", BVH8AggregatedBuffer);
                } finally {}
                cmd.DispatchCompute(Refitter, NodeCompress, (int)Mathf.Ceil(NodeBuffer.count / (float)256), 1, 1);
                // cmd.EndSample("TLAS Refit Node Compress");

            #endif
        }

        private bool ChangedLastFrame = true;


        public int UpdateTLAS(CommandBuffer cmd)
        {  //Allows for objects to be moved in the scene or animated while playing 

            bool LightsHaveUpdated = false;
            AccumulateData(cmd);

                // UnityEngine.Profiling.Profiler.BeginSample("Lights Update");
            if (!didstart || PrevLightCount != RayTracingMaster._rayTracingLights.Count || UnityLights.Count == 0)
            {
                UnityLights.Clear();
                UnityLightCount = 0;
                foreach (RayTracingLights RayLight in RayTracingMaster._rayTracingLights) {
                    UnityLightCount++;
                    RayLight.UpdateLight();
                    if (RayLight.ThisLightData.Type == 1) SunDirection = RayLight.ThisLightData.Direction;
                    RayLight.ArrayIndex = UnityLightCount - 1;
                    RayLight.ThisLightData.Radiance *= LightEnergyScale;
                    UnityLights.Add(RayLight.ThisLightData);
                }
                if (UnityLights.Count == 0) { UnityLights.Add(new LightData() { }); }
                if (PrevLightCount != RayTracingMaster._rayTracingLights.Count) LightsHaveUpdated = true;
                PrevLightCount = RayTracingMaster._rayTracingLights.Count;
            } else {
                int LightCount = RayTracingMaster._rayTracingLights.Count;
                RayTracingLights RayLight;
                for (int i = 0; i < LightCount; i++) {
                    RayLight = RayTracingMaster._rayTracingLights[i];
                    RayLight.UpdateLight();
                    RayLight.ThisLightData.Radiance *= LightEnergyScale;
                    if (RayLight.ThisLightData.Type == 1) SunDirection = RayLight.ThisLightData.Direction;
                    try{UnityLights[RayLight.ArrayIndex] = RayLight.ThisLightData;} catch(System.Exception throwawayerror) {}
                    // finally {PrevLightCount = 0;}
                }
            }
                // UnityEngine.Profiling.Profiler.EndSample();

            int MatOffset = 0;
            int MeshDataCount = RenderQueue.Count;
            int aggregated_bvh_node_count = 2 * (MeshDataCount + InstanceRenderQueue.Count);
            int AggNodeCount = aggregated_bvh_node_count;
            int AggTriCount = 0;
            if (ChildrenUpdated || !didstart || OnlyInstanceUpdated || MyMeshesCompacted.Count == 0)
            {
                MyMeshesCompacted.Clear();
                AggData[] Aggs = new AggData[InstanceData.RenderQueue.Count];
                // UnityEngine.Profiling.Profiler.BeginSample("Remake Initial Data");
                for (int i = 0; i < MeshDataCount; i++) {
                    if (!RenderQueue[i].IsSkinnedGroup) RenderQueue[i].UpdateAABB(RenderTransforms[i]);
                    MyMeshesCompacted.Add(new MyMeshDataCompacted() {
                        mesh_data_bvh_offsets = aggregated_bvh_node_count,
                        Transform = RenderTransforms[i].worldToLocalMatrix,
                        AggIndexCount = AggTriCount,
                        AggNodeCount = AggNodeCount,
                        MaterialOffset = MatOffset,
                        LightTriCount = RenderQueue[i].LightTriangles.Count
                    });
                    RenderQueue[i].CompactedMeshData = i;
                    MatOffset += RenderQueue[i].MatOffset;
                    MeshAABBs[i] = RenderQueue[i].aabb;
                    AggNodeCount += RenderQueue[i].AggBVHNodeCount;//Can I replace this with just using aggregated_bvh_node_count below?
                    AggTriCount += RenderQueue[i].AggIndexCount;
                    #if !HardwareRT
                        aggregated_bvh_node_count += RenderQueue[i].BVH.cwbvhnode_count;
                    #endif
                }
                // UnityEngine.Profiling.Profiler.EndSample();

                // UnityEngine.Profiling.Profiler.BeginSample("Remake Initial Instance Data A");
                int InstanceCount = InstanceData.RenderQueue.Count;
                for (int i = 0; i < InstanceCount; i++)
                {
                    InstanceData.RenderQueue[i].CompactedMeshData = i;
                    Aggs[i].AggIndexCount = AggTriCount;
                    Aggs[i].AggNodeCount = AggNodeCount;
                    Aggs[i].MaterialOffset = MatOffset;
                    Aggs[i].mesh_data_bvh_offsets = aggregated_bvh_node_count;
                    Aggs[i].LightTriCount = InstanceData.RenderQueue[i].LightTriangles.Count;
                    MatOffset += InstanceData.RenderQueue[i].MatOffset;
                    AggNodeCount += InstanceData.RenderQueue[i].AggBVHNodeCount;//Can I replace this with just using aggregated_bvh_node_count below?
                    AggTriCount += InstanceData.RenderQueue[i].AggIndexCount;
                    aggregated_bvh_node_count += InstanceData.RenderQueue[i].BVH.cwbvhnode_count;
                }
                // UnityEngine.Profiling.Profiler.EndSample();
                // UnityEngine.Profiling.Profiler.BeginSample("Remake Initial Instance Data B");
                InstanceCount = InstanceRenderQueue.Count;
                int MeshCount = MyMeshesCompacted.Count;
                for (int i = 0; i < InstanceCount; i++)
                {
                    int Index = InstanceRenderQueue[i].InstanceParent.CompactedMeshData;
                    MyMeshesCompacted.Add(new MyMeshDataCompacted()
                    {
                        mesh_data_bvh_offsets = Aggs[Index].mesh_data_bvh_offsets,
                        Transform = InstanceRenderQueue[i].transform.worldToLocalMatrix,
                        AggIndexCount = Aggs[Index].AggIndexCount,
                        AggNodeCount = Aggs[Index].AggNodeCount,
                        MaterialOffset = Aggs[Index].MaterialOffset,
                        LightTriCount = Aggs[Index].LightTriCount

                    });
                    InstanceRenderQueue[i].CompactedMeshData = MeshCount + i;
                    AABB aabb = InstanceRenderQueue[i].InstanceParent.aabb_untransformed;
                    CreateAABB(InstanceRenderQueue[i].transform, ref aabb);
                    MeshAABBs[RenderQueue.Count + i] = aabb;
                }
                // UnityEngine.Profiling.Profiler.EndSample();


                Debug.Log("Total Object Count: " + MeshAABBs.Length);
                // UnityEngine.Profiling.Profiler.BeginSample("Update Materials");
                UpdateMaterials();
                // UnityEngine.Profiling.Profiler.EndSample();
                ConstructNewTLAS();
                #if !HardwareRT
                    BVH8AggregatedBuffer.SetData(TempBVHArray, 0, 0, TempBVHArray.Length);
                #endif
            } else {
                // UnityEngine.Profiling.Profiler.BeginSample("Update Transforms");
                Transform TargetTransform;
                ParentObject TargetParent;
                int ClosedCount = 0;
                MyMeshDataCompacted TempMesh2;
                for (int i = 0; i < MeshDataCount; i++)
                {
                    if (RenderTransforms[i].hasChanged)
                    {
                        TargetParent = RenderQueue[i];
                        // RenderTransforms[i].hasChanged = false;
                        if (TargetParent.IsSkinnedGroup) continue;
                        TargetTransform = RenderTransforms[i];
                        TargetParent.UpdateAABB(TargetTransform);
                        TempMesh2 = MyMeshesCompacted[i];
                        TempMesh2.Transform = TargetTransform.worldToLocalMatrix;
                        MyMeshesCompacted[i] = TempMesh2;
                        #if HardwareRT
                            foreach(var a in TargetParent.Renderers) {
                                AccelStruct.UpdateInstanceTransform(a);
                            }
                        #endif
                        MeshAABBs[i] = TargetParent.aabb;
                    }
                }
                if(MeshDataCount != 1 && ClosedCount == MeshDataCount - 1) return 0;
                // UnityEngine.Profiling.Profiler.EndSample();

                // UnityEngine.Profiling.Profiler.BeginSample("Refit TLAS");
                int ListCount = InstanceRenderQueue.Count;
                for (int i = 0; i < ListCount; i++)
                {
                    if (InstanceRenderTransforms[i].hasChanged || ChangedLastFrame)
                    {
                        TargetTransform = InstanceRenderTransforms[i];
                        MyMeshDataCompacted TempMesh = MyMeshesCompacted[InstanceRenderQueue[i].CompactedMeshData];
                        TempMesh.Transform = TargetTransform.worldToLocalMatrix;
                        MyMeshesCompacted[InstanceRenderQueue[i].CompactedMeshData] = TempMesh;
                        TargetTransform.hasChanged = false;
                        AABB aabb = InstanceRenderQueue[i].InstanceParent.aabb_untransformed;
                        CreateAABB(TargetTransform, ref aabb);
                        MeshAABBs[InstanceRenderQueue[i].CompactedMeshData] = aabb;
                    }
                }

                AggNodeCount = 0;
                // UnityEngine.Profiling.Profiler.EndSample();
                #if HardwareRT
                    AccelStruct.Build();
                #endif
                cmd.BeginSample("TLAS Refitting");
                RefitTLAS(MeshAABBs, cmd);
                cmd.EndSample("TLAS Refitting");
            }
            LightMeshData CurLightMesh;
            for (int i = 0; i < LightMeshCount; i++)
            {
                CurLightMesh = LightMeshes[i];
                CurLightMesh.Center = LightTransforms[i].position;
                LightMeshes[i] = CurLightMesh;
            }
            if (!didstart) didstart = true;

            AggNodeCount = 0;

            ChangedLastFrame = ChildrenUpdated;
            return (LightsHaveUpdated ? 2 : 0) + (ChildrenUpdated ? 1 : 0);//The issue is that all light triangle indices start at 0, and thus might not get correctly sorted for indices
        }

        public bool UpdateMaterials()
        {//Allows for live updating of material properties of any object
            bool HasChangedMaterials = false;
            int ParentCount = RenderQueue.Count;
            RayTracingObject CurrentMaterial;
            MaterialData TempMat;
            int ChangedMaterialCount = MaterialsChanged.Count;
            int MatCount = _Materials.Count;
            for (int i = 0; i < ChangedMaterialCount; i++)
            {
                CurrentMaterial = MaterialsChanged[i];
                int MaterialCount = (int)Mathf.Min(CurrentMaterial.MaterialIndex.Length, CurrentMaterial.Indexes.Length);
                for (int i3 = 0; i3 < MaterialCount; i3++)
                {
                    int Index = CurrentMaterial.Indexes[i3];
                    if(CurrentMaterial.MaterialIndex[i3] >= MatCount) continue;
                    TempMat = _Materials[CurrentMaterial.MaterialIndex[i3]];
                    TempMat.BaseColor = CurrentMaterial.BaseColor[Index];
                    TempMat.Emissive = CurrentMaterial.Emission[Index];
                    TempMat.Roughness = ((int)CurrentMaterial.MaterialOptions[Index] != 1) ? CurrentMaterial.Roughness[Index] : Mathf.Max(CurrentMaterial.Roughness[Index], 0.000001f);
                    TempMat.TransmittanceColor = CurrentMaterial.TransmissionColor[Index];
                    TempMat.MatType = (int)CurrentMaterial.MaterialOptions[Index];
                    TempMat.EmissionColor = CurrentMaterial.EmissionColor[Index];
                    TempMat.metallic = CurrentMaterial.Metallic[Index];
                    TempMat.specularTint = CurrentMaterial.SpecularTint[Index];
                    TempMat.sheen = CurrentMaterial.Sheen[Index];
                    TempMat.sheenTint = CurrentMaterial.SheenTint[Index];
                    TempMat.ClearCoat = CurrentMaterial.ClearCoat[Index];
                    TempMat.IOR = CurrentMaterial.IOR[Index];
                    TempMat.Thin = CurrentMaterial.Thin[Index];
                    TempMat.ClearCoatGloss = CurrentMaterial.ClearCoatGloss[Index];
                    TempMat.specTrans = CurrentMaterial.SpecTrans[Index];
                    TempMat.anisotropic = CurrentMaterial.Anisotropic[Index];
                    TempMat.diffTrans = CurrentMaterial.DiffTrans[Index];
                    TempMat.flatness = CurrentMaterial.Flatness[Index];
                    TempMat.Specular = CurrentMaterial.Specular[Index];
                    TempMat.scatterDistance = CurrentMaterial.ScatterDist[Index];
                    TempMat.IsSmoothness = CurrentMaterial.IsSmoothness[Index] ? 1 : 0;
                    if(CurrentMaterial.TilingChanged) {
                        string MatTile = AssetManager.data.Material[AssetManager.ShaderNames.IndexOf(CurrentMaterial.SharedMaterials[Index].shader.name)].BaseColorTex;
                        TempMat.AlbedoTextureScale = new Vector4(CurrentMaterial.SharedMaterials[Index].GetTextureScale(MatTile).x, CurrentMaterial.SharedMaterials[Index].GetTextureScale(MatTile).y, CurrentMaterial.SharedMaterials[Index].GetTextureOffset(MatTile).x, CurrentMaterial.SharedMaterials[Index].GetTextureOffset(MatTile).y);
                    }
                    _Materials[CurrentMaterial.MaterialIndex[i3]] = TempMat;
                    #if HardwareRT
                        if(CurrentMaterial.gameObject.GetComponent<Renderer>() != null) {
                            if(TempMat.specTrans == 1) AccelStruct.UpdateInstanceMask(CurrentMaterial.gameObject.GetComponent<Renderer>(), 0x2);
                            else AccelStruct.UpdateInstanceMask(CurrentMaterial.gameObject.GetComponent<Renderer>(), 0x1);
                        } else {
                            if(TempMat.specTrans == 1) AccelStruct.UpdateInstanceMask(CurrentMaterial.gameObject.GetComponent<SkinnedMeshRenderer>(), 0x2);
                            else AccelStruct.UpdateInstanceMask(CurrentMaterial.gameObject.GetComponent<SkinnedMeshRenderer>(), 0x1);
                        }
                    #endif
                    HasChangedMaterials = true;
                }
                CurrentMaterial.TilingChanged = false;
                CurrentMaterial.NeedsToUpdate = false;
            }
            MaterialsChanged.Clear();

            ParentCount = InstanceData.RenderQueue.Count;
            // UnityEngine.Profiling.Profiler.BeginSample("Update Materials");

            for (int i = 0; i < ParentCount; i++)
            {
                int ChildCount = InstanceData.RenderQueue[i].ChildObjects.Count;
                for (int i2 = 0; i2 < ChildCount; i2++)
                {
                    CurrentMaterial = InstanceData.RenderQueue[i].ChildObjects[i2];
                    if (CurrentMaterial.NeedsToUpdate || !didstart)
                    {
                        int MaterialCount = (int)Mathf.Min(CurrentMaterial.MaterialIndex.Length, CurrentMaterial.Indexes.Length);
                        for (int i3 = 0; i3 < MaterialCount; i3++)
                        {
                            int Index = CurrentMaterial.Indexes[i3];
                            TempMat = _Materials[CurrentMaterial.MaterialIndex[i3]];
                            TempMat.BaseColor = CurrentMaterial.BaseColor[Index];
                            TempMat.Emissive = CurrentMaterial.Emission[Index];
                            TempMat.Roughness = ((int)CurrentMaterial.MaterialOptions[Index] != 1) ? CurrentMaterial.Roughness[Index] : Mathf.Max(CurrentMaterial.Roughness[Index], 0.000001f);
                            TempMat.TransmittanceColor = CurrentMaterial.TransmissionColor[Index];
                            TempMat.MatType = (int)CurrentMaterial.MaterialOptions[Index];
                            TempMat.EmissionColor = CurrentMaterial.EmissionColor[Index];
                            TempMat.metallic = CurrentMaterial.Metallic[Index];
                            TempMat.specularTint = CurrentMaterial.SpecularTint[Index];
                            TempMat.sheen = CurrentMaterial.Sheen[Index];
                            TempMat.sheenTint = CurrentMaterial.SheenTint[Index];
                            TempMat.ClearCoat = CurrentMaterial.ClearCoat[Index];
                            TempMat.IOR = CurrentMaterial.IOR[Index];
                            TempMat.Thin = CurrentMaterial.Thin[Index];
                            TempMat.ClearCoatGloss = CurrentMaterial.ClearCoatGloss[Index];
                            TempMat.specTrans = CurrentMaterial.SpecTrans[Index];
                            TempMat.anisotropic = CurrentMaterial.Anisotropic[Index];
                            TempMat.diffTrans = CurrentMaterial.DiffTrans[Index];
                            TempMat.flatness = CurrentMaterial.Flatness[Index];
                            TempMat.Specular = CurrentMaterial.Specular[Index];
                            TempMat.scatterDistance = CurrentMaterial.ScatterDist[Index];
                            TempMat.IsSmoothness = CurrentMaterial.IsSmoothness[Index] ? 1 : 0;
                            _Materials[CurrentMaterial.MaterialIndex[i3]] = TempMat;
                            HasChangedMaterials = true;
                        }
                    }
                }
            }
            // UnityEngine.Profiling.Profiler.EndSample();
            return HasChangedMaterials;

        }
      

    }
}