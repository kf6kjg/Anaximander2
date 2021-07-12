using DataReader;

using renderX2;

using System;
using System.Drawing;

namespace Anaximander.Renderers.renderXFRenderer
{
	public unsafe class AnaxXFShaders
	{
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private GLTexture _terrainTexture;
		private int* _terrainTextureAddr;
		private readonly GLTexture _waterTexture;

		private readonly Color _waterColor;

		private readonly float _ambientStrength = 0.1f;
		private readonly Vector3 _lightColor = new(1f, 1f, 1f);
		private readonly Vector3 _lightDir = Vector3.Normalize(new Vector3(-.5f, .5f, 1f));

		private float _waterHeight = 20f;

		public AnaxXFShaders(Color waterColor, GLTexture waterOverlay = null)
		{
			_waterColor = waterColor;
			_waterTexture = waterOverlay;
		}

		public void SetTerrain(Terrain terrain, DirectBitmap terrainBitmap)
		{
			_terrainTexture = new GLTexture(terrainBitmap.Bitmap, MemoryLocation.Heap, DuringLoad.Flip);
			_terrainTextureAddr = (int*)_terrainTexture.GetAddress();
			_waterHeight = (float)terrain.WaterHeight;
		}

		public unsafe void ScaleCorrectionVS(float* OUT, float* IN, int Index)
		{
			// LOG.Debug($"TerrainShaderVS {Index.ToString("D10")} XYZ<{IN[0].ToString("N5")},{IN[1].ToString("N5")},{IN[3].ToString("N5")}> UV<{IN[4].ToString("N5")},{IN[5].ToString("N5")}>");

			OUT[0] = IN[0];
			OUT[1] = IN[1];
			OUT[2] = IN[2];
		}

		#region Terrain Shaders

		public unsafe void TerrainShaderFS(byte* bgr, float* attributes, int faceIndex)
		{
			// UVZ nXYZ
			// 012  345
			var u = renderX.Clamp01(attributes[0]);
			var v = renderX.Clamp01(attributes[1]);
			var terrainHeight = attributes[2];
			var normal = Vector3.Normalize(new Vector3(attributes[3], attributes[4], attributes[5]));

			if (float.IsNaN(u) || float.IsNaN(v) || float.IsNaN(terrainHeight) || float.IsNaN(normal.x) || float.IsNaN(normal.y) || float.IsNaN(normal.z))
			{
				bgr[0] = 0;
				bgr[1] = 0;
				bgr[2] = 255;

				return;
			}

			var terTexX = (int)(u * (_terrainTexture.Width - 1));
			var terTexY = (int)(v * (_terrainTexture.Height - 1));

			var terrainTexColorEncoded = (_terrainTexture == null) ? 0 : _terrainTextureAddr[terTexX + terTexY * _terrainTexture.Width];
			var terrainColor = Color.FromArgb(terrainTexColorEncoded);
			var objectColor = new Vector3(terrainColor.R / 255f, terrainColor.G / 255f, terrainColor.B / 255f);

			// LOG.Debug($"TerrainShaderFS {faceIndex.ToString("D10")} XYZ<{attributes[0].ToString("N5")},{attributes[1].ToString("N5")},{attributes[3].ToString("N5")}> UV<{attributes[4].ToString("N5")},{attributes[5].ToString("N5")}>");

			if (terrainHeight <= _waterHeight)
			{
				// Under water.
				var depth = _waterHeight - terrainHeight;
				// var fogFactor = depth / _waterHeight;//(float)Math.Pow(2f, -0.015f * depth);
				// var fogFactor = renderX.Clamp01((float)Math.Pow(2f, -0.8f * (depth + 0.5)));
				var fogFactor = renderX.Clamp01((float)Math.Pow(2f, -0.25f * (depth + 0.5)));
				// var fogFactor = (float)MathUtilities.SCurve(terrainHeight / _waterHeight);

				// var hsvTerrain = new HSV(terrainColor); // Result is very green in comparison to the Lerp, which stays more blue.
				// var hsvWater = new HSV(_waterColor);
				// var water = hsvWater.InterpolateHSV(ref hsvTerrain, fogFactor).ToColor();
				// bgr[0] = water.B;
				// bgr[1] = water.G;
				// bgr[2] = water.R;

				objectColor.x = renderX.Lerp(_waterColor.R, terrainColor.R, fogFactor) / 255f;
				objectColor.y = renderX.Lerp(_waterColor.G, terrainColor.G, fogFactor) / 255f;
				objectColor.z = renderX.Lerp(_waterColor.B, terrainColor.B, fogFactor) / 255f;

				if (_waterTexture != null)
				{
					// Overlay the water image
					var waterTexX = (int)(u * (_waterTexture.Width - 1));
					var waterTexY = (int)(v * (_waterTexture.Height - 1));

					var waterTexPtr = (_waterTexture == null) ? (int*)0 : (int*)_waterTexture.GetAddress();

					var overlayColor = Color.FromArgb(waterTexPtr[waterTexX + waterTexY * _waterTexture.Width]);
					// Convert to premultiplied alpha
					var alpha = overlayColor.A / 255f;
					var overlayColorPremultiplied = new Vector3(
						overlayColor.R * alpha / 255f, // BUG: Conversion makes all the info go away.
						overlayColor.G * alpha / 255f,
						overlayColor.B * alpha / 255f
					);

					objectColor.x += overlayColorPremultiplied.x * (1f - alpha);
					objectColor.y += overlayColorPremultiplied.y * (1f - alpha);
					//objectColor.z += overlayColorPremultiplied.z * (1f - alpha);
				}
			}

			var ambient = _ambientStrength * _lightColor;
			var diff = Math.Max(Vector3.Dot(normal, _lightDir), 0f);
			var diffuse = diff * _lightColor;

			var result = (ambient + diffuse) * objectColor;
			bgr[2] = (byte)(result.x * 255f);
			bgr[1] = (byte)(result.y * 255f);
			bgr[0] = (byte)(result.z * 255f);
		}

		#endregion

		public unsafe void CubeFS(byte* bgr, float* attributes, int faceIndex)
		{
			bgr[0] = (byte)(attributes[0] * 255);
			bgr[1] = (byte)(attributes[1] * 255);
			bgr[2] = 0;
		}
	}
}
