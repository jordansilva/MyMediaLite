// Copyright (C) 2015 Zeno Gantner
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
using System.Runtime.Serialization;

namespace MyMediaLite.Data
{
	///
	[Serializable]
	public class Checkin : ISerializable
	{
		///
		public int User { get; set; }
		///
		public int Item { get; set; }
		///
		public Coordinate Coordinates { get; set; }
		///
		public DateTime Date { get; set; }
		///
		public IList<int> Candidates { get; set; }

		///
		public Checkin ()
		{
		}

		///
		public Checkin (SerializationInfo info, StreamingContext context)
		{
			User = (int)info.GetValue ("User", typeof (int));
			Item = (int)info.GetValue ("Item", typeof (int));
			Coordinates = (Coordinate)info.GetValue ("Coordinates", typeof (Coordinate));
			Date = (DateTime)info.GetValue ("Date", typeof (DateTime));
			Candidates = (List<int>)info.GetValue ("Candidates", typeof (List<int>));
		}

		///
		public Checkin (int user, int item, float lat, float lng, DateTime date, IList<int> candidates)
		{
			User = user;
			Item = item;
			Coordinates = new Coordinate (lat, lng);
			Date = date;
			Candidates = candidates;
		}

		///
		public void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			info.AddValue ("User", User);
			info.AddValue ("Item", Item);
			info.AddValue ("Coordinates", Coordinates);
			info.AddValue ("Date", Date);
			info.AddValue ("Candidates", Candidates);
		}
	}




}
