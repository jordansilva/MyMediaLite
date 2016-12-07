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
using System.Linq;

namespace MyMediaLite.Data
{
	public class QueryResult
	{
		public string Algorithm { get; set; }
		public string Description { get; set; }
		public List<QueryItem> Items { get; set; }
		public List<Tuple<string, double>> Metrics { get; set; }

		public QueryResult (string algorithm, string description)
		{
			Algorithm = algorithm;
			Description = description;
			Items = new List<QueryItem> ();
			Metrics = new List<Tuple<string, double>> ();
		}

		public void Add (int id, IList<Tuple<int, float>> rank, string description)
		{
			Items.Add(QueryItem.Create (id, rank, description));				
		}

		public void AddMetric (string metric, double result)
		{
			Metrics.Add(Tuple.Create (metric, result));
		}

		public double GetMetric (string metric)
		{
			Tuple<string, double> tuple = Metrics.First (m => m.Item1.Equals (metric, StringComparison.OrdinalIgnoreCase));
			if (tuple == null)
				throw new Exception ("Metric does not exist!");
			else
				return tuple.Item2;
		}
	}

	public class QueryItem
	{
		public int Id { get; set; }
		public IList<Tuple<int, float>> Rank { get; set; }
		public string Description { get; set; }

		public static QueryItem Create (int id, IList<Tuple<int, float>> rank)
		{
			return new QueryItem { Id = id, Rank = rank };
		}

		public static QueryItem Create (int id, IList<Tuple<int, float>> rank, string description)
		{
			return new QueryItem { Id = id, Rank = rank, Description = description };
		}
	}

}
