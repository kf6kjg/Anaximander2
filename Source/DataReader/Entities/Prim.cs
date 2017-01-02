// Prim.cs
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

namespace DataReader {
	public class Prim {
		#region Public Properties and Accessors

		public int ObjectFlags { get { return _primData.ObjectFlags; } }

		public int State { get { return _primData.State; } }

		public float PositionX  { get { return (float) _primData.PositionX; } }

		public float PositionY { get { return (float) _primData.PositionY; } }

		public float PositionZ { get { return (float) _primData.PositionZ; } }

		public float GroupPositionX { get { return (float) _primData.GroupPositionX; } }

		public float GroupPositionY { get { return (float) _primData.GroupPositionY; } }

		public float GroupPositionZ { get { return (float) _primData.GroupPositionZ; } }

		public float RotationX { get { return (float) _primData.RotationX; } }

		public float RotationY { get { return (float) _primData.RotationY; } }

		public float RotationZ { get { return (float) _primData.RotationZ; } }

		public float RotationW { get { return (float) _primData.RotationW; } }

		public float ScaleX { get { return (float) _primData.ScaleX; } }

		public float ScaleY { get { return (float) _primData.ScaleY; } }

		public float ScaleZ { get { return (float) _primData.ScaleZ; } }

		public float? RootRotationX { get { return (float?) _primData.RootRotationX; } }

		public float? RootRotationY { get { return (float?) _primData.RootRotationY; } }

		public float? RootRotationZ { get { return (float?) _primData.RootRotationZ; } }

		public float? RootRotationW { get { return (float?) _primData.RootRotationW; } }

		public byte[] Texture { get { return _primData.Texture; } }

		#endregion

		#region Private Properties

		private RegionPrimData _primData;

		#endregion

		#region Constructors

		public Prim(ref RegionPrimData data) {
			_primData = data;
		}

		#endregion
	}
}

