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
using MyMediaLite.ItemRecommendation;
using MyMediaLite.DataType;

namespace Baselines.Commands
{

	public class xQuADCommand : Command
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger (System.Reflection.MethodBase.GetCurrentMethod ().DeclaringType);

		//ITimedRatings FeedbackRatings;
		IList<POI> Items;
		IList<User> Users;
		double ambiguity = 0.5;
		int depth = 100;
		string item_attributes_file;
		IBooleanMatrix item_attributes;

		public xQuADCommand (string training, string test) : base (typeof (xQuAD))
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
				{ "ambiguity=",      v              => ambiguity      = double.Parse(v)},
				{ "depth=",          v              => depth          = int.Parse(v)},
				{ "item-attributes=", v	            => item_attributes_file = v }
			};
			options.Parse (args);

			if (string.IsNullOrEmpty (item_attributes_file)) 
				throw new Exception ("Recommender expects --item-attributes=FILE.");

			item_attributes = AttributeData.Read (item_attributes_file, item_mapping, true);

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


			if (Feedback != null)
				((xQuAD)Recommender).Feedback = Feedback;

			if (path_test != null)
				Test = LoadTest (path_test);
			
			((xQuAD)Recommender).FeedbackCheckins = Test;
			((xQuAD)Recommender).λ = ambiguity;
			((xQuAD)Recommender).SetItemAttributes(item_attributes);
			((xQuAD)Recommender).Depth = depth;
		}

		protected override void Init ()
		{
			user_mapping = new IdentityMapping ();
			item_mapping = new IdentityMapping ();

			//if (File.Exists ("user.mapping")) {
			//	user_mapping = "user.mapping".LoadMapping ();
			//	item_mapping = "item.mapping".LoadMapping ();
			//}

			if (!string.IsNullOrEmpty (path_training)) {
				Console.WriteLine ("Loading training data");
				Feedback = ItemData.Read (path_training, user_mapping, item_mapping, true);
			}

			if (!string.IsNullOrEmpty (path_test)) {
				Console.WriteLine ("Loading test data");
				TestFeedback = ItemData.Read (path_test, user_mapping, item_mapping, true);
			}

			user_mapping.SaveMapping ("user.mapping");
			item_mapping.SaveMapping ("item.mapping");
		}

		public override void Tunning ()
		{
			if (Test == null || Test.Count == 0)
			  throw new Exception ("Test data can not be null");

			((xQuAD)Recommender).Feedback = Feedback;
			((xQuAD)Recommender).FeedbackCheckins = Test;

			Console.WriteLine (Feedback.Statistics ());

			var customRank = ComputeCustomRank ();
			((xQuAD)Recommender).CustomRank = customRank;

			var AMBIGUITY = new double [] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0};

			foreach (var amb in AMBIGUITY) {
				((xQuAD)Recommender).λ = amb;
				((xQuAD)Recommender).Depth = depth;
				var t = Wrap.MeasureTime (Recommender.Train);
				Console.WriteLine ("xQuAD: {0} seconds\n\n", t.TotalSeconds);
			}
		}

		private QueryResult ComputeCustomRank () {
			BPRMFCommand customRecommender = new BPRMFCommand (path_training, path_test);
			customRecommender.LoadModel ("/Volumes/Tyr/Projects/UFMG/Apocalypse/results/NYC/reduced/algorithms/BPRMF/validation/fold_1/model/BPRMF.model");
			return customRecommender.Evaluate ();
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
				MyMediaLite.Helper.Utils.SaveRank ("rankgeofm", result);
				foreach (var metric in result.Metrics)
					log.Info (string.Format ("{0} = {1}", metric.Item1, metric.Item2));
			}
		}


		public override void SaveModel (string path)
		{
			throw new NotImplementedException ();
		}
	}
}
