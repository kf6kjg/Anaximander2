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
using System.Drawing.Drawing2D;
using System.Linq;
using DataReader;
using Nini.Config;
using OpenMetaverse;

namespace Anaximander {
	public class OBBRenderer : IRegionRenderer {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private readonly Color _waterColor;
		private readonly Color _beachColor;
		private readonly Color[,] _waterOverlay;

		public struct DrawStruct {
			public float SortOrder;
			public SolidBrush Brush;
			public Point[] Vertices;
		}

		public OBBRenderer(IConfigSource config) {
			var tileInfo = config.Configs["MapTileInfo"];

			_waterColor = Color.FromArgb(
				tileInfo?.GetInt("OceanColorRed", Constants.OceanColor.R) ?? Constants.OceanColor.R,
				tileInfo?.GetInt("OceanColorGreen", Constants.OceanColor.G) ?? Constants.OceanColor.G,
				tileInfo?.GetInt("OceanColorBlue", Constants.OceanColor.B) ?? Constants.OceanColor.B
			);
			_beachColor = Color.FromArgb(
				tileInfo?.GetInt("BeachColorRed", Constants.BeachColor.R) ?? Constants.BeachColor.R,
				tileInfo?.GetInt("BeachColorGreen", Constants.BeachColor.G) ?? Constants.BeachColor.G,
				tileInfo?.GetInt("BeachColorBlue", Constants.BeachColor.B) ?? Constants.BeachColor.B
			);

			var waterOverlayPath = tileInfo?.GetString("WaterOverlay", Constants.WaterOverlay) ?? Constants.WaterOverlay;

			var pixelScale = tileInfo?.GetInt("PixelScale", Constants.PixelScale) ?? Constants.PixelScale;

			DirectBitmap waterOverlay;

			if (!string.IsNullOrWhiteSpace(waterOverlayPath)) {
				try {
					var overlay = new Bitmap(Image.FromFile(waterOverlayPath));

					waterOverlay = new DirectBitmap(pixelScale, pixelScale);
					using (var gfx = Graphics.FromImage(waterOverlay.Bitmap)) {
						gfx.CompositingMode = CompositingMode.SourceCopy;
						gfx.DrawImage(overlay, 0, 0, pixelScale, pixelScale);
					}
				}
				catch (Exception e) {
					LOG.Warn($"Error loading water overlay file '{waterOverlayPath}', skipping.", e);
					waterOverlay = null;
				}

				if (waterOverlay != null) {
					_waterOverlay = new Color[waterOverlay.Bitmap.Width, waterOverlay.Bitmap.Height];

					// Convert to premultiplied alpha
					for (int y = 0; y < waterOverlay.Bitmap.Height; y++) {
						for (int x = 0; x < waterOverlay.Bitmap.Width; x++) {
							var c = waterOverlay.Bitmap.GetPixel(x, y);

							var r = c.R * c.A / 255;
							var g = c.G * c.A / 255;
							var b = c.B * c.A / 255;

							var result = Color.FromArgb(c.A, r, g, b);

							_waterOverlay[x, y] = result;
						}
					}
				}
			}
		}

		public DirectBitmap RenderTileFrom(DataReader.Region region, DirectBitmap bitmap) {
			var watch = System.Diagnostics.Stopwatch.StartNew();

			// Draw the terrain.
			LOG.Debug($"Rendering Maptile Terrain (Textured) for region {region.Id} named '{region.Name}'");
			watch.Restart();
			var terrain = TerrainToBitmap(region, bitmap);
			watch.Stop();
			LOG.Info($"Completed terrain for region {region.Id} named '{region.Name}' in {watch.ElapsedMilliseconds}ms");

			// Get the prims
			LOG.Debug($"Getting prims for region {region.Id} named '{region.Name}'");
			watch.Restart();
			var prims = region.GetPrims();
			watch.Stop();
			LOG.Debug($"Completed getting {prims?.Count()} prims for region {region.Id} named '{region.Name}' in {watch.ElapsedMilliseconds}ms");

			if (prims != null) {
				// Draw the prims.
				LOG.Debug($"Rendering OBB prims for region {region.Id} named '{region.Name}'");
				watch.Restart();
				DrawObjects(prims, terrain, bitmap);
				watch.Stop();
				LOG.Debug($"Completed OBB prims for region {region.Id} named '{region.Name}' in {watch.ElapsedMilliseconds}ms");
			}
			else {
				LOG.Debug($"Unable to render OBB prims for region {region.Id} named '{region.Name}': there was a problem getting the prims from the DB.");
			}

			return bitmap;
		}

		private Terrain TerrainToBitmap(DataReader.Region region, DirectBitmap mapbmp) {
			var terrain = region.GetTerrain();

			var textures = new Texture[4];
			try {
				textures[0] = Texture.GetByUUID(terrain.TerrainTexture1, Texture.TERRAIN_TEXTURE_1_COLOR);
			}
			catch (InvalidOperationException e) {
				LOG.Warn($"Error decoding image asset {terrain.TerrainTexture1} for terrain texture 1 in region {region.Id}, continuing using default texture color.", e);

				textures[0] = new Texture(color: Texture.TERRAIN_TEXTURE_1_COLOR);
			}

			try {
				textures[1] = Texture.GetByUUID(terrain.TerrainTexture2, Texture.TERRAIN_TEXTURE_2_COLOR);
			}
			catch (InvalidOperationException e) {
				LOG.Warn($"Error decoding image asset {terrain.TerrainTexture2} for terrain texture 2 in region {region.Id}, continuing using default texture color.", e);

				textures[1] = new Texture(color: Texture.TERRAIN_TEXTURE_2_COLOR);
			}

			try {
				textures[2] = Texture.GetByUUID(terrain.TerrainTexture3, Texture.TERRAIN_TEXTURE_3_COLOR);
			}
			catch (InvalidOperationException e) {
				LOG.Warn($"Error decoding image asset {terrain.TerrainTexture3} for terrain texture 3 in region {region.Id}, continuing using default texture color.", e);

				textures[2] = new Texture(color: Texture.TERRAIN_TEXTURE_3_COLOR);
			}

			try {
				textures[3] = Texture.GetByUUID(terrain.TerrainTexture4, Texture.TERRAIN_TEXTURE_4_COLOR);
			}
			catch (InvalidOperationException e) {
				LOG.Warn($"Error decoding image asset {terrain.TerrainTexture4} for terrain texture 4 in region {region.Id}, continuing using default texture color.", e);

				textures[3] = new Texture(color: Texture.TERRAIN_TEXTURE_4_COLOR);
			}

			// the four terrain colors as HSVs for interpolation
			var hsv1 = new HSV(textures[0].AverageColor);
			var hsv2 = new HSV(textures[1].AverageColor);
			var hsv3 = new HSV(textures[2].AverageColor);
			var hsv4 = new HSV(textures[3].AverageColor);

			for (int x = 0; x < mapbmp.Width; x++) {
				var columnRatio = (double)x / (mapbmp.Width - 1); // 0 - 1, for interpolation

				for (int y = 0; y < mapbmp.Height; y++) {
					var rowRatio = (double)y / (mapbmp.Height - 1); // 0 - 1, for interpolation

					// Y flip the cordinates for the bitmap: hf origin is lower left, bm origin is upper left
					var yr = (mapbmp.Height - 1) - y;

					var heightvalue = mapbmp.Width == 256 ?
						terrain.GetBlendedHeight(x, y) :
						terrain.GetBlendedHeight(255 * columnRatio, 255 * rowRatio)
					;

					if (double.IsInfinity(heightvalue) || double.IsNaN(heightvalue)) {
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
					if (high < low) {
						// someone tried to fool us. High value should be higher than low every time
						var tmp = high;
						high = low;
						low = tmp;
					}

					Color result;
					if (heightvalue > terrain.WaterHeight) {
						HSV hsv;
						// Above water
						if (hmod <= low) {
							hsv = hsv1; // too low
						}
						else if (hmod >= high) {
							hsv = hsv4; // too high
						}
						else {
							// HSV-interpolate along the colors
							// first, rescale h to 0.0 - 1.0
							hmod = (hmod - low) / (high - low);
							// now we have to split: 0.00 => color1, 0.33 => color2, 0.67 => color3, 1.00 => color4
							if (hmod < 1d / 3d) {
								hsv = hsv1.InterpolateHSV(ref hsv2, (float)(hmod * 3d));
							}
							else if (hmod < 2d / 3d) {
								hsv = hsv2.InterpolateHSV(ref hsv3, (float)((hmod * 3d) - 1d));
							}
							else {
								hsv = hsv3.InterpolateHSV(ref hsv4, (float)((hmod * 3d) - 2d));
							}
						}

						result = hsv.ToColor();
					}
					else {
						// Under water.
						var deepwater = new HSV(_waterColor);
						var beachwater = new HSV(_beachColor);

						var water = deepwater.InterpolateHSV(ref beachwater, (float)MathUtilities.SCurve(heightvalue / terrain.WaterHeight));

						if (_waterOverlay == null) {
							result = water.ToColor();
						}
						else {
							// Overlay the water image
							var baseColor = water.ToColor();

							var overlayColor = _waterOverlay[x, y];

							var resultR = overlayColor.R + (baseColor.R * (255 - overlayColor.A) / 255);
							var resultG = overlayColor.G + (baseColor.G * (255 - overlayColor.A) / 255);
							var resultB = overlayColor.B + (baseColor.B * (255 - overlayColor.A) / 255);

							result = Color.FromArgb(resultR, resultG, resultB);
						}
					}

					// Shade the terrain for shadows
					//if (x < (mapbmp.Width - 1) && y < (mapbmp.Height - 1)) {
					//	var hfvaluecompare = getHeight(region.heightmapData, (x + 1) / (mapbmp.Width - 1), (y + 1) / (mapbmp.Height - 1)); // light from north-east => look at land height there
					//	if (Double.IsInfinity(hfvaluecompare) || Double.IsNaN(hfvaluecompare))
					//		hfvaluecompare = 0d;
					//
					//	var hfdiff = heightvalue - hfvaluecompare;  // => positive if NE is lower, negative if here is lower
					//	hfdiff *= 0.06d; // some random factor so "it looks good"
					//	if (hfdiff > 0.02d) {
					//		var highlightfactor = 0.18d;
					//		// NE is lower than here
					//		// We have to desaturate and lighten the land at the same time
					//		hsv.s = (hsv.s - (hfdiff * highlightfactor) > 0d) ? (float)(hsv.s - (hfdiff * highlightfactor)) : 0f;
					//		hsv.v = (hsv.v + (hfdiff * highlightfactor) < 1d) ? (float)(hsv.v + (hfdiff * highlightfactor)) : 1f;
					//	}
					//	else if (hfdiff < -0.02f) {
					//		var highlightfactor = 1.0d;
					//		// here is lower than NE:
					//		// We have to desaturate and blacken the land at the same time
					//		hsv.s = (hsv.s + (hfdiff * highlightfactor) > 0f) ? (float)(hsv.s + (hfdiff * highlightfactor)) : 0f;
					//		hsv.v = (hsv.v + (hfdiff * highlightfactor) > 0f) ? (float)(hsv.v + (hfdiff * highlightfactor)) : 0f;
					//	}
					//}

					mapbmp.Bitmap.SetPixel(x, yr, result);
				}
			}

			return terrain;
		}

		private static DirectBitmap DrawObjects(IEnumerable<Prim> prims, Terrain terrain, DirectBitmap mapbmp) {
			var scale_factor = mapbmp.Height / 256f;

			var drawdata_for_sorting = new List<DrawStruct>();

			// Get all the faces for valid prims and prep them for drawing.
			foreach (var prim in prims) {
				DrawStruct drawdata;

				var vertices = new Vector3[8];

				var pos_nullable = prim.ComputeWorldPosition();
				var rot_nullable = prim.ComputeWorldRotation();
				if (pos_nullable == null || rot_nullable == null) {
					return null;
				}

				var pos = (Vector3)pos_nullable;
				var rot = (Quaternion)rot_nullable;

				if (
					// skip prim in non-finite position
					float.IsNaN(pos.X) || float.IsNaN(pos.Y) ||
					float.IsInfinity(pos.X) || float.IsInfinity(pos.Y) ||

					// skip prim Z at or above 256m above the terrain at that position, but only if actually above terrain.
					// BUG: will still cause discrepencies when the terrain changes height drastically between regions and the prim is in the high "iffy" area.
					(pos.X >= 0f && pos.X < 256f && pos.Y >= 0f && pos.Y < 256f && pos.Z >= (terrain.GetBlendedHeight(pos.X, pos.Y) + 256f))
				) {
					return null;
				}

				var radial_scale = prim.Scale * 0.5f;
				Vector3 rotated_radial_scale;

				/* * * * * * * * * * * * * * * * * * */
				// OBB VERTEX COMPUTATION
				/* * * * * * * * * * * * * * * * * * */

				/*
				Vertex pattern:
				# XYZ
				0 --+
				1 +-+
				2 +++
				3 -++
				4 ---
				5 +--
				6 ++-
				7 -+-
				*/
				rotated_radial_scale.X = -radial_scale.X;
				rotated_radial_scale.Y = -radial_scale.Y;
				rotated_radial_scale.Z = radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[0].X = pos.X + rotated_radial_scale.X;
				vertices[0].Y = pos.Y + rotated_radial_scale.Y;
				vertices[0].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = radial_scale.X;
				rotated_radial_scale.Y = -radial_scale.Y;
				rotated_radial_scale.Z = radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[1].X = pos.X + rotated_radial_scale.X;
				vertices[1].Y = pos.Y + rotated_radial_scale.Y;
				vertices[1].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = radial_scale.X;
				rotated_radial_scale.Y = radial_scale.Y;
				rotated_radial_scale.Z = radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[2].X = pos.X + rotated_radial_scale.X;
				vertices[2].Y = pos.Y + rotated_radial_scale.Y;
				vertices[2].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = -radial_scale.X;
				rotated_radial_scale.Y = radial_scale.Y;
				rotated_radial_scale.Z = radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[3].X = pos.X + rotated_radial_scale.X;
				vertices[3].Y = pos.Y + rotated_radial_scale.Y;
				vertices[3].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = -radial_scale.X;
				rotated_radial_scale.Y = -radial_scale.Y;
				rotated_radial_scale.Z = -radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[4].X = pos.X + rotated_radial_scale.X;
				vertices[4].Y = pos.Y + rotated_radial_scale.Y;
				vertices[4].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = radial_scale.X;
				rotated_radial_scale.Y = -radial_scale.Y;
				rotated_radial_scale.Z = -radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[5].X = pos.X + rotated_radial_scale.X;
				vertices[5].Y = pos.Y + rotated_radial_scale.Y;
				vertices[5].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = radial_scale.X;
				rotated_radial_scale.Y = radial_scale.Y;
				rotated_radial_scale.Z = -radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[6].X = pos.X + rotated_radial_scale.X;
				vertices[6].Y = pos.Y + rotated_radial_scale.Y;
				vertices[6].Z = pos.Z + rotated_radial_scale.Z;

				rotated_radial_scale.X = -radial_scale.X;
				rotated_radial_scale.Y = radial_scale.Y;
				rotated_radial_scale.Z = -radial_scale.Z;
				rotated_radial_scale *= rot;
				vertices[7].X = pos.X + rotated_radial_scale.X;
				vertices[7].Y = pos.Y + rotated_radial_scale.Y;
				vertices[7].Z = pos.Z + rotated_radial_scale.Z;

				/* * * * * * * * * * * * * * * * * * */
				// SORT HEIGHT CALC
				/* * * * * * * * * * * * * * * * * * */
				drawdata.SortOrder =
					Math.Max(vertices[0].Z,
						Math.Max(vertices[1].Z,
							Math.Max(vertices[2].Z,
								Math.Max(vertices[3].Z,
									Math.Max(vertices[4].Z,
										Math.Max(vertices[5].Z,
											Math.Max(vertices[6].Z,
												vertices[7].Z
											)
										)
									)
								)
							)
						)
					)
				;

				/* * * * * * * * * * * * * * * * * * */
				// OBB DRAWING PREPARATION PASS
				/* * * * * * * * * * * * * * * * * * */

				// Compute face 0 of OBB and add if facing up.
				if (MathUtilities.ZOfCrossDiff(ref vertices[0], ref vertices[1], ref vertices[3]) > 0) {
					drawdata.Brush = GetFaceBrush(prim, 0);

					drawdata.Vertices = new Point[4];
					drawdata.Vertices[0].X = (int)(vertices[0].X * scale_factor);
					drawdata.Vertices[0].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);

					drawdata.Vertices[1].X = (int)(vertices[1].X * scale_factor);
					drawdata.Vertices[1].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);

					drawdata.Vertices[2].X = (int)(vertices[2].X * scale_factor);
					drawdata.Vertices[2].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);

					drawdata.Vertices[3].X = (int)(vertices[3].X * scale_factor);
					drawdata.Vertices[3].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);

					drawdata_for_sorting.Add(drawdata);
				}

				// Compute face 1 of OBB and add if facing up.
				if (MathUtilities.ZOfCrossDiff(ref vertices[4], ref vertices[5], ref vertices[0]) > 0) {

					drawdata.Brush = GetFaceBrush(prim, 1);

					drawdata.Vertices = new Point[4];
					drawdata.Vertices[0].X = (int)(vertices[4].X * scale_factor);
					drawdata.Vertices[0].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);

					drawdata.Vertices[1].X = (int)(vertices[5].X * scale_factor);
					drawdata.Vertices[1].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

					drawdata.Vertices[2].X = (int)(vertices[1].X * scale_factor);
					drawdata.Vertices[2].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);

					drawdata.Vertices[3].X = (int)(vertices[0].X * scale_factor);
					drawdata.Vertices[3].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);

					drawdata_for_sorting.Add(drawdata);
				}

				// Compute face 2 of OBB and add if facing up.
				if (MathUtilities.ZOfCrossDiff(ref vertices[5], ref vertices[6], ref vertices[1]) > 0) {
					drawdata.Brush = GetFaceBrush(prim, 2);

					drawdata.Vertices = new Point[4];
					drawdata.Vertices[0].X = (int)(vertices[5].X * scale_factor);
					drawdata.Vertices[0].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

					drawdata.Vertices[1].X = (int)(vertices[6].X * scale_factor);
					drawdata.Vertices[1].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

					drawdata.Vertices[2].X = (int)(vertices[2].X * scale_factor);
					drawdata.Vertices[2].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);

					drawdata.Vertices[3].X = (int)(vertices[1].X * scale_factor);
					drawdata.Vertices[3].Y = mapbmp.Height - (int)(vertices[1].Y * scale_factor);

					drawdata_for_sorting.Add(drawdata);
				}

				// Compute face 3 of OBB and add if facing up.
				if (MathUtilities.ZOfCrossDiff(ref vertices[6], ref vertices[7], ref vertices[2]) > 0) {
					drawdata.Brush = GetFaceBrush(prim, 3);

					drawdata.Vertices = new Point[4];
					drawdata.Vertices[0].X = (int)(vertices[6].X * scale_factor);
					drawdata.Vertices[0].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

					drawdata.Vertices[1].X = (int)(vertices[7].X * scale_factor);
					drawdata.Vertices[1].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

					drawdata.Vertices[2].X = (int)(vertices[3].X * scale_factor);
					drawdata.Vertices[2].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);

					drawdata.Vertices[3].X = (int)(vertices[2].X * scale_factor);
					drawdata.Vertices[3].Y = mapbmp.Height - (int)(vertices[2].Y * scale_factor);

					drawdata_for_sorting.Add(drawdata);
				}

				// Compute face 4 of OBB and add if facing up.
				if (MathUtilities.ZOfCrossDiff(ref vertices[7], ref vertices[4], ref vertices[3]) > 0) {
					drawdata.Brush = GetFaceBrush(prim, 4);

					drawdata.Vertices = new Point[4];
					drawdata.Vertices[0].X = (int)(vertices[7].X * scale_factor);
					drawdata.Vertices[0].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

					drawdata.Vertices[1].X = (int)(vertices[4].X * scale_factor);
					drawdata.Vertices[1].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);

					drawdata.Vertices[2].X = (int)(vertices[0].X * scale_factor);
					drawdata.Vertices[2].Y = mapbmp.Height - (int)(vertices[0].Y * scale_factor);

					drawdata.Vertices[3].X = (int)(vertices[3].X * scale_factor);
					drawdata.Vertices[3].Y = mapbmp.Height - (int)(vertices[3].Y * scale_factor);

					drawdata_for_sorting.Add(drawdata);
				}

				// Compute face 5 of OBB and add if facing up.
				if (MathUtilities.ZOfCrossDiff(ref vertices[7], ref vertices[6], ref vertices[4]) > 0) {
					drawdata.Brush = GetFaceBrush(prim, 5);

					drawdata.Vertices = new Point[4];
					drawdata.Vertices[0].X = (int)(vertices[7].X * scale_factor);
					drawdata.Vertices[0].Y = mapbmp.Height - (int)(vertices[7].Y * scale_factor);

					drawdata.Vertices[1].X = (int)(vertices[6].X * scale_factor);
					drawdata.Vertices[1].Y = mapbmp.Height - (int)(vertices[6].Y * scale_factor);

					drawdata.Vertices[2].X = (int)(vertices[5].X * scale_factor);
					drawdata.Vertices[2].Y = mapbmp.Height - (int)(vertices[5].Y * scale_factor);

					drawdata.Vertices[3].X = (int)(vertices[4].X * scale_factor);
					drawdata.Vertices[3].Y = mapbmp.Height - (int)(vertices[4].Y * scale_factor);

					drawdata_for_sorting.Add(drawdata);
				}
			}

			// Sort faces by Z position
			drawdata_for_sorting.Sort((h1, h2) => h1.SortOrder.CompareTo(h2.SortOrder));

			// Draw the faces
			using (var g = Graphics.FromImage(mapbmp.Bitmap)) {
				g.CompositingMode = CompositingMode.SourceCopy;
				g.CompositingQuality = CompositingQuality.HighSpeed;
				g.SmoothingMode = SmoothingMode.None;
				g.PixelOffsetMode = PixelOffsetMode.None;
				g.InterpolationMode = InterpolationMode.NearestNeighbor;

				for (int s = 0; s < drawdata_for_sorting.Count; s++) {
					g.FillPolygon(drawdata_for_sorting[s].Brush, drawdata_for_sorting[s].Vertices);
				}
			}

			return mapbmp;
		}

		#region Utility Helpers

		private static readonly SolidBrush DefaultBrush = new SolidBrush(Color.Black);
		private static SolidBrush GetFaceBrush(Prim prim, uint face) {
			// Block sillyness that would cause an exception.
			if (face >= Primitive.TextureEntry.MAX_FACES) {
				return DefaultBrush;
			}

			var textures = new Primitive.TextureEntry(prim.Texture, 0, prim.Texture.Length);

			var facetexture = textures.GetFace(face);
			// GetFace throws a generic exception if the parameter is greater than MAX_FACES.

			// Compute a color from the texture data AND the color applied.  The operation is "Multiplication" aka per-pixel (A*B)/255 or if float in domain 0-1: (A*B)
			Texture texture = null;

			try {
				texture = Texture.GetByUUID(facetexture.TextureID.Guid);
			}
			catch (Exception e) {
				var location = prim.ComputeWorldPosition();
				LOG.Warn($"Error decoding image asset {facetexture.TextureID} on face {face} of prim {prim.Id} named '{prim.Name}' at {location} in region {prim.RegionId}, continuing using default texture.", e);
			}

			if (texture == null) {
				texture = Texture.DEFAULT;
			}

			return new SolidBrush(Color.FromArgb(
				Math.Max(0, Math.Min(255, (int)(facetexture.RGBA.R * 255f) * texture.AverageColor.R / 255)),
				Math.Max(0, Math.Min(255, (int)(facetexture.RGBA.G * 255f) * texture.AverageColor.G / 255)),
				Math.Max(0, Math.Min(255, (int)(facetexture.RGBA.B * 255f) * texture.AverageColor.B / 255))
			));
			// FromARGB can throw an exception if a parameter is outside 0-255, but that is prevented.
		}

		#endregion
	}
}

