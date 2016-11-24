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
using CsvHelper.Configuration;

namespace Baselines.Algorithms
{
	public class Checkin
	{
		public int User { get; set; }
		public int Item { get; set; }
		public Coordinate Coordinates { get; set; }
		public DateTime Date { get; set; }
		public IList<int> CandidatesChecked { get; set; }
		public IList<int> CandidatesAll { get; set; }
		public Checkin ()
		{
		}

		public Checkin (int user, int item, float lat, float lng, DateTime date, IList<int> candidates_checked, IList<int> candidates_all)
		{
			User = user;
			Item = item;
			Coordinates = new Coordinate(lat, lng);
			Date = date;
			CandidatesChecked = candidates_checked;
			CandidatesAll = candidates_all;
		}
	}

	public class Coordinate
	{
		public float Latitude { get; set; }
		public float Longitude { get; set; }

		public Coordinate ()
		{
		}

		public Coordinate (float lat, float lng)
		{
			Latitude = lat;
			Longitude = lng;
		}
	}

	public sealed class CheckinMap : CsvClassMap<Checkin>
	{
		public CheckinMap ()
		{
			Map(m => m.User).Name("user");
			Map(m => m.Item).Name("venue");
			References<CoordinateMap> (m => m.Coordinates);
			Map (m => m.Date).Name ("time");
			Map (m => m.CandidatesChecked).Name ("cand_checked").TypeConverter(new Helper.EnumarableConverter());
			Map (m => m.CandidatesAll).Name ("cand_all").TypeConverter (new Helper.EnumarableConverter ());
		}
	}

	public sealed class CoordinateMap : CsvClassMap<Coordinate>
	{
		public CoordinateMap ()
		{
			Map (m => m.Latitude).Index(1);
			Map (m => m.Longitude).Index(0);
		}
	}

}
