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
using CsvHelper.Configuration;

namespace MyMediaLite.Data
{
	///
	public class User
	{
		///
		public int Id { get; set; }
		public string TwitterId { get; set; }
		public string CanonicalPath { get; set; }

		///
		public User ()
		{
		}

		///
		public User (int id, string twitter_id, string canonical)
		{
			Id = id;
			TwitterId = twitter_id;
			CanonicalPath = canonical;
		}
	}

	///
	public sealed class UserMap : CsvClassMap<User>
	{
		///
		public UserMap ()
		{
			Map (m => m.Id).Name ("uid");
			Map (m => m.TwitterId).Name ("twitter_id");
			Map (m => m.CanonicalPath).Name ("canonicalPath");
		}
	}

}
