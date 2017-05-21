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
using System.Drawing;
using System.Reflection;
using log4net;
using Nini.Config;

namespace Anaximander {
	public class TileGenerator {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		private readonly int _pixelSize;

		private readonly string _rendererName;

		private readonly RegionRendererInterface _regionRenderer;

		private readonly FlatTileRenderer _flatRenderer;

		public TileGenerator(IConfigSource config) {
			var tileInfo = config.Configs["MapTileInfo"];
			_pixelSize = tileInfo?.GetInt("PixelScale", Constants.PixelScale) ?? Constants.PixelScale;
			_rendererName = tileInfo?.GetString("RenderTechnique", Constants.RenderTechnique) ?? Constants.RenderTechnique;

			switch (_rendererName.ToLowerInvariant()) {
				case "obbrenderer":
					_regionRenderer = new OBBRenderer(config);
				break;
				default:
					LOG.Error($"[RENDER] Unknown renderer '{_rendererName}', defaulting to 'OBBRenderer'.");
					_regionRenderer = new OBBRenderer(config);
				break;
			}

			_flatRenderer = new FlatTileRenderer(config);
		}

		public DirectBitmap GenerateOceanTile() {
			var bitmap = new DirectBitmap(_pixelSize, _pixelSize);
			_flatRenderer.RenderToBitmap(bitmap);
			return bitmap;
		}

		public DirectBitmap GenerateConstantColorTile(Color color) {
			var bitmap = new DirectBitmap(_pixelSize, _pixelSize);
			_flatRenderer.RenderToBitmap(color, bitmap);
			return bitmap;
		}

		public DirectBitmap RenderRegionTile(DataReader.Region region) {
			var watch = System.Diagnostics.Stopwatch.StartNew();

			var bitmap = new DirectBitmap(_pixelSize, _pixelSize);
			watch.Stop();
			LOG.Debug($"[RENDER]: Init'd image for {region.regionId} in " + (watch.ElapsedMilliseconds) + " ms");

			// Draw the terrain.
			LOG.Debug($"[RENDER]: Rendering region {region.regionId}");
			watch.Restart();
			_regionRenderer.RenderTileFrom(region, bitmap);
			watch.Stop();
			LOG.Info($"[RENDER]: Completed render for {region.regionId} in " + (watch.ElapsedMilliseconds) + " ms");

			return bitmap;
		}
	}
}

