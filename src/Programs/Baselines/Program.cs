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
using MyMediaLite.Eval.Measures;
using Baselines.Algorithms;
using System.IO;
using CsvHelper;
using System.Linq;
using System.Collections.Generic;
using Baselines.Helper;
using MyMediaLite;

namespace Baselines
{
	
	class Program
	{
		public static void Main (string [] args)
		{
			var program = new Program ();
			program.Run (args);
		}


		void Run (string [] args)
		{
			string model = "/Volumes/Tyr/Projects/UFMG/Baselines/MyMediaLite-3.11/bin/model-100/model-n100.test";
			string validation = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc/fold_1/validation.txt";
			//string test = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc/fold_1/test.txt";

			//Loading data
			IList<Checkin> data = LoadData(validation);

			//Loading algorithm
			IBaseline algorithm = LoadModel (model);

			//Evaluation
			QueryResult result = null;
			t = Wrap.MeasureTime (delegate () { result = Evaluation (algorithm, data); });
			Console.WriteLine ("Predicting {0} items: {1} seconds", result.Items.Count(), t.TotalSeconds);

			#if DEBUG
    		Console.WriteLine ("Press enter to close...");
			Console.ReadLine ();
			#endif
		}

		IBaseline LoadModel (string model)
		{
			IBaseline algorithm = null;
			TimeSpan t = Wrap.MeasureTime (delegate () { algorithm = new RunBPRMF (model); });
			Console.WriteLine ("Loading model: {0} seconds", t.TotalSeconds);
			return algorithm;
		}

		IList<Checkin> LoadData (String filename)
		{
			IList<Checkin> result = null;
			Console.WriteLine ("Loading data: {0}", filename);
			TimeSpan t = Wrap.MeasureTime (delegate () { result = Helper.Utils.ReadCheckins (filename); });
			Console.WriteLine ("Read data: {0} seconds", t.TotalSeconds);
			return result;
		}

		QueryResult Evaluation (IBaseline baseline, IList<Checkin> test)
		{
			QueryResult queryResult = new QueryResult (baseline.Name (), baseline.ToString ());
			int i = 0;
			double evaluation = 0.0f;
			foreach (Checkin item in test) {
				i++;
				IList<Tuple<int, float>> ratings = baseline.Predict (item.User, item.CandidatesAll);
				queryResult.Add (i, ratings, String.Format ("u{0}", item.User));
				evaluation += ReciprocalRank.Compute (ratings.Select (x => x.Item1).ToList (), new List<int> () { item.Item });
			}

			evaluation = evaluation/(i*1.0f);
			queryResult.AddMetric ("MRR", evaluation);
			Console.WriteLine ("MRR {0}", evaluation);
			return queryResult;
		}



	}
}
