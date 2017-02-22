/*
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
using OpenMetaverse;
using ProtoBuf;

namespace AssetReader {
	[ProtoContract]
	public class AssetBase {
		[ProtoMember(1)]
		public Guid Id { get; set;}
		[ProtoMember(2)]
		public sbyte Type { get; set; }
		[ProtoMember(3)]
		public byte[] Data { get; set; }

		public AssetBase() {
		}

		public AssetBase(UUID id, byte type, byte[] data) {
			Id = id.Guid;
			Type = (sbyte)type;
			Data = data;
		}

		public bool ContainsReferences {
			get {
				return
						IsTextualAsset && (
						Type != (sbyte)AssetType.Notecard
						&& Type != (sbyte)AssetType.CallingCard
						&& Type != (sbyte)AssetType.LSLText
						&& Type != (sbyte)AssetType.Landmark);
			}
		}

		public bool IsTextualAsset {
			get {
				return !IsBinaryAsset;
			}
		}

		public bool IsBinaryAsset {
			get {
				return
						(Type == (sbyte)AssetType.Animation ||
						 Type == (sbyte)AssetType.Gesture ||
						 Type == (sbyte)AssetType.Simstate ||
						 Type == (sbyte)AssetType.Unknown ||
						 Type == (sbyte)AssetType.Object ||
						 Type == (sbyte)AssetType.Sound ||
						 Type == (sbyte)AssetType.SoundWAV ||
						 Type == (sbyte)AssetType.Texture ||
						 Type == (sbyte)AssetType.TextureTGA ||
						 Type == (sbyte)AssetType.Folder ||
						 Type == (sbyte)AssetType.RootFolder ||
						 Type == (sbyte)AssetType.LostAndFoundFolder ||
						 Type == (sbyte)AssetType.SnapshotFolder ||
						 Type == (sbyte)AssetType.TrashFolder ||
						 Type == (sbyte)AssetType.ImageJPEG ||
						 Type == (sbyte)AssetType.ImageTGA ||
						 Type == (sbyte)AssetType.LSLBytecode);
			}
		}

		public bool IsImageAsset {
			get {
				return (
						 Type == (sbyte)AssetType.Texture ||
						 Type == (sbyte)AssetType.TextureTGA ||
						 Type == (sbyte)AssetType.ImageJPEG ||
						 Type == (sbyte)AssetType.ImageTGA
						 );
			}
		}

		public OpenMetaverse.Assets.AssetTexture ToTexture() {
			if (IsImageAsset) {
				return new OpenMetaverse.Assets.AssetTexture(new UUID(Id), Data);
			}

			return null;
		}
	}
}
