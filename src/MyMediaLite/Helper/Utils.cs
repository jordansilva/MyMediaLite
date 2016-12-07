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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CsvHelper;
using MyMediaLite.Data;
using MyMediaLite.IO;

namespace MyMediaLite.Helper
{
	public class Utils
	{

		public static IList<POI> ReadPOIs (string filename, bool skip_header = true)
		{
			TextReader stream = File.OpenText (filename);

			var csvHelper = new CsvReader (stream);
			csvHelper.Configuration.Delimiter = ",";
			csvHelper.Configuration.HasHeaderRecord = skip_header;
			csvHelper.Configuration.RegisterClassMap<POIMap> ();
			csvHelper.Configuration.RegisterClassMap<POICoordinateMap> ();
			var items = csvHelper.GetRecords<POI> ().ToList ();
			stream.Close ();

			return items;
		}

		public static IList<Checkin> ReadCheckins(string filename, bool skip_header = true)
		{
			string binary_filename = filename + ".bin.Baselines";
			if (File.Exists (binary_filename))
				return FileSerializer.Deserialize (binary_filename) as IList<Checkin>;
			
			TextReader stream = File.OpenText (filename);

			var csvHelper = new CsvReader (stream);
			csvHelper.Configuration.Delimiter = ",";
			csvHelper.Configuration.HasHeaderRecord = skip_header;
			csvHelper.Configuration.RegisterClassMap<CheckinMap> ();
			csvHelper.Configuration.RegisterClassMap<CoordinateMap> ();
			var checkins = csvHelper.GetRecords<Checkin> ().ToList();
			stream.Close ();

			return checkins;
		}

		public static void CreateFile (string filename)
		{
			string path = Path.GetDirectoryName (filename);

			if (File.Exists (filename))
				File.Delete (filename);

			if (!Directory.Exists (path))
				Directory.CreateDirectory (path);
			
			FileStream stream = File.Create (filename);
			stream.Close ();
		}

		public static void SaveRank (string filename, QueryResult result)
		{
			//Saving results
			string path = string.Format ("output/{0}", filename);
			CreateFile (path);

			TextWriter writer = new StreamWriter (path, true, System.Text.Encoding.UTF8);
			foreach (var query in result.Items) {
				int pos = 1;
				foreach (var item in query.Rank) {
					string line = string.Format ("Q{0}\t0\t{1}\t{2}\t{3}\t{4}", query.Id, item.Item1, pos, item.Item2, query.Description);
					writer.WriteLine (line);
					pos++;
				}
			}
			writer.Close ();

			//Saving metrics
			string pathMetrics = string.Format ("output/{0}.metrics", filename);
			CreateFile (pathMetrics);
			writer = new StreamWriter (pathMetrics, true, System.Text.Encoding.UTF8);

			writer.WriteLine (result.Algorithm);
			writer.WriteLine (result.Description);
			foreach (var item in result.Metrics) {
				writer.WriteLine ("{0}: {1}", item.Item1, item.Item2);
			}
			writer.Close ();
		}
	}
}
