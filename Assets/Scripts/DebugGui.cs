using UnityEngine;
using System.Collections;
using UnityEditor;

public class DebugGui : MonoBehaviour
{
	[SerializeField] private AtmosphericScatter scatter;
	[SerializeField] private new FlyCamera camera;
	[SerializeField] private Transform orientationGizmo;
	private float deltaTime;
	private bool showOrientationGizmo;

	private void Start()
	{
		orientationGizmo.gameObject.SetActive(showOrientationGizmo);
    }

	private void OnGUI()
	{
		int h = Screen.height;

		GUIStyle style = new GUIStyle();
		style.alignment = TextAnchor.UpperLeft;
		style.fontSize = h * 2 / 100;
		style.normal.textColor = Color.white;
		float msec = deltaTime * 1000.0f;
		float fps = 1.0f / deltaTime;
		string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
		GUILayout.Label(text, style);
		GUILayout.Label("Speed: " + camera.speedMultiply, style);

		showOrientationGizmo = GUILayout.Toggle(showOrientationGizmo, "Show Orientation");
		QualitySettings.vSyncCount = GUILayout.Toggle(QualitySettings.vSyncCount == 1, "Enable VSync") ? 1 : 0;
		GUILayout.BeginHorizontal();
		GUILayout.Label("Downsample");
		scatter.downscale = (int)GUILayout.HorizontalSlider(scatter.downscale, 1, 10,GUILayout.Width(100));
		GUILayout.EndHorizontal();

		if (GUI.changed)
		{
			orientationGizmo.gameObject.SetActive(showOrientationGizmo);
        }
	}
	
	// Update is called once per frame
	void Update () {
		deltaTime += (Time.deltaTime - deltaTime) * 0.1f;

		orientationGizmo.transform.rotation = Quaternion.identity;
	}
}
