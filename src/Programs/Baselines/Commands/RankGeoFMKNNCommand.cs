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
using MyMediaLite;
using MyMediaLite.RatingPrediction;
using MyMediaLite.IO;
using MyMediaLite.Data;
using Mono.Options;
using System.IO;

namespace Baselines.Commands
{

	public class RankGeoFMKNNCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		//ITimedRatings FeedbackRatings;
		IList<POI> Items;
		IList<User> Users;
		IMapping user_mappings;
		IMapping item_mappings;
		uint iterations = 1000;
		bool isPaperVersion = true;

		IList<Checkin> FeedbackCheckins;


		public RankGeoFMKNNCommand (string training, string test) : base (typeof (RankGeoFMKNN))
		{
			path_training = training;
			path_test = test;
			Init ();
		}

		public override void SetupOptions (string [] args)
		{
			base.SetupOptions (args);

			string user_file = null;
			string item_file = null;

			var options = new OptionSet {
				{ "user-file=",      v              => user_file      = v },
				{ "item-file=",      v              => item_file      = v },
				{ "num-iterations=", v              => iterations     = uint.Parse(v)},
				{ "matlab",          v              => isPaperVersion = v == null}
			};
			options.Parse (args);

			if (!string.IsNullOrEmpty (item_file)) {
				Console.Write ("Loading items data...");
				Items = MyMediaLite.Helper.Utils.ReadPOIs (item_file);
				Console.WriteLine ("Loaded!");
			}

			if (!string.IsNullOrEmpty (user_file)) {
				Console.Write ("Loading users data... ");
				Users = MyMediaLite.Helper.Utils.ReadUsers (user_file);
				Console.WriteLine ("Loaded!");
			}

			((RankGeoFMKNN)Recommender).Items = Items;
			((RankGeoFMKNN)Recommender).Users = Users;
			((RankGeoFMKNN)Recommender).IsPaperVersion = isPaperVersion;
			((RankGeoFMKNN)Recommender).MaxIterations = iterations;

			if (Feedback != null)
				((RankGeoFMKNN)Recommender).Feedback = Feedback;

			if (FeedbackCheckins != null)
				((RankGeoFMKNN)Recommender).FeedbackCheckins = FeedbackCheckins;
			//if (FeedbackRatings != null)
			//  ((RankGeoFMKNN)Recommender).Ratings = FeedbackRatings;

			if (TestFeedback != null)
				((RankGeoFMKNN)Recommender).Validation = TestFeedback;

			if (user_mappings != null)
				((RankGeoFMKNN)Recommender).UserMapping = user_mappings;

			if (item_mappings != null)
				((RankGeoFMKNN)Recommender).ItemMapping = item_mappings;
		}

		protected override void Init ()
		{
			user_mappings = new IdentityMapping ();
			item_mappings = new IdentityMapping ();

			if (File.Exists ("user.mapping")) {
				user_mappings = "user.mapping".LoadMapping ();
				item_mappings = "item.mapping".LoadMapping ();
			}

			if (!string.IsNullOrEmpty (path_training)) {
				Console.WriteLine ("Loading training data");
				Feedback = ItemData.Read (path_training, user_mappings, item_mappings, true);
				FeedbackCheckins = MyMediaLite.Helper.Utils.ReadCheckins (path_training);
			}

			if (!string.IsNullOrEmpty (path_test)) {
				Console.WriteLine ("Loading test data");
				TestFeedback = ItemData.Read (path_test, user_mappings, item_mappings, true);
			}

			user_mappings.SaveMapping ("user.mapping");
			item_mappings.SaveMapping ("item.mapping");

			//if (!string.IsNullOrEmpty (path_test)) {
			//  Console.WriteLine ("Loading test data");
			//  Test = LoadTest (path_test);
			//}
		}

		public override void Tunning ()
		{
			//if (FeedbackRatings == null || FeedbackRatings.Count == 0)
			//  throw new Exception ("Training data can not be null");

			//if (Test == null || Test.Count == 0)
			//  throw new Exception ("Test data can not be null");
			//K = 100;
			//C = 1.0f;
			//ε = 0.3f;
			//γ = 0.0001f;
			//α = 0.2f;

			((RankGeoFMKNN)Recommender).Items = Items;
			((RankGeoFMKNN)Recommender).Users = Users;
			//((RankGeoFMKNN)Recommender).Ratings = FeedbackRatings;
			((RankGeoFMKNN)Recommender).Feedback = Feedback;
			((RankGeoFMKNN)Recommender).FeedbackCheckins = FeedbackCheckins;
			((RankGeoFMKNN)Recommender).Validation = TestFeedback;
			((RankGeoFMKNN)Recommender).UserMapping = user_mappings;
			((RankGeoFMKNN)Recommender).ItemMapping = item_mappings;
			((RankGeoFMKNN)Recommender).IsPaperVersion = isPaperVersion;
			((RankGeoFMKNN)Recommender).MaxIterations = iterations;

			var t = Wrap.MeasureTime (Recommender.Train);
			Console.WriteLine ("RankGeoFMKNN: {0} seconds", t.TotalSeconds);
		}

		public void Eval ()
		{
			Console.WriteLine ("Loading model...");
			CreateModel (typeof (RankGeoFMKNN));
			((RankGeoFMKNN)Recommender).Items = Items;
			//((RankGeoFMKNN)Recommender).Ratings = FeedbackRatings;
			((RankGeoFMKNN)Recommender).LoadModel ("");

			Console.WriteLine ("Evaluating");
			var results = MyMediaLite.Eval.Items.Evaluate (Recommender, TestFeedback, Feedback, n: 500);
			foreach (var item in results) {
				Console.WriteLine ("{0}/{1}", item.Key, item.Value);
			}

			//MyMediaLite.Eval.Items.Evaluate(Recommender, Feedback, Feedback, 
			//Console.WriteLine ("Training and Evaluate model: {0} seconds", t.TotalSeconds);
			//string filename = string.Format ("RankGeoFMKNN");
			//MyMediaLite.Helper.Utils.SaveRank (filename, result);
		}

		//QueryResult Train (uint num_factors, float learn_rate, float regularization)
		//{
		//  bool evaluate = true;
		//  QueryResult result = null;
		//  while (evaluate) {
		//      try {
		//          CreateModel (typeof (WeatherContextAwareItemRecommender));
		//          ((WeatherContextAwareItemRecommender)Recommender).weather_aware = false;
		//          ((WeatherContextAwareItemRecommender)Recommender).max_iter = 7000;
		//          ((WeatherContextAwareItemRecommender)Recommender).rangeSize = 10;
		//          ((WeatherContextAwareItemRecommender)Recommender).evaluation_at = 20;
		//          ((WeatherContextAwareItemRecommender)Recommender).Ratings = FeedbackRatings;
		//          ((WeatherContextAwareItemRecommender)Recommender).Items = Items;
		//          TimeSpan t = Wrap.MeasureTime (delegate () {
		//              Train ();
		//              result = Evaluate ();
		//          });

		//          Console.WriteLine ("Training and Evaluate model: {0} seconds", t.TotalSeconds);
		//          evaluate = false;
		//      } catch (Exception ex) {
		//          Console.WriteLine (ex.Message);
		//          evaluate = true;
		//      }
		//  }

		//  return result;
		//}

		void Log (double metric)
		{
			log.Info (string.Format ("RankGeoFMKNN MRR={0}", metric));
		}

		public override void Evaluate (string filename)
		{
			if (!string.IsNullOrEmpty (filename)) {
				if (Test == null) {
					Console.WriteLine ("Loading test data");
					Test = LoadTest (filename);
					TestFeedback = LoadPositiveFeedback (filename, ItemDataFileFormat.IGNORE_FIRST_LINE);
				}

				var result = Evaluate ();
				MyMediaLite.Helper.Utils.SaveRank ("rankgeofmKNN", result);
				Log (result.GetMetric ("MRR"));
			}
		}


		public override void SaveModel (string path)
		{
			throw new NotImplementedException ();
		}
	}
}
