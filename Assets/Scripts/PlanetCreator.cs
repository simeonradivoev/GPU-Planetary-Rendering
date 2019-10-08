    using UnityEngine;
    using System.Collections;
    using System.Collections.Generic;

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
                AddToChunkPool(CreateBasicChunk());
            }


            StartCoroutine(ManageChunkSplit());
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

        //precompute atmosphere, normal and vertex data
        public void Precompute()
        {
            K = Screen.width / (2f * Mathf.Tan((65f / 2f) * Mathf.Deg2Rad));
            m_perlin = new ImprovedPerlinNoise(0);
            m_perlin.LoadResourcesFor3DNoise();

            Texture2D perm = m_perlin.GetPermutationTable2D();
            Texture2D grad = m_perlin.GetGradient3D();
            VertexComputeShader.SetTexture(0, "_PermTable2D", perm);
            VertexComputeShader.SetTexture(0, "_Gradient3D", grad);

            mat.SetTexture("_PermTable2D", perm);
            mat.SetTexture("_Gradient3D", grad);

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
            GetChunk(p);
            rootChunks.Add(p);
        }

        //Create a Chunk
        public void CreateChunk(PlanetChunkProperties parent, Vector2 min, int index)
        {
            parent.Chunks[index] =
                CreateChunkProperties(parent, parent.Rotation, parent.Size / 2f, parent.LODLevel + 1, min);
            parent.Chunks[index].Chunk = GetChunk(parent.Chunks[index]);
            parent.Chunks[index].Bounds = parent.Chunks[index].Chunk.GetComponent<MeshRenderer>().bounds;
        }

        //Create a basic chunk
        private PlanetChunkObject CreateBasicChunk()
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
        public PlanetChunkObject UpdateChunk(PlanetChunkProperties chunkProperties, PlanetChunkObject chunk)
        {
            chunk.transform.localPosition = Vector3.zero;
            chunk.Properties = chunkProperties;
            chunkProperties.Chunk = chunk;
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

        //Get free chunk from pool
        public PlanetChunkObject GetChunkFromPool()
        {
            while (MeshPool.Count > 0)
            {
                return MeshPool.Pop();
            }

            return null;
        }

        //Get Free Chunk if none exists, create
        public PlanetChunkObject GetChunk(PlanetChunkProperties chunkProperties)
        {
            PlanetChunkObject c = GetChunkFromPool();

            if (c == null)
            {
                c = CreateBasicChunk();
            }

            UpdateChunk(chunkProperties, c);

            return c;
        }

        //Update the Chunk mesh
        public void UpdateChunkMesh(PlanetChunkObject chunk)
        {
            if (chunk.Filter.sharedMesh == null)
            {
                chunk.Filter.sharedMesh = CopyMesh(BasicPlane);
            }

            Vector3[] vertices, normals;
            Vector2[] uv;
            int[] triangles = chunk.Filter.sharedMesh.triangles;

            CaluclateVertex(out vertices, out normals, out uv, triangles, chunk.Properties);
            ApplyMesh(chunk, vertices, normals, uv);

            /*Loom.RunAsync(() =>
                {
                    Loom.QueueOnMainThread(() =>
                    {
                    
                    });
                });*/
        }

        //Update the GPU shader noise
        private void UpdateNoise()
        {
            VertexComputeShader.SetFloat("_Frequency", TerrainFreq);
            VertexComputeShader.SetFloat("_Lacunarity", Terrainlacunarity);
            VertexComputeShader.SetFloat("_Gain", TerrainGain);

            mat.SetFloat("_Frequency", TerrainFreq);
            mat.SetFloat("_Lacunarity", Terrainlacunarity);
            mat.SetFloat("_Gain", TerrainGain);
            mat.SetFloat("_PlanetRadius", SphereRadius);

        }

        //ad to chunk pool
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
            chunk.Planet = this;

            chunk.LODLevel = LodLevel;
            chunk.Size = Size;
            chunk.min = min;
            chunk.Center = (chunk.Rotation * new Vector3(chunk.Middle.x - 0.5f, 1f, chunk.Middle.y - 0.5f)).normalized *
                           SphereRadius;

            chunk.maxGeoError = Mathf.Pow(2f, MaxLodLevel - chunk.LODLevel);
            return chunk;
        }


        private void ApplyMesh(PlanetChunkObject chunk, Vector3[] vertices, Vector3[] normals, Vector2[] uv)
        {
            chunk.name = "Recycled Mesh";
            chunk.Filter.mesh.vertices = vertices;
            chunk.Filter.mesh.normals = normals;
            chunk.Filter.mesh.uv = uv;
            chunk.Filter.mesh.RecalculateBounds();
            //chunk.Filter.mesh.RecalculateNormals();

            chunk.GetComponent<Renderer>().enabled = true;

            if (chunk.Properties != null)
            {

                if (chunk.Properties.LODLevel >= LodColliderStart)
                {
                    chunk.Collider.sharedMesh = null;
                    chunk.Collider.sharedMesh = chunk.Filter.mesh;
                    chunk.Collider.enabled = true;
                }
                else
                {
                    chunk.Collider.enabled = false;
                }

                chunk.Properties.Active = true;
            }

        }

        //Caluclate the elevation data and create it as a Mesh (on CPU to use Physics)
        public void CaluclateVertex(out Vector3[] vert, out Vector3[] normal, out Vector2[] uvs, int[] triangles,
            PlanetChunkProperties chunk)
        {

            int hCount2 = ChunkSegments + 2;
            int vCount2 = ChunkSegments + 2;
            int numVertices = hCount2 * vCount2;

            Vector3[] vertices = new Vector3[numVertices];
            Vector3[] normals = new Vector3[numVertices];
            Vector2[] uv = new Vector2[numVertices];
            float Scale = chunk.Size / (float) ChunkSegments;
            int index = 0;

            for (float y = 0; y < vCount2; y++)
            {
                for (float x = 0; x < hCount2; x++)
                {
                    float px = chunk.BottomLeft.x + x * Scale - 0.5f;
                    float py = chunk.BottomLeft.y + y * Scale - 0.5f;

                    Vector3 pos = new Vector3(px, 0.5f, py);

                    vertices[index] = GeVertex(chunk.Rotation, SphereRadius, px, py);
                    uv[index] = chunk.BottomLeft + new Vector2(x * Scale, y * Scale);
                    index++;
                }
            }

            VertexComputeBuffer.SetData(vertices);
            NormalComputeBuffer.SetData(normals);

            VertexComputeShader.SetFloat("Scale", Scale);
            VertexComputeShader.SetFloat("TerrainScale", TerrainScale);
            VertexComputeShader.SetFloat("TerrainBumpScale", TerrainBumpScale);

            VertexComputeShader.Dispatch(0, numVertices / 16, 1, 1);

            VertexComputeBuffer.GetData(vertices);
            NormalComputeBuffer.GetData(normals);

            /*
                int index = 0;
                float Scale = chunk.Size / (float)ChunkSegments;
                float uvFactor = chunk.Size / (float)ChunkSegments;
    
                for (float y = 0; y < vCount2; y++)
                {
                    for (float x = 0; x < hCount2; x++)
                    {
                        float px = chunk.BottomLeft.x + x * Scale - 0.5f;
                        float py = chunk.BottomLeft.y + y * Scale - 0.5f;
    
                        Vector3 pos = new Vector3(px, 0.5f, py);
    
                        vertices[index] = GeVertex(chunk.Rotation, SphereRadius, px, py);
    
                        uv[index] = chunk.BottomLeft + new Vector2(x * uvFactor, y * uvFactor);
    
                        Vector3 va = GeVertex(chunk.Rotation, SphereRadius, px + Scale, py);
                        Vector3 vb = GeVertex(chunk.Rotation, SphereRadius, px, py + Scale);
                        Vector3 vc = GeVertex(chunk.Rotation, SphereRadius, px - Scale, py);
                        Vector3 vd = GeVertex(chunk.Rotation, SphereRadius, px, py - Scale);
    
                        normals[index] = ((Vector3.Cross(va, vb) + Vector3.Cross(vb, vc) + Vector3.Cross(vc, vd) + Vector3.Cross(vd, va)) / -4).normalized;
                        index++;
                    }
                }*/

            vert = vertices;
            normal = normals;
            uvs = uv;

            //VertexComputeBuffer.Release();
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
            Mesh m = new Mesh();
            m.vertices = mesh.vertices;
            m.triangles = mesh.triangles;
            m.normals = mesh.normals;
            m.colors = mesh.colors;
            m.tangents = mesh.tangents;
            m.uv = mesh.uv;

            return m;
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

        //normalize plane
        private Vector3 ToSphere(Vector3 vector)
        {
            Vector3 output = new Vector3();
            output = vector.normalized;
            //output.x = vector.x * Mathf.Sqrt(1 - (vector.y * vector.y / 2f) - (vector.z * vector.z / 2f) - ((vector.y * vector.y * vector.z * vector.z / 3f)));
            //output.y = vector.x * Mathf.Sqrt(1 - (vector.z * vector.z / 2f) - (vector.x * vector.x / 2f) - ((vector.z * vector.z * vector.x * vector.x / 3f)));
            //output.z = vector.x * Mathf.Sqrt(1 - (vector.x * vector.x / 2f) - (vector.y * vector.y / 2f) - ((vector.x * vector.x * vector.y * vector.y / 3f)));
            return output;
        }

        private Vector3 SphericalPos(Vector3 pos, float radius)
        {
            return pos.normalized * radius;
        }

        private void Update()
        {
            ManageChunks();
            UpdateNoise();
        }

        private IEnumerator ManageChunkSplit()
        {
            while (true)
            {
                while (SplitPool.Count > 0)
                {
                    PlanetChunkProperties c = SplitPool.Dequeue();
                    c.Split();
                    yield return null;
                }

                yield return null;
            }
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
                chunk.ManageRecursive();
            }
        }

        private void OnDisable()
        {
            VertexComputeBuffer.Dispose();
            NormalComputeBuffer.Dispose();
            PlanetMapCreatorBuffer.Dispose();
        }
    }