// CommonTextures.cs
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
using OpenMetaverse;

namespace Anaximander {
	public static class CommonTextures {
		public static readonly Texture BLANK_TEXTURE = new Texture(new UUID("5748decc-f629-461c-9a36-a35a221fe21f"), Color.White);

		// some hardcoded terrain UUIDs that work with SL 1.20 (the four default textures and "Blank").
		// The color-values were choosen because they "look right"
		public static readonly Texture TERRAIN_TEXTURE_1 = new Texture(new UUID("0bc58228-74a0-7e83-89bc-5c23464bcec5"), Color.FromArgb(165, 137, 118));
		public static readonly Texture TERRAIN_TEXTURE_2 = new Texture(new UUID("63338ede-0037-c4fd-855b-015d77112fc8"), Color.FromArgb(69, 89, 49));
		public static readonly Texture TERRAIN_TEXTURE_3 = new Texture(new UUID("303cd381-8560-7579-23f1-f0a880799740"), Color.FromArgb(162, 154, 141));
		public static readonly Texture TERRAIN_TEXTURE_4 = new Texture(new UUID("53a2f406-4895-1d13-d541-d2e3b86bc19c"), Color.FromArgb(200, 200, 200));

	}
}

