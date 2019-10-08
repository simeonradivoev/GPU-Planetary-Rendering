using System;
using UnityEngine;
using System.Collections;

public class FlyCamera : MonoBehaviour
{
    public float cameraSensitivity = 90;
    public float rollSpeed = 4;
    public float normalMoveSpeed = 10;
    public float slowMoveFactor = 0.25f;
    public float fastMoveFactor = 3;
	public float speedMultiplySpeed = 2;
	public float speedMultiply = 1;
	public float rotationRougness = 10;
	public PlanetCreator planetCreator;

    private bool cursorLocked = true;
	private Quaternion acumilatedRotation;

	private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

    void Update()
    {
	    if (cursorLocked)
	    {
		    transform.localRotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * cameraSensitivity * Time.deltaTime, Vector3.up);
		    transform.localRotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse Y") * cameraSensitivity * Time.deltaTime, Vector3.left);

		    Vector3 direction = Vector3.zero;

		    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
		    {
			    direction = transform.forward * (normalMoveSpeed * fastMoveFactor) * Input.GetAxis("Vertical") * Time.deltaTime * speedMultiply;
			    direction += transform.right * (normalMoveSpeed * fastMoveFactor) * Input.GetAxis("Horizontal") * Time.deltaTime * speedMultiply;
		    }
		    else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
		    {
				direction = transform.forward * (normalMoveSpeed * slowMoveFactor) * Input.GetAxis("Vertical") * Time.deltaTime * speedMultiply;
				direction += transform.right * (normalMoveSpeed * slowMoveFactor) * Input.GetAxis("Horizontal") * Time.deltaTime * speedMultiply;
		    }
		    else
		    {
				direction = transform.forward * normalMoveSpeed * Input.GetAxis("Vertical") * Time.deltaTime * speedMultiply;
				direction += transform.right * normalMoveSpeed * Input.GetAxis("Horizontal") * Time.deltaTime * speedMultiply;
		    }

			transform.position += direction;

			speedMultiply *= 1 + Input.GetAxis("Mouse ScrollWheel") * Time.deltaTime * speedMultiplySpeed;
			speedMultiply = Mathf.Max(0, speedMultiply);


			if (Input.GetKey(KeyCode.Q))
		    {
				acumilatedRotation *= Quaternion.AngleAxis(rollSpeed * Time.deltaTime, new Vector3(0, 0, 1));
		    }
		    if (Input.GetKey(KeyCode.E))
		    {
				acumilatedRotation *= Quaternion.AngleAxis(rollSpeed * Time.deltaTime, new Vector3(0, 0, -1));
		    }

		    transform.localRotation *= acumilatedRotation;
		    acumilatedRotation = Quaternion.Slerp(acumilatedRotation, Quaternion.identity, Time.deltaTime * rotationRougness);
	    }

	    if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
		{
			cursorLocked = !cursorLocked;
            Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !cursorLocked;
		}

	    if (Input.GetKeyDown(KeyCode.C))
	    {
		    ScreenCapture.CaptureScreenshot(Application.dataPath.Replace("Assets", "Screenshots") + string.Format("{0:yyyy-MM-dd_hh-mm-ss-tt}", DateTime.Now) + ".png",2);
	    }
	}

	private void OnDrawGizmos()
	{
		if(planetCreator == null) return;
		var nearestChunk = planetCreator.GetNearestChunkProperties(transform.position);
		if (nearestChunk != null)
		{
			//if(nearestChunk.Active)
				//Gizmos.DrawWireMesh(nearestChunk.Chunk.Filter.sharedMesh);

			Gizmos.DrawWireCube(nearestChunk.Bounds.center, nearestChunk.Bounds.size);

		}
	}
}