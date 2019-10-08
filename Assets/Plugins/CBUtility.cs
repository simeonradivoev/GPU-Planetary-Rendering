using UnityEngine;
using System.Collections;
using System.IO;

static public class CBUtility
{

	static public void ReadFromRenderTexture(RenderTexture tex, int channels, ComputeBuffer buffer, ComputeShader readData)
	{
		if(tex == null)
		{
			Debug.Log("CBUtility::ReadFromRenderTexture - RenderTexture is null");
			return;
		}
		
		if(buffer == null)
		{
			Debug.Log("CBUtility::ReadFromRenderTexture - buffer is null");
			return;
		}
		
		if(readData == null)
		{
			Debug.Log("CBUtility::ReadFromRenderTexture - Computer shader is null");
			return;
		}
		
		if(channels < 1 || channels > 4)
		{
			Debug.Log("CBUtility::ReadFromRenderTexture - Channels must be 1, 2, 3, or 4");
			return;
		}
		
		int kernel = -1;
		int depth = 1;
		string D = "2D";
		string C = "C" + channels.ToString();
		
		if(tex.isVolume)
		{
			depth = tex.volumeDepth;
			D = "3D";
		}
		
		kernel = readData.FindKernel("read"+D+C);
		
		if(kernel == -1)
		{
			Debug.Log("CBUtility::ReadFromRenderTexture - could not find kernel " + "read"+D+C);
			return;
		}
		
		int width = tex.width;
		int height = tex.height;
		
		//set the compute shader uniforms
		readData.SetTexture(kernel, "_Tex"+D, tex);
		readData.SetInt("_Width", width);
		readData.SetInt("_Height", height);
		readData.SetBuffer(kernel, "_Buffer"+D+C, buffer); //tex will be write into this
		//run the  compute shader     
		readData.Dispatch(kernel, Mathf.Max(1, width/8), Mathf.Max(1, height/8), Mathf.Max(1, depth/8));

	}
	
	static public void WriteIntoRenderTexture(RenderTexture tex, int channels, ComputeBuffer buffer, ComputeShader writeData)
	{
		if(tex == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - RenderTexture is null");
			return;
		}
		
		if(buffer == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - buffer is null");
			return;
		}
		
		if(writeData == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - Computer shader is null");
			return;
		}
		
		if(channels < 1 || channels > 4)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - Channels must be 1, 2, 3, or 4");
			return;
		}
		
		if(!tex.enableRandomWrite)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - you must enable random write on render texture");
			return;
		}
		
		int kernel = -1;
		int depth = 1;
		string D = "2D";
		string C = "C" + channels.ToString();
		
		if(tex.isVolume)
		{
			depth = tex.volumeDepth;
			D = "3D";
		}
		
		kernel = writeData.FindKernel("write"+D+C);
		
		if(kernel == -1)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - could not find kernel " + "write"+D+C);
			return;
		}
		
		int width = tex.width;
		int height = tex.height;
		
		//set the compute shader uniforms
		writeData.SetTexture(kernel, "_Des"+D+C, tex);
		writeData.SetInt("_Width", width);
		writeData.SetInt("_Height", height);
		writeData.SetBuffer(kernel, "_Buffer"+D+C, buffer);
		//run the  compute shader     
		writeData.Dispatch(kernel, Mathf.Max(1, width/8), Mathf.Max(1, height/8), Mathf.Max(1, depth/8));
	}
	
	static public void WriteIntoRenderTexture(RenderTexture tex, int channels, string path, ComputeBuffer buffer, ComputeShader writeData)
	{
		if(tex == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - RenderTexture is null");
			return;
		}
		
		if(buffer == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - buffer is null");
			return;
		}
		
		if(writeData == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - Computer shader is null");
			return;
		}
		
		if(channels < 1 || channels > 4)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - Channels must be 1, 2, 3, or 4");
			return;
		}
		
		if(!tex.enableRandomWrite)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - you must enable random write on render texture");
			return;
		}
		
		int kernel = -1;
		int depth = 1;
		string D = "2D";
		string C = "C" + channels.ToString();
		
		if(tex.isVolume)
		{
			depth = tex.volumeDepth;
			D = "3D";
		}
		
		kernel = writeData.FindKernel("write"+D+C);
		
		if(kernel == -1)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - could not find kernel " + "write"+D+C);
			return;
		}
		
		int width = tex.width;
		int height = tex.height;
		int size = width*height*depth*channels;
		
		float[] map = new float[size];
		
		if(!LoadRawFile(path, map, size)) return;
		
		buffer.SetData(map);
		
		//set the compute shader uniforms
		writeData.SetTexture(kernel, "_Des"+D+C, tex);
		writeData.SetInt("_Width", width);
		writeData.SetInt("_Height", height);
		writeData.SetBuffer(kernel, "_Buffer"+D+C, buffer);
		//run the  compute shader     
		writeData.Dispatch(kernel, Mathf.Max(1, width/8), Mathf.Max(1, height/8), Mathf.Max(1, depth/8));
	}

	static public void WriteIntoRenderTexture(RenderTexture tex, int channels, TextAsset raw, ComputeBuffer buffer, ComputeShader writeData)
	{
		if (tex == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - RenderTexture is null");
			return;
		}

		if (buffer == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - buffer is null");
			return;
		}

		if (writeData == null)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - Computer shader is null");
			return;
		}

		if (channels < 1 || channels > 4)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - Channels must be 1, 2, 3, or 4");
			return;
		}

		if (!tex.enableRandomWrite)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - you must enable random write on render texture");
			return;
		}

		int kernel = -1;
		int depth = 1;
		string D = "2D";
		string C = "C" + channels.ToString();

		if (tex.isVolume)
		{
			depth = tex.volumeDepth;
			D = "3D";
		}

		kernel = writeData.FindKernel("write" + D + C);

		if (kernel == -1)
		{
			Debug.Log("CBUtility::WriteIntoRenderTexture - could not find kernel " + "write" + D + C);
			return;
		}

		int width = tex.width;
		int height = tex.height;
		int size = width * height * depth * channels;

		float[] map = new float[size];

		if (!LoadRawFile(raw, map, size)) return;

		buffer.SetData(map);

		//set the compute shader uniforms
		writeData.SetTexture(kernel, "_Des" + D + C, tex);
		writeData.SetInt("_Width", width);
		writeData.SetInt("_Height", height);
		writeData.SetBuffer(kernel, "_Buffer" + D + C, buffer);
		//run the  compute shader     
		writeData.Dispatch(kernel, Mathf.Max(1, width / 8), Mathf.Max(1, height / 8), Mathf.Max(1, depth / 8));
	}

	static bool LoadRawFile(string path, float[] map, int size) 
	{	
		FileInfo fi = new FileInfo(path);
		
		if(fi == null)
		{
			Debug.Log("CBUtility::LoadRawFile - Raw file not found");
			return false;
		}
		
		FileStream fs = fi.OpenRead();
		
		byte[] data = new byte[fi.Length];
		fs.Read(data, 0, (int)fi.Length);
		fs.Close();
		
		//divide by 4 as there are 4 bytes in a 32 bit float
		if(size > fi.Length/4)
		{
			Debug.Log("CBUtility::LoadRawFile - Raw file is not the required size");
			return false;
		}
		
		for(int x = 0, i = 0; x < size; x++, i+=4) 
		{
			//Convert 4 bytes to 1 32 bit float
			map[x] = System.BitConverter.ToSingle(data, i);
		};

		return true;
	}

	static bool LoadRawFile(TextAsset raw, float[] map, int size)
	{
		byte[] data = raw.bytes;

		//divide by 4 as there are 4 bytes in a 32 bit float
		//if (size > data.Length / 4)
		//{
			//Debug.Log("CBUtility::LoadRawFile - Raw file is not the required size");
			//return false;
		//}

		for (int x = 0, i = 0; x < size; x++, i += 4)
		{
			//Convert 4 bytes to 1 32 bit float
			map[x] = System.BitConverter.ToSingle(data, i);
		};

		return true;
	}
}




















