using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

[RequireComponent(typeof(Camera))]
public class AtmosphericScatter : MonoBehaviour
{
	public int downscale;
    public float SCALE = 1000.0f;

    const int TRANSMITTANCE_WIDTH = 256;
    const int TRANSMITTANCE_HEIGHT = 64;
    const int TRANSMITTANCE_CHANNELS = 3;

	const int IRRADIANCE_WIDTH = 64;
	const int IRRADIANCE_HEIGHT = 16;
	const int IRRADIANCE_CHANNELS = 3;

    const int INSCATTER_WIDTH = 256;
    const int INSCATTER_HEIGHT = 128;
    const int INSCATTER_DEPTH = 32;
    const int INSCATTER_CHANNELS = 4;

    public GameObject m_sun;
    //public RenderTexture m_skyMap;
    public Vector3 m_betaR = new Vector3(0.0058f, 0.0135f, 0.0331f);
    public float m_mieG = 0.75f;
    public float m_sunIntensity = 100.0f;
    public ComputeShader m_writeData;
    public Vector3 EarthPosition;
    public float RG = 6360.0f, RT = 6420.0f, RL = 6421.0f;
    public float HR = 8;
    public float HM = 1.2f;

	private CommandBuffer lightingBuffer;
	private new Camera camera;

	public float MinViewDistance = 3000;

	public RenderTexture m_transmittance;
	public RenderTexture m_inscatter;
	public RenderTexture m_irradiance;
	public Material m_atmosphereImageEffect;
	void Start()
	{
		Application.runInBackground = false;
		lightingBuffer = new CommandBuffer();
		camera = GetComponent<Camera>();
		camera.AddCommandBuffer(CameraEvent.BeforeLighting, lightingBuffer);
		camera.depthTextureMode = DepthTextureMode.DepthNormals;
        //m_skyMap.format = RenderTextureFormat.ARGBHalf; //must be floating point format

		CreateTextures();
		CopyDataToTextures();
		InitMaterial(m_atmosphereImageEffect);
		UpdateMaterialTextures(m_atmosphereImageEffect);
	}

	private void CreateTextures()
	{
		m_transmittance = new RenderTexture(TRANSMITTANCE_WIDTH, TRANSMITTANCE_HEIGHT, 0, RenderTextureFormat.ARGBHalf)
		{
		wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, enableRandomWrite = true
		};
		m_transmittance.Create();

		m_inscatter = new RenderTexture(INSCATTER_WIDTH, INSCATTER_HEIGHT, 0, RenderTextureFormat.ARGBHalf)
		{
			volumeDepth = INSCATTER_DEPTH,
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
			dimension = TextureDimension.Tex3D,
			enableRandomWrite = true
		};
		m_inscatter.Create();

		m_irradiance = new RenderTexture(IRRADIANCE_WIDTH, IRRADIANCE_HEIGHT, 0, RenderTextureFormat.ARGBHalf)
		{
		wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, enableRandomWrite = true
		};
		m_irradiance.Create();
	}

    private void CopyDataToTextures()
	{
		//Transmittance is responsible for the change in the sun color as it moves
		//The raw file is a 2D array of 32 bit floats with a range of 0 to 1
		string path = Application.streamingAssetsPath + "/Textures/transmittance.raw";
		ComputeBuffer buffer = new ComputeBuffer(TRANSMITTANCE_WIDTH * TRANSMITTANCE_HEIGHT, sizeof(float) * TRANSMITTANCE_CHANNELS);
		CBUtility.WriteIntoRenderTexture(m_transmittance, TRANSMITTANCE_CHANNELS, path, buffer, m_writeData);
		buffer.Release();

		//Inscatter is responsible for the change in the sky color as the sun moves
		//The raw file is a 4D array of 32 bit floats with a range of 0 to 1.589844
		//As there is not such thing as a 4D texture the data is packed into a 3D texture 
		//and the shader manually performs the sample for the 4th dimension
		path = Application.streamingAssetsPath + "/Textures/inscatter.raw";
		buffer = new ComputeBuffer(INSCATTER_WIDTH * INSCATTER_HEIGHT * INSCATTER_DEPTH, sizeof(float) * INSCATTER_CHANNELS);
		CBUtility.WriteIntoRenderTexture(m_inscatter, INSCATTER_CHANNELS, path, buffer, m_writeData);
		buffer.Release();

		//The raw file is a 2D array of 32 bit floats with a range of 0 to 1
		path = Application.streamingAssetsPath + "/Textures/irradiance.raw";
		buffer = new ComputeBuffer(IRRADIANCE_WIDTH * IRRADIANCE_HEIGHT, sizeof(float) * IRRADIANCE_CHANNELS);
		CBUtility.WriteIntoRenderTexture(m_irradiance, IRRADIANCE_CHANNELS, path, buffer, m_writeData);
		buffer.Release();
	}

    void Update()
    {
		UpdateMat(m_atmosphereImageEffect);
		camera.farClipPlane = Mathf.Max(MinViewDistance,(transform.position - transform.position.normalized * (RG * SCALE / 3f)).magnitude);

	    UpdateRenderBuffer();
    }

	private void UpdateRenderBuffer()
	{
		lightingBuffer.Clear();
		Matrix4x4 P = camera.projectionMatrix;
		Vector4 CamScreenDir = new Vector4(1f / P[0], 1f / P[5], 1f, 1f);
		lightingBuffer.SetGlobalVector("_CamScreenDir", CamScreenDir);
		Matrix4x4 viewProjInverse = (camera.projectionMatrix * camera.worldToCameraMatrix).inverse;
		lightingBuffer.SetGlobalMatrix("_ViewProjectInverse", viewProjInverse);
		Matrix4x4 CameraInv = camera.cameraToWorldMatrix.inverse;
		lightingBuffer.SetGlobalMatrix("_CameraInv", CameraInv);
		lightingBuffer.SetGlobalMatrix("_ViewMatrix", camera.worldToCameraMatrix);
		lightingBuffer.SetGlobalFloat("_FarPlane", camera.farClipPlane);
		lightingBuffer.SetGlobalTexture("_CameraDepthNormalsTexture", BuiltinRenderTextureType.GBuffer2);
		RenderTargetIdentifier active = new RenderTargetIdentifier(BuiltinRenderTextureType.GBuffer0);
		RenderTargetIdentifier target = new RenderTargetIdentifier(BuiltinRenderTextureType.CameraTarget);
		int downScaleTex = Shader.PropertyToID("_DownsampledTarget");
        lightingBuffer.GetTemporaryRT(downScaleTex, camera.pixelWidth / Mathf.Max(1, downscale),camera.pixelHeight / Mathf.Max(1, downscale),0,FilterMode.Bilinear,RenderTextureFormat.ARGBFloat);
		lightingBuffer.Blit(active, downScaleTex, m_atmosphereImageEffect);
        lightingBuffer.Blit(downScaleTex, target);
		lightingBuffer.ReleaseTemporaryRT(downScaleTex);
    }

    void UpdateMat(Material mat)
    {
		mat.SetVector("betaR", m_betaR / SCALE);
        mat.SetFloat("mieG", m_mieG);
        mat.SetVector("SUN_DIR", m_sun.transform.forward * -1);
        mat.SetFloat("SUN_INTENSITY", m_sunIntensity);
        mat.SetVector("EARTH_POS", EarthPosition);
        mat.SetVector("CAMERA_POS", transform.position);
		mat.SetVector("betaMSca",(Vector4.one * 4e-3f)/SCALE);
		mat.SetVector("betaMEx",(Vector4.one * 4e-3f * 0.9f)/SCALE);

        mat.SetFloat("SCALE", SCALE);
        mat.SetFloat("Rg", RG * SCALE);
        mat.SetFloat("Rt", RT * SCALE);
        mat.SetFloat("Rl", RL * SCALE);
        mat.SetFloat("HR", HR * SCALE);
		mat.SetFloat("HM", HM * SCALE);
    }

	void UpdateMaterialTextures(Material mat)
	{
		mat.SetTexture("_Transmittance", m_transmittance);
		mat.SetTexture("_Inscatter", m_inscatter);
		mat.SetTexture("_Irradiance", m_irradiance);
	}

    void InitMaterial(Material mat)
    {
		//Consts, best leave these alone
		mat.SetFloat("M_PI", Mathf.PI);
        mat.SetFloat("SCALE", SCALE);
        mat.SetFloat("Rg", 6360.0f * SCALE);
        mat.SetFloat("Rt", 6420.0f * SCALE);
        mat.SetFloat("Rl", 6421.0f * SCALE);
        mat.SetFloat("RES_R", 32.0f);
        mat.SetFloat("RES_MU", 128.0f);
        mat.SetFloat("RES_MU_S", 32.0f);
        mat.SetFloat("RES_NU", 8.0f);
        mat.SetFloat("SUN_INTENSITY", m_sunIntensity);
        mat.SetVector("SUN_DIR", m_sun.transform.forward * -1.0f);
    }

    void OnDestroy()
    {
		if(m_transmittance) m_irradiance.Release();
        if(m_transmittance) m_transmittance.Release();
        if(m_inscatter) m_inscatter.Release();
    }
}
