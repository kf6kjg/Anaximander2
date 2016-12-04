// RulesModel.cs
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
using System.Collections.Generic;

namespace RestApi {
	public class RulesModel {

		public GeneralRulesModel Info { get; set; }

		// TODO: find rules for notifying, if any, that can be known after region DB persist time. Otherwise it'll jsut be "notify after every DB commit"
		// Theoretically it could even be the DB itself sending the push notify.s

		// The below cannot be used because at the time the region knows about this,
		//  the data has not yet been persisted to the DB.
		// To use, it woudl require the region sending the data to the db or directly to Anax - a waste of resources either way.
		// -----
		// flag mask: (PrimFlags.Physics | PrimFlags.Temporary | PrimFlags.TemporaryOnRez)
		// Min part scale: part.Scale.X > 1f || part.Scale.Y > 1f || part.Scale.Z > 1f
		// Max part scale
		// allow offregion prims? (should there be a difference if center (AllowObjectOriginOffRegion) vs any OBB corner?)
		// Max altitude
		// altitude reference: Region, Water, Terrain (, maybe water|Terrain?)
		// ?Min altitude
		// MinimumTaintedMapTileWaitTime in seconds
		// MaximumTaintedMapTileWaitTime in seconds
		// Invalid PCode[] = PCode.Tree, PCode.NewTree, PCode.Grass
		// 
	}

	public class GeneralRulesModel {
		public Uri PushNotifyUri { get; set; }
		public IEnumerable<PushNotifyOn> PushNotifyEvents { get; set; }
	}

	public enum PushNotifyOn {
		DBUpdate
	}
}

