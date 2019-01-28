using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class ScriptComputeManager : MonoBehaviour {
	public ComputeShader cs;

	public List<Color> map_colors;

	private int tex_dim = 64;
	private const int thread_dim = 32;
	private const int tex_dim_max = 4096;

	private RenderTexture in_tex;
	private RenderTexture out_tex;
	
	private List<GameObject> planes = new List<GameObject>();

	// Start is called before the first frame update
	void Start() {
		BootCS();
	}

	// Update is called once per frame
	void Update() {
		if (Input.GetKeyDown(KeyCode.A) && tex_dim >= tex_dim_max)
			Debug.Log("Texture too big");
		if (Input.GetKeyDown(KeyCode.A) && tex_dim < tex_dim_max)
			PropagateCS();

		if (Input.GetKeyDown(KeyCode.S))
			BlurCS();
		if (Input.GetKeyDown(KeyCode.D))
			WriteTexture();
	}

	void BootCS() {
		// write data into a Texture2D, because RenderTexture is a nuisance to write into

		Texture2D start_tex = GetComponent<Renderer>().material.mainTexture as Texture2D;
		if (start_tex != null)
			tex_dim = Mathf.Min(start_tex.width, start_tex.height);
		if (start_tex == null) {
			// float[,] h = PerlinMap.GetPerlin(tex_dim, tex_dim, perlin_alpha: 9.97f);
			float[,] h = PerlinMap.GetTerrain(tex_dim, tex_dim);
			start_tex = PerlinMap.MapToTex(ref h, ref map_colors);
		}

		in_tex = new RenderTexture(tex_dim, tex_dim, 1) { enableRandomWrite = true, filterMode = FilterMode.Point };
		in_tex.Create();

		// CSLoad writes data from Texture2D "start_tex" into RenderTexture "in_tex"
		int kernel_ind = cs.FindKernel("CSLoad");
		cs.SetInt("smaller_dim", tex_dim);
		cs.SetTexture(kernel_ind, "start_tex", start_tex);
		cs.SetTexture(kernel_ind, "in_tex", in_tex);

		cs.Dispatch(kernel_ind, tex_dim / thread_dim, tex_dim / thread_dim, 1);

		GetComponent<Renderer>().material.mainTexture = in_tex;
	}

	[ContextMenu("Propagate texture")]
	void PropagateCS() {
		GameObject prev = GameObject.CreatePrimitive(PrimitiveType.Plane);
		prev.transform.position = transform.position;
		planes.Add(prev);
		prev.GetComponent<Renderer>().material.mainTexture = GetComponent<Renderer>().material.mainTexture;

		transform.position += new Vector3(12.5f, 0f, 0f);
		Camera.main.transform.position = transform.position + new Vector3(0f, 10f, 0f);

		int kernel_ind_prop = cs.FindKernel("CSPropagate");

		// propagate-by-doubling from in_tex to out_tex
		out_tex = new RenderTexture(tex_dim * 2, tex_dim * 2, 1) { enableRandomWrite = true, filterMode = FilterMode.Point };
		out_tex.Create();

		cs.SetInt("smaller_dim", tex_dim);
		cs.SetTexture(kernel_ind_prop, "in_tex", in_tex);
		cs.SetTexture(kernel_ind_prop, "out_tex", out_tex);

		cs.Dispatch(kernel_ind_prop, tex_dim / thread_dim, tex_dim / thread_dim, 1);

		tex_dim = tex_dim * 2;

		in_tex = out_tex;

		gameObject.GetComponent<Renderer>().material.mainTexture = in_tex;
	}

	[ContextMenu("Blur texture")]
	void BlurCS() {
		int kernel_ind_blur = cs.FindKernel("CSBlur");
		cs.SetInt("smaller_dim", tex_dim);
		cs.SetTexture(kernel_ind_blur, "in_tex", in_tex);
		cs.Dispatch(kernel_ind_blur, tex_dim / thread_dim, tex_dim / thread_dim, 1);
	}

	[ContextMenu("Write texture")]
	void WriteTexture() {
		RenderTexture active = RenderTexture.active;

		RenderTexture.active = in_tex;
		Texture2D tex = new Texture2D(tex_dim, tex_dim, TextureFormat.RGB24, false);
		RenderTexture.active = in_tex;
		tex.ReadPixels(new Rect(0, 0, in_tex.width, in_tex.height), 0, 0);
		tex.Apply();

		File.WriteAllBytes(Application.dataPath + "/../" + gameObject.name + "_" + tex_dim.ToString() + ".png", tex.EncodeToPNG());

		RenderTexture.active = active;
	}
}

internal static class PerlinMap {
	public static float[,] GetPerlin(int xdim, int zdim, float perlin_alpha = 6.3f) {
		float[,] h = new float[xdim, zdim];

		float init_x = Random.Range(0, 10000);
		float init_y = Random.Range(0, 10000);

		for (int i = 0; i < xdim; i++) {
			for (int j = 0; j < zdim; j++) {
				h[i, j] = Mathf.PerlinNoise(init_x + i * perlin_alpha / xdim, init_y + j * perlin_alpha / zdim);
			}
		}

		return (h);
	}

	public static float[,] GetTerrain(int xdim, int zdim) {
		float[,] map = new float[xdim, zdim];
		const float mask_max = 0.99f;
		const float mask_min = 0.01f;
		const float mask_threshold = 0.2f;

		float[,] pmask = GetPerlin(xdim, zdim, perlin_alpha: 4.01f);
		float[,] p0 = GetPerlin(xdim, zdim, perlin_alpha: 3.93f);
		float[,] p1 = GetPerlin(xdim, zdim, perlin_alpha: 2.97f);
		float[,] p2 = GetPerlin(xdim, zdim, perlin_alpha: 1.97f);
		float[,] w = GetPerlin(xdim, zdim, perlin_alpha: 12.36f);

		for (int i = 0; i < map.GetLength(0); i++) {
			for (int j = 0; j < map.GetLength(1); j++) {
				float mask = (Mathf.Sin(i * 3.14f / xdim) * Mathf.Sin(j * 3.14f / zdim) > mask_threshold) ? mask_max : mask_min;
				// float perlin_mask = (0.3f > pmask[i, j] || 0.5f < pmask[i,j]) ? mask_max : mask_min;
				float perlin_mask = (0.3f > pmask[i, j] || 0.5f < pmask[i, j]) ? mask_max : mask_min;
				float w_inst = w[i, j];
				float w_conj = (1 - w[i, j]) / 2;

				map[i, j] = mask * perlin_mask * (w_inst * p0[i, j] + w_conj * p1[i, j] + w_conj * p2[i,j]);
			}
		}
		return (map);
	}

	public static Texture2D MapToTex(ref float[,] map) {
		Texture2D tex = new Texture2D(map.GetLength(0), map.GetLength(1));

		for (int i = 0; i < map.GetLength(0); i++)
			for (int j = 0; j < map.GetLength(1); j++)
				tex.SetPixel(i, j, Color.Lerp(Color.red, Color.green, map[i, j]));
		tex.Apply();
		return (tex);
	}

	public static Texture2D MapToTex(ref float[,] map, ref List<Color> colors) {
		Texture2D tex = new Texture2D(map.GetLength(0), map.GetLength(1));

		for (int i = 0; i < map.GetLength(0); i++) {
			for (int j = 0; j < map.GetLength(1); j++) {
				int ind = Mathf.Clamp(Mathf.FloorToInt(map[i, j] * colors.Count), 0, colors.Count - 1);
				int ind2 = Mathf.Clamp(ind + 1, 0, colors.Count - 1);
				float alpha = (map[i, j] * colors.Count) % 1;
				// tex.SetPixel(i, j, Color.Lerp(colors[ind], colors[ind2], alpha));
				// tex.SetPixel(i, j, Color.Lerp(colors[ind], colors[ind2], (alpha > 0.5f? 0.2f: 0.8f)));
				tex.SetPixel(i, j, colors[ind]);
			}
		}
		tex.Apply();
		return (tex);
	}
}
