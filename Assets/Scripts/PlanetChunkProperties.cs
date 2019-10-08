using UnityEngine;

public class PlanetChunkProperties
{
    public PlanetChunkProperties Parent;
    public Vector3 Center;
    public Bounds Bounds;
    public Quaternion Rotation;
    public PlanetChunkProperties[] Children;
    public PlanetChunkObject ChunkObject;

    public bool isSplit
    {
        get
        {
            if (Children == null)
                return false;
            else
                foreach (PlanetChunkProperties chunk in Children)
                {
                    if (chunk == null)
                        return false;
                    else if (chunk.ChunkObject == null)
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

    public Vector2 min;
    public int LODLevel = 0;
    public bool isSpliting = false;
    public int SplitCount = 0;
    public bool isMerged = true;
    public bool Active = false;

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
        get => Children;
        set => Children = value;
    }
}