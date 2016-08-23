// AuthenticationTests.cs
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
using System.Collections;
using System.Linq;
using System.Net;
using NUnit.Framework;
using RestSharp;

namespace UnitTests {
	[TestFixture]
	public class AuthenticationTests {
		private static readonly string _domain = "localhost";
		private static readonly uint _port = 6473;
		private static readonly bool _useSSL = false;
		private static readonly string _protocol = _useSSL ? "https" : "http";

		private static string _regionUUID;
		private static IEnumerable MapRulesDelegate(string uuid = null) {
			_regionUUID = uuid;
			return Enumerable.Repeat("abc",1);
		}

		private static void UpdateRegionDelegate(string uuid) {
			_regionUUID = uuid;
		}

		[SetUp]
		public void Init() {
			RestApi.RestAPI.StartHost(UpdateRegionDelegate, MapRulesDelegate, _domain, _port, _useSSL);
		}

		[Test]
		public void CheckActive() {
			var client = new RestClient($"{_protocol}://{_domain}:{_port}");
			var request = new RestRequest("test", Method.GET);
			IRestResponse response = client.Execute(request);
			var content = response.Content; // raw content as string

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Bad Status: \n\n" + response.Content);
			Assert.AreEqual("OK", content);
		}

		[Test]
		public void TestGetMapRulesGlobal() {
			var client = new RestClient($"{_protocol}://{_domain}:{_port}");
			var request = new RestRequest("maprules", Method.GET);
			IRestResponse response = client.Execute(request);
			var content = response.Content;

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Bad Status: \n\n" + response.Content);
			Assert.Null(_regionUUID);
		}

		[Test]
		public void TestGetMapRulesRegionSpecific() {
			string regionUUID = Guid.NewGuid().ToString();

			var client = new RestClient($"{_protocol}://{_domain}:{_port}");
			var request = new RestRequest($"maprules/{regionUUID}", Method.GET);
			IRestResponse response = client.Execute(request);
			var content = response.Content;

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Bad Status: \n\n" + response.Content);
			Assert.AreEqual(regionUUID, _regionUUID);
		}
	}
}

