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
using MyMediaLite.Data;
using MyMediaLite.IO;

namespace Baselines.Helper
{
	public class MappingFoldHelper
	{
		internal Dictionary<string, int> original_to_internal = new Dictionary<string, int> ();

		List<int> mCandidatesOriginal;

		
		public MappingFoldHelper (string training, string validation, string test)
		{
			mCandidatesOriginal = new List<int> ();

			FoldTest fold = LoadData(training, validation, test);

			AddItems (fold.Training);
			//AddItems (fold.Validation);
			//AddItems (fold.Test);

			var grouping = mCandidatesOriginal.GroupBy (x => x);
			grouping = grouping.OrderByDescending (x => x.Count()).ToList ();

			var grouping2 = grouping.Select (x => new { Key = x.Key, Value = x.Count() }).ToList();
			grouping2 = grouping2.Take (5).ToList();

			foreach (var item in grouping2)
				Console.WriteLine ("{0}: {1}", item.Key, item.Value);
			
			//Console.WriteLine ("Grouping: {0}", grouping.Count());

			Distinct ();
		}

		FoldTest LoadData (string training, string validation, string test)
		{
			string filename = "fold.bin.Baselines";
			if (File.Exists (filename))
				return (FoldTest)FileSerializer.Deserialize (filename);

			FoldTest fold = new FoldTest ();
			fold.Training = MyMediaLite.Helper.Utils.ReadCheckins (training);
			fold.Validation = MyMediaLite.Helper.Utils.ReadCheckins (validation);
			fold.Test = MyMediaLite.Helper.Utils.ReadCheckins (test);

			if (FileSerializer.CanWrite (filename))
				fold.Serialize (filename);

			return fold;
		}

		void AddItems (IList<Checkin> training_data)
		{
			mCandidatesOriginal.AddRange (training_data.Select (x => x.Item));
			//foreach (var item in training_data)
			//	mCandidatesOriginal.AddRange (item.CandidatesAll);
		}

		void Distinct () {
			mCandidatesOriginal = mCandidatesOriginal.Distinct ().OrderBy (x => x).ToList ();
			Console.WriteLine ("total candidates items: {0}", mCandidatesOriginal.Count);
		}

	}
}
