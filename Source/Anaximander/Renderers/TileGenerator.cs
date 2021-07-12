// OceanTileGenerator.cs
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

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using Nini.Config;

namespace Anaximander {
	public class TileGenerator {
		private static readonly log4net.ILog LOG = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		private readonly int _pixelSize;

		private readonly IRegionRenderer _regionRenderer;

		private readonly FlatTileRenderer _flatRenderer;

		private static Bitmap _oceanOverlay;

		public TileGenerator(IConfigSource config) {
			var tileInfo = config.Configs["MapTileInfo"];
			_pixelSize = tileInfo?.GetInt("PixelScale", Constants.PixelScale) ?? Constants.PixelScale;
			var rendererName = tileInfo?.GetString("RenderTechnique", Constants.RenderTechnique) ?? Constants.RenderTechnique;

			switch (rendererName.ToLowerInvariant()) {
				case "obbrenderer":
					_regionRenderer = new OBBRenderer(config);
					break;
				case "xfrenderer":
					_regionRenderer = new XFRenderer(config);
					break;
				default:
					LOG.Error($"Unknown renderer '{rendererName}', defaulting to 'OBBRenderer'.");
					_regionRenderer = new OBBRenderer(config);
					break;
			}

			_flatRenderer = new FlatTileRenderer(config);

			var oceanOverlayPath = tileInfo?.GetString("OceanOverlay", Constants.OceanOverlay) ?? Constants.OceanOverlay;

			var pixelScale = tileInfo?.GetInt("PixelScale", Constants.PixelScale) ?? Constants.PixelScale;

			if (!string.IsNullOrWhiteSpace(oceanOverlayPath)) {
				try {
					var overlay = new Bitmap(Image.FromFile(oceanOverlayPath));

					_oceanOverlay = new Bitmap(pixelScale, pixelScale);

					using (var gfx = Graphics.FromImage(_oceanOverlay)) {
						gfx.CompositingMode = CompositingMode.SourceCopy;
						gfx.DrawImage(overlay, 0, 0, pixelScale, pixelScale);
					}
				}
				catch (Exception e) {
					LOG.Warn($"Error loading ocean overlay file '{oceanOverlayPath}', skipping.", e);
				}
			}
		}

		public DirectBitmap GenerateOceanTile() {
			var bitmap = new DirectBitmap(_pixelSize, _pixelSize);
			_flatRenderer.RenderToBitmap(bitmap, _oceanOverlay);
			return bitmap;
		}

		public DirectBitmap GenerateConstantColorTile(Color color) {
			var bitmap = new DirectBitmap(_pixelSize, _pixelSize);
			FlatTileRenderer.RenderToBitmap(color, bitmap);
			return bitmap;
		}

		public DirectBitmap RenderRegionTile(DataReader.Region region) {
			var watch = System.Diagnostics.Stopwatch.StartNew();

			var bitmap = new DirectBitmap(_pixelSize, _pixelSize);
			watch.Stop();
			LOG.Debug($"Init'd image for {region.Id} in " + (watch.ElapsedMilliseconds) + " ms");

			// Draw the terrain.
			LOG.Debug($"Rendering region {region.Id}");
			watch.Restart();
			_regionRenderer.RenderTileFrom(region, bitmap);
			watch.Stop();
			LOG.Info($"Completed render for {region.Id} in " + (watch.ElapsedMilliseconds) + " ms");

			return bitmap;
		}
	}
}

