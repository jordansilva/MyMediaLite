// Copyright (C) 2012 Zeno Gantner
//
// This file is part of MyMediaLite.
//
// MyMediaLite is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// MyMediaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with MyMediaLite.  If not, see <http://www.gnu.org/licenses/>.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baselines.Algorithms;
using CsvHelper;

namespace Baselines.Helper
{
	public class Utils
	{
		public static IList<Checkin> ReadCheckins(string filename, bool skip_header = true)
		{
			TextReader stream = File.OpenText (filename);

			var csvHelper = new CsvReader (stream);
			csvHelper.Configuration.Delimiter = ",";
			csvHelper.Configuration.HasHeaderRecord = true;
			csvHelper.Configuration.RegisterClassMap<CheckinMap> ();
			csvHelper.Configuration.RegisterClassMap<CoordinateMap> ();

			//csvHelper.ReadHeader ();
			//if (csvHelper.Read ()) {
			//	Checkin checkin = csvHelper.GetRecord<Checkin> ();
			//	Console.WriteLine (checkin);
			//}

			var checkins = csvHelper.GetRecords<Checkin> ().ToList();
			stream.Close ();

			return checkins;
		}

	}
}
