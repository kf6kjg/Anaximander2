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
using System.Collections.Generic;
using System.Net;
using NUnit.Framework;
using RestSharp;
using RestApi;

namespace UnitTests {
	[TestFixture]
	public class AuthenticationTests {
		private static readonly string _domain = "localhost";
		private static readonly uint _port = 6473;
		private static readonly bool _useSSL = false;
		private static readonly string _protocol = _useSSL ? "https" : "http";

		private static string _regionUUID;
		private static RulesModel MapRulesDelegate(string uuid = null) {
			_regionUUID = uuid;
			return new RulesModel
			{
				Info = new GeneralRulesModel {
					PushNotifyEvents = new List<PushNotifyOn> {
						PushNotifyOn.DBUpdate,
					},
					PushNotifyUri = new Uri($"{_protocol}://{_domain}:{_port}/wherever_I_want_it_to__be"),
				},
			};
		}

		private static void UpdateRegionDelegate(string uuid, ChangeInfo changeData) {
			_regionUUID = uuid;
		}

		private static bool CheckAPIKeyDelegate(string apiKey, string uuid) {
			_regionUUID = uuid;
			return apiKey == uuid;
		}

		[TestFixtureSetUp]
		public void Init() {
			RestAPI.StartHost(UpdateRegionDelegate, MapRulesDelegate, CheckAPIKeyDelegate, _domain, _port, _useSSL);
		}

		[TestFixtureTearDown]
		public void Stop() {
			RestAPI.StopHost();
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

		#region Map Rules
		[Test]
		public void TestGetMapRulesGlobal() {
			var client = new RestClient($"{_protocol}://{_domain}:{_port}");
			var request = new RestRequest("maprules", Method.GET);
			var response = client.Execute<RulesModel>(request);

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Bad Status: \n\n" + response.Content);
			Assert.Null(_regionUUID);
			//Assert.AreEqual(true, response.Data?.terrainShape);
			//Assert.AreEqual(false, response.Data?.terrainTexture);
			//Assert.AreEqual(2, response.Data?.minPrimScaleX);
		}

		[Test]
		public void TestGetMapRulesRegionSpecific() {
			string regionUUID = Guid.NewGuid().ToString();

			var client = new RestClient($"{_protocol}://{_domain}:{_port}");
			var request = new RestRequest($"maprules/{regionUUID}", Method.GET);
			var response = client.Execute<Dictionary<string,object>>(request);
			//var response = client.Execute(request);
			//{"info":{"pushNotifyUri":"http://localhost:6473/wherever_I_want_it_to__be","pushNotifyEvents":["DBUpdate"]}}

			Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, "Bad Status: \n\n" + response.Content);
			Assert.AreEqual(regionUUID, _regionUUID);
			Assert.True(response.Data.ContainsKey("info"), "Missing 'info' key in response!");

			var info = (IDictionary<string,object>)response.Data["info"];
			Assert.IsNotNull(info, "Key 'info' must be a JSON object!");
			Assert.True(info.ContainsKey("pushNotifyUri"), "Missing 'pushNotifyUri' key in the 'info' object!");
			Assert.AreEqual($"{_protocol}://{_domain}:{_port}/wherever_I_want_it_to__be", info["pushNotifyUri"]);

			Assert.True(info.ContainsKey("pushNotifyEvents"), "Missing 'pushNotifyEvents' key in the 'info' object!");
			var events = (JsonArray)info["pushNotifyEvents"];
			Assert.IsNotNull(events, "Key 'pushNotifyEvents' must be a JSON array!");
			Assert.AreEqual("DBUpdate", events[0]);
		}
		#endregion

		#region Update Region
		[Test]
		public void TestUpdateRegionGoodKey() {
			string regionUUID = Guid.NewGuid().ToString();

			string APIKey = regionUUID;

			var client = new RestClient($"{_protocol}://{_domain}:{_port}");
			var request = new RestRequest($"updateregion/{regionUUID}", Method.POST);
			request.RequestFormat = DataFormat.Json;

			var body = new
			{
				terrainHeight = true
			};

			request.AddJsonBody(body);
			request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
			request.AddHeader("Authorization", APIKey);

			var response = client.Execute(request);

			Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode, "Bad Status: \n\n" + response.Content);
			Assert.AreEqual(regionUUID, _regionUUID);
		}

		[Test]
		public void TestUpdateRegionBadKey() {
			string regionUUID = Guid.NewGuid().ToString();

			const string APIKey = "greensleeves";

			var client = new RestClient($"{_protocol}://{_domain}:{_port}");
			var request = new RestRequest($"updateregion/{regionUUID}", Method.POST);
			request.RequestFormat = DataFormat.Json;

			var body = new
			{
				terrainHeight = true
			};

			request.AddJsonBody(body);
			request.OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; };
			request.AddHeader("Authorization", APIKey);

			var response = client.Execute(request);

			Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode, "Bad Status: \n\n" + response.Content);
			Assert.AreEqual(regionUUID, _regionUUID);
		}
		#endregion
	}
}

