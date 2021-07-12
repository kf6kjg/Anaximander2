// OBBRenderer.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2017 Richard Curtice
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using Anaximander.Renderers.renderXFRenderer;

using DataReader;

using Nini.Config;

using OpenMetaverse;

using renderX2;

namespace Anaximander
{
	public class XFRenderer : IRegionRenderer
	{
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private int _pixelScale;
		private Color _waterColor;
		private GLTexture _waterTexture = null;

		public XFRenderer(IConfigSource config)
		{
			var tileInfo = config.Configs["MapTileInfo"];

			_pixelScale = tileInfo?.GetInt("PixelScale", Constants.PixelScale) ?? Constants.PixelScale;

			_waterColor = Color.FromArgb(
				tileInfo?.GetInt("OceanColorRed", Constants.OceanColor.R) ?? Constants.OceanColor.R,
				tileInfo?.GetInt("OceanColorGreen", Constants.OceanColor.G) ?? Constants.OceanColor.G,
				tileInfo?.GetInt("OceanColorBlue", Constants.OceanColor.B) ?? Constants.OceanColor.B
			);

			var waterOverlayPath = tileInfo?.GetString("WaterOverlay", Constants.WaterOverlay) ?? Constants.WaterOverlay;

			if (!string.IsNullOrWhiteSpace(waterOverlayPath))
			{
				try
				{
					_waterTexture = new GLTexture(waterOverlayPath, MemoryLocation.Heap, DuringLoad.Flip);
					LOG.Debug($"Water overlay file '{waterOverlayPath}' loaded.");
				}
				catch (Exception e)
				{
					LOG.Warn($"Error loading water overlay file '{waterOverlayPath}', skipping.", e);
				}
			}
			else
			{
				LOG.Debug($"Water overlay file '{waterOverlayPath}' blank or not set, skipping.");
			}
		}

		public DirectBitmap RenderTileFrom(DataReader.Region region, DirectBitmap bitmap)
		{
			var watch = System.Diagnostics.Stopwatch.StartNew();

			var shaders = new AnaxXFShaders(_waterColor, _waterTexture);

			var GL = new renderX(_pixelScale, _pixelScale);
			const float FOV = 90f;
			GL.SetMatrixData(FOV, 128f, 1f); // 0=Perspective, 1=Orthographic

			GL.ForceCameraPosition(new renderX2.Vector3(128f, 128f, 4000f));
			GL.ForceCameraRotation(new renderX2.Vector3(0f, 180f, 0f)); // Angles degree

			const bool cullfaces = true;
			const bool boolFrontTrueBackFalse = true;
			GL.SetFaceCulling(cullfaces, boolFrontTrueBackFalse);

			// Draw the terrain.
			LOG.Debug($"Rendering Maptile Terrain (Textured) for region {region.Id} named '{region.Name}'");
			watch.Restart();
			var terrainBitmap = new DirectBitmap(bitmap.Width, bitmap.Height);
			var terrain = TerrainToBitmap(region, terrainBitmap);
			shaders.SetTerrain(terrain, terrainBitmap);
			watch.Stop();
			LOG.Info($"Completed terrain for region {region.Id} named '{region.Name}' in {watch.ElapsedMilliseconds}ms");

			// Get the prims
			LOG.Debug($"Getting prims for region {region.Id} named '{region.Name}'");
			watch.Restart();
			var prims = region.GetPrims();
			watch.Stop();
			LOG.Debug($"Completed getting {prims?.Count()} prims for region {region.Id} named '{region.Name}' in {watch.ElapsedMilliseconds}ms");

			if (prims == null)
			{
				LOG.Debug($"Unable to render prims for region {region.Id} named '{region.Name}': there was a problem getting the prims from the DB.");
			}

			// Draw the region.
			LOG.Debug($"Rendering region {region.Id} named '{region.Name}'");
			watch.Restart();
			DrawObjects(prims, terrain, bitmap, GL, shaders, region.Name);
			watch.Stop();
			LOG.Debug($"Completed region {region.Id} named '{region.Name}' in {watch.ElapsedMilliseconds}ms");

			return bitmap;
		}

		private Terrain TerrainToBitmap(DataReader.Region region, DirectBitmap mapbmp)
		{
			var terrain = region.GetTerrain();

			var textures = new Texture[4];
			try
			{
				textures[0] = Texture.GetByUUID(terrain.TerrainTexture1, Texture.TERRAIN_TEXTURE_1_COLOR);
			}
			catch (InvalidOperationException e)
			{
				LOG.Warn($"Error decoding image asset {terrain.TerrainTexture1} for terrain texture 1 in region {region.Id}, continuing using default texture color.", e);

				textures[0] = new Texture(color: Texture.TERRAIN_TEXTURE_1_COLOR);
			}

			try
			{
				textures[1] = Texture.GetByUUID(terrain.TerrainTexture2, Texture.TERRAIN_TEXTURE_2_COLOR);
			}
			catch (InvalidOperationException e)
			{
				LOG.Warn($"Error decoding image asset {terrain.TerrainTexture2} for terrain texture 2 in region {region.Id}, continuing using default texture color.", e);

				textures[1] = new Texture(color: Texture.TERRAIN_TEXTURE_2_COLOR);
			}

			try
			{
				textures[2] = Texture.GetByUUID(terrain.TerrainTexture3, Texture.TERRAIN_TEXTURE_3_COLOR);
			}
			catch (InvalidOperationException e)
			{
				LOG.Warn($"Error decoding image asset {terrain.TerrainTexture3} for terrain texture 3 in region {region.Id}, continuing using default texture color.", e);

				textures[2] = new Texture(color: Texture.TERRAIN_TEXTURE_3_COLOR);
			}

			try
			{
				textures[3] = Texture.GetByUUID(terrain.TerrainTexture4, Texture.TERRAIN_TEXTURE_4_COLOR);
			}
			catch (InvalidOperationException e)
			{
				LOG.Warn($"Error decoding image asset {terrain.TerrainTexture4} for terrain texture 4 in region {region.Id}, continuing using default texture color.", e);

				textures[3] = new Texture(color: Texture.TERRAIN_TEXTURE_4_COLOR);
			}

			// the four terrain colors as HSVs for interpolation
			var hsv1 = new HSV(textures[0].AverageColor);
			var hsv2 = new HSV(textures[1].AverageColor);
			var hsv3 = new HSV(textures[2].AverageColor);
			var hsv4 = new HSV(textures[3].AverageColor);

			for (int x = 0; x < mapbmp.Width; x++)
			{
				var columnRatio = (double)x / (mapbmp.Width - 1); // 0 - 1, for interpolation

				for (int y = 0; y < mapbmp.Height; y++)
				{
					var rowRatio = (double)y / (mapbmp.Height - 1); // 0 - 1, for interpolation

					// Y flip the cordinates for the bitmap: hf origin is lower left, bm origin is upper left
					var yr = (mapbmp.Height - 1) - y;

					var heightvalue = mapbmp.Width == 256 ?
						terrain.GetBlendedHeight(x, y) :
						terrain.GetBlendedHeight(255 * columnRatio, 255 * rowRatio)
					;

					if (double.IsInfinity(heightvalue) || double.IsNaN(heightvalue))
					{
						heightvalue = 0d;
					}

					var tileScalarX = 256f / mapbmp.Width; // Used to hack back in those constants that were hand-tuned to 256px tiles.
					var tileScalarY = 256f / mapbmp.Height; // Used to hack back in those constants that were hand-tuned to 256px tiles.

					// add a bit noise for breaking up those flat colors:
					// - a large-scale noise, for the "patches" (using an doubled s-curve for sharper contrast)
					// - a small-scale noise, for bringing in some small scale variation
					//float bigNoise = (float)TerrainUtil.InterpolatedNoise(x / 8.0, y / 8.0) * .5f + .5f; // map to 0.0 - 1.0
					//float smallNoise = (float)TerrainUtil.InterpolatedNoise(x + 33, y + 43) * .5f + .5f;
					//float hmod = heightvalue + smallNoise * 3f + S(S(bigNoise)) * 10f;
					var hmod =
						heightvalue +
						TerrainUtil.InterpolatedNoise(tileScalarX * (x + 33 + (int)region.Location?.X * mapbmp.Width), tileScalarY * (y + 43 + (int)region.Location?.Y * mapbmp.Height)) * 1.5f + 1.5f + // 0 - 3
						MathUtilities.SCurve(MathUtilities.SCurve(TerrainUtil.InterpolatedNoise(tileScalarX * (x + (int)region.Location?.X * mapbmp.Width) / 8.0, tileScalarY * (y + (int)region.Location?.Y * mapbmp.Height) / 8.0) * .5f + .5f)) * 10f; // 0 - 10

					// find the low/high values for this point (interpolated bilinearily)
					// (and remember, x=0,y=0 is SW)
					var low = terrain.ElevationSWLow * (1f - rowRatio) * (1f - columnRatio) +
						terrain.ElevationSELow * (1f - rowRatio) * columnRatio +
						terrain.ElevationNWLow * rowRatio * (1f - columnRatio) +
						terrain.ElevationNELow * rowRatio * columnRatio
					;
					var high = terrain.ElevationSWHigh * (1f - rowRatio) * (1f - columnRatio) +
						terrain.ElevationSEHigh * (1f - rowRatio) * columnRatio +
						terrain.ElevationNWHigh * rowRatio * (1f - columnRatio) +
						terrain.ElevationNEHigh * rowRatio * columnRatio
					;
					if (high < low)
					{
						// someone tried to fool us. High value should be higher than low every time
						var tmp = high;
						high = low;
						low = tmp;
					}

					Color result;
					HSV hsv;
					if (hmod <= low)
					{
						hsv = hsv1; // too low
					}
					else if (hmod >= high)
					{
						hsv = hsv4; // too high
					}
					else
					{
						// HSV-interpolate along the colors
						// first, rescale h to 0.0 - 1.0
						hmod = (hmod - low) / (high - low);
						// now we have to split: 0.00 => color1, 0.33 => color2, 0.67 => color3, 1.00 => color4
						if (hmod < 1d / 3d)
						{
							hsv = hsv1.InterpolateHSV(ref hsv2, (float)(hmod * 3d));
						}
						else if (hmod < 2d / 3d)
						{
							hsv = hsv2.InterpolateHSV(ref hsv3, (float)((hmod * 3d) - 1d));
						}
						else
						{
							hsv = hsv3.InterpolateHSV(ref hsv4, (float)((hmod * 3d) - 2d));
						}
					}

					result = hsv.ToColor();

					mapbmp.Bitmap.SetPixel(x, yr, result);
				}
			}

			return terrain;
		}

		private unsafe DirectBitmap DrawObjects(IEnumerable<Prim> prims, Terrain terrain, DirectBitmap mapbmp, renderX GL, AnaxXFShaders shaders, string regionName)
		{
			// Generate mesh for terrain. Anything that goes below waterline is flattened to the waterline to make a solid surface.
			const int terrainVertexStride = 9; // XYZ,UVZ,normXYZ - the first three are eaten by RenderXF, the reaminder are passed through to the frag shader.

			var quadCountColumns = mapbmp.Width - 1;
			var quadCountRows = mapbmp.Height - 1;
			var terrainVertexPoints = new float[2 * 3 * quadCountColumns * quadCountRows * terrainVertexStride]; // Two triangles per quad, 3 vertices per tri.

			float textureULeft, textureURight;
			float textureVTop, textureVBottom;
			int quadCol, quadRow;
			var cornerBottomLeft = new renderX2.Vector3();
			var cornerBottomRight = new renderX2.Vector3();
			var cornerTopLeft = new renderX2.Vector3();
			var cornerTopRight = new renderX2.Vector3();
			for (quadCol = 0; quadCol < quadCountColumns; ++quadCol)
			{
				cornerBottomLeft.x = cornerTopLeft.x = 256f * quadCol / quadCountColumns; // 0 - 1, for interpolation.
				textureULeft = (float)quadCol / quadCountColumns;
				cornerBottomRight.x = cornerTopRight.x = 256f * (quadCol + 1) / quadCountColumns; // 0 - 1, for interpolation.
				textureURight = (float)(quadCol + 1) / quadCountColumns;

				for (quadRow = 0; quadRow < quadCountRows; ++quadRow)
				{
					cornerBottomLeft.y = cornerBottomRight.y = 256f * quadRow / quadCountRows;
					textureVBottom = (float)quadRow / quadCountRows; // 0 - 1, for interpolation.
					cornerTopLeft.y = cornerTopRight.y = 256f * (quadRow + 1) / quadCountRows;
					textureVTop = (float)(quadRow + 1) / quadCountRows; // 0 - 1, for interpolation.

					cornerBottomLeft.z = (float)terrain.GetBlendedHeight(255 * textureULeft, 255 * textureVBottom);
					cornerBottomRight.z = (float)terrain.GetBlendedHeight(255 * textureURight, 255 * textureVBottom);
					cornerTopLeft.z = (float)terrain.GetBlendedHeight(255 * textureULeft, 255 * textureVTop);
					cornerTopRight.z = (float)terrain.GetBlendedHeight(255 * textureURight, 255 * textureVTop);

					if (double.IsInfinity(cornerBottomLeft.z) || double.IsNaN(cornerBottomLeft.z))
					{
						cornerBottomLeft.z = 0f;
					}
					if (double.IsInfinity(cornerBottomRight.z) || double.IsNaN(cornerBottomRight.z))
					{
						cornerBottomRight.z = 0f;
					}
					if (double.IsInfinity(cornerTopLeft.z) || double.IsNaN(cornerTopLeft.z))
					{
						cornerTopLeft.z = 0f;
					}
					if (double.IsInfinity(cornerTopRight.z) || double.IsNaN(cornerTopRight.z))
					{
						cornerTopRight.z = 0f;
					}

					var vertexIndex = (quadCol + quadRow * quadCountColumns) * terrainVertexStride * 6; // 6 verts per quad, due to decomposing to 2 tris per quad first.

					// Triangle 1: Right, Top

					terrainVertexPoints[vertexIndex++] = cornerTopRight.x; // X
					terrainVertexPoints[vertexIndex++] = cornerTopRight.y; // Y
					terrainVertexPoints[vertexIndex++] = cornerTopRight.z; // Z
					terrainVertexPoints[vertexIndex++] = textureURight; // U
					terrainVertexPoints[vertexIndex++] = textureVTop; // V
					terrainVertexPoints[vertexIndex++] = cornerTopRight.z; // Z
					terrainVertexPoints[vertexIndex++] = 0f; // normalX
					terrainVertexPoints[vertexIndex++] = 0f; // normalY
					terrainVertexPoints[vertexIndex++] = 1f; // normalZ

					// Triangle 1: Left, Bottom
					terrainVertexPoints[vertexIndex++] = cornerBottomLeft.x; // X
					terrainVertexPoints[vertexIndex++] = cornerBottomLeft.y; // Y
					terrainVertexPoints[vertexIndex++] = cornerBottomLeft.z; // Z
					terrainVertexPoints[vertexIndex++] = textureULeft; // U
					terrainVertexPoints[vertexIndex++] = textureVBottom; // V
					terrainVertexPoints[vertexIndex++] = cornerBottomLeft.z; // Z
					terrainVertexPoints[vertexIndex++] = 0f; // normalX
					terrainVertexPoints[vertexIndex++] = 0f; // normalY
					terrainVertexPoints[vertexIndex++] = 1f; // normalZ

					// Triangle 1: Right, Bottom
					terrainVertexPoints[vertexIndex++] = cornerBottomRight.x; // X
					terrainVertexPoints[vertexIndex++] = cornerBottomRight.y; // Y
					terrainVertexPoints[vertexIndex++] = cornerBottomRight.z; // Z
					terrainVertexPoints[vertexIndex++] = textureURight; // U
					terrainVertexPoints[vertexIndex++] = textureVBottom; // V
					terrainVertexPoints[vertexIndex++] = cornerBottomRight.z; // Z
					terrainVertexPoints[vertexIndex++] = 0f; // normalX
					terrainVertexPoints[vertexIndex++] = 0f; // normalY
					terrainVertexPoints[vertexIndex++] = 1f; // normalZ

					// Triangle 2: Left, Top
					terrainVertexPoints[vertexIndex++] = cornerTopLeft.x; // X
					terrainVertexPoints[vertexIndex++] = cornerTopLeft.y; // Y
					terrainVertexPoints[vertexIndex++] = cornerTopLeft.z; // Z
					terrainVertexPoints[vertexIndex++] = textureULeft; // U
					terrainVertexPoints[vertexIndex++] = textureVTop; // V
					terrainVertexPoints[vertexIndex++] = cornerTopLeft.z; // Z
					terrainVertexPoints[vertexIndex++] = 0f; // normalX
					terrainVertexPoints[vertexIndex++] = 0f; // normalY
					terrainVertexPoints[vertexIndex++] = 1f; // normalZ

					// Triangle 2: Left, Bottom
					terrainVertexPoints[vertexIndex++] = cornerBottomLeft.x; // X
					terrainVertexPoints[vertexIndex++] = cornerBottomLeft.y; // Y
					terrainVertexPoints[vertexIndex++] = cornerBottomLeft.z; // Z
					terrainVertexPoints[vertexIndex++] = textureULeft; // U
					terrainVertexPoints[vertexIndex++] = textureVBottom; // V
					terrainVertexPoints[vertexIndex++] = cornerBottomLeft.z; // Z
					terrainVertexPoints[vertexIndex++] = 0f; // normalX
					terrainVertexPoints[vertexIndex++] = 0f; // normalY
					terrainVertexPoints[vertexIndex++] = 1f; // normalZ

					// Triangle 2: Right, Top
					terrainVertexPoints[vertexIndex++] = cornerTopRight.x; // X
					terrainVertexPoints[vertexIndex++] = cornerTopRight.y; // Y
					terrainVertexPoints[vertexIndex++] = cornerTopRight.z; // Z
					terrainVertexPoints[vertexIndex++] = textureURight; // U
					terrainVertexPoints[vertexIndex++] = textureVTop; // V
					terrainVertexPoints[vertexIndex++] = cornerTopRight.z; // Z
					terrainVertexPoints[vertexIndex++] = 0f; // normalX
					terrainVertexPoints[vertexIndex++] = 0f; // normalY
					terrainVertexPoints[vertexIndex++] = 1f; // normalZ
				}
			}

			// Compute denormalized normal vectors. https://mrl.cs.nyu.edu/~perlin/courses/fall2002ugrad/meshnormals.html
			{
				renderX2.Vector3 vertex0, vertex1, vertex2;
				renderX2.Vector3 edge0, edge1;
				renderX2.Vector3 normal;
				int vertexIndex;

				var faceCount = terrainVertexPoints.Length / (3 * terrainVertexStride); // 3 verts per face.
				for (var faceIndex = 0; faceIndex < faceCount; ++faceIndex)
				{
					// Extract the verts.
					vertexIndex = (3 * faceIndex + 0) * terrainVertexStride; // 3 verts per face.
					vertex0.x = terrainVertexPoints[vertexIndex++];
					vertex0.y = terrainVertexPoints[vertexIndex++];
					vertex0.z = terrainVertexPoints[vertexIndex++];

					vertexIndex = (3 * faceIndex + 1) * terrainVertexStride; // 3 verts per face.
					vertex1.x = terrainVertexPoints[vertexIndex++];
					vertex1.y = terrainVertexPoints[vertexIndex++];
					vertex1.z = terrainVertexPoints[vertexIndex++];

					vertexIndex = (3 * faceIndex + 2) * terrainVertexStride; // 3 verts per face.
					vertex2.x = terrainVertexPoints[vertexIndex++];
					vertex2.y = terrainVertexPoints[vertexIndex++];
					vertex2.z = terrainVertexPoints[vertexIndex++];

					// Compute the edges
					edge0 = vertex0 - vertex1;
					edge1 = vertex1 - vertex2;

					normal.x = edge0.y * edge1.z - edge0.z * edge1.y;
					normal.y = edge0.z * edge1.x - edge0.x * edge1.z;
					normal.z = edge0.x * edge1.y - edge0.y * edge1.x;

					// Add the normal to the existing vertex normal.
					vertexIndex = (3 * faceIndex + 0) * terrainVertexStride + 6; // 3 verts per face.
					terrainVertexPoints[vertexIndex++] += normal.x;
					terrainVertexPoints[vertexIndex++] += normal.y;
					terrainVertexPoints[vertexIndex++] += normal.z;

					vertexIndex = (3 * faceIndex + 1) * terrainVertexStride + 6; // 3 verts per face.
					terrainVertexPoints[vertexIndex++] += normal.x;
					terrainVertexPoints[vertexIndex++] += normal.y;
					terrainVertexPoints[vertexIndex++] += normal.z;

					vertexIndex = (3 * faceIndex + 2) * terrainVertexStride + 6; // 3 verts per face.
					terrainVertexPoints[vertexIndex++] += normal.x;
					terrainVertexPoints[vertexIndex++] += normal.y;
					terrainVertexPoints[vertexIndex++] += normal.z;
				}
			}

			// Normalize the normal vectors.
			{
				renderX2.Vector3 normal;
				for (var vertexIndex = 0; vertexIndex < terrainVertexPoints.Length; vertexIndex += terrainVertexStride)
				{
					normal.x = terrainVertexPoints[vertexIndex + 6];
					normal.y = terrainVertexPoints[vertexIndex + 7];
					normal.z = terrainVertexPoints[vertexIndex + 8];

					normal = renderX2.Vector3.Normalize(normal);

					terrainVertexPoints[vertexIndex + 6] = normal.x;
					terrainVertexPoints[vertexIndex + 7] = normal.y;
					terrainVertexPoints[vertexIndex + 8] = normal.z;
				}
			}

			// var stlLines = new string[2 + 7 * terrainVertexPoints.Length / (terrainVertexStride * 3)];
			// stlLines[0] = "solid region";
			// stlLines[stlLines.Length - 1] = "endsolid region";
			// var lineBase = 1;
			// for (var baseIndex = 0; baseIndex < terrainVertexPoints.Length - 15; baseIndex += 15, lineBase += 7) {
			// 	stlLines[lineBase + 0] = " facet normal 0 0 0";
			// 	stlLines[lineBase + 1] = "  outer loop";
			// 	stlLines[lineBase + 2] = $"   vertex {terrainVertexPoints[baseIndex + 0]} {terrainVertexPoints[baseIndex + 1]} {terrainVertexPoints[baseIndex + 2]}";
			// 	stlLines[lineBase + 3] = $"   vertex {terrainVertexPoints[baseIndex + 5]} {terrainVertexPoints[baseIndex + 6]} {terrainVertexPoints[baseIndex + 7]}";
			// 	stlLines[lineBase + 4] = $"   vertex {terrainVertexPoints[baseIndex + 10]} {terrainVertexPoints[baseIndex + 11]} {terrainVertexPoints[baseIndex + 12]}";
			// 	stlLines[lineBase + 5] = "  endloop";
			// 	stlLines[lineBase + 6] = " endfacet";
			// }
			// File.WriteAllLines("region.stl", stlLines);

			GL.Clear(0, 0, 0);
			GL.ClearDepth();

			using (var vertexBuffer = new GLBuffer(terrainVertexPoints, terrainVertexStride, MemoryLocation.Heap))
			{
				GL.SelectBuffer(vertexBuffer);
				var terrainShader = new Shader(shaders.ScaleCorrectionVS, shaders.TerrainShaderFS, GLRenderMode.Triangle);
				terrainShader.SetOverrideAttributeCount(terrainVertexStride - 3); // UVZ
				GL.SelectShader(terrainShader);
				GL.Draw();
			}

			if (prims?.Count() > 0)
			{
				// TODO: build prims.

				// using (var vertexBuffer = new GLBuffer(renderX.PrimitiveTypes.Cube(), 5, MemoryLocation.Heap))
				// {
				// 	GL.SelectBuffer(vertexBuffer);
				// 	GL.SelectShader(new Shader(null, shaders.CubeFS, GLRenderMode.Triangle));
				// 	GL.Draw();
				// }
			}

			//var screenSpaceShader = new Shader(shaders.ScreenSpacePass);
			//GL.SelectShader(screenSpaceShader);
			//GL.Pass();

			// Render to bitmap
			GL.BlitIntoBitmap(mapbmp.Bitmap, new Point(), new Rectangle(0, 0, mapbmp.Width, mapbmp.Height));
			mapbmp.Bitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);

			return mapbmp;
		}

		#region Utility Helpers

		#endregion
	}
}

