using UnityEngine;

public class PlanetChunkObject : MonoBehaviour
{
	public PlanetChunkProperties Properties;
	public MeshFilter Filter;
	public MeshCollider Collider;
    public Renderer Renderer;

	private void OnDrawGizmos()
	{
		//Gizmos.color = Color.white;
		//Gizmos.DrawWireSphere(Properties.Bounds.center, 100 * Properties.Size);
		//Gizmos.DrawWireCube(Properties.Bounds.center, Properties.Bounds.size);
	}
}