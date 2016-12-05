// TexturedMapTileRenderer.cs
//
// Author:
//       Ricky Curtice <ricky@rwcproductions.com>
//
// Copyright (c) 2016 Richard Curtice
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
//
//
/* Some portions are copied from the Halcyon project and are licensed under:
* Copyright (c) InWorldz Halcyon Developers
* Copyright (c) Contributors, http://opensimulator.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
	* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using log4net;
using OpenMetaverse;
using AssetReader;

namespace Anaximander {
	// Hue, Saturation, Value; used for color-interpolation
	struct HSV {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public float h;
		public float s;
		public float v;

		public HSV(float h, float s, float v)
		{
			this.h = h;
			this.s = s;
			this.v = v;
		}

		// (for info about algorithm, see http://en.wikipedia.org/wiki/HSL_and_HSV)
		public HSV(Color c)
		{
			float r = c.R / 255f;
			float g = c.G / 255f;
			float b = c.B / 255f;
			float max = Math.Max(Math.Max(r, g), b);
			float min = Math.Min(Math.Min(r, g), b);
			float diff = max - min;

			if (Math.Abs(max - min) < 0.001) h = 0f;
			else if (Math.Abs(max - r) < 0.001) h = (g - b) / diff * 60f;
			else if (Math.Abs(max - g) < 0.001) h = (b - r) / diff * 60f + 120f;
			else h = (r - g) / diff * 60f + 240f;
			if (h < 0f) h += 360f;

			if (Math.Abs(max) <= 0.001f) s = 0f;
			else s = diff / max;

			v = max;
		}

		// (for info about algorithm, see http://en.wikipedia.org/wiki/HSL_and_HSV)
		public Color toColor()
		{
			if (s < 0f) LOG.Debug("S < 0: " + s);
			else if (s > 1f) LOG.Debug("S > 1: " + s);
			if (v < 0f) LOG.Debug("V < 0: " + v);
			else if (v > 1f) LOG.Debug("V > 1: " + v);

			float f = h / 60f;
			int sector = (int)f % 6;
			f = f - (int)f;
			int pi = (int)(v * (1f - s) * 255f);
			int qi = (int)(v * (1f - s * f) * 255f);
			int ti = (int)(v * (1f - (1f - f) * s) * 255f);
			int vi = (int)(v * 255f);

			if (pi < 0) pi = 0;
			if (pi > 255) pi = 255;
			if (qi < 0) qi = 0;
			if (qi > 255) qi = 255;
			if (ti < 0) ti = 0;
			if (ti > 255) ti = 255;
			if (vi < 0) vi = 0;
			if (vi > 255) vi = 255;

			switch (sector)
			{
				case 0:
				return Color.FromArgb(vi, ti, pi);
				case 1:
				return Color.FromArgb(qi, vi, pi);
				case 2:
				return Color.FromArgb(pi, vi, ti);
				case 3:
				return Color.FromArgb(pi, qi, vi);
				case 4:
				return Color.FromArgb(ti, pi, vi);
				default:
				return Color.FromArgb(vi, pi, qi);
			}
		}
	}

	public static class TexturedMapTileRenderer {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		#region Helpers

		// S-curve: f(x) = 3x² - 2x³:
		// f(0) = 0, f(0.5) = 0.5, f(1) = 1,
		// f'(x) = 0 at x = 0 and x = 1; f'(0.5) = 1.5,
		// f''(0.5) = 0, f''(x) != 0 for x != 0.5
		private static double Scurve(double v) {
			return (v * v * (3f - 2f * v));
		}

		// interpolate two colors in HSV space and return the resulting color
		private static HSV interpolateHSV(ref HSV c1, ref HSV c2, float ratio) {
			if (ratio <= 0f) return c1;
			if (ratio >= 1f) return c2;

			// make sure we are on the same side on the hue-circle for interpolation
			// We change the hue of the parameters here, but we don't change the color
			// represented by that value
			if (c1.h - c2.h > 180f) c1.h -= 360f;
			else if (c2.h - c1.h > 180f) c1.h += 360f;

			return new HSV(c1.h * (1f - ratio) + c2.h * ratio,
				c1.s * (1f - ratio) + c2.s * ratio,
				c1.v * (1f - ratio) + c2.v * ratio);
		}

		/// <summary>
		/// Gets the height at the specified location, blending the value depending on the proximity to the pixel center.
		/// AKA: bilinear filtering.
		/// </summary>
		/// <returns>The height in meters.</returns>
		/// <param name="hm">The heightmap array.</param>
		/// <param name="x">The x coordinate.</param>
		/// <param name="y">The y coordinate.</param>
		private static double getHeight(double[,] hm, double x, double y) {
			int x_0 = (int)x,  y_0 = (int)y;
			int x_1 = x_0 + 1, y_1 = y_0 + 1;

			var x_ratio = x - x_0; // The fractional part gives the 0-1 ratio needed.
			var y_ratio = y - y_0;

			// Unit square interpretation of bilinear filtering.
			if (x_0 < hm.GetLength(0) - 1 && y_0 < hm.GetLength(1) - 1)
				return
					hm[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					hm[x_1, y_0] *      x_ratio  * (1 - y_ratio) +
					hm[x_0, y_1] * (1 - x_ratio) *      y_ratio  +
					hm[x_1, y_1] *      x_ratio  *      y_ratio
					;
			else if (x_0 < hm.GetLength(0) - 1)
				return
					hm[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					hm[x_1, y_0] *      x_ratio  * (1 - y_ratio)
					;
			else if (y_0 < hm.GetLength(1) - 1)
				return
					hm[x_0, y_0] * (1 - x_ratio) * (1 - y_ratio) +
					hm[x_0, y_1] * (1 - x_ratio) *      y_ratio
					;
			else
				return
					hm[x_0, y_0]
					;
		}
		#endregion

		public static void TerrainToBitmap(DataReader.Region region, DirectBitmap mapbmp)
		{
			var textures = new Texture[4];
			textures[0] = new Texture(UUID.Parse(region.terrainTexture1), CommonTextures.TERRAIN_TEXTURE_1.AverageColor);
			textures[1] = new Texture(UUID.Parse(region.terrainTexture2), CommonTextures.TERRAIN_TEXTURE_2.AverageColor);
			textures[2] = new Texture(UUID.Parse(region.terrainTexture3), CommonTextures.TERRAIN_TEXTURE_3.AverageColor);
			textures[3] = new Texture(UUID.Parse(region.terrainTexture4), CommonTextures.TERRAIN_TEXTURE_4.AverageColor);

			int tc = Environment.TickCount;
			LOG.Info("[TERRAIN]: Generating Maptile Terrain (Textured)");

			// the four terrain colors as HSVs for interpolation
			var hsv1 = new HSV(textures[0].AverageColor);
			var hsv2 = new HSV(textures[1].AverageColor);
			var hsv3 = new HSV(textures[2].AverageColor);
			var hsv4 = new HSV(textures[3].AverageColor);

			var levelNElow = region.elevation1NE;
			var levelNEhigh = region.elevation2NE;

			var levelNWlow = region.elevation1NW;
			var levelNWhigh = region.elevation2NW;

			var levelSElow = region.elevation1SE;
			var levelSEhigh = region.elevation2SE;

			var levelSWlow = region.elevation1SW;
			var levelSWhigh = region.elevation2SW;

			var waterHeight = region.waterHeight;

			for (int x = 0; x < mapbmp.Width; x++)
			{
				var columnRatio = (double)x / (mapbmp.Width - 1); // 0 - 1, for interpolation

				for (int y = 0; y < mapbmp.Height; y++)
				{
					var rowRatio = (double)y / (mapbmp.Height - 1); // 0 - 1, for interpolation

					// Y flip the cordinates for the bitmap: hf origin is lower left, bm origin is upper left
					var yr = (mapbmp.Height - 1) - y;

					var heightvalue = mapbmp.Width == 256 ?
						getHeight(region.heightmapData, (int)(255 * columnRatio), (int)(255 * rowRatio)) :
						getHeight(region.heightmapData, 255 * columnRatio, 255 * rowRatio);

					if (Double.IsInfinity(heightvalue) || Double.IsNaN(heightvalue))
						heightvalue = 0d;

					if (heightvalue > waterHeight)
					{
						// add a bit noise for breaking up those flat colors:
						// - a large-scale noise, for the "patches" (using an doubled s-curve for sharper contrast)
						// - a small-scale noise, for bringing in some small scale variation
						//float bigNoise = (float)TerrainUtil.InterpolatedNoise(x / 8.0, y / 8.0) * .5f + .5f; // map to 0.0 - 1.0
						//float smallNoise = (float)TerrainUtil.InterpolatedNoise(x + 33, y + 43) * .5f + .5f;
						//float hmod = heightvalue + smallNoise * 3f + S(S(bigNoise)) * 10f;
						var hmod =
							heightvalue +
							TerrainUtil.InterpolatedNoise(x + 33, y + 43) * 1.5f + 1.5f + // 0 - 3
							Scurve(Scurve(TerrainUtil.InterpolatedNoise(x / 8.0, y / 8.0) * .5f + .5f)) * 10f; // 0 - 10

						// find the low/high values for this point (interpolated bilinearily)
						// (and remember, x=0,y=0 is SW)
						var low = levelSWlow * (1f - rowRatio) * (1f - columnRatio) +
							levelSElow * (1f - rowRatio) * columnRatio +
							levelNWlow * rowRatio * (1f - columnRatio) +
							levelNElow * rowRatio * columnRatio;
						var high = levelSWhigh * (1f - rowRatio) * (1f - columnRatio) +
							levelSEhigh * (1f - rowRatio) * columnRatio +
							levelNWhigh * rowRatio * (1f - columnRatio) +
							levelNEhigh * rowRatio * columnRatio;
						if (high < low)
						{
							// someone tried to fool us. High value should be higher than low every time
							var tmp = high;
							high = low;
							low = tmp;
						}

						HSV hsv;
						if (hmod <= low) hsv = hsv1; // too low
						else if (hmod >= high) hsv = hsv4; // too high
						else
						{
							// HSV-interpolate along the colors
							// first, rescale h to 0.0 - 1.0
							hmod = (hmod - low) / (high - low);
							// now we have to split: 0.00 => color1, 0.33 => color2, 0.67 => color3, 1.00 => color4
							if (hmod < 1d/3d) hsv = interpolateHSV(ref hsv1, ref hsv2, (float)(hmod * 3d));
							else if (hmod < 2d/3d) hsv = interpolateHSV(ref hsv2, ref hsv3, (float)((hmod * 3d) - 1d));
							else hsv = interpolateHSV(ref hsv3, ref hsv4, (float)((hmod * 3d) - 2d));
						}

						// Shade the terrain for shadows
						if (x < (mapbmp.Width - 1) && y < (mapbmp.Height - 1))
						{
							var hfvaluecompare = getHeight(region.heightmapData, (x + 1) / (mapbmp.Width - 1), (y + 1) / (mapbmp.Height - 1)); // light from north-east => look at land height there
							if (Double.IsInfinity(hfvaluecompare) || Double.IsNaN(hfvaluecompare))
								hfvaluecompare = 0d;

							var hfdiff = heightvalue - hfvaluecompare;  // => positive if NE is lower, negative if here is lower
							hfdiff *= 0.06d; // some random factor so "it looks good"
							if (hfdiff > 0.02d)
							{
								var highlightfactor = 0.18d;
								// NE is lower than here
								// We have to desaturate and lighten the land at the same time
								hsv.s = (hsv.s - (hfdiff * highlightfactor) > 0d) ? (float) (hsv.s - (hfdiff * highlightfactor)) : 0f;
								hsv.v = (hsv.v + (hfdiff * highlightfactor) < 1d) ? (float) (hsv.v + (hfdiff * highlightfactor)) : 1f;
							}
							else if (hfdiff < -0.02f)
							{
								// here is lower than NE:
								// We have to desaturate and blacken the land at the same time
								hsv.s = (hsv.s + hfdiff > 0f) ? (float) (hsv.s + hfdiff) : 0f;
								hsv.v = (hsv.v + hfdiff > 0f) ? (float) (hsv.v + hfdiff) : 0f;
							}
						}
						mapbmp.Bitmap.SetPixel(x, yr, hsv.toColor());
					}
					else
					{
						// We're under the water level with the terrain, so paint water instead of land

						heightvalue = waterHeight - heightvalue;
						if (Double.IsInfinity(heightvalue) || Double.IsNaN(heightvalue))
							heightvalue = 0f;
						else if (heightvalue > 19f)
							heightvalue = 19f;
						else if (heightvalue < 0f)
							heightvalue = 0f;

						heightvalue = 100f - (heightvalue * 100f) / 19f;  // 0 - 19 => 100 - 0

						Color water = Color.FromArgb((int)heightvalue, (int)heightvalue, 255);
						mapbmp.Bitmap.SetPixel(x, yr, water);
					}
				}
			}
			LOG.Info("[TERRAIN]: Done in " + (Environment.TickCount - tc) + " ms");
		}

	}
}

