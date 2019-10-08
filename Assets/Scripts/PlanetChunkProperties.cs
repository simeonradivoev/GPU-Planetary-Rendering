using UnityEngine;

public class PlanetChunkProperties
{
    public PlanetChunkProperties Parent;
    public Vector3 Center;
    public Bounds Bounds;
    public Quaternion Rotation;
    public PlanetCreator Planet;
    private PlanetChunkProperties[] chunks;
    public PlanetChunkObject Chunk;

    public bool isSplit
    {
        get
        {
            if (chunks == null)
                return false;
            else
                foreach (PlanetChunkProperties chunk in chunks)
                {
                    if (chunk == null)
                        return false;
                    else if (chunk.Chunk == null)
                        return false;
                    else
                    {
                        if (!chunk.Active)
                            return false;
                    }
                }

            return true;
        }
    }

    public float Size;
    public float maxGeoError;

    public float maxVerError =>
        (maxGeoError / Mathf.Sqrt(Chunk.GetComponent<Renderer>().bounds
             .SqrDistance(Planet.MainCamera.transform
                 .position)) /*Vector3.Distance(Planet.MainCamera.transform.position,Center * Planet.SphereRadius)*/
        ) * Planet.K;

    public bool needsSplit => maxVerError > Planet.MaxError;

    public Vector2 min;
    public int LODLevel = 0;
    public bool isSpliting = false;
    public int SplitCount = 0;
    public bool isMerged = true;
    public bool Active = false;

    public void ManageRecursive()
    {
        if (needsSplit && LODLevel < Planet.MaxLodLevel)
        {
            if (isSplit)
            {
                HideChunk();
                isSpliting = false;

                foreach (PlanetChunkProperties child in chunks)
                {
                    if (child != null)
                        child.ManageRecursive();
                }
            }
            else
            {
                Planet.AddToSplitPool(this);
            }
        }
        else
        {
            Merge();

        }


    }

    public void Merge()
    {
        if (!isMerged)
        {
            if (chunks != null)
                foreach (PlanetChunkProperties child in chunks)
                {
                    MergeChildren(child);
                }

            EnableChunk();
            isMerged = true;
        }
    }

    public void Split()
    {
        if (Chunk != null)
        {
            if (needsSplit)
            {
                if (chunks == null)
                {
                    chunks = new PlanetChunkProperties[4];
                    Planet.CreateChunk(this, min, 0);
                    Planet.CreateChunk(this, MiddleLeft, 1);
                    Planet.CreateChunk(this, BottomMiddle, 2);
                    Planet.CreateChunk(this, Middle, 3);
                }
                else
                {
                    foreach (PlanetChunkProperties child in chunks)
                    {
                        child.EnableChunk();
                    }
                }

                isMerged = false;
            }
        }
    }

    void MergeChildren(PlanetChunkProperties child)
    {
        if (!child.isMerged)
        {
            if (child.chunks != null)
            {
                foreach (PlanetChunkProperties c in child.chunks)
                {
                    MergeChildren(c);
                }
            }
        }

        child.DisableChunk();
        isMerged = true;
    }

    public void EnableChunk()
    {
        if (Chunk == null)
        {
            Chunk = Planet.GetChunk(this);
        }

        Chunk.GetComponent<Renderer>().enabled = true;
    }

    public void HideChunk()
    {
        if (Chunk != null)
        {
            if (Chunk.GetComponent<Renderer>().enabled)
            {
                Chunk.GetComponent<Renderer>().enabled = false;
            }
        }
    }

    public void DisableChunk()
    {
        if (Chunk.Collider != null)
            Chunk.Collider.enabled = false;

        HideChunk();
        Planet.AddToChunkPool(Chunk);
        Chunk = null;
        Active = false;
    }

    public Vector2 BottomLeft => min;

    public Vector2 BottomRight => min + Vector2.right * Size;

    public Vector2 BottomMiddle => min + Vector2.right * (Size / 2f);

    public Vector2 TopLeft => min + Vector2.up * Size;

    public Vector2 TopRight => min + Vector2.up * Size + Vector2.right * Size;

    public Vector2 TopMiddle => min + Vector2.up * Size + Vector2.right * (Size / 2f);

    public Vector2 Middle => min + Vector2.up * (Size / 2f) + Vector2.right * (Size / 2f);

    public Vector2 MiddleLeft => min + Vector2.up * (Size / 2f);

    public Vector2 MiddleRight => min + Vector2.right * Size + Vector2.up * (Size / 2f);

    public PlanetChunkProperties[] Chunks
    {
        get => chunks;
        set => chunks = value;
    }

    public PlanetChunkObject ChunkObject => Chunk;
}