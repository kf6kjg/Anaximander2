// RollbarAppender.cs
//
// Copyright (c) 2014 Morten Teinum
// Copied from https://github.com/mteinum/log4net.Rollbar  as of commit 95779818788a8dc2630a3241b6ae266831f9c022
// Further changes (c) 2016 Richard Curtice under the same license.
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
using System.Configuration;
using log4net.Appender;
using log4net.Core;
using System.Threading.Tasks;
using RollbarNET;
using RollbarNET.Serialization;

namespace log4net_RollbarNET {
	public class RollbarAppender : AppenderSkeleton {
		private RollbarNET.Configuration _configuration;

		public string AccessToken { get; set; }

		public string Environment { get; set; }

		public string Endpoint { get; set; }

		public string Framework { get; set; }

		public string GitSha { get; set; }

		public string Language { get; set; }

		public string Platform { get; set; }

		public string ScrubParams { get; set; }

		/// <summary>
		/// Send log events to Rollbar asynchronously
		/// </summary>
		public bool Asynchronous { get; set; }

		public RollbarAppender() {
			Asynchronous = true;
		}

		public override void ActivateOptions() {
			_configuration = new RollbarNET.Configuration(GetConfigSetting(AccessToken, "Rollbar.AccessToken"));

			_configuration.Endpoint = GetConfigSetting(Endpoint, "Rollbar.Endpoint", _configuration.Endpoint);
			_configuration.Environment = GetConfigSetting(Environment, "Rollbar.Environment", _configuration.Environment);
			_configuration.Framework = GetConfigSetting(Framework, "Rollbar.Framework", _configuration.Framework);
			_configuration.GitSha = GetConfigSetting(GitSha, "Rollbar.GitSha");
			_configuration.Language = GetConfigSetting(Language, "Rollbar.CodeLanguage", _configuration.Language);
			_configuration.Platform = GetConfigSetting(Platform, "Rollbar.Platform", _configuration.Platform);
			Asynchronous = bool.Parse(GetConfigSetting(Platform, "Rollbar.Asynchronous", Asynchronous.ToString()));

			var scrubParams = GetConfigSetting(ScrubParams, "Rollbar.ScrubParams");
			_configuration.ScrubParams = scrubParams == null ? RollbarNET.Configuration.DefaultScrubParams : scrubParams.Split(',');
		}

		private static string GetConfigSetting(string param, string name, string fallback = null) {
			return param ?? ConfigurationManager.AppSettings[name] ?? fallback;
		}

		/// <summary>
		/// Sends the given event to Rollbar
		/// </summary>
		/// <param name="loggingEvent">The event to report</param>
		protected override void Append(LoggingEvent loggingEvent) {
			var client = new RollbarClient(_configuration);

			Task task = null;
			if (loggingEvent.Level >= Level.Critical) {
				task = Send(loggingEvent, client.SendCriticalMessage, client.SendCriticalException);
			}
			else if (loggingEvent.Level >= Level.Error) {
				task = Send(loggingEvent, client.SendErrorMessage, client.SendErrorException);
			}
			else if (loggingEvent.Level >= Level.Warn) {
				task = Send(loggingEvent, client.SendWarningMessage, client.SendWarningException);
			}
			else if (loggingEvent.Level >= Level.Info) {
				task = client.SendInfoMessage(loggingEvent.RenderedMessage);
			}
			else if (loggingEvent.Level >= Level.Debug) {
				task = client.SendDebugMessage(loggingEvent.RenderedMessage);
			}

			if (task != null && !Asynchronous) {
				task.Wait(TimeSpan.FromSeconds(5));
			}
		}

		/// <summary>
		/// Helper method for reporting a given event. Keeps the code DRY
		/// </summary>
		/// <param name="loggingEvent"></param>
		/// <param name="sendMessage"></param>
		/// <param name="sendException"></param>
		private static Task Send(
			LoggingEvent loggingEvent,
			Func<string, IDictionary<string, object>, Action<DataModel>, string, Task> sendMessage,
			Func<Exception, string, Action<DataModel>, string, Task> sendException) {
			if (loggingEvent.ExceptionObject == null) {
				return sendMessage(loggingEvent.RenderedMessage, null, null, null);
			}

			return sendException(loggingEvent.ExceptionObject, null, null, null);
		}
	}
}