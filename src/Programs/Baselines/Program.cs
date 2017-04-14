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
using Baselines.Commands;
using Mono.Options;
using MyMediaLite.Data;
using MyMediaLite.Helper;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace Baselines
{

	class Program
	{
		static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		const string MODEL_FOLDER = "/Volumes/Tyr/Projects/UFMG/Baselines/MyMediaLite-3.11/bin/"; //model-100/
		const string DATASET_FOLDER = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc/";
		string mFold;
		string mTraining;
		string mValidation;
		string mTest;
		string mMethod;
		//private string mTest;


		public static void Main (string [] args)
		{
			//Proof that Python has a slower iteration than other languages
			//long x = 0;
			//int c = 10000;
			//TimeSpan t = Wrap.MeasureTime (delegate () {
			//	for (int i = 0; i < c; i++) {
			//		for (int j = 0; j < c; j++) {
			//			x = x + i;
			//		}
			//	}
			//});
			//Console.WriteLine ("Count {0}: {1} seconds", c, t.TotalSeconds);

			Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo ("en-US");

			var program = new Program ();
			//program.mFold = "fold_1";
			//program.mTraining = string.Format ("{0}/{1}/train_all.txt", DATASET_FOLDER, program.mFold);
			//program.mValidation = string.Format ("{0}/{1}/validation.txt", DATASET_FOLDER, program.mFold);
			//program.mTest = string.Format ("{0}/{1}/test.txt", DATASET_FOLDER, program.mFold);

			////MappingFoldHelper mapping = new MappingFoldHelper (program.mTraining, program.mValidation, program.mTest);

			program.Run (args);

#if DEBUG
			//Console.WriteLine ("Press enter to close...");
			//Console.ReadLine ();
#endif
		}

		void Run (string [] args)
		{
			bool tunning = false;
			string mLoadModel = null;
			log.Info ("Running baselines");
			var options = new OptionSet {
				{ "training-file=",       v              => mTraining        = v },
				{ "tunning",              v              => tunning          = v != null },
				{ "test-file=",           v              => mValidation      = v },
				{ "load-model=",          v              => mLoadModel       = v },
				{ "recommender=",         v              => mMethod          = v }};

			options.Parse (args);
			//ReorganizeFiles ();

#if DEBUG
			string [] args_data = ConfigureDataset (1);
			string args_algo = ConfigureAlgorithm (Algorithms.xQuAD);

			var list_args = new List<string> ();
			if (args_data != null)
				list_args.AddRange (args_data);

			if (args_algo != null)
				list_args.AddRange (args_algo.Split(' '));

			args = list_args.ToArray ();
			tunning = true;
#endif

			//mLoadModel = "/Volumes/Tyr/Projects/UFMG/Baselines/Jordan/MyMediaLite-Research/src/Programs/Baselines/bin/best_iteration/best_iteration";

			string methodName = string.Format ("Baselines.Commands.{0}Command", mMethod);
			Type type = Type.GetType (methodName);
			if (type == null)
				throw new Exception ("Recommender method not found!");

			Command command = Create (type, mTraining, mValidation);
			command.SetupOptions (args);

			if (tunning) {
				Console.WriteLine ("Tunning algorithm");
				command.Tunning ();
			} else {
				if (!string.IsNullOrEmpty (mLoadModel)) {
					Console.WriteLine ("Loading model...");
					command.LoadModel (mLoadModel);
				} else {
					Console.WriteLine ("Training algorithm");
					command.Train ();
					var filename = string.Format ("output/model/{0}.model", mMethod);
					Utils.CreateFile (filename);

					if (!methodName.Contains ("RankGeoFM")) {
						try {
							command.SaveModel (filename);
						} catch (Exception ex) {
							Console.WriteLine (ex.Message);
						}
					}
				}

				command.Evaluate (mValidation);
			}

			log.Info ("Process ending");
		}

		Command Create (Type type, string training, string validation)
		{
			return Activator.CreateInstance (type, training, validation) as Command;
		}

		Command Create (Type type, string path)
		{
			Command command = Activator.CreateInstance (type) as Command;
			command.LoadModel (path);
			return command;
		}

		string [] ConfigureDataset (int dataset)
		{
			switch (dataset) {
			case 2:
				//SG
				mTraining = "/Volumes/Tyr/Projects/UFMG/Baselines/Jordan/MyMediaLite-Research/src/Programs/Baselines/bin/parallel/SG_NEW/train_tensor.txt";
				mValidation = "/Volumes/Tyr/Projects/UFMG/Baselines/Jordan/MyMediaLite-Research/src/Programs/Baselines/bin/parallel/SG_NEW/test_tensor.txt";
				return new string [] { "--item-file=/Volumes/Tyr/Projects/UFMG/Baselines/Jordan/MyMediaLite-Research/src/Programs/Baselines/bin/parallel/SG_NEW/tensor_lat_lng.txt",
				"--user-file=/Volumes/Tyr/Projects/UFMG/Baselines/Jordan/MyMediaLite-Research/src/Programs/Baselines/bin/parallel/SG_NEW/users.txt"};
			case 1:
			default:
				//Reduced NYC
				mTraining = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc-reduced/fold_1/training.txt";
				mValidation = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc-reduced/fold_1/test.txt";
				return new string [] { "--item-file=/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc-reduced/venues.txt", "--user-file=/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc-reduced/users.txt" };
			}
		}

		public enum Algorithms
		{
			GEO = 0,
			BPRMF = 1,
			WRMF = 2,
			WBPRMF = 3,
			ItemKNN = 4,
			UserKNN = 5,
			MostPopular = 6,
			MostPopular_False = 7,
			RankGeoFM = 8,
			WPOI = 9,
			xQuAD = 10
		}

		string ConfigureAlgorithm (Algorithms algo)
		{
			switch (algo) {
			case Algorithms.BPRMF:
				mMethod = "BPRMF";
				return "--num-factors=100 --learn-rate=0.1 --regularization=0.03";
			case Algorithms.WRMF:
				mMethod = "WRMF";
				return "--num-factors=5 --regularization=0.1";
			case Algorithms.WBPRMF:
				mMethod = "WBPRMF";
				return "--num-factors=1000 --regularization=0.1 --learn-rate=0.1";
			case Algorithms.ItemKNN:
				mMethod = "ItemKNN";
				return "--k=80";
			case Algorithms.UserKNN:
				mMethod = "UserKNN";
				return "--k=1000";
			case Algorithms.MostPopular:
				mMethod = "MostPopular";
				return "--byuser=true";
			case Algorithms.MostPopular_False:
				mMethod = "MostPopular";
				return "--byuser=true";
			case Algorithms.RankGeoFM:
				mMethod = "RankGeoFM";
				return null;
			case Algorithms.xQuAD:
				mMethod = "xQuAD";
				return "--item-attributes=/Volumes/Tyr/Projects/UFMG/Apocalypse/datasets/NYC/reduced/attributes.csv";
			default:
				return null;
			}
		}


		void ReorganizeFiles ()
		{
			string path = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/all/";
			string file_poi = "/Volumes/Tyr/Projects/UFMG/Datasets/Ours/nyc/places.txt";

			Console.WriteLine ("Reading POIs...");
			IList<POI> pois = MyMediaLite.Helper.Utils.ReadPOIs (file_poi);
			var poisCoord = new Dictionary<int, Coordinate> ();
			foreach (var item in pois) {
				poisCoord.Add (item.Id, item.Coordinates);
			}

			ParallelOptions opts = new ParallelOptions {
				MaxDegreeOfParallelism =
				Convert.ToInt32 (Math.Ceiling ((Environment.ProcessorCount * 0.95) * 1.0))
			};

			//Parallel.For (1, 11, opts, index => {
			//	if (index > 10)
			//		return;

			//	string training = string.Format ("{0}/train_{1}-down.txt", path, index);
			//	Console.WriteLine ("Training {0}...", index);
			//	ReorganizeCheckins (training, poisCoord);
			//});

			//Parallel.For (4, 13, opts, index => {
			//	if (index > 13)
			//		return;
			//	string test = string.Format ("{0}/test-{1}-down.txt", path, index);
			//	Console.WriteLine ("Test {0}...", index);
			//	ReorganizeCheckins (test, poisCoord);
			//});
		}

		void ReorganizeCheckins (string filename, Dictionary<int, Coordinate> pois)
		{
			Console.WriteLine ("Read checkins...");
			IList<Checkin> checkins = MyMediaLite.Helper.Utils.ReadCheckins (filename);
			var checkinsNew = new List<Checkin> ();

			Console.WriteLine ("Computing distance...");
			foreach (var item in checkins) {
				var candidatesChecked = new Dictionary<int, double> ();
				foreach (var item2 in item.Candidates) {
					var coord = pois [item2];
					var distance = 0;
					//var distance = DistanceHelper.Distance (item.Coordinates.Latitude, item.Coordinates.Longitude,
					//						 coord.Latitude, coord.Longitude);
					candidatesChecked.Add (item2, distance);
				}

				item.Candidates = candidatesChecked.OrderBy (x => x.Value).Select (x => x.Key).ToList ();
				checkinsNew.Add (item);
			}

			MyMediaLite.Helper.Utils.SaveCheckins (string.Format ("{0}.new", filename), checkinsNew);
		}

	}
}
