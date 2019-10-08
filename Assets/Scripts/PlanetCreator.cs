    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Collections;
    using UnityEngine.Rendering;

    public class PlanetCreator : MonoBehaviour
    {
        public Transform Sun; //Transform of the Sun (to be used in shader)
        public int ChunkSegments = 32; //Segments of chunks
        public float SphereRadius = 10f; //Radius of Sphere
        public float ChunkSize = 128f; //Physical size of chunks
        public Material mat; //Ground mat
        public float TerrainFreq = 0.006f; //Freq  of ground noise
        public float TerrainGain = 0.5f; //Grain of ground noise
        public float Terrainlacunarity = 2.0f; //Lacunarity of Ground noise
        public float TerrainScale = 20f; //scale of terrain height
        public float TerrainBumpScale = 20; //bumpiness of terrain normals
        public Camera MainCamera; //main camera
        private List<PlanetChunkProperties> rootChunks; //chunk array
        public int MaxLodLevel = 7; //max lod division level
        public int LodColliderStart = 5; //what level to start collision generation
        public float MaxError = 1; //max lod error
        public float MaxSplitsPerSecond = 20;

        public float K { get; set; }

        private Mesh BasicPlane;

        private Stack<PlanetChunkObject> MeshPool; //Mesh Pool
        private Queue<PlanetChunkProperties> SplitPool; //Split pool

        public int PoolStartPopulation;
        public ComputeBuffer VertexComputeBuffer; //Compute Vertex positions
        public ComputeBuffer NormalComputeBuffer; //Compute Normal positions  

        public ComputeBuffer PlanetMapCreatorBuffer; //Some calculations for noises

        public ComputeShader VertexComputeShader; //Vertex compute shader
        public ComputeShader PlanetMapGeneratorShader; //Final Compute shader

        private ImprovedPerlinNoise m_perlin; //gpu perlin noise

        public int MeshPoolSize => MeshPool.Count;
        public int SplitPoolSize => SplitPool.Count;

        //used to avoid GC
        private readonly List<Vector2> uvsTmp = new List<Vector2>();
        private readonly List<Vector3> vertexTmp = new List<Vector3>();
        private readonly List<Vector3> normalTmp = new List<Vector3>();
        private float lastSplitTime;

        private static readonly int Frequency = Shader.PropertyToID("_Frequency");
        private static readonly int Lacunarity = Shader.PropertyToID("_Lacunarity");
        private static readonly int Gain = Shader.PropertyToID("_Gain");
        private static readonly int PlanetRadius = Shader.PropertyToID("_PlanetRadius");
        private static readonly int PermTable2D = Shader.PropertyToID("_PermTable2D");
        private static readonly int Gradient3D = Shader.PropertyToID("_Gradient3D");

        public void Start()
        {
            Precompute();
            MeshPool = new Stack<PlanetChunkObject>();
            SplitPool = new Queue<PlanetChunkProperties>();
            BasicPlane = GeneratePremadePlane(ChunkSegments);

            rootChunks = new List<PlanetChunkProperties>();

            CreateRoot(Vector3.left, 0);
            CreateRoot(Vector3.left, 90);
            CreateRoot(Vector3.left, 180);
            CreateRoot(Vector3.left, 270);

            CreateRoot(Vector3.forward, 90);
            CreateRoot(Vector3.forward, 270);

            for (int i = 0; i < PoolStartPopulation; i++)
            {
                AddToChunkPool(CreateBasicChunkObject());
            }

            //CreateTemperatureMap();
        }

        private void CreateTemperatureMap()
        {
            for (int i = 0; i < 4; i++)
            {
                Texture2D Texture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
                Vector3[] a = new Vector3[1024 * 1024];
                Color32[] b = new Color32[1024 * 1024];

                int index = 0;
                for (int x = 0; x < 1024; x++)
                {
                    for (int y = 0; y < 1024; y++)
                    {
                        a[index++] = new Vector3(x, y, 0);
                    }
                }


                PlanetMapCreatorBuffer.SetData(a);
                PlanetMapGeneratorShader.Dispatch(0, 1024 * 1024, 1, 1);
                PlanetMapCreatorBuffer.GetData(a);

                for (int n = 0; n < 1024 * 1024; n++)
                {
                    b[n] = new Color32((byte) (a[n].x * 255), 0, 0, 255);
                }

                Texture.SetPixels32(b);

                using (System.IO.FileStream f =
                    new System.IO.FileStream("D:/Test/" + i + ".png", System.IO.FileMode.OpenOrCreate))
                {
                    System.IO.BinaryWriter br = new System.IO.BinaryWriter(f);
                    br.Write(Texture.EncodeToPNG());
                }
            }
        }

        /// <summary>
        /// precompute atmosphere, normal and vertex data
        /// </summary>
        public void Precompute()
        {
            K = Screen.width / (2f * Mathf.Tan((65f / 2f) * Mathf.Deg2Rad));
            m_perlin = new ImprovedPerlinNoise(0);
            m_perlin.LoadResourcesFor3DNoise();

            Texture2D perm = m_perlin.GetPermutationTable2D();
            Texture2D grad = m_perlin.GetGradient3D();
            VertexComputeShader.SetTexture(0, "_PermTable2D", perm);
            VertexComputeShader.SetTexture(0, "_Gradient3D", grad);

            mat.SetTexture(PermTable2D, perm);
            mat.SetTexture(Gradient3D, grad);

            VertexComputeBuffer = new ComputeBuffer((ChunkSegments + 2) * (ChunkSegments + 2), 12);
            NormalComputeBuffer = new ComputeBuffer((ChunkSegments + 2) * (ChunkSegments + 2), 12);
            PlanetMapCreatorBuffer = new ComputeBuffer(1024 * 1024, 32);

            VertexComputeShader.SetBuffer(0, "vertexBuffer", VertexComputeBuffer);
            VertexComputeShader.SetBuffer(0, "normalBuffer", NormalComputeBuffer);

            PlanetMapGeneratorShader.SetBuffer(0, "Output", PlanetMapCreatorBuffer);
            PlanetMapGeneratorShader.SetTexture(0, "_PermTable2D", perm);
            PlanetMapGeneratorShader.SetTexture(0, "_Gradient3D", grad);

            UpdateNoise();
        }

        //Create the root planet node
        public void CreateRoot(Vector3 rotationDir, float angle)
        {
            PlanetChunkProperties p =
                CreateChunkProperties(null, Quaternion.AngleAxis(angle, rotationDir), 1f, 0, Vector2.zero);
            GetFreeChunkObject(p);
            rootChunks.Add(p);
        }

        //Create a Chunk
        public void CreateChunk(PlanetChunkProperties parent, Vector2 min, int index)
        {
            parent.Chunks[index] =
                CreateChunkProperties(parent, parent.Rotation, parent.Size / 2f, parent.LODLevel + 1, min);
            parent.Chunks[index].ChunkObject = GetFreeChunkObject(parent.Chunks[index]);
            parent.Chunks[index].Bounds = parent.Chunks[index].ChunkObject.GetComponent<MeshRenderer>().bounds;
        }

        /// <summary>
        /// Create a basic chunk
        /// </summary>
        private PlanetChunkObject CreateBasicChunkObject()
        {
            GameObject chunkObject = new GameObject("Chunk", typeof(MeshFilter), typeof(MeshCollider),
                typeof(MeshRenderer), typeof(PlanetChunkObject));

            PlanetChunkObject chunk = chunkObject.GetComponent<PlanetChunkObject>();
            chunk.Filter = chunk.GetComponent<MeshFilter>();
            chunk.Collider = chunk.GetComponent<MeshCollider>();
            chunk.Renderer = chunk.GetComponent<Renderer>();
            chunk.Renderer.sharedMaterial = mat;
            return chunk;
        }

        //Update the chunk's position
        public PlanetChunkObject UpdateChunkObject(PlanetChunkProperties chunkProperties, PlanetChunkObject chunk)
        {
            chunk.transform.localPosition = Vector3.zero;
            chunk.Properties = chunkProperties;
            chunkProperties.ChunkObject = chunk;
            UpdateChunkMesh(chunk);
            return chunk;
        }

        public PlanetChunkProperties GetNearestChunkProperties(Vector3 point)
        {
            if (rootChunks == null) return null;
            float nearestDistance = float.PositiveInfinity;
            PlanetChunkProperties nearestChunk = null;
            foreach (var rootChunk in rootChunks)
            {
                float d = (rootChunk.Bounds.center - point).sqrMagnitude;
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearestChunk = rootChunk;
                }
            }

            return GetNearestChunkProperties(nearestChunk, point);
        }

        private PlanetChunkProperties GetNearestChunkProperties(PlanetChunkProperties parent, Vector3 point)
        {
            if (parent.Chunks == null) return parent;
            float nearestDistance = float.PositiveInfinity;
            PlanetChunkProperties nearestChunk = null;
            foreach (var chunk in parent.Chunks)
            {
                float d = (chunk.Bounds.center - point).sqrMagnitude;
                if (d < nearestDistance)
                {
                    nearestChunk = chunk;
                    nearestDistance = d;
                }
            }

            return GetNearestChunkProperties(nearestChunk, point);
        }

        /// <summary>
        /// Get free chunk from pool. Returns null if not exists.
        /// </summary>
        public PlanetChunkObject GetChunkObjectFromPool()
        {
            while (MeshPool.Count > 0)
            {
                return MeshPool.Pop();
            }

            return null;
        }

        /// <summary>
        /// Get Free Chunk object if none exists, create
        /// </summary>
        public PlanetChunkObject GetFreeChunkObject(PlanetChunkProperties chunkProperties)
        {
            PlanetChunkObject c = GetChunkObjectFromPool();

            if (c == null)
            {
                c = CreateBasicChunkObject();
            }

            UpdateChunkObject(chunkProperties, c);

            return c;
        }

        /// <summary>
        /// Update the Chunk mesh
        /// </summary>
        public void UpdateChunkMesh(PlanetChunkObject chunk)
        {
            if (chunk.Filter.sharedMesh == null)
            {
                chunk.Filter.sharedMesh = CopyMesh(BasicPlane);
            }

            CaluclateVertex(chunk);
        }

        /// <summary>
        /// Update the GPU shader noise
        /// </summary>
        private void UpdateNoise()
        {
            VertexComputeShader.SetFloat(Frequency, TerrainFreq);
            VertexComputeShader.SetFloat(Lacunarity, Terrainlacunarity);
            VertexComputeShader.SetFloat(Gain, TerrainGain);

            mat.SetFloat(Frequency, TerrainFreq);
            mat.SetFloat(Lacunarity, Terrainlacunarity);
            mat.SetFloat(Gain, TerrainGain);
            mat.SetFloat(PlanetRadius, SphereRadius);
        }

        /// <summary>
        /// add to chunk pool
        /// </summary>
        public void AddToChunkPool(PlanetChunkObject chunk)
        {
            MeshPool.Push(chunk);
        }

        public PlanetChunkProperties CreateChunkProperties(PlanetChunkProperties parent, Quaternion rotation,
            float Size, int LodLevel, Vector2 min)
        {
            PlanetChunkProperties chunk = new PlanetChunkProperties();
            chunk.Rotation = rotation;
            chunk.Parent = parent;

            chunk.LODLevel = LodLevel;
            chunk.Size = Size;
            chunk.min = min;
            chunk.Center = (chunk.Rotation * new Vector3(chunk.Middle.x - 0.5f, 1f, chunk.Middle.y - 0.5f)).normalized *
                           SphereRadius;

            chunk.maxGeoError = Mathf.Pow(2f, MaxLodLevel - chunk.LODLevel);
            return chunk;
        }

        /// <summary>
        /// Calculate the elevation data and create it as a Mesh (on CPU to use Physics)
        /// </summary>
        public void CaluclateVertex(PlanetChunkObject chunk)
        {
            int hCount2 = ChunkSegments + 2;
            int vCount2 = ChunkSegments + 2;
            int numVertices = hCount2 * vCount2;

            vertexTmp.Clear();
            normalTmp.Clear();
            uvsTmp.Clear();

            float Scale = chunk.Properties.Size / (float) ChunkSegments;

            for (float y = 0; y < vCount2; y++)
            {
                for (float x = 0; x < hCount2; x++)
                {
                    float px = chunk.Properties.BottomLeft.x + x * Scale - 0.5f;
                    float py = chunk.Properties.BottomLeft.y + y * Scale - 0.5f;

                    vertexTmp.Add(GeVertex(chunk.Properties.Rotation, SphereRadius, px, py));
                    uvsTmp.Add(chunk.Properties.BottomLeft + new Vector2(x * Scale, y * Scale));
                }
            }

            VertexComputeBuffer.SetData(vertexTmp);
            NormalComputeBuffer.SetData(normalTmp);

            VertexComputeShader.SetFloat("Scale", Scale);
            VertexComputeShader.SetFloat("TerrainScale", TerrainScale);
            VertexComputeShader.SetFloat("TerrainBumpScale", TerrainBumpScale);

            VertexComputeShader.Dispatch(0, numVertices / 16, 1, 1);

            AsyncGPUReadback.Request(VertexComputeBuffer, chunk.ApplyVertexData);
            AsyncGPUReadback.Request(NormalComputeBuffer, chunk.ApplyNormalData);
            chunk.MarkCalculatingVertexData();

            chunk.name = "Recycled Mesh";
            chunk.Filter.sharedMesh.SetUVs(0, uvsTmp);
            chunk.SetVisible(true);

            if (chunk.Properties != null)
            {
                if (chunk.Properties.LODLevel >= LodColliderStart)
                {
                    chunk.Collider.sharedMesh = null;
                    chunk.Collider.sharedMesh = chunk.Filter.sharedMesh;
                    chunk.Collider.enabled = true;
                }
                else
                {
                    chunk.Collider.enabled = false;
                }

                chunk.Properties.Active = true;
            }
        }

        public Vector3 GeVertex(Quaternion rotation, float SphereRadius, float X, float Y)
        {
            Vector3 pos = rotation * new Vector3(X, 0.5f, Y);
            Vector3 posN = ToSphere(pos);
            Vector3 posS = posN * SphereRadius;
            return posS;
        }

        public static void calculateMeshTangents(Mesh mesh)
        {
            //speed up math by copying the mesh arrays
            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            Vector3[] normals = mesh.normals;

            //variable definitions
            int triangleCount = triangles.Length;
            int vertexCount = vertices.Length;

            Vector3[] tan1 = new Vector3[vertexCount];
            Vector3[] tan2 = new Vector3[vertexCount];

            Vector4[] tangents = new Vector4[vertexCount];

            for (long a = 0; a < triangleCount; a += 3)
            {
                long i1 = triangles[a + 0];
                long i2 = triangles[a + 1];
                long i3 = triangles[a + 2];

                Vector3 v1 = vertices[i1];
                Vector3 v2 = vertices[i2];
                Vector3 v3 = vertices[i3];

                Vector2 w1 = uv[i1];
                Vector2 w2 = uv[i2];
                Vector2 w3 = uv[i3];

                float x1 = v2.x - v1.x;
                float x2 = v3.x - v1.x;
                float y1 = v2.y - v1.y;
                float y2 = v3.y - v1.y;
                float z1 = v2.z - v1.z;
                float z2 = v3.z - v1.z;

                float s1 = w2.x - w1.x;
                float s2 = w3.x - w1.x;
                float t1 = w2.y - w1.y;
                float t2 = w3.y - w1.y;

                float r = 1.0f / (s1 * t2 - s2 * t1);

                Vector3 sdir = new Vector3((t2 * x1 - t1 * x2) * r, (t2 * y1 - t1 * y2) * r, (t2 * z1 - t1 * z2) * r);
                Vector3 tdir = new Vector3((s1 * x2 - s2 * x1) * r, (s1 * y2 - s2 * y1) * r, (s1 * z2 - s2 * z1) * r);

                tan1[i1] += sdir;
                tan1[i2] += sdir;
                tan1[i3] += sdir;

                tan2[i1] += tdir;
                tan2[i2] += tdir;
                tan2[i3] += tdir;
            }


            for (long a = 0; a < vertexCount; ++a)
            {
                Vector3 n = normals[a];
                Vector3 t = tan1[a];

                //Vector3 tmp = (t - n * Vector3.Dot(n, t)).normalized;
                //tangents[a] = new Vector4(tmp.x, tmp.y, tmp.z);
                Vector3.OrthoNormalize(ref n, ref t);
                tangents[a].x = t.x;
                tangents[a].y = t.y;
                tangents[a].z = t.z;

                tangents[a].w = (Vector3.Dot(Vector3.Cross(n, t), tan2[a]) < 0.0f) ? -1.0f : 1.0f;
            }

            mesh.tangents = tangents;
        }


        public static Mesh CopyMesh(Mesh mesh)
        {
            return Instantiate(mesh);
        }

        //Fast Plane Generation
        public Mesh GeneratePremadePlane(int Segments)
        {
            Mesh plane = new Mesh();

            int points = Segments + 2;
            int numTriangles = Segments * Segments * 6;
            int numVertices = points * points;

            int index = 0;
            float uvFactorX = 1.0f / (float) Segments;
            float uvFactorY = 1.0f / (float) Segments;
            float scaleX = 1f / (float) Segments;
            float scaleY = 1f / (float) Segments;

            int[] triangles = new int[numTriangles];
            Vector2[] uvs = new Vector2[numVertices];
            Vector3[] vertices = new Vector3[numVertices];

            for (float y = 0; y < points; y++)
            {
                for (float x = 0; x < points; x++)
                {

                    float px = x * scaleX - 0.5f;
                    float py = y * scaleY - 0.5f;

                    uvs[index] = new Vector2(x * uvFactorX, y * uvFactorY);
                    vertices[index++] = new Vector3(px, 1f, py);
                }
            }

            index = 0;
            for (int y = 0; y < Segments; y++)
            {
                for (int x = 0; x < Segments; x++)
                {
                    triangles[index] = (y * points) + x;
                    triangles[index + 1] = ((y + 1) * points) + x;
                    triangles[index + 2] = (y * points) + x + 1;

                    triangles[index + 3] = ((y + 1) * points) + x;
                    triangles[index + 4] = ((y + 1) * points) + x + 1;
                    triangles[index + 5] = (y * points) + x + 1;
                    index += 6;
                }
            }

            plane.vertices = vertices;
            plane.triangles = triangles;
            plane.uv = uvs;
            plane.RecalculateNormals();
            plane.RecalculateBounds();
            calculateMeshTangents(plane);
            calculateMeshTangents(plane);

            return plane;
        }

        public static Bounds CopyBounds(Bounds bounds)
        {
            Bounds b = new Bounds();
            b.min = bounds.min;
            b.max = bounds.max;
            b.size = bounds.size;
            b.center = bounds.center;
            return b;
        }

        /// <summary>
        /// normalize plane
        /// </summary>
        private Vector3 ToSphere(Vector3 vector)
        {
            return vector.normalized;
        }

        private Vector3 SphericalPos(Vector3 pos, float radius)
        {
            return pos.normalized * radius;
        }

        private void Update()
        {
            ManageChunks();
            UpdateNoise();

            if (SplitPool.Count > 0 && lastSplitTime - Time.time <= 0)
            {
                PlanetChunkProperties c = SplitPool.Dequeue();
                Split(c);
                lastSplitTime = Time.time + 1f / MaxSplitsPerSecond;
            }
        }

        public bool NeedsSplit(PlanetChunkProperties chunk)
        {
            var maxVerError =
                (chunk.maxGeoError / Mathf.Sqrt(chunk.ChunkObject.Renderer.bounds
                     .SqrDistance(MainCamera.transform
                         .position)) /*Vector3.Distance(Planet.MainCamera.transform.position,Center * Planet.SphereRadius)*/
                ) * K;

            return maxVerError > MaxError;
        }

        public void AddToSplitPool(PlanetChunkProperties chunk)
        {
            if (!SplitPool.Contains(chunk))
            {
                SplitPool.Enqueue(chunk);
            }
        }

        private void ManageChunks()
        {
            foreach (PlanetChunkProperties chunk in rootChunks)
            {
                ManageRecursive(chunk);
            }
        }

        public void ManageRecursive(PlanetChunkProperties chunk)
        {
            if (NeedsSplit(chunk) && chunk.LODLevel < MaxLodLevel)
            {
                if (chunk.isSplit)
                {
                    bool childrenGeneratingFlag = false;

                    foreach (PlanetChunkProperties child in chunk.Children)
                    {
                        childrenGeneratingFlag |= child.ChunkObject.IsCalculating;
                        ManageRecursive(child);
                    }

                    if (!childrenGeneratingFlag)
                    {
                        HideChunk(chunk);
                    }

                    chunk.isSpliting = false;
                }
                else
                {
                    AddToSplitPool(chunk);
                }
            }
            else
            {
                Merge(chunk);
            }
        }

        public void Split(PlanetChunkProperties chunk)
        {
            if (chunk.ChunkObject != null)
            {
                if (NeedsSplit(chunk))
                {
                    if (chunk.Children == null)
                    {
                        chunk.Children = new PlanetChunkProperties[4];
                        CreateChunk(chunk, chunk.min, 0);
                        CreateChunk(chunk, chunk.MiddleLeft, 1);
                        CreateChunk(chunk, chunk.BottomMiddle, 2);
                        CreateChunk(chunk, chunk.Middle, 3);
                    }
                    else
                    {
                        foreach (PlanetChunkProperties child in chunk.Children)
                        {
                            EnableChunk(child);
                        }
                    }

                    chunk.isMerged = false;
                }
            }
        }

        public void Merge(PlanetChunkProperties chunk)
        {
            if (!chunk.isMerged)
            {
                if (chunk.Children != null)
                {
                    foreach (PlanetChunkProperties child in chunk.Children)
                    {
                        MergeChildren(chunk, child);
                    }
                }

                EnableChunk(chunk);
                chunk.isMerged = true;
            }
        }

        void MergeChildren(PlanetChunkProperties parent, PlanetChunkProperties child)
        {
            if (!child.isMerged)
            {
                if (child.Children != null)
                {
                    foreach (PlanetChunkProperties c in child.Children)
                    {
                        //recursively merge
                        MergeChildren(parent, c);
                    }
                }
            }

            DisableChunk(child);
            parent.isMerged = true;
        }

        public void EnableChunk(PlanetChunkProperties chunk)
        {
            if (chunk.ChunkObject == null)
            {
                chunk.ChunkObject = GetFreeChunkObject(chunk);
            }

            chunk.ChunkObject.SetVisible(true);
        }

        public void HideChunk(PlanetChunkProperties chunk)
        {
            if (chunk.ChunkObject != null)
            {
                if (chunk.ChunkObject.IsVisible)
                {
                    chunk.ChunkObject.SetVisible(false);
                }
            }
        }

        public void DisableChunk(PlanetChunkProperties chunk)
        {
            if (chunk.ChunkObject.Collider != null)
                chunk.ChunkObject.Collider.enabled = false;

            HideChunk(chunk);
            AddToChunkPool(chunk.ChunkObject);
            chunk.ChunkObject = null;
            chunk.Active = false;
        }

        private void OnDisable()
        {
            VertexComputeBuffer.Dispose();
            NormalComputeBuffer.Dispose();
            PlanetMapCreatorBuffer.Dispose();
        }
    }