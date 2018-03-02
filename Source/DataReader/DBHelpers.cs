// DBHelpers.cs
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
using System.Data;
using System.Reflection;
using log4net;
using MySql.Data.MySqlClient;

namespace DataReader {
	public static class DBHelpers {
		private static readonly ILog LOG = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public static MySqlConnection GetConnection(string connection_string) {
			try {
				var conn = new MySqlConnection(connection_string);
				conn.Open();

				return conn;
			}
			catch (MySqlException e) {
				throw new DatabaseException("MySQL server refused connection or is not running.", e);
			}
		}

		public static IDataReader ExecuteReader(IDbCommand c) {
			IDataReader reader = null;
			bool errorSeen = false;

			try {
				while (true) {
					try {
						reader = c.ExecuteReader();
					}
					catch (Exception) {
						if (!errorSeen) {
							errorSeen = true;
							continue;
						}
						throw;
					}

					break;
				}
			}
			catch (MySqlException e) {
				LOG.Error($"MySQL query failed or the MySQL server was not available.\n{Environment.StackTrace}", e);
			}

			return reader;
		}

	}
}

