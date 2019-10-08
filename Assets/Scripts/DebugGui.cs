using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;

public class DebugGui : MonoBehaviour
{
	[SerializeField] private AtmosphericScatter scatter;
	[SerializeField] private new FlyCamera camera;
	[SerializeField] private Transform orientationGizmo;
    [SerializeField] private PlanetCreator planetCreator;
	private float deltaTime;
	private bool showOrientationGizmo;
    private bool showDetailedInfo;

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
		string text = $"{msec:0.0} ms ({fps:0.} fps)";
		GUILayout.Label(text, style);
		GUILayout.Label("Speed: " + camera.speedMultiply, style);

        showDetailedInfo = GUILayout.Toggle(showDetailedInfo, "Show Detailed Info");
		showOrientationGizmo = GUILayout.Toggle(showOrientationGizmo, "Show Orientation");
		QualitySettings.vSyncCount = GUILayout.Toggle(QualitySettings.vSyncCount == 1, "Enable VSync") ? 1 : 0;
		GUILayout.BeginHorizontal();
		GUILayout.Label("Downsample");
		scatter.downscale = (int)GUILayout.HorizontalSlider(scatter.downscale, 1, 10,GUILayout.Width(100));
		GUILayout.EndHorizontal();

        if (showDetailedInfo)
        {
            Process proc = Process.GetCurrentProcess();

            GUILayout.Label($"Mesh Pool Size: {planetCreator.MeshPoolSize}");
            GUILayout.Label($"Split Pool Size: {planetCreator.SplitPoolSize}");
            GUILayout.Label($"Mono Memory Usage: {Profiler.GetMonoUsedSizeLong() * 1e-6:F0}mb");
            GUILayout.Label($"Process Memory Usage: {GC.GetTotalMemory(false) * 1e-6:F0}mb");
        }

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
