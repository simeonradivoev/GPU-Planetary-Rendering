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
    public PlanetCreator planetCreator;

    private bool cursorLocked = true;
    private Quaternion rotation = Quaternion.identity;

    private void Start()
	{
		Cursor.lockState = CursorLockMode.Locked;
		Cursor.visible = false;
	}

    void Update()
    {
	    if (cursorLocked)
	    {
            transform.localRotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse X") * cameraSensitivity, Vector3.up);
            transform.localRotation *= Quaternion.AngleAxis(Input.GetAxis("Mouse Y") * cameraSensitivity, Vector3.left);

		    Vector3 direction;

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

			speedMultiply *= 1 + Input.GetAxis("Mouse ScrollWheel") * speedMultiplySpeed;
			speedMultiply = Mathf.Max(0, speedMultiply);


			if (Input.GetKey(KeyCode.Q))
            {
                transform.localRotation *= Quaternion.AngleAxis(rollSpeed * Time.deltaTime, Vector3.back);
            }
		    if (Input.GetKey(KeyCode.E))
		    {
                transform.localRotation *= Quaternion.AngleAxis(rollSpeed * Time.deltaTime,Vector3.forward);
            }
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

        if (Input.GetKeyDown(KeyCode.X))
        {
            Application.Quit();
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