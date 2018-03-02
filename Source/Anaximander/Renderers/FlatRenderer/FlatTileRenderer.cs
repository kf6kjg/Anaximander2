// OceanMapTileRenderer.cs
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

using System.Drawing;
using Nini.Config;

namespace Anaximander {
	public class FlatTileRenderer {
		private readonly IConfig _tileInfo;

		public FlatTileRenderer(IConfigSource config) {
			_tileInfo = config.Configs["MapTileInfo"];
		}

		public DirectBitmap RenderToBitmap(DirectBitmap mapbmp) => RenderToBitmap(mapbmp, null);

		public DirectBitmap RenderToBitmap(DirectBitmap mapbmp, Bitmap overlay) {
			var dbitmap = RenderToBitmap(Color.FromArgb(
				_tileInfo?.GetInt("OceanColorRed", Constants.OceanColor.R) ?? Constants.OceanColor.R,
				_tileInfo?.GetInt("OceanColorGreen", Constants.OceanColor.G) ?? Constants.OceanColor.G,
				_tileInfo?.GetInt("OceanColorBlue", Constants.OceanColor.B) ?? Constants.OceanColor.B
			), mapbmp);

			if (overlay != null) {
				using (var gfx = Graphics.FromImage(dbitmap.Bitmap)) {
					gfx.DrawImage(overlay, 0, 0, dbitmap.Width, dbitmap.Height);
				}
			}

			return dbitmap;
		}

		public static DirectBitmap RenderToBitmap(Color color, DirectBitmap mapbmp) {
			using (var gfx = Graphics.FromImage(mapbmp.Bitmap)) {
				using (var brush = new SolidBrush(color)) {
					gfx.FillRectangle(brush, 0, 0, mapbmp.Width, mapbmp.Height);
				}
			}
			return mapbmp;
		}
	}
}
