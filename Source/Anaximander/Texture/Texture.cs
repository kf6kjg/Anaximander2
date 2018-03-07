// Texture.cs
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
using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using Chattel;
using ChattelAssetTools;
using InWorldz.Data.Assets.Stratus;
using log4net;

namespace Anaximander {
	/// <summary>
	/// An immutable representation of a texture.
	/// </summary>
	public class Texture {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		#region Constants

		public static readonly string BLANK_TEXTURE_ID = "5748decc-f629-461c-9a36-a35a221fe21f";
		public static readonly Color BLANK_TEXTURE_COLOR = Color.White;

		// some hardcoded terrain UUIDs that work with SL 1.20 (the four default textures and "Blank").
		// The color-values were chosen by someone in old OpenSim code because they "look right"
		public static readonly string TERRAIN_TEXTURE_1_ID = "0bc58228-74a0-7e83-89bc-5c23464bcec5";
		public static readonly string TERRAIN_TEXTURE_2_ID = "63338ede-0037-c4fd-855b-015d77112fc8";
		public static readonly string TERRAIN_TEXTURE_3_ID = "303cd381-8560-7579-23f1-f0a880799740";
		public static readonly string TERRAIN_TEXTURE_4_ID = "53a2f406-4895-1d13-d541-d2e3b86bc19c";
		public static readonly Color TERRAIN_TEXTURE_1_COLOR = Color.FromArgb(165, 137, 118);
		public static readonly Color TERRAIN_TEXTURE_2_COLOR = Color.FromArgb(69, 89, 49);
		public static readonly Color TERRAIN_TEXTURE_3_COLOR = Color.FromArgb(162, 154, 141);
		public static readonly Color TERRAIN_TEXTURE_4_COLOR = Color.FromArgb(200, 200, 200);

		public static readonly Texture DEFAULT = new Texture();

		#endregion

		private static ChattelReader _assetReader;

		private static readonly ConcurrentDictionary<Guid, Texture> _memoryCache = new ConcurrentDictionary<Guid, Texture>();

		public static void Initialize(ChattelReader assetReader) {
			if (_assetReader == null) {
				_assetReader = assetReader;
			}
			else {
				LOG.Warn($"Attempt to inialize the asset reader in the Texture class more than once! Re-initialization ignored.");
			}

			CSJ2K.Util.BitmapImageCreator.Register();
		}

		public static Texture GetByUUID(Guid id) => GetByUUID(id, null);

		public static Texture GetByUUID(Guid id, Color? defaultColor) {
			if (_memoryCache.TryGetValue(id, out var cachedTexture)) {
				return cachedTexture;
			}

			if (_assetReader == null && defaultColor == null) {
				return DEFAULT;
				// No cache needed for default.
			}
			Texture texture = null;
			Exception ex = null;

			var wait = new System.Threading.AutoResetEvent(false);
			_assetReader.GetAssetAsync(id, asset => {
				if (asset == null || !asset.IsTextureAsset()) {
					texture = new Texture(color: defaultColor);
					// No cache when the asset was not found: maybe next time it will be.
				}
				else {
					try {
						texture = new Texture(asset);

						_memoryCache.TryAdd(id, texture);
					}
					catch (Exception e) {
						ex = e;
					}
				}
				wait.Set();
			});
			wait.WaitOne(60000);

			if (ex != null) {
				throw new Exception("See inner exception", ex);
			}

			return texture;
		}

		#region Properties

		public Color AverageColor { get; private set; } = BLANK_TEXTURE_COLOR;

		public Image Image { get; private set; }

		#endregion

		#region Constructors

		private Texture() {
			// No need to chain defaults if all defaults are assigned in the properties.
		}

		private Texture(StratusAsset asset) {
			if (asset.IsTextureAsset()) {
				LOG.Debug($"Decoding image {asset.Id} named '{asset.Name}'. Notable data: Type={asset.Type}, Temp={asset.Temporary}, Data Length={asset.Data?.Length}.");

				var bitmap = asset.ToImage<Bitmap>();

				Image = bitmap;
				AverageColor = ComputeAverageColor(bitmap);
			}
		}

		public Texture(Bitmap image)
			: this(image, null) {
		}

		public Texture(Color? color)
			: this(null, color) {
		}

		public Texture(Bitmap image, Color? color) {
			if (image != null) {
				Image = new Bitmap(image); // Deep copy that image to make sure we don't lose immutability.
				AverageColor = ComputeAverageColor(image);
			}

			if (color != null) {
				AverageColor = (Color)color;
			}
		}

		#endregion

		#region Helpers

		// Compute the average color of a texture.
		private static Color ComputeAverageColor(Bitmap bmp) {
			// we have 256 x 256 pixel, each with 256 possible color-values per
			// color-channel, so 2^24 is the maximum value we can get, adding everything.
			// int is be big enough for that.
			int r = 0, g = 0, b = 0;
			for (var y = 0; y < bmp.Height; ++y) {
				for (var x = 0; x < bmp.Width; ++x) {
					var c = bmp.GetPixel(x, y);
					r += (int)c.R & 0xff;
					g += (int)c.G & 0xff;
					b += (int)c.B & 0xff;
				}
			}

			var pixels = bmp.Width * bmp.Height;
			return Color.FromArgb(r / pixels, g / pixels, b / pixels);
		}

		#endregion
	}
}

